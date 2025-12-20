using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LeHub.Models;
using LeHub.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class AppFormWindow : Window, INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _exePath = string.Empty;
    private string _arguments = string.Empty;
    private string _tagSearchText = string.Empty;
    private bool _isEditMode;
    private readonly List<SelectableTag> _allTags = new();

    public AppFormWindow()
    {
        InitializeComponent();
        DataContext = this;
        FilteredTags = new ObservableCollection<SelectableTag>();
        LoadTags();
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            _isEditMode = value;
            FormTitle.Text = value ? "Modifier l'application" : "Ajouter une application";
            Title = FormTitle.Text;
        }
    }

    public string AppName
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public new string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string ExePath
    {
        get => _exePath;
        set
        {
            _exePath = value;
            OnPropertyChanged();
        }
    }

    public string Arguments
    {
        get => _arguments;
        set
        {
            _arguments = value;
            OnPropertyChanged();
        }
    }

    public string TagSearchText
    {
        get => _tagSearchText;
        set
        {
            if (_tagSearchText != value)
            {
                _tagSearchText = value;
                OnPropertyChanged();
                ApplyTagFilter();
            }
        }
    }

    public ObservableCollection<SelectableTag> FilteredTags { get; }

    public AppEntry? ResultApp { get; private set; }

    public int EditingAppId { get; private set; }

    private void LoadTags()
    {
        _allTags.Clear();
        FilteredTags.Clear();

        var tags = DatabaseService.Instance.GetAllTags();
        foreach (var tag in tags)
        {
            var selectableTag = new SelectableTag
            {
                Id = tag.Id,
                Name = tag.Name,
                IsSelected = false
            };
            _allTags.Add(selectableTag);
            FilteredTags.Add(selectableTag);
        }
    }

    private void ApplyTagFilter()
    {
        FilteredTags.Clear();

        var filtered = _allTags.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_tagSearchText))
        {
            var search = _tagSearchText.ToLowerInvariant();
            filtered = filtered.Where(t => t.Name.ToLowerInvariant().Contains(search));
        }

        foreach (var tag in filtered)
        {
            FilteredTags.Add(tag);
        }
    }

    public void LoadFromApp(AppEntry app)
    {
        EditingAppId = app.Id;
        Name = app.Name;
        ExePath = app.ExePath;
        Arguments = app.Arguments ?? "";

        // Mark existing tags as selected
        foreach (var tag in app.Tags)
        {
            var selectableTag = _allTags.FirstOrDefault(t => t.Id == tag.Id);
            if (selectableTag != null)
            {
                selectableTag.IsSelected = true;
            }
        }

        IsEditMode = true;
    }

    public void PreFill(string name, string exePath)
    {
        Name = name;
        ExePath = exePath;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selectionner un executable",
            Filter = "Executables (*.exe)|*.exe|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            ExePath = dialog.FileName;

            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        var inputWindow = new InputDialog("Nouveau tag", "Nom du tag:");
        inputWindow.Owner = this;

        if (inputWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputWindow.InputValue))
        {
            var tagName = inputWindow.InputValue.Trim();

            // Check if already exists
            var existing = _allTags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsSelected = true;
                return;
            }

            // Create new tag in database
            var id = DatabaseService.Instance.AddTag(tagName);
            var newTag = new SelectableTag { Id = id, Name = tagName, IsSelected = true };
            _allTags.Add(newTag);
            ApplyTagFilter();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Le nom est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        // Get selected tags
        var selectedTags = _allTags.Where(t => t.IsSelected)
            .Select(t => new Tag { Id = t.Id, Name = t.Name })
            .ToList();

        ResultApp = new AppEntry
        {
            Id = EditingAppId,
            Name = Name.Trim(),
            ExePath = ExePath.Trim(),
            Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
            Tags = selectedTags
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class SelectableTag : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
