using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LeHub.Models;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class AppFormWindow : Window, INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _exePath = string.Empty;
    private string _arguments = string.Empty;
    private string _category = string.Empty;
    private string _tagsText = string.Empty;
    private bool _isEditMode;

    public AppFormWindow()
    {
        InitializeComponent();
        DataContext = this;
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

    public string Category
    {
        get => _category;
        set
        {
            _category = value;
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

    public void LoadFromApp(AppEntry app)
    {
        Name = app.Name;
        ExePath = app.ExePath;
        Arguments = app.Arguments ?? "";
        Category = app.Category ?? "";
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

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Le nom est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        ResultApp = new AppEntry
        {
            Name = Name.Trim(),
            ExePath = ExePath.Trim(),
            Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
            Category = Category?.Trim() ?? "",
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
