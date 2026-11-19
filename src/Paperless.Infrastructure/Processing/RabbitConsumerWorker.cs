using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paperless.Infrastructure.Processing;

public abstract class RabbitConsumerWorker<T>(IServiceScopeFactory scopes, IOptions<ProcessingOptions> options, ILogger logger) : BackgroundService where T : class
{
    protected abstract string QueueName { get; }
    protected abstract string ClientName { get; }
    protected abstract T? Parse(BasicDeliverEventArgs delivery);
    protected abstract Task<bool> TryProcessAsync(IServiceProvider services, T message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ConsumeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Consumer for {Queue} disconnected; reconnecting.", QueueName); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using var connection = await RabbitTransport.ConnectAsync(options.Value, ClientName, lifetime.Token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: lifetime.Token);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, _) => { lifetime.Cancel(); disconnected.TrySetResult(); return Task.CompletedTask; };
        channel.ChannelShutdownAsync += (_, _) => { lifetime.Cancel(); disconnected.TrySetResult(); return Task.CompletedTask; };
        await RabbitTransport.DeclareAsync(channel, lifetime.Token);
        await channel.BasicQosAsync(0, 1, false, lifetime.Token);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var message = delivery.Body.Length is > 0 and <= 8192 && delivery.BasicProperties.ContentType == "application/json" ? Parse(delivery) : null;
                if (message is null)
                {
                    await channel.BasicRejectAsync(delivery.DeliveryTag, false, lifetime.Token);
                    logger.LogWarning("Invalid message on {Queue} rejected to its dead-letter queue.", QueueName);
                    return;
                }
                while (!lifetime.IsCancellationRequested)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        if (await TryProcessAsync(scope.ServiceProvider, message, lifetime.Token))
                        {
                            await channel.BasicAckAsync(delivery.DeliveryTag, false, lifetime.Token);
                            logger.LogInformation("Delivery {MessageId} on {Queue} completed.", delivery.BasicProperties.MessageId, QueueName);
                            return;
                        }
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
                    catch (Exception exception) { logger.LogWarning(exception, "Delivery on {Queue} could not be saved; it remains unacknowledged.", QueueName); }
                    await Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Delivery on {Queue} interrupted; broker redelivery is required.", QueueName);
                lifetime.Cancel();
                disconnected.TrySetResult();
            }
        };
        await channel.BasicConsumeAsync(QueueName, false, consumer, lifetime.Token);
        logger.LogInformation("Waiting for messages on {Queue}.", QueueName);
        try { await disconnected.Task.WaitAsync(lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
}
