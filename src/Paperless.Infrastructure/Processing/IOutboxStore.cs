namespace Paperless.Infrastructure.Processing;

internal sealed record OutboxDelivery(Guid Id, int DocumentId, string CorrelationId, string Payload,
    Guid PublishLeaseId, int PublishAttempts, string Exchange, string RoutingKey, string MessageType);

internal interface IOutboxStore
{
    Task RecoverAsync(CancellationToken cancellationToken);
    Task<OutboxDelivery?> ClaimAsync(CancellationToken cancellationToken);
    Task CompleteAsync(OutboxDelivery message, bool published, CancellationToken cancellationToken);
}
