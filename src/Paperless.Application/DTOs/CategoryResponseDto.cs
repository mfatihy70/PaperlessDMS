namespace Paperless.Application.DTOs;

/// <summary>A stored document category.</summary>
/// <param name="Id">The generated category ID.</param>
/// <param name="Name">The category name.</param>
/// <param name="Description">The category description, or null if none was provided.</param>
public sealed record CategoryResponseDto(int Id, string Name, string? Description);
