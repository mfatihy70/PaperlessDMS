namespace Paperless.Domain.Entities;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Description { get; set; }
}

public class DocumentTag
{
    public int DocumentId { get; set; }
    public int TagId { get; set; }
}
