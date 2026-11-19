using System.Text;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Application.Mapping;
using Paperless.Domain.Entities;

namespace Paperless.Application.Services;

public sealed class TagService(ITagRepository repository) : ITagService
{
    public async Task<IReadOnlyList<TagResponseDto>> ListAsync(CancellationToken ct) =>
        (await repository.ListAsync(ct)).Select(ResponseMapper.ToResponse).ToList();
    public async Task<TagResponseDto?> GetAsync(int id, CancellationToken ct)
    {
        ValidateId(id);
        var tag = await repository.GetAsync(id, ct);
        return tag is null ? null : ResponseMapper.ToResponse(tag);
    }
    public async Task<TagResponseDto> CreateAsync(string? name, string? description, CancellationToken ct) =>
        ResponseMapper.ToResponse(await repository.CreateAsync(Validate(0, name, description), ct));
    public Task<bool> UpdateAsync(int id, string? name, string? description, CancellationToken ct)
    {
        ValidateId(id);
        return repository.UpdateAsync(Validate(id, name, description), ct);
    }
    public Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        ValidateId(id);
        return repository.DeleteAsync(id, ct);
    }
    public async Task<IReadOnlyList<TagResponseDto>?> GetDocumentTagsAsync(int documentId, CancellationToken ct)
    {
        ValidateId(documentId);
        return (await repository.GetDocumentTagsAsync(documentId, ct))?.Select(ResponseMapper.ToResponse).ToList();
    }
    public async Task<IReadOnlyList<TagResponseDto>?> SetDocumentTagsAsync(int documentId, IReadOnlyList<int>? tagIds, CancellationToken ct)
    {
        ValidateId(documentId);
        if (tagIds is null || tagIds.Count > 50 || tagIds.Any(id => id <= 0) || tagIds.Distinct().Count() != tagIds.Count)
            throw new ApplicationValidationException("tagIds", "Provide at most 50 distinct positive tag IDs; an empty array removes all assignments.");
        return (await repository.SetDocumentTagsAsync(documentId, tagIds, ct))?.Select(ResponseMapper.ToResponse).ToList();
    }
    private static void ValidateId(int id)
    {
        if (id <= 0) throw new ApplicationValidationException("id", "The ID must be positive.");
    }
    private static Tag Validate(int id, string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name.Any(char.IsControl))
            throw new ApplicationValidationException("name", "Provide a nonblank name of at most 100 characters without control characters.");
        try { name = name.Trim().Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { throw new ApplicationValidationException("name", "The tag name must contain valid Unicode."); }
        var normalized = name.ToUpperInvariant();
        if (name.Length is 0 or > 100 || normalized.Length > 100)
            throw new ApplicationValidationException("name", "The normalized tag name must contain 1 to 100 characters.");
        if (description?.Length > 1000 || description?.Any(c => c == '\0') == true)
            throw new ApplicationValidationException("description", "Descriptions may contain at most 1,000 characters and no null character.");
        return new Tag { Id = id, Name = name, NormalizedName = normalized, Description = description?.Trim() };
    }
}
