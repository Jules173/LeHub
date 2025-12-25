namespace LeHub.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public ProjectType ProjectType { get; set; }
    public string? Framework { get; set; }
    public string? LastPublishMode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Tag> Tags { get; set; } = new();

    // Helper properties
    public string TypeDisplay => ProjectType.ToString();

    public string FrameworkDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Framework)) return "";
            var frameworks = FrameworkOptions.GetFrameworks(ProjectType);
            return frameworks.FirstOrDefault(f => f.Id == Framework)?.DisplayName ?? Framework;
        }
    }

    public string PublishPath => System.IO.Path.Combine(RootPath, ".lehub", "publish");

    public bool FolderExists => System.IO.Directory.Exists(RootPath);
}
