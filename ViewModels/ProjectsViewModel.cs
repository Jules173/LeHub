using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LeHub.Models;
using LeHub.Services;
using MessageBox = System.Windows.MessageBox;
using Directory = System.IO.Directory;
using SearchOption = System.IO.SearchOption;

namespace LeHub.ViewModels;

public class ProjectsViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private TagFilterOption? _selectedTagFilter;
    private ObservableCollection<ProjectCardViewModel> _filteredProjects = new();
    private readonly List<ProjectCardViewModel> _allProjects = new();
    private ObservableCollection<TagFilterOption> _tagFilterOptions = new();
    private ProjectCardViewModel? _selectedProject;

    public ProjectsViewModel()
    {
        AddProjectCommand = new RelayCommand(ExecuteAddProject);
        EditProjectCommand = new RelayCommand(ExecuteEditProject);
        DeleteProjectCommand = new RelayCommand(ExecuteDeleteProject);
        OpenFolderCommand = new RelayCommand(ExecuteOpenFolder);
        OpenVSCodeCommand = new RelayCommand(ExecuteOpenVSCode, CanOpenVSCode);
        OpenVisualStudioCommand = new RelayCommand(ExecuteOpenVisualStudio, CanOpenVisualStudio);
        BuildCommand = new RelayCommand(ExecuteBuild);
        RunCommand = new RelayCommand(ExecuteRun);
        TestCommand = new RelayCommand(ExecuteTest);
        PublishCommand = new RelayCommand(ExecutePublish);
        PublishAndAddToHubCommand = new RelayCommand(ExecutePublishAndAddToHub);

        LoadProjects();
        LoadTags();
    }

    public ObservableCollection<ProjectCardViewModel> FilteredProjects
    {
        get => _filteredProjects;
        private set
        {
            _filteredProjects = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TagFilterOption> TagFilterOptions
    {
        get => _tagFilterOptions;
        private set
        {
            _tagFilterOptions = value;
            OnPropertyChanged();
        }
    }

    public TagFilterOption? SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            if (_selectedTagFilter != value)
            {
                _selectedTagFilter = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    public List<ProjectCardViewModel> AllProjects => _allProjects;

    public ProjectCardViewModel? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject != value)
            {
                _selectedProject = value;
                OnPropertyChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    public ICommand AddProjectCommand { get; }
    public ICommand EditProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenVSCodeCommand { get; }
    public ICommand OpenVisualStudioCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand PublishAndAddToHubCommand { get; }

    public event Action? RequestAddProject;
    public event Action<ProjectCardViewModel>? RequestEditProject;
    public event Action<ProjectCardViewModel, ProjectTask, bool>? RequestRunTask;
    public event Action<ProjectCardViewModel>? RequestDeleteProject;
    public event Action<ProjectCardViewModel>? RequestPublishMode;

    public void LoadProjects()
    {
        _allProjects.Clear();
        var projects = DatabaseService.Instance.GetAllProjects();

        foreach (var project in projects)
        {
            _allProjects.Add(new ProjectCardViewModel(project));
        }

        ApplyFilter();
    }

    public void LoadTags()
    {
        var tags = DatabaseService.Instance.GetAllTags();
        var options = new List<TagFilterOption> { TagFilterOption.All };
        options.AddRange(tags.Select(TagFilterOption.FromTag));
        TagFilterOptions = new ObservableCollection<TagFilterOption>(options);

        if (_selectedTagFilter == null)
        {
            SelectedTagFilter = TagFilterOptions.FirstOrDefault();
        }
    }

    private void ApplyFilter()
    {
        var filtered = _allProjects.AsEnumerable();

        if (_selectedTagFilter?.Tag != null)
        {
            var tagId = _selectedTagFilter.Tag.Id;
            filtered = filtered.Where(p => p.Tags.Any(t => t.Id == tagId));
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(search) ||
                p.TypeDisplay.ToLowerInvariant().Contains(search) ||
                (p.FrameworkDisplay?.ToLowerInvariant().Contains(search) ?? false) ||
                p.Tags.Any(t => t.Name.ToLowerInvariant().Contains(search)));
        }

        FilteredProjects = new ObservableCollection<ProjectCardViewModel>(
            filtered.OrderBy(p => p.Name));
    }

    public void SelectProject(ProjectCardViewModel? project)
    {
        if (_selectedProject != null)
        {
            _selectedProject.IsSelected = false;
        }

        _selectedProject = project;
        if (_selectedProject != null)
        {
            _selectedProject.IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectedProject));
    }

    private void ExecuteAddProject(object? _)
    {
        RequestAddProject?.Invoke();
    }

    private void ExecuteEditProject(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            RequestEditProject?.Invoke(project);
        }
    }

    private void ExecuteDeleteProject(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            RequestDeleteProject?.Invoke(project);
        }
    }

    private void ExecuteOpenFolder(object? parameter)
    {
        if (parameter is not ProjectCardViewModel project)
            return;

        if (project.IsFolderMissing)
        {
            MessageBox.Show($"Le dossier du projet '{project.Name}' n'existe plus :\n{project.RootPath}",
                "Dossier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Try VS Code first (no pre-detection)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                Arguments              = $"/c code -n \"{project.RootPath}\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                WorkingDirectory       = project.RootPath,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                // optional: wait a bit to detect immediate failure
                p.WaitForExit(800);
                if (!p.HasExited || p.ExitCode == 0)
                    return; // VS Code launched OK (or still launching)
            }
        }
        catch
        {
            // ignore => fallback
        }

        // Fallback Explorer
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"\"{project.RootPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir :\n{ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanOpenVSCode(object? parameter)
    {
        return ToolDetectionService.Instance.IsVSCodeAvailable();
    }

    private void ExecuteOpenVSCode(object? parameter)
    {
        if (parameter is not ProjectCardViewModel project)
            return;

        if (project.IsFolderMissing)
        {
            MessageBox.Show($"Le dossier du projet '{project.Name}' n'existe plus.",
                "Dossier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ToolDetectionService.Instance.IsVSCodeAvailable())
        {
            MessageBox.Show("VS Code n'est pas installe ou n'est pas dans le PATH.\n\nInstallez-le depuis https://code.visualstudio.com/",
                "VS Code non trouve", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Launch via cmd.exe to properly resolve 'code' from PATH
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c code -n \"{project.RootPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = project.RootPath
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VS Code] Error: {ex.Message}");
            MessageBox.Show($"Impossible d'ouvrir VS Code :\n{ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanOpenVisualStudio(object? parameter)
    {
        if (parameter is not ProjectCardViewModel project)
            return false;

        // Only for .NET projects with .sln or .csproj
        if (project.ProjectType != ProjectType.DotNet)
            return false;

        var vsPath = ToolDetectionService.Instance.FindVisualStudio();
        return vsPath != null;
    }

    private void ExecuteOpenVisualStudio(object? parameter)
    {
        if (parameter is not ProjectCardViewModel project)
            return;

        if (project.IsFolderMissing)
        {
            MessageBox.Show($"Le dossier du projet '{project.Name}' n'existe plus.",
                "Dossier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vsPath = ToolDetectionService.Instance.FindVisualStudio();
        if (vsPath == null)
        {
            MessageBox.Show("Visual Studio n'a pas ete trouve.",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Find .sln or .csproj
        var slnFile = Directory.GetFiles(project.RootPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var csprojFile = Directory.GetFiles(project.RootPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
        var fileToOpen = slnFile ?? csprojFile ?? project.RootPath;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = vsPath,
                Arguments = $"\"{fileToOpen}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir Visual Studio :\n{ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteBuild(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            RequestRunTask?.Invoke(project, ProjectTask.Build, false);
        }
    }

    private void ExecuteRun(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            RequestRunTask?.Invoke(project, ProjectTask.Run, false);
        }
    }

    private void ExecuteTest(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            RequestRunTask?.Invoke(project, ProjectTask.Test, false);
        }
    }

    private void ExecutePublish(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            // For Web/Node, show publish mode dialog
            if (project.ProjectType == ProjectType.Web || project.ProjectType == ProjectType.Node)
            {
                RequestPublishMode?.Invoke(project);
            }
            else
            {
                RequestRunTask?.Invoke(project, ProjectTask.Publish, false);
            }
        }
    }

    private void ExecutePublishAndAddToHub(object? parameter)
    {
        if (parameter is ProjectCardViewModel project)
        {
            // For Web/Node, show publish mode dialog first
            if (project.ProjectType == ProjectType.Web || project.ProjectType == ProjectType.Node)
            {
                RequestPublishMode?.Invoke(project);
            }
            else
            {
                RequestRunTask?.Invoke(project, ProjectTask.Publish, true);
            }
        }
    }

    public void RefreshProject(int projectId)
    {
        var project = _allProjects.FirstOrDefault(p => p.Id == projectId);
        if (project != null)
        {
            var updated = DatabaseService.Instance.GetAllProjects().FirstOrDefault(p => p.Id == projectId);
            if (updated != null)
            {
                project.UpdateFromData(updated);
            }
        }
        ApplyFilter();
    }

    public void RemoveProject(ProjectCardViewModel project)
    {
        if (_selectedProject == project)
        {
            SelectProject(null);
        }
        _allProjects.Remove(project);
        ApplyFilter();
    }

    public void AddProjectToList(Project project)
    {
        _allProjects.Add(new ProjectCardViewModel(project));
        ApplyFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
