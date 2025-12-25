using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LeHub.Models;
using LeHub.Services;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class ProjectFormWindow : Window
{
    private readonly List<SelectableTag> _allTags = new();
    private bool _isEditMode;
    private int _editingProjectId;

    public ProjectFormWindow()
    {
        InitializeComponent();

        // Load project types
        TypeComboBox.ItemsSource = Enum.GetValues<ProjectType>();
        TypeComboBox.SelectedIndex = 0;

        // Load tags
        LoadTags();

        // Set default folder
        FolderTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            _isEditMode = value;
            FormTitle.Text = value ? "Modifier le projet" : "Nouveau projet";
            Title = FormTitle.Text;
            CreateButton.Content = value ? "Enregistrer" : "Creer";
        }
    }

    public Project? ResultProject { get; private set; }

    private void LoadTags()
    {
        _allTags.Clear();
        var tags = DatabaseService.Instance.GetAllTags();
        foreach (var tag in tags)
        {
            _allTags.Add(new SelectableTag { Id = tag.Id, Name = tag.Name, IsSelected = false });
        }
        TagsItemsControl.ItemsSource = _allTags;
    }

    public void LoadFromProject(Project project)
    {
        _editingProjectId = project.Id;
        IsEditMode = true;

        NameTextBox.Text = project.Name;
        FolderTextBox.Text = System.IO.Path.GetDirectoryName(project.RootPath) ?? "";
        TypeComboBox.SelectedItem = project.ProjectType;

        // Select the framework
        if (!string.IsNullOrEmpty(project.Framework))
        {
            var frameworks = FrameworkOptions.GetFrameworks(project.ProjectType);
            var match = frameworks.FirstOrDefault(f => f.Id == project.Framework);
            if (match != null)
            {
                FrameworkComboBox.SelectedItem = match;
            }
        }

        // Select tags
        foreach (var tag in _allTags)
        {
            tag.IsSelected = project.Tags.Any(t => t.Id == tag.Id);
        }
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeComboBox.SelectedItem is ProjectType selectedType)
        {
            var frameworks = FrameworkOptions.GetFrameworks(selectedType);
            FrameworkComboBox.ItemsSource = frameworks;
            FrameworkComboBox.SelectedIndex = frameworks.Count > 0 ? 0 : -1;

            UpdateToolWarnings(selectedType);
        }
    }

    private void UpdateToolWarnings(ProjectType type)
    {
        var warnings = new List<string>();

        switch (type)
        {
            case ProjectType.DotNet:
                if (!ToolDetectionService.Instance.IsDotNetAvailable())
                    warnings.Add($"dotnet CLI - {ToolDetectionService.GetInstallUrl("dotnet")}");
                break;
            case ProjectType.Node:
                if (!ToolDetectionService.Instance.IsNpmAvailable())
                    warnings.Add($"npm (Node.js) - {ToolDetectionService.GetInstallUrl("npm")}");
                break;
            case ProjectType.Python:
                if (!ToolDetectionService.Instance.IsPythonAvailable())
                    warnings.Add($"Python - {ToolDetectionService.GetInstallUrl("python")}");
                break;
            case ProjectType.Web:
                // Web projects can work without tools (static sites)
                break;
        }

        if (!ToolDetectionService.Instance.IsGitAvailable())
        {
            warnings.Add($"Git (optionnel) - {ToolDetectionService.GetInstallUrl("git")}");
        }

        if (warnings.Count > 0)
        {
            WarningText.Text = string.Join("\n", warnings);
            WarningBorder.Visibility = Visibility.Visible;
        }
        else
        {
            WarningBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choisir le dossier parent du projet",
            UseDescriptionForTitle = true,
            SelectedPath = FolderTextBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        var parentFolder = FolderTextBox.Text.Trim();
        var type = (ProjectType)TypeComboBox.SelectedItem;
        var framework = (FrameworkComboBox.SelectedItem as FrameworkInfo)?.Id;

        // Validation
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Veuillez entrer un nom de projet.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(parentFolder) || !System.IO.Directory.Exists(parentFolder))
        {
            MessageBox.Show("Veuillez choisir un dossier parent valide.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Disable button during creation
        CreateButton.IsEnabled = false;
        CreateButton.Content = "Creation...";

        try
        {
            if (IsEditMode)
            {
                // Update existing project
                ResultProject = new Project
                {
                    Id = _editingProjectId,
                    Name = name,
                    RootPath = System.IO.Path.Combine(parentFolder, name),
                    ProjectType = type,
                    Framework = framework,
                    Tags = _allTags.Where(t => t.IsSelected).Select(t => new Tag { Id = t.Id, Name = t.Name }).ToList()
                };

                DialogResult = true;
                Close();
            }
            else
            {
                // Create new project using scaffold service
                var result = await ProjectScaffoldService.Instance.CreateProjectAsync(name, parentFolder, type, framework);

                if (result.Success && result.ProjectPath != null)
                {
                    ResultProject = new Project
                    {
                        Name = name,
                        RootPath = result.ProjectPath,
                        ProjectType = type,
                        Framework = framework,
                        Tags = _allTags.Where(t => t.IsSelected).Select(t => new Tag { Id = t.Id, Name = t.Name }).ToList()
                    };

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CreateButton.IsEnabled = true;
            CreateButton.Content = IsEditMode ? "Enregistrer" : "Creer";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
