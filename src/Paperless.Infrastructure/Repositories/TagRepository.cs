using Microsoft.EntityFrameworkCore;
using Npgsql;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Repositories;

public sealed class TagRepository(IDbContextFactory<PaperlessDbContext> factory) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> ListAsync(CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        return await context.Tags.AsNoTracking().OrderBy(tag => tag.Name).ThenBy(tag => tag.Id).ToListAsync(ct);
    }
    public async Task<Tag?> GetAsync(int id, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        return await context.Tags.AsNoTracking().SingleOrDefaultAsync(tag => tag.Id == id, ct);
    }
    public async Task<Tag> CreateAsync(Tag tag, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        context.Tags.Add(tag);
        await SaveAsync(context, ct);
        return tag;
    }
    public async Task<bool> UpdateAsync(Tag tag, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        var existing = await context.Tags.SingleOrDefaultAsync(row => row.Id == tag.Id, ct);
        if (existing is null) return false;
        existing.Name = tag.Name;
        existing.NormalizedName = tag.NormalizedName;
        existing.Description = tag.Description;
        await SaveAsync(context, ct);
        return true;
    }
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        return await context.Tags.Where(tag => tag.Id == id).ExecuteDeleteAsync(ct) > 0;
    }
    public async Task<IReadOnlyList<Tag>?> GetDocumentTagsAsync(int documentId, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        // One statement distinguishes an empty collection from a missing document under concurrent deletion.
        var row = await context.Documents.Where(doc => doc.Id == documentId).Select(doc => new
        {
            Tags = (from link in context.DocumentTags
                    join tag in context.Tags on link.TagId equals tag.Id
                    where link.DocumentId == doc.Id
                    orderby tag.Name, tag.Id
                    select tag).ToList()
        }).SingleOrDefaultAsync(ct);
        return row?.Tags;
    }
    public async Task<IReadOnlyList<Tag>?> SetDocumentTagsAsync(int documentId, IReadOnlyList<int> tagIds, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM public.\"Documents\" WHERE \"Id\" = {documentId} FOR UPDATE", ct);
        if (!await context.Documents.AnyAsync(doc => doc.Id == documentId, ct)) return null;
        // Lock target tags before mutating links; deletion of a selected tag must wait for this transaction.
        var tags = new List<Tag>();
        foreach (var id in tagIds.Order())
        {
            var tag = await context.Tags.FromSqlInterpolated($"SELECT * FROM public.\"Tags\" WHERE \"Id\" = {id} FOR KEY SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (tag is null) throw new ApplicationValidationException("tagIds", $"Tag {id} does not exist.");
            tags.Add(tag);
        }
        await context.DocumentTags.Where(link => link.DocumentId == documentId).ExecuteDeleteAsync(ct);
        context.DocumentTags.AddRange(tagIds.Select(id => new DocumentTag { DocumentId = documentId, TagId = id }));
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return tags.OrderBy(tag => tag.Name).ThenBy(tag => tag.Id).ToList();
    }
    private static async Task SaveAsync(PaperlessDbContext context, CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Tags_NormalizedName" })
        {
            throw new ApplicationConflictException("A tag with this name already exists.", exception);
        }
    }
}
