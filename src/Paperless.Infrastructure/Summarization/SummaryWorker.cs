using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Services;
using Paperless.Infrastructure.Processing;
using RabbitMQ.Client.Events;

namespace Paperless.Infrastructure.Summarization;

public sealed class SummaryWorker(IServiceScopeFactory scopes, IOptions<ProcessingOptions> options, ILogger<SummaryWorker> logger)
    : RabbitConsumerWorker<SummarizeDocumentMessage>(scopes, options, logger)
{
    protected override string QueueName => RabbitTransport.SummaryQueue;
    protected override string ClientName => "paperless-summarizer";
    protected override Task<bool> TryProcessAsync(IServiceProvider services, SummarizeDocumentMessage message, CancellationToken ct) =>
        services.GetRequiredService<DocumentSummarization>().TryProcessAsync(message, ct);

    protected override SummarizeDocumentMessage? Parse(BasicDeliverEventArgs delivery)
    {
        if (delivery.BasicProperties.Type != "summarize-document.v1") return null;
        try
        {
            var message = JsonSerializer.Deserialize<SummarizeDocumentMessage>(delivery.Body.Span, ProcessingScheduler.Json);
            return message is { SchemaVersion: 1, DocumentId: > 0 } && message.MessageId != Guid.Empty && message.JobId != Guid.Empty &&
                Guid.TryParse(delivery.BasicProperties.MessageId, out var id) && id == message.MessageId ? message : null;
        }
        catch (JsonException) { return null; }
    }
}
