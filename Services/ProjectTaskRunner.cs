using System.Diagnostics;
using System.IO;
using LeHub.Models;

namespace LeHub.Services;

public enum ProjectTask
{
    Build,
    Run,
    Test,
    Publish
}

public class ProjectTaskRunner
{
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;
    public event Action<int>? TaskCompleted;
    public event Action? TaskStarted;

    private Process? _currentProcess;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _currentProcess != null && !_currentProcess.HasExited;

    public async Task<(bool Success, int ExitCode, string? ArtifactPath)> RunTaskAsync(
        Project project,
        ProjectTask task,
        string? publishMode = null,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Une tache est deja en cours d'execution.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var (command, args, workingDir) = GetCommandForTask(project, task, publishMode);

        if (string.IsNullOrEmpty(command))
        {
            ErrorReceived?.Invoke("Commande non configuree pour ce type de projet.");
            return (false, -1, null);
        }

        // Check if tool is available
        if (!IsToolAvailableForCommand(command))
        {
            var toolName = command.Split(' ')[0];
            ErrorReceived?.Invoke($"Outil '{toolName}' non installe. Installez-le depuis {ToolDetectionService.GetInstallUrl(toolName)}");
            return (false, -1, null);
        }

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _currentProcess = new Process { StartInfo = psi };

            _currentProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data);
            };

            _currentProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) ErrorReceived?.Invoke(e.Data);
            };

            OutputReceived?.Invoke($"$ {command} {args}");
            OutputReceived?.Invoke($"Dossier: {workingDir}");
            OutputReceived?.Invoke(new string('-', 50));

            TaskStarted?.Invoke();

            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();

            await _currentProcess.WaitForExitAsync(_cts.Token);

            var exitCode = _currentProcess.ExitCode;

            OutputReceived?.Invoke(new string('-', 50));
            OutputReceived?.Invoke($"Termine avec code: {exitCode}");

            TaskCompleted?.Invoke(exitCode);

            // For publish task, try to find the artifact
            string? artifactPath = null;
            if (task == ProjectTask.Publish && exitCode == 0)
            {
                artifactPath = FindPublishArtifact(project);
            }

            return (exitCode == 0, exitCode, artifactPath);
        }
        catch (OperationCanceledException)
        {
            OutputReceived?.Invoke("Tache annulee.");
            return (false, -1, null);
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke($"Erreur: {ex.Message}");
            return (false, -1, null);
        }
        finally
        {
            _currentProcess?.Dispose();
            _currentProcess = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Kill(entireProcessTree: true);
            }
        }
        catch { }
    }

    private (string Command, string Args, string WorkingDir) GetCommandForTask(
        Project project, ProjectTask task, string? publishMode)
    {
        var rootPath = project.RootPath;
        var publishPath = project.PublishPath;

        // Ensure publish directory exists
        if (task == ProjectTask.Publish)
        {
            Directory.CreateDirectory(publishPath);
        }

        return (project.ProjectType, task) switch
        {
            // DotNet
            (ProjectType.DotNet, ProjectTask.Build) => ("dotnet", "build", rootPath),
            (ProjectType.DotNet, ProjectTask.Run) => ("dotnet", "run", rootPath),
            (ProjectType.DotNet, ProjectTask.Test) => ("dotnet", "test", rootPath),
            (ProjectType.DotNet, ProjectTask.Publish) => ("dotnet", $"publish -c Release -o \"{publishPath}\"", rootPath),

            // Node
            (ProjectType.Node, ProjectTask.Build) => ("npm", "run build", rootPath),
            (ProjectType.Node, ProjectTask.Run) => GetNodeRunCommand(rootPath, publishMode),
            (ProjectType.Node, ProjectTask.Test) => ("npm", "test", rootPath),
            (ProjectType.Node, ProjectTask.Publish) => GetNodePublishCommand(rootPath, publishPath, publishMode),

            // Python
            (ProjectType.Python, ProjectTask.Build) => ("python", "-m py_compile main.py", rootPath),
            (ProjectType.Python, ProjectTask.Run) => GetPythonRunCommand(rootPath, project.Framework),
            (ProjectType.Python, ProjectTask.Test) => ("python", "-m pytest", rootPath),
            (ProjectType.Python, ProjectTask.Publish) => ("python", "-m py_compile main.py", rootPath), // Python doesn't really "publish"

            // Web
            (ProjectType.Web, ProjectTask.Build) => ("npm", "run build", rootPath),
            (ProjectType.Web, ProjectTask.Run) => GetWebRunCommand(rootPath, publishMode),
            (ProjectType.Web, ProjectTask.Test) => ("npm", "test", rootPath),
            (ProjectType.Web, ProjectTask.Publish) => ("npm", "run build", rootPath),

            _ => ("", "", rootPath)
        };
    }

    private (string Command, string Args, string WorkingDir) GetNodeRunCommand(string rootPath, string? mode)
    {
        // Check package.json for available scripts
        var packageJsonPath = Path.Combine(rootPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            var content = File.ReadAllText(packageJsonPath);

            // Try common dev scripts
            if (content.Contains("\"dev\""))
                return ("npm", "run dev", rootPath);
            if (content.Contains("\"start\""))
                return ("npm", "start", rootPath);
            if (content.Contains("\"serve\""))
                return ("npm", "run serve", rootPath);
        }

        return ("npm", "start", rootPath);
    }

    private (string Command, string Args, string WorkingDir) GetNodePublishCommand(string rootPath, string publishPath, string? mode)
    {
        // For Node projects, we run build and the output is typically in dist/
        // We'll copy it to .lehub/publish after
        return ("npm", "run build", rootPath);
    }

    private (string Command, string Args, string WorkingDir) GetPythonRunCommand(string rootPath, string? framework)
    {
        return framework switch
        {
            "flask" => ("python", "main.py", rootPath),
            "fastapi" => ("python", "-m uvicorn main:app --reload", rootPath),
            "django" => ("python", "manage.py runserver", rootPath),
            _ => ("python", "main.py", rootPath)
        };
    }

    private (string Command, string Args, string WorkingDir) GetWebRunCommand(string rootPath, string? mode)
    {
        var packageJsonPath = Path.Combine(rootPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            var content = File.ReadAllText(packageJsonPath);
            if (content.Contains("\"dev\""))
                return ("npm", "run dev", rootPath);
            if (content.Contains("\"start\""))
                return ("npm", "start", rootPath);
        }

        // For static sites, use a simple server if available
        return ("npx", "serve .", rootPath);
    }

    private bool IsToolAvailableForCommand(string command)
    {
        var tool = command.ToLower().Split(' ')[0];
        return tool switch
        {
            "dotnet" => ToolDetectionService.Instance.IsDotNetAvailable(),
            "npm" or "npx" => ToolDetectionService.Instance.IsNpmAvailable(),
            "node" => ToolDetectionService.Instance.IsNodeAvailable(),
            "python" or "python3" => ToolDetectionService.Instance.IsPythonAvailable(),
            "git" => ToolDetectionService.Instance.IsGitAvailable(),
            _ => true // Assume available for unknown commands
        };
    }

    private string? FindPublishArtifact(Project project)
    {
        var publishPath = project.PublishPath;

        if (!Directory.Exists(publishPath))
            return null;

        switch (project.ProjectType)
        {
            case ProjectType.DotNet:
                // Find the main .exe (exclude createdump.exe)
                var exeFiles = Directory.GetFiles(publishPath, "*.exe", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).Equals("createdump.exe", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (exeFiles.Length == 1)
                    return exeFiles[0];

                // If multiple, try to find one matching project name
                var projectNameExe = exeFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(project.Name, StringComparison.OrdinalIgnoreCase));

                return projectNameExe ?? exeFiles.FirstOrDefault();

            case ProjectType.Node:
            case ProjectType.Web:
                // For web projects, the artifact is the dist folder or index.html
                var distPath = Path.Combine(project.RootPath, "dist");
                if (Directory.Exists(distPath))
                    return distPath;

                var buildPath = Path.Combine(project.RootPath, "build");
                if (Directory.Exists(buildPath))
                    return buildPath;

                return null;

            case ProjectType.Python:
                // Python doesn't have a traditional artifact, return main.py
                var mainPy = Path.Combine(project.RootPath, "main.py");
                return File.Exists(mainPy) ? mainPy : null;

            default:
                return null;
        }
    }
}
