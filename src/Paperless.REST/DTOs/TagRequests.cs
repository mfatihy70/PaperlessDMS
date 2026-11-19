using System.ComponentModel.DataAnnotations;

namespace Paperless.REST.DTOs;

/// <summary>Creates or replaces the display name and description of a tag.</summary>
public class TagRequest
{
    /// <summary>A nonblank name, at most 100 characters; unique ignoring case and Unicode compatibility differences.</summary>
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    /// <summary>An optional description of at most 1,000 characters; null clears it on update.</summary>
    [StringLength(1000)]
    public string? Description { get; set; }
}

/// <summary>Replaces the complete set of a document's manually assigned tags.</summary>
public class DocumentTagsRequest
{
    /// <summary>At most 50 distinct positive IDs of existing tags. An empty array removes all assignments; null is invalid.</summary>
    [Required, MaxLength(50)]
    public int[]? TagIds { get; set; }
}
