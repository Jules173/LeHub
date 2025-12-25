namespace LeHub.Models;

public static class FrameworkOptions
{
    public static readonly Dictionary<ProjectType, List<FrameworkInfo>> Frameworks = new()
    {
        {
            ProjectType.DotNet, new List<FrameworkInfo>
            {
                new("console", "Console App"),
                new("webapi", "Web API"),
                new("blazorserver", "Blazor Server"),
                new("blazorwasm", "Blazor WebAssembly"),
                new("wpf", "WPF Application"),
                new("winforms", "Windows Forms"),
                new("classlib", "Class Library")
            }
        },
        {
            ProjectType.Node, new List<FrameworkInfo>
            {
                new("react", "React (TypeScript)"),
                new("vue", "Vue.js"),
                new("next", "Next.js"),
                new("express", "Express Server"),
                new("vanilla", "Vanilla JS/TS")
            }
        },
        {
            ProjectType.Python, new List<FrameworkInfo>
            {
                new("script", "Script simple"),
                new("flask", "Flask"),
                new("fastapi", "FastAPI"),
                new("django", "Django")
            }
        },
        {
            ProjectType.Web, new List<FrameworkInfo>
            {
                new("static", "Site statique"),
                new("vite", "Vite")
            }
        }
    };

    public static List<FrameworkInfo> GetFrameworks(ProjectType type)
        => Frameworks.TryGetValue(type, out var list) ? list : new List<FrameworkInfo>();
}

public class FrameworkInfo
{
    public string Id { get; }
    public string DisplayName { get; }

    public FrameworkInfo(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
