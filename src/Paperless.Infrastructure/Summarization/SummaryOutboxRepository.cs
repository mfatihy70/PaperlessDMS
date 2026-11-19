
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;

namespace Paperless.Infrastructure.Summarization;

internal sealed class SummaryOutboxRepository(IDbContextFactory<PaperlessDbContext> factory, IOptions<ProcessingOptions> options) : IOutboxStore
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-options.Value.RedeliverySeconds);
        await context.SummaryOutbox.Where(message => !message.IsDeadLetter && message.PublishedAt != null &&
            (message.PublishLeaseUntil == null || message.PublishLeaseUntil < now) && context.SummaryJobs.Any(job =>
                job.CurrentMessageId == message.Id && ((job.State == SummaryState.Summarizing && job.LeaseUntil < now) ||
                    ((job.State == SummaryState.Queued || job.State == SummaryState.RetryScheduled) && message.PublishedAt < cutoff))))
            .ExecuteUpdateAsync(set => set.SetProperty(message => message.PublishedAt, (DateTime?)null).SetProperty(message => message.AvailableAt, now), cancellationToken);
        await context.SummaryOutbox.Where(message => !message.IsDeadLetter && !context.SummaryJobs.Any(job => job.CurrentMessageId == message.Id) &&
            (message.PublishLeaseUntil == null || message.PublishLeaseUntil < now)).ExecuteDeleteAsync(cancellationToken);
        var retention = now.AddDays(-7);
        await context.SummaryOutbox.Where(message => message.IsDeadLetter && message.PublishedAt < retention).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<OutboxDelivery?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var message = await context.SummaryOutbox.FromSqlInterpolated($"""
            SELECT * FROM public."SummaryOutbox"
            WHERE "PublishedAt" IS NULL AND "AvailableAt" <= {now}
              AND ("PublishLeaseUntil" IS NULL OR "PublishLeaseUntil" < {now})
            ORDER BY "AvailableAt", "CreatedAt" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);
        if (message is null) return null;
        message.PublishLeaseId = Guid.NewGuid();
        message.PublishLeaseUntil = now.AddSeconds(options.Value.BrokerTimeoutSeconds + 15);
        message.PublishAttempts++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OutboxDelivery(message.Id, message.DocumentId, message.JobId.ToString(), message.Payload,
            message.PublishLeaseId.Value, message.PublishAttempts,
            message.IsDeadLetter ? RabbitTransport.SummaryDeadExchange : RabbitTransport.SummaryExchange,
            message.IsDeadLetter ? RabbitTransport.DeadRoutingKey : RabbitTransport.SummaryRoutingKey,
            message.IsDeadLetter ? "summary-failed.v1" : "summarize-document.v1");
    }

    public async Task CompleteAsync(OutboxDelivery message, bool published, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var retryAt = now.AddSeconds(Math.Min(60, options.Value.PollIntervalSeconds * Math.Min(message.PublishAttempts, 30)));
        await context.SummaryOutbox.Where(row => row.Id == message.Id && row.PublishLeaseId == message.PublishLeaseId)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.PublishedAt, published ? now : (DateTime?)null)
                .SetProperty(row => row.LastError, published ? null : "RabbitMQ has not confirmed summary delivery. The saved message will be sent again.")
                .SetProperty(row => row.AvailableAt, retryAt).SetProperty(row => row.PublishLeaseId, (Guid?)null)
                .SetProperty(row => row.PublishLeaseUntil, (DateTime?)null), cancellationToken);
    }
}
