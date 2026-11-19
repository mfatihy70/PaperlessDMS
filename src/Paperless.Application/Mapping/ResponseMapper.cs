using Paperless.Application.DTOs;
using Paperless.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Paperless.Application.Mapping;

/// <summary>Generates the entity-to-response mappings at compile time.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class ResponseMapper
{
    [MapProperty(nameof(Document.Category) + "." + nameof(DocumentCategory.Name), nameof(DocumentResponseDto.CategoryName))]
    [MapPropertyFromSource(nameof(DocumentResponseDto.HasFile), Use = nameof(HasStoredFile))]
    public static partial DocumentResponseDto ToResponse(Document document);

    public static partial CategoryResponseDto ToResponse(DocumentCategory category);

    [MapProperty(nameof(Document.FileContent), nameof(DocumentFileDto.Content))]
    public static partial DocumentFileDto ToFile(Document document);

    // Metadata queries omit file bytes and supply HasFile instead; in-memory entities may contain the bytes.
    private static bool HasStoredFile(Document document) =>
        document.HasFile || document.StorageKey is not null || document.FileContent is { Length: > 0 };
}
