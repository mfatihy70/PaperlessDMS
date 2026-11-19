using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Processing;

internal sealed class OutboxRepository(IDbContextFactory<PaperlessDbContext> factory, IOptions<ProcessingOptions> options)
{
    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-options.Value.RedeliverySeconds);
        // Retained current messages allow recovery after a crash or a lost broker delivery.
        await context.ProcessingOutbox.Where(m => !m.IsDeadLetter && m.PublishedAt != null &&
            (m.PublishLeaseUntil == null || m.PublishLeaseUntil < now) && context.ProcessingJobs.Any(j =>
                j.CurrentMessageId == m.Id &&
                (((j.State == ProcessingState.Processing || j.State == ProcessingState.RecognizingText) && j.LeaseUntil < now) ||
                 ((j.State == ProcessingState.Queued || j.State == ProcessingState.RetryScheduled || j.State == ProcessingState.ReadyForOcr) && m.PublishedAt < cutoff))))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.PublishedAt, (DateTime?)null)
                .SetProperty(m => m.AvailableAt, now), cancellationToken);
        await context.ProcessingOutbox.Where(m => !m.IsDeadLetter && !context.ProcessingJobs.Any(j => j.CurrentMessageId == m.Id) &&
            (m.PublishLeaseUntil == null || m.PublishLeaseUntil < now)).ExecuteDeleteAsync(cancellationToken);
        var retention = now.AddDays(-7);
        await context.ProcessingOutbox.Where(m => m.IsDeadLetter && m.PublishedAt < retention).ExecuteDeleteAsync(cancellationToken);
    }

    internal async Task<ProcessingOutboxMessage?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var message = await context.ProcessingOutbox.FromSqlInterpolated($"""
            SELECT * FROM public."ProcessingOutbox"
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
        return message;
    }

    internal async Task CompleteAsync(ProcessingOutboxMessage message, bool published, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var retryAt = now.AddSeconds(Math.Min(60, options.Value.PollIntervalSeconds * Math.Min(message.PublishAttempts, 30)));
        await context.ProcessingOutbox.Where(m => m.Id == message.Id && m.PublishLeaseId == message.PublishLeaseId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.PublishedAt, published ? now : (DateTime?)null)
                .SetProperty(m => m.LastError, published ? null : "RabbitMQ has not confirmed delivery. The saved message will be sent again.")
                .SetProperty(m => m.AvailableAt, retryAt)
                .SetProperty(m => m.PublishLeaseId, (Guid?)null)
                .SetProperty(m => m.PublishLeaseUntil, (DateTime?)null), cancellationToken);
    }
}
