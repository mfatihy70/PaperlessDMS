using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Services;
using RabbitMQ.Client.Events;

namespace Paperless.Infrastructure.Processing;

public sealed class PreparationWorker(IServiceScopeFactory scopes, IOptions<ProcessingOptions> options, ILogger<PreparationWorker> logger)
    : RabbitConsumerWorker<PrepareDocumentMessage>(scopes, options, logger)
{
    protected override string QueueName => RabbitTransport.Queue;
    protected override string ClientName => "paperless-preparation";
    protected override Task<bool> TryProcessAsync(IServiceProvider services, PrepareDocumentMessage message, CancellationToken cancellationToken) =>
        services.GetRequiredService<DocumentPreparation>().TryProcessAsync(message, cancellationToken);

    protected override PrepareDocumentMessage? Parse(BasicDeliverEventArgs delivery)
    {
        if (delivery.BasicProperties.Type != "prepare-document.v1") return null;
        try
        {
            var message = JsonSerializer.Deserialize<PrepareDocumentMessage>(delivery.Body.Span, ProcessingScheduler.Json);
            return message is { SchemaVersion: 1, DocumentId: > 0, StorageKey.Length: 42 } &&
                message.JobId != Guid.Empty && message.MessageId != Guid.Empty &&
                message.StorageKey.StartsWith("documents/", StringComparison.Ordinal) && Guid.TryParseExact(message.StorageKey[10..], "N", out _) &&
                Guid.TryParse(delivery.BasicProperties.MessageId, out var id) && id == message.MessageId ? message : null;
        }
        catch (JsonException) { return null; }
    }
}
