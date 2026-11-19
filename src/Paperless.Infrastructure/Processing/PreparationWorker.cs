using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paperless.Infrastructure.Processing;

public sealed class PreparationWorker(IServiceScopeFactory scopes, IOptions<ProcessingOptions> options, ILogger<PreparationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Document processing consumer disconnected; reconnecting.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using var connection = await RabbitTransport.ConnectAsync(options.Value, "paperless-preparation", lifetime.Token);
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
                var message = Parse(delivery);
                if (message is null)
                {
                    await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, lifetime.Token);
                    logger.LogWarning("An invalid processing message was rejected to the dead-letter queue.");
                    return;
                }
                while (!lifetime.IsCancellationRequested)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        if (await scope.ServiceProvider.GetRequiredService<DocumentPreparation>().TryProcessAsync(message, lifetime.Token))
                        {
                            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, lifetime.Token);
                            logger.LogInformation("Processing delivery completed for document {DocumentId}, job {JobId}.", message.DocumentId, message.JobId);
                            return;
                        }
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
                    catch (Exception exception)
                    {
                        logger.LogWarning(exception, "Processing for document {DocumentId} could not be saved; delivery remains unacknowledged.", message.DocumentId);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Processing delivery was interrupted; the broker will redeliver it.");
                lifetime.Cancel();
                disconnected.TrySetResult();
            }
        };
        await channel.BasicConsumeAsync(RabbitTransport.Queue, autoAck: false, consumer, lifetime.Token);
        logger.LogInformation("Waiting for document processing messages.");
        try { await disconnected.Task.WaitAsync(lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private static PrepareDocumentMessage? Parse(BasicDeliverEventArgs delivery)
    {
        if (delivery.Body.Length is 0 or > 8192 || delivery.BasicProperties.Type != "prepare-document.v1" ||
            delivery.BasicProperties.ContentType != "application/json") return null;
        try
        {
            var message = JsonSerializer.Deserialize<PrepareDocumentMessage>(delivery.Body.Span, ProcessingScheduler.Json);
            return message is { SchemaVersion: 1, DocumentId: > 0, StorageKey.Length: 42 } &&
                message.JobId != Guid.Empty && message.MessageId != Guid.Empty &&
                message.StorageKey.StartsWith("documents/", StringComparison.Ordinal) &&
                Guid.TryParseExact(message.StorageKey[10..], "N", out _) &&
                Guid.TryParse(delivery.BasicProperties.MessageId, out var id) && id == message.MessageId
                ? message : null;
        }
        catch (JsonException) { return null; }
    }
}
