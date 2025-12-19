namespace LeHub.Models;

public class AppEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public bool IsFavorite { get; set; }
    public string Category { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Tag> Tags { get; set; } = new();
}
