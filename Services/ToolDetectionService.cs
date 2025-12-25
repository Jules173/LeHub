using System.Diagnostics;
using System.IO;

namespace LeHub.Services;

public class ToolDetectionService
{
    private static ToolDetectionService? _instance;
    public static ToolDetectionService Instance => _instance ??= new ToolDetectionService();

    private readonly Dictionary<string, bool?> _cache = new();

    private ToolDetectionService() { }

    public bool IsGitAvailable() => IsToolAvailable("git", "--version");

    public bool IsDotNetAvailable() => IsToolAvailable("dotnet", "--version");

    public bool IsNpmAvailable() => IsToolAvailable("npm", "--version");

    public bool IsNodeAvailable() => IsToolAvailable("node", "--version");

    public bool IsPythonAvailable() => IsToolAvailable("python", "--version") || IsToolAvailable("python3", "--version");

    public bool IsVSCodeAvailable() => IsToolAvailable("code", "--version");

    public string? GetToolVersion(string tool)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);

            return process.ExitCode == 0 ? output.Split('\n')[0] : null;
        }
        catch
        {
            return null;
        }
    }

    public string? FindVisualStudio()
    {
        var paths = new[]
        {
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2019\Professional\Common7\IDE\devenv.exe",
            @"C:\Program Files\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.exe",
        };

        return paths.FirstOrDefault(File.Exists);
    }

    public bool IsVisualStudioAvailable() => FindVisualStudio() != null;

    public bool IsToolAvailable(string tool, string args = "--version")
    {
        var key = $"{tool}:{args}";
        if (_cache.TryGetValue(key, out var cached) && cached.HasValue)
            return cached.Value;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _cache[key] = false;
                return false;
            }

            process.WaitForExit(5000);
            var available = process.ExitCode == 0;
            _cache[key] = available;
            return available;
        }
        catch
        {
            _cache[key] = false;
            return false;
        }
    }

    public void InvalidateCache() => _cache.Clear();

    /// <summary>
    /// Gets a summary of all tools' availability
    /// </summary>
    public Dictionary<string, bool> GetToolsSummary()
    {
        return new Dictionary<string, bool>
        {
            { "git", IsGitAvailable() },
            { "dotnet", IsDotNetAvailable() },
            { "npm", IsNpmAvailable() },
            { "node", IsNodeAvailable() },
            { "python", IsPythonAvailable() },
            { "code", IsVSCodeAvailable() },
            { "vs", IsVisualStudioAvailable() }
        };
    }

    /// <summary>
    /// Gets the installation URL for a tool
    /// </summary>
    public static string GetInstallUrl(string tool)
    {
        return tool.ToLower() switch
        {
            "git" => "https://git-scm.com/downloads",
            "dotnet" => "https://dotnet.microsoft.com/download",
            "npm" or "node" => "https://nodejs.org/",
            "python" => "https://www.python.org/downloads/",
            "code" => "https://code.visualstudio.com/",
            "vs" => "https://visualstudio.microsoft.com/",
            _ => ""
        };
    }
}
