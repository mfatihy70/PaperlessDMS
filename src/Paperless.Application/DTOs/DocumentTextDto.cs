namespace Paperless.Application.DTOs;

/// <summary>Persisted OCR text for the current original. A successful blank document has empty text and HasText false.</summary>
public class DocumentTextDto
{
    /// <summary>The document ID.</summary>
    public int DocumentId { get; set; }
    /// <summary>The job that produced this result.</summary>
    public Guid JobId { get; set; }
    /// <summary>Recognized text in page order, separated by blank lines. OCR is approximate and can contain recognition errors.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Whether the result contains non-whitespace text.</summary>
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    /// <summary>The configured Tesseract languages, for example deu+eng.</summary>
    public string Language { get; set; } = string.Empty;
    /// <summary>The number of recognized pages.</summary>
    public int PageCount { get; set; }
    /// <summary>When the result was saved, in UTC.</summary>
    public DateTime ExtractedAt { get; set; }
}
