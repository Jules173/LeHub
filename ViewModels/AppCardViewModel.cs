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
    private bool _isSelected;
    private string _name = string.Empty;
    private string _exePath = string.Empty;
    private string? _arguments;
    private int? _categoryId;
    private Category? _category;
    private int _sortOrder;

    public AppCardViewModel(AppEntry app)
    {
        _app = app;
        _name = app.Name;
        _exePath = app.ExePath;
        _arguments = app.Arguments;
        _isFavorite = app.IsFavorite;
        _categoryId = app.CategoryId;
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

    public int? CategoryId
    {
        get => _categoryId;
        set
        {
            if (_categoryId != value)
            {
                _categoryId = value;
                _app.CategoryId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CategoryName));
            }
        }
    }

    public Category? Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                _app.Category = value;
                _categoryId = value?.Id;
                _app.CategoryId = value?.Id;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CategoryName));
            }
        }
    }

    public string CategoryName => _category?.Name ?? "";

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

    // Display up to 2 tags + "+N" indicator
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
        CategoryId = _app.CategoryId;
        Category = _app.Category;
        SortOrder = _app.SortOrder;
        IsFavorite = _app.IsFavorite;
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(CategoryName));
        CheckExeExists();
        RefreshIcon();
    }

    public void UpdateFromData(AppEntry data)
    {
        System.Diagnostics.Debug.WriteLine($"[LeHub] AppCardViewModel.UpdateFromData: Before - Id={Id}, _name='{_name}', _app.Name='{_app.Name}'");
        System.Diagnostics.Debug.WriteLine($"[LeHub] AppCardViewModel.UpdateFromData: Incoming data.Name='{data.Name}'");

        _app.Name = data.Name;
        _app.ExePath = data.ExePath;
        _app.Arguments = data.Arguments;
        _app.CategoryId = data.CategoryId;
        _app.Category = data.Category;
        _app.Tags = data.Tags;

        _name = data.Name;
        _exePath = data.ExePath;
        _arguments = data.Arguments;
        _categoryId = data.CategoryId;
        _category = data.Category;

        System.Diagnostics.Debug.WriteLine($"[LeHub] AppCardViewModel.UpdateFromData: After - _name='{_name}', _app.Name='{_app.Name}'");

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ExePath));
        OnPropertyChanged(nameof(Arguments));
        OnPropertyChanged(nameof(CategoryId));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(DisplayTags));

        System.Diagnostics.Debug.WriteLine($"[LeHub] AppCardViewModel.UpdateFromData: PropertyChanged fired, Name property now returns='{Name}'");

        CheckExeExists();
        RefreshIcon();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
