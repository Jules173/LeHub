using System.ComponentModel;
using System.Runtime.CompilerServices;
using LeHub.Models;

namespace LeHub.ViewModels;

public class ProjectCardViewModel : INotifyPropertyChanged
{
    private readonly Project _project;
    private bool _isSelected;
    private bool _isFolderMissing;
    private string _name = string.Empty;
    private string _rootPath = string.Empty;
    private ProjectType _projectType;
    private string? _framework;

    public ProjectCardViewModel(Project project)
    {
        _project = project;
        _name = project.Name;
        _rootPath = project.RootPath;
        _projectType = project.ProjectType;
        _framework = project.Framework;

        CheckFolderExists();
    }

    public int Id => _project.Id;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                _project.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string RootPath
    {
        get => _rootPath;
        set
        {
            if (_rootPath != value)
            {
                _rootPath = value;
                _project.RootPath = value;
                CheckFolderExists();
                OnPropertyChanged();
            }
        }
    }

    public ProjectType ProjectType
    {
        get => _projectType;
        set
        {
            if (_projectType != value)
            {
                _projectType = value;
                _project.ProjectType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeDisplay));
            }
        }
    }

    public string? Framework
    {
        get => _framework;
        set
        {
            if (_framework != value)
            {
                _framework = value;
                _project.Framework = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FrameworkDisplay));
            }
        }
    }

    public string TypeDisplay => _project.TypeDisplay;

    public string FrameworkDisplay => _project.FrameworkDisplay;

    public string? LastPublishMode
    {
        get => _project.LastPublishMode;
        set
        {
            if (_project.LastPublishMode != value)
            {
                _project.LastPublishMode = value;
                OnPropertyChanged();
            }
        }
    }

    public List<Tag> Tags => _project.Tags;

    public string TagsDisplay => Tags.Count > 0
        ? string.Join(", ", Tags.Select(t => t.Name))
        : "";

    public List<string> DisplayTags
    {
        get
        {
            var result = new List<string>();
            var tagNames = Tags.Select(t => t.Name).ToList();

            if (tagNames.Count <= 2)
            {
                result.AddRange(tagNames);
            }
            else
            {
                result.Add(tagNames[0]);
                result.Add(tagNames[1]);
                result.Add($"+{tagNames.Count - 2}");
            }
            return result;
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsFolderMissing
    {
        get => _isFolderMissing;
        private set
        {
            if (_isFolderMissing != value)
            {
                _isFolderMissing = value;
                OnPropertyChanged();
            }
        }
    }

    public Project GetModel() => _project;

    private void CheckFolderExists()
    {
        IsFolderMissing = !string.IsNullOrWhiteSpace(_rootPath) && !System.IO.Directory.Exists(_rootPath);
    }

    public void RefreshFromModel()
    {
        Name = _project.Name;
        RootPath = _project.RootPath;
        ProjectType = _project.ProjectType;
        Framework = _project.Framework;
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(DisplayTags));
        OnPropertyChanged(nameof(LastPublishMode));
        CheckFolderExists();
    }

    public void UpdateFromData(Project data)
    {
        _project.Name = data.Name;
        _project.RootPath = data.RootPath;
        _project.ProjectType = data.ProjectType;
        _project.Framework = data.Framework;
        _project.LastPublishMode = data.LastPublishMode;
        _project.Tags = data.Tags;

        _name = data.Name;
        _rootPath = data.RootPath;
        _projectType = data.ProjectType;
        _framework = data.Framework;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(RootPath));
        OnPropertyChanged(nameof(ProjectType));
        OnPropertyChanged(nameof(TypeDisplay));
        OnPropertyChanged(nameof(Framework));
        OnPropertyChanged(nameof(FrameworkDisplay));
        OnPropertyChanged(nameof(LastPublishMode));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(DisplayTags));

        CheckFolderExists();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
