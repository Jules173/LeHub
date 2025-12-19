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
    private Category? _selectedCategory;
    private string _categoryText = string.Empty;
    private string _tagsText = string.Empty;
    private bool _isEditMode;

    public AppFormWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadCategories();
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

    public ObservableCollection<Category> Categories { get; } = new();

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            if (value != null)
            {
                _categoryText = value.Name;
                OnPropertyChanged(nameof(CategoryText));
            }
        }
    }

    public string CategoryText
    {
        get => _categoryText;
        set
        {
            _categoryText = value;
            OnPropertyChanged();
        }
    }

    public string TagsText
    {
        get => _tagsText;
        set
        {
            _tagsText = value;
            OnPropertyChanged();
        }
    }

    public AppEntry? ResultApp { get; private set; }

    public int EditingAppId { get; private set; }

    private void LoadCategories()
    {
        Categories.Clear();
        var cats = DatabaseService.Instance.GetAllCategories();
        foreach (var cat in cats)
        {
            Categories.Add(cat);
        }
    }

    public void LoadFromApp(AppEntry app)
    {
        EditingAppId = app.Id;
        Name = app.Name;
        ExePath = app.ExePath;
        Arguments = app.Arguments ?? "";

        if (app.Category != null)
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == app.Category.Id);
            CategoryText = app.Category.Name;
        }
        else
        {
            CategoryText = "";
        }

        TagsText = string.Join(", ", app.Tags.Select(t => t.Name));
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

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var categoryName = CategoryText?.Trim();
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            MessageBox.Show("Entrez un nom de categorie.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if already exists
        var existing = Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedCategory = existing;
            return;
        }

        // Create new category
        var id = DatabaseService.Instance.AddCategory(categoryName);
        var newCategory = new Category { Id = id, Name = categoryName, CreatedAt = DateTime.Now };
        Categories.Add(newCategory);
        SelectedCategory = newCategory;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Le nom est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        // Determine category ID
        int? categoryId = null;
        Category? category = null;

        if (SelectedCategory != null)
        {
            categoryId = SelectedCategory.Id;
            category = SelectedCategory;
        }
        else if (!string.IsNullOrWhiteSpace(CategoryText))
        {
            // Create new category from text
            var existing = Categories.FirstOrDefault(c => c.Name.Equals(CategoryText.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                categoryId = existing.Id;
                category = existing;
            }
            else
            {
                var id = DatabaseService.Instance.GetOrCreateCategory(CategoryText.Trim());
                categoryId = id;
                category = new Category { Id = id, Name = CategoryText.Trim() };
            }
        }

        ResultApp = new AppEntry
        {
            Id = EditingAppId,
            Name = Name.Trim(),
            ExePath = ExePath.Trim(),
            Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
            CategoryId = categoryId,
            Category = category,
            Tags = ParseTags(TagsText)
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static List<Tag> ParseTags(string tagsText)
    {
        if (string.IsNullOrWhiteSpace(tagsText))
            return new List<Tag>();

        return tagsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => new Tag { Name = t.Trim() })
            .ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
