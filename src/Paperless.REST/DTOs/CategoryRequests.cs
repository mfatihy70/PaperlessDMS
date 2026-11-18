using System.ComponentModel.DataAnnotations;

namespace Paperless.REST.DTOs;

/// <summary>A new category for organizing documents.</summary>
public sealed class CreateCategoryDto(string name, string? description)
{
    /// <summary>A nonblank category name of at most 100 characters, for example Invoices.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = name;

    /// <summary>An optional description of the category.</summary>
    public string? Description { get; init; } = description;
}

/// <summary>Replacement name and description for an existing category.</summary>
public sealed class UpdateCategoryDto(string name, string? description)
{
    /// <summary>A nonblank category name of at most 100 characters.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = name;

    /// <summary>The description, or null to remove the existing description.</summary>
    public string? Description { get; init; } = description;
}
