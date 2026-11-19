using Paperless.Application.DTOs;
using Paperless.Domain.Entities;

namespace Paperless.Application.Abstractions;

public interface ITagService
{
    Task<IReadOnlyList<TagResponseDto>> ListAsync(CancellationToken cancellationToken);
    Task<TagResponseDto?> GetAsync(int id, CancellationToken cancellationToken);
    Task<TagResponseDto> CreateAsync(string? name, string? description, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(int id, string? name, string? description, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagResponseDto>?> GetDocumentTagsAsync(int documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TagResponseDto>?> SetDocumentTagsAsync(int documentId, IReadOnlyList<int>? tagIds, CancellationToken cancellationToken);
}

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken);
    Task<Tag?> GetAsync(int id, CancellationToken cancellationToken);
    Task<Tag> CreateAsync(Tag tag, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Tag tag, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Tag>?> GetDocumentTagsAsync(int documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Tag>?> SetDocumentTagsAsync(int documentId, IReadOnlyList<int> tagIds, CancellationToken cancellationToken);
}
