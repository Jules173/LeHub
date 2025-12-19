namespace LeHub.Models;

public class Preset
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DelayMs { get; set; } = 200;
    public DateTime CreatedAt { get; set; }
    public List<PresetApp> Apps { get; set; } = new();
}

public class PresetApp
{
    public int PresetId { get; set; }
    public int AppId { get; set; }
    public int OrderIndex { get; set; }
    public AppEntry? App { get; set; }
}
