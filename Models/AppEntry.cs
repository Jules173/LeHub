namespace LeHub.Models;

public class AppEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public bool IsFavorite { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Tag> Tags { get; set; } = new();

    // Helper property for display
    public string CategoryName => Category?.Name ?? "";
}
