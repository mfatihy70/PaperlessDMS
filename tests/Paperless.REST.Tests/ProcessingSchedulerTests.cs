using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;
using Xunit;

namespace Paperless.REST.Tests;

public class ProcessingSchedulerTests
{
    [Fact]
    public void Message_UsesCurrentJobData()
    {
        var job = CreateJob();

        var message = ProcessingScheduler.Message(job);

        Assert.Equal(1, message.SchemaVersion);
        Assert.Equal(job.CurrentMessageId, message.MessageId);
        Assert.Equal(job.JobId, message.JobId);
        Assert.Equal(job.DocumentId, message.DocumentId);
        Assert.Equal(job.StorageKey, message.StorageKey);
    }

    [Fact]
    public void Enqueue_AddsOutboxMessage()
    {
        using var context = CreateContext();
        var job = CreateJob();
        var availableAt = DateTime.UtcNow;

        ProcessingScheduler.Enqueue(context, job, availableAt);

        var outbox = Assert.Single(context.ProcessingOutbox.Local);
        Assert.Equal(job.CurrentMessageId, outbox.Id);
        Assert.Equal(job.DocumentId, outbox.DocumentId);
        Assert.Equal(job.JobId, outbox.JobId);
        Assert.Equal(availableAt, outbox.AvailableAt);
        Assert.False(outbox.IsDeadLetter);
        Assert.Null(outbox.PublishedAt);

        var payload = JsonSerializer.Deserialize<PrepareDocumentMessage>(outbox.Payload, ProcessingScheduler.Json);
        Assert.NotNull(payload);
        Assert.Equal(job.CurrentMessageId, payload.MessageId);
        Assert.Equal(job.StorageKey, payload.StorageKey);
    }

    [Fact]
    public void Enqueue_GeneratesNewMessageIdEachTime()
    {
        using var context = CreateContext();
        var job = CreateJob();

        ProcessingScheduler.Enqueue(context, job, DateTime.UtcNow);
        var firstId = job.CurrentMessageId;

        ProcessingScheduler.Enqueue(context, job, DateTime.UtcNow);

        Assert.NotEqual(firstId, job.CurrentMessageId);
        Assert.Equal(2, context.ProcessingOutbox.Local.Count);
    }

    [Fact]
    public void Fail_MarksJobFailedAndAddsDeadLetter()
    {
        using var context = CreateContext();
        var job = CreateJob();
        job.State = ProcessingState.Processing;
        job.Attempts = 3;
        job.LeaseId = Guid.NewGuid();
        job.LeaseUntil = DateTime.UtcNow.AddMinutes(2);

        ProcessingScheduler.Fail(context, job, "OCR_ERROR", "OCR failed");

        Assert.Equal(ProcessingState.Failed, job.State);
        Assert.Equal("OCR_ERROR", job.LastErrorCode);
        Assert.Equal("OCR failed", job.LastError);
        Assert.NotNull(job.FinishedAt);
        Assert.Null(job.LeaseId);
        Assert.Null(job.LeaseUntil);

        var outbox = Assert.Single(context.ProcessingOutbox.Local);
        Assert.True(outbox.IsDeadLetter);
        Assert.Equal(job.DocumentId, outbox.DocumentId);

        var failure = JsonSerializer.Deserialize<PreparationFailure>(outbox.Payload, ProcessingScheduler.Json);
        Assert.NotNull(failure);
        Assert.Equal("OCR_ERROR", failure.ErrorCode);
        Assert.Equal(3, failure.Attempts);
        Assert.Equal(job.DocumentId, failure.Message.DocumentId);
    }

    private static DocumentProcessingJob CreateJob() => new()
    {
        DocumentId = 10,
        JobId = Guid.NewGuid(),
        CurrentMessageId = Guid.NewGuid(),
        StorageKey = "documents/" + Guid.NewGuid().ToString("N"),
        FileSha256 = new string('a', 64),
        FileSizeBytes = 500,
        State = ProcessingState.Queued,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static PaperlessDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaperlessDbContext>()
            .UseNpgsql("Host=localhost;Database=paperless_tests;Username=test;Password=test")
            .Options;
        return new PaperlessDbContext(options);
    }
}
