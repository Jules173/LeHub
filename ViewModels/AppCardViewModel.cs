using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using LeHub.Models;
using LeHub.Services;
using File = System.IO.File;

namespace LeHub.ViewModels;

public class AppCardViewModel : INotifyPropertyChanged
{
    private readonly AppEntry _app;
    private ImageSource? _icon;
    private bool _isFavorite;
    private bool _isExeMissing;
    private string _name = string.Empty;
    private string _exePath = string.Empty;
    private string? _arguments;
    private string _category = string.Empty;
    private int _sortOrder;

    public AppCardViewModel(AppEntry app)
    {
        _app = app;
        _name = app.Name;
        _exePath = app.ExePath;
        _arguments = app.Arguments;
        _isFavorite = app.IsFavorite;
        _category = app.Category;
        _sortOrder = app.SortOrder;

        CheckExeExists();
        LoadIcon();
    }

    public int Id => _app.Id;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                _app.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string ExePath
    {
        get => _exePath;
        set
        {
            if (_exePath != value)
            {
                _exePath = value;
                _app.ExePath = value;
                CheckExeExists();
                OnPropertyChanged();
            }
        }
    }

    public string? Arguments
    {
        get => _arguments;
        set
        {
            if (_arguments != value)
            {
                _arguments = value;
                _app.Arguments = value;
                OnPropertyChanged();
            }
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                _app.Category = value;
                OnPropertyChanged();
            }
        }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set
        {
            if (_sortOrder != value)
            {
                _sortOrder = value;
                _app.SortOrder = value;
                OnPropertyChanged();
            }
        }
    }

    public List<Tag> Tags => _app.Tags;

    public string TagsDisplay => Tags.Count > 0
        ? string.Join(", ", Tags.Select(t => t.Name))
        : "";

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                _app.IsFavorite = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExeMissing
    {
        get => _isExeMissing;
        private set
        {
            if (_isExeMissing != value)
            {
                _isExeMissing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanLaunch));
            }
        }
    }

    public bool CanLaunch => !IsExeMissing && !string.IsNullOrWhiteSpace(ExePath);

    public ImageSource? Icon
    {
        get => _icon;
        private set
        {
            _icon = value;
            OnPropertyChanged();
        }
    }

    public AppEntry GetModel() => _app;

    private void CheckExeExists()
    {
        IsExeMissing = !string.IsNullOrWhiteSpace(_exePath) && !File.Exists(_exePath);
    }

    private void LoadIcon()
    {
        Icon = IconExtractorService.Instance.GetIcon(ExePath);
    }

    public void RefreshIcon()
    {
        IconExtractorService.Instance.InvalidateCache(ExePath);
        Icon = IconExtractorService.Instance.GetIcon(ExePath);
    }

    public void RefreshFromModel()
    {
        Name = _app.Name;
        ExePath = _app.ExePath;
        Arguments = _app.Arguments;
        Category = _app.Category;
        SortOrder = _app.SortOrder;
        IsFavorite = _app.IsFavorite;
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
        CheckExeExists();
        RefreshIcon();
    }

    public void UpdateFromData(AppEntry data)
    {
        _app.Name = data.Name;
        _app.ExePath = data.ExePath;
        _app.Arguments = data.Arguments;
        _app.Category = data.Category;
        _app.Tags = data.Tags;

        Name = data.Name;
        ExePath = data.ExePath;
        Arguments = data.Arguments;
        Category = data.Category;

        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
        CheckExeExists();
        RefreshIcon();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
