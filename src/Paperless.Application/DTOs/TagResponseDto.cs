namespace Paperless.Application.DTOs;

/// <summary>A reusable, manually managed document label.</summary>
public class TagResponseDto
{
    /// <summary>The positive tag ID.</summary>
    public int Id { get; set; }
    /// <summary>The trimmed display name, at most 100 characters; unique ignoring case and Unicode compatibility differences.</summary>
    public string Name { get; set; } = "";
    /// <summary>Optional description, at most 1,000 characters.</summary>
    public string? Description { get; set; }
}
