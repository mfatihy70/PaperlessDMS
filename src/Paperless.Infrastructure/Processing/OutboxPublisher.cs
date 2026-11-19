using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Paperless.Infrastructure.Processing;

internal sealed class OutboxPublisher(IEnumerable<IOutboxStore> repositories, IOptions<ProcessingOptions> options, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DispatchEnabled) return;
        IConnection? connection = null;
        IChannel? channel = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var repository in repositories)
                    {
                        await repository.RecoverAsync(stoppingToken);
                        for (var count = 0; count < 25 && !stoppingToken.IsCancellationRequested; count++)
                        {
                            var message = await repository.ClaimAsync(stoppingToken);
                            if (message is null) break;
                            var published = false;
                            try
                            {
                                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                                timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.BrokerTimeoutSeconds));
                                if (connection is null || !connection.IsOpen || channel is null || !channel.IsOpen)
                                {
                                    if (channel is not null) await channel.DisposeAsync();
                                    if (connection is not null) await connection.DisposeAsync();
                                    connection = await RabbitTransport.ConnectAsync(options.Value, "paperless-outbox", timeout.Token);
                                    channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), timeout.Token);
                                    await RabbitTransport.DeclareAsync(channel, timeout.Token);
                                }
                                var properties = new BasicProperties
                                {
                                    Persistent = true,
                                    ContentType = "application/json",
                                    Type = message.MessageType,
                                    MessageId = message.Id.ToString(),
                                    CorrelationId = message.CorrelationId
                                };
                                await channel.BasicPublishAsync(message.Exchange, message.RoutingKey,
                                    mandatory: true, properties, Encoding.UTF8.GetBytes(message.Payload), timeout.Token);
                                published = true;
                            }
                            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                            {
                                logger.LogWarning(exception, "RabbitMQ did not confirm outbox message {MessageId}; delivery remains queued.", message.Id);
                                if (channel is not null) await channel.DisposeAsync();
                                if (connection is not null) await connection.DisposeAsync();
                                channel = null;
                                connection = null;
                            }
                            await repository.CompleteAsync(message, published, stoppingToken);
                            if (!published) break; // One broker failure per polling cycle, rather than one per queued document.
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Outbox dispatch is temporarily unavailable; saved messages remain in PostgreSQL.");
                }
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            if (channel is not null) await channel.DisposeAsync();
            if (connection is not null) await connection.DisposeAsync();
        }
    }
}
