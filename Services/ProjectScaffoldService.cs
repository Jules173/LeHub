using System.Diagnostics;
using System.IO;
using LeHub.Models;

namespace LeHub.Services;

public class ProjectScaffoldService
{
    private static ProjectScaffoldService? _instance;
    public static ProjectScaffoldService Instance => _instance ??= new ProjectScaffoldService();

    private ProjectScaffoldService() { }

    public async Task<(bool Success, string Message, string? ProjectPath)> CreateProjectAsync(
        string name,
        string parentFolder,
        ProjectType type,
        string? framework)
    {
        var projectPath = Path.Combine(parentFolder, name);

        try
        {
            // 1. Create directory
            if (Directory.Exists(projectPath))
            {
                return (false, $"Le dossier '{projectPath}' existe deja.", null);
            }

            Directory.CreateDirectory(projectPath);

            // 2. Create .lehub folder structure
            var lehubFolder = Path.Combine(projectPath, ".lehub");
            Directory.CreateDirectory(lehubFolder);
            Directory.CreateDirectory(Path.Combine(lehubFolder, "publish"));

            // 3. Git init (if available)
            if (ToolDetectionService.Instance.IsGitAvailable())
            {
                var gitResult = await RunCommandAsync("git", "init", projectPath);
                if (!gitResult.Success)
                {
                    return (false, $"Erreur git init: {gitResult.Output}", null);
                }

                // Create .gitignore
                await CreateGitignoreAsync(projectPath, type);
            }

            // 4. Scaffold based on type
            var scaffoldResult = await ScaffoldProjectAsync(projectPath, name, type, framework);
            if (!scaffoldResult.Success)
            {
                return (false, scaffoldResult.Message, null);
            }

            // 5. Initial commit (if git available)
            if (ToolDetectionService.Instance.IsGitAvailable())
            {
                await RunCommandAsync("git", "add .", projectPath);
                await RunCommandAsync("git", "commit -m \"init project\"", projectPath);
            }

            return (true, "Projet cree avec succes.", projectPath);
        }
        catch (Exception ex)
        {
            // Cleanup on failure
            try
            {
                if (Directory.Exists(projectPath))
                {
                    Directory.Delete(projectPath, true);
                }
            }
            catch { }

            return (false, $"Erreur: {ex.Message}", null);
        }
    }

    private async Task<(bool Success, string Message)> ScaffoldProjectAsync(
        string path, string name, ProjectType type, string? framework)
    {
        return type switch
        {
            ProjectType.DotNet => await ScaffoldDotNetAsync(path, name, framework),
            ProjectType.Node => await ScaffoldNodeAsync(path, framework),
            ProjectType.Python => await ScaffoldPythonAsync(path, name, framework),
            ProjectType.Web => await ScaffoldWebAsync(path, framework),
            _ => (false, "Type de projet inconnu")
        };
    }

    private async Task<(bool Success, string Message)> ScaffoldDotNetAsync(string path, string name, string? framework)
    {
        if (!ToolDetectionService.Instance.IsDotNetAvailable())
        {
            return (false, "dotnet CLI non installe. Installez le SDK .NET depuis https://dotnet.microsoft.com/download");
        }

        var template = framework ?? "console";
        var result = await RunCommandAsync("dotnet", $"new {template} -n \"{name}\" --force", path);

        if (!result.Success)
        {
            return (false, $"Erreur dotnet new: {result.Output}");
        }

        return (true, "Projet .NET cree.");
    }

    private async Task<(bool Success, string Message)> ScaffoldNodeAsync(string path, string? framework)
    {
        if (!ToolDetectionService.Instance.IsNpmAvailable())
        {
            return (false, "npm non installe. Installez Node.js depuis https://nodejs.org/");
        }

        // For complex frameworks, use specific create commands
        switch (framework)
        {
            case "react":
                var reactResult = await RunCommandAsync("npx", "create-react-app . --template typescript", path);
                if (!reactResult.Success)
                {
                    // Fallback: create basic package.json
                    await CreateBasicPackageJson(path, "react", new[] { "react", "react-dom", "react-scripts", "typescript" });
                }
                break;

            case "vue":
                var vueResult = await RunCommandAsync("npm", "create vue@latest . -- --typescript --router --pinia", path);
                if (!vueResult.Success)
                {
                    await CreateBasicPackageJson(path, "vue", new[] { "vue" });
                }
                break;

            case "next":
                var nextResult = await RunCommandAsync("npx", "create-next-app . --typescript --eslint --app", path);
                if (!nextResult.Success)
                {
                    await CreateBasicPackageJson(path, "next", new[] { "next", "react", "react-dom" });
                }
                break;

            case "express":
                await CreateBasicPackageJson(path, "express", new[] { "express" });
                await CreateExpressTemplate(path);
                break;

            case "vanilla":
            default:
                await CreateBasicPackageJson(path, "vanilla", Array.Empty<string>());
                await CreateVanillaTemplate(path);
                break;
        }

        return (true, "Projet Node.js cree.");
    }

    private async Task<(bool Success, string Message)> ScaffoldPythonAsync(string path, string name, string? framework)
    {
        // Create basic Python structure
        var mainPy = framework switch
        {
            "flask" => GetFlaskTemplate(name),
            "fastapi" => GetFastApiTemplate(name),
            "django" => GetDjangoNote(),
            _ => GetBasicPythonTemplate(name)
        };

        await File.WriteAllTextAsync(Path.Combine(path, "main.py"), mainPy);

        // Create requirements.txt
        var requirements = framework switch
        {
            "flask" => "flask>=2.0\n",
            "fastapi" => "fastapi>=0.100\nuvicorn>=0.23\n",
            "django" => "django>=4.0\n",
            _ => "# Add your dependencies here\n"
        };

        await File.WriteAllTextAsync(Path.Combine(path, "requirements.txt"), requirements);

        // Create venv if python available
        if (ToolDetectionService.Instance.IsPythonAvailable())
        {
            await RunCommandAsync("python", "-m venv venv", path);
        }

        return (true, "Projet Python cree.");
    }

    private async Task<(bool Success, string Message)> ScaffoldWebAsync(string path, string? framework)
    {
        if (framework == "vite" && ToolDetectionService.Instance.IsNpmAvailable())
        {
            var result = await RunCommandAsync("npm", "create vite@latest . -- --template vanilla-ts", path);
            if (result.Success)
            {
                return (true, "Projet Vite cree.");
            }
        }

        // Create basic static site
        await CreateStaticTemplate(path);
        return (true, "Site statique cree.");
    }

    private async Task CreateGitignoreAsync(string path, ProjectType type)
    {
        var content = GetGitignoreContent(type);
        // Add .lehub/ entry
        content += "\n# LeHub\n.lehub/\n";
        await File.WriteAllTextAsync(Path.Combine(path, ".gitignore"), content);
    }

    private string GetGitignoreContent(ProjectType type) => type switch
    {
        ProjectType.DotNet => @"# .NET
bin/
obj/
*.user
*.suo
.vs/
*.csproj.user
",
        ProjectType.Node => @"# Node.js
node_modules/
dist/
build/
.env
.env.local
*.log
",
        ProjectType.Python => @"# Python
__pycache__/
*.pyc
*.pyo
venv/
.env
*.egg-info/
dist/
build/
",
        ProjectType.Web => @"# Web
node_modules/
dist/
build/
.env
",
        _ => ""
    };

    private async Task CreateBasicPackageJson(string path, string name, string[] dependencies)
    {
        var deps = dependencies.Length > 0
            ? string.Join(",\n    ", dependencies.Select(d => $"\"{d}\": \"latest\""))
            : "";

        var content = $$"""
        {
          "name": "{{name.ToLower().Replace(" ", "-")}}",
          "version": "1.0.0",
          "description": "",
          "main": "index.js",
          "scripts": {
            "dev": "echo \"No dev script configured\"",
            "build": "echo \"No build script configured\"",
            "start": "node index.js",
            "test": "echo \"No tests configured\""
          },
          "dependencies": {
            {{deps}}
          }
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(path, "package.json"), content);
    }

    private async Task CreateExpressTemplate(string path)
    {
        var content = """
        const express = require('express');
        const app = express();
        const port = process.env.PORT || 3000;

        app.use(express.json());

        app.get('/', (req, res) => {
            res.json({ message: 'Hello from Express!' });
        });

        app.listen(port, () => {
            console.log(`Server running at http://localhost:${port}`);
        });
        """;

        await File.WriteAllTextAsync(Path.Combine(path, "index.js"), content);
    }

    private async Task CreateVanillaTemplate(string path)
    {
        var htmlContent = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>My App</title>
            <link rel="stylesheet" href="style.css">
        </head>
        <body>
            <h1>Hello World!</h1>
            <script src="main.js"></script>
        </body>
        </html>
        """;

        var cssContent = """
        body {
            font-family: system-ui, -apple-system, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 2rem;
        }
        """;

        var jsContent = """
        console.log('App loaded');
        """;

        await File.WriteAllTextAsync(Path.Combine(path, "index.html"), htmlContent);
        await File.WriteAllTextAsync(Path.Combine(path, "style.css"), cssContent);
        await File.WriteAllTextAsync(Path.Combine(path, "main.js"), jsContent);
    }

    private async Task CreateStaticTemplate(string path)
    {
        await CreateVanillaTemplate(path);
    }

    private string GetFlaskTemplate(string name) => $$"""
        from flask import Flask, jsonify

        app = Flask(__name__)

        @app.route('/')
        def home():
            return jsonify({"message": "Hello from {{name}}!"})

        if __name__ == '__main__':
            app.run(debug=True, port=5000)
        """;

    private string GetFastApiTemplate(string name) => $$"""
        from fastapi import FastAPI

        app = FastAPI(title="{{name}}")

        @app.get("/")
        def root():
            return {"message": "Hello from {{name}}!"}

        # Run with: uvicorn main:app --reload
        """;

    private string GetDjangoNote() => """
        # Django project
        # Run: django-admin startproject myproject .
        # Then: python manage.py runserver
        print("Create Django project with: django-admin startproject myproject .")
        """;

    private string GetBasicPythonTemplate(string name) =>
$@"#!/usr/bin/env python3
""""""
{name} - Main entry point
""""""

def main():
    print(""Hello from {name}!"")

if __name__ == ""__main__"":
    main()
";

    private async Task<(bool Success, string Output)> RunCommandAsync(
        string command, string args, string workingDir)
    {
        try
        {
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

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "Impossible de demarrer le processus");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var fullOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";

            return (process.ExitCode == 0, fullOutput);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
