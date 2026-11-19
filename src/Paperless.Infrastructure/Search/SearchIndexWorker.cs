using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Services;
using Paperless.Infrastructure.Processing;
using RabbitMQ.Client.Events;

namespace Paperless.Infrastructure.Search;

public sealed class SearchIndexWorker(IServiceScopeFactory scopes, IOptions<ProcessingOptions> options, ILogger<SearchIndexWorker> logger)
    : RabbitConsumerWorker<IndexDocumentMessage>(scopes, options, logger)
{
    protected override string QueueName => RabbitTransport.IndexQueue;
    protected override string ClientName => "paperless-indexer";
    protected override Task<bool> TryProcessAsync(IServiceProvider services, IndexDocumentMessage message, CancellationToken cancellationToken) =>
        services.GetRequiredService<DocumentIndexing>().TryProcessAsync(message, cancellationToken);

    protected override IndexDocumentMessage? Parse(BasicDeliverEventArgs delivery)
    {
        if (delivery.BasicProperties.Type != "index-document.v1") return null;
        try
        {
            var message = JsonSerializer.Deserialize<IndexDocumentMessage>(delivery.Body.Span, ProcessingScheduler.Json);
            return message is { SchemaVersion: 1, DocumentId: > 0, Revision: > 0 } && message.MessageId != Guid.Empty &&
                Guid.TryParse(delivery.BasicProperties.MessageId, out var id) && id == message.MessageId ? message : null;
        }
        catch (JsonException) { return null; }
    }
}
