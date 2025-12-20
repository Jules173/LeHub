using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LeHub.Models;
using LeHub.Services;
using MessageBox = System.Windows.MessageBox;
using File = System.IO.File;

namespace LeHub.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private bool _showFavoritesOnly;
    private TagFilterOption? _selectedTagFilter;
    private ObservableCollection<AppCardViewModel> _filteredApps = new();
    private readonly List<AppCardViewModel> _allApps = new();
    private ObservableCollection<TagFilterOption> _tagFilterOptions = new();
    private ObservableCollection<PresetViewModel> _presets = new();
    private AppCardViewModel? _selectedApp;

    public MainViewModel()
    {
        AddAppCommand = new RelayCommand(ExecuteAddApp);
        ManageTagsCommand = new RelayCommand(ExecuteManageTags);
        LaunchAppCommand = new RelayCommand(ExecuteLaunchApp);
        EditAppCommand = new RelayCommand(ExecuteEditApp);
        DeleteAppCommand = new RelayCommand(ExecuteDeleteApp);
        ToggleFavoriteCommand = new RelayCommand(ExecuteToggleFavorite);
        MoveUpCommand = new RelayCommand(ExecuteMoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(ExecuteMoveDown, CanMoveDown);
        AddPresetCommand = new RelayCommand(ExecuteAddPreset);
        LaunchPresetCommand = new RelayCommand(ExecuteLaunchPreset);
        DeletePresetCommand = new RelayCommand(ExecuteDeletePreset);
        OpenLocationCommand = new RelayCommand(ExecuteOpenLocation);
        CopyPathCommand = new RelayCommand(ExecuteCopyPath);
        LaunchAsAdminCommand = new RelayCommand(ExecuteLaunchAsAdmin);

        LoadApps();
        LoadPresets();
        LoadTags();
    }

    public ObservableCollection<AppCardViewModel> FilteredApps
    {
        get => _filteredApps;
        private set
        {
            _filteredApps = value;
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

    public ObservableCollection<PresetViewModel> Presets
    {
        get => _presets;
        private set
        {
            _presets = value;
            OnPropertyChanged();
        }
    }

    public List<AppCardViewModel> AllApps => _allApps;

    public AppCardViewModel? SelectedApp
    {
        get => _selectedApp;
        set
        {
            if (_selectedApp != value)
            {
                _selectedApp = value;
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

    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set
        {
            if (_showFavoritesOnly != value)
            {
                _showFavoritesOnly = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }


    public ICommand AddAppCommand { get; }
    public ICommand ManageTagsCommand { get; }
    public ICommand LaunchAppCommand { get; }
    public ICommand EditAppCommand { get; }
    public ICommand DeleteAppCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand AddPresetCommand { get; }
    public ICommand LaunchPresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand OpenLocationCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand LaunchAsAdminCommand { get; }

    public event Action? RequestAddApp;
    public event Action<AppCardViewModel>? RequestEditApp;
    public event Action<string, string>? RequestAddAppWithData;
    public event Action? RequestAddPreset;
    public event Action? RequestManageTags;

    public void LoadApps()
    {
        _allApps.Clear();
        var apps = DatabaseService.Instance.GetAllApps();

        foreach (var app in apps)
        {
            _allApps.Add(new AppCardViewModel(app));
        }

        ApplyFilter();
    }

    public void LoadPresets()
    {
        var presets = DatabaseService.Instance.GetAllPresets();
        Presets = new ObservableCollection<PresetViewModel>(
            presets.Select(p => new PresetViewModel(p)));
    }

    public void LoadTags()
    {
        var tags = DatabaseService.Instance.GetAllTags();
        var options = new List<TagFilterOption> { TagFilterOption.All };
        options.AddRange(tags.Select(TagFilterOption.FromTag));
        TagFilterOptions = new ObservableCollection<TagFilterOption>(options);

        // Select "Tous" by default if nothing selected
        if (_selectedTagFilter == null)
        {
            SelectedTagFilter = TagFilterOptions.FirstOrDefault();
        }
    }

    private void ApplyFilter()
    {
        var filtered = _allApps.AsEnumerable();

        if (_showFavoritesOnly)
        {
            filtered = filtered.Where(a => a.IsFavorite);
        }

        // Filter by tag (Tag = null means "All")
        if (_selectedTagFilter?.Tag != null)
        {
            var tagId = _selectedTagFilter.Tag.Id;
            filtered = filtered.Where(a => a.Tags.Any(t => t.Id == tagId));
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(a =>
                a.Name.ToLowerInvariant().Contains(search) ||
                a.Tags.Any(t => t.Name.ToLowerInvariant().Contains(search)));
        }

        FilteredApps = new ObservableCollection<AppCardViewModel>(
            filtered.OrderBy(a => a.SortOrder).ThenBy(a => a.Name));
    }

    public void SelectApp(AppCardViewModel? app)
    {
        // Deselect previous
        if (_selectedApp != null)
        {
            _selectedApp.IsSelected = false;
        }

        // Select new
        _selectedApp = app;
        if (_selectedApp != null)
        {
            _selectedApp.IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectedApp));
    }

    private void ExecuteAddApp(object? _)
    {
        RequestAddApp?.Invoke();
    }

    private void ExecuteManageTags(object? _)
    {
        RequestManageTags?.Invoke();
    }

    public void ExecuteLaunchApp(object? parameter)
    {
        if (parameter is not AppCardViewModel app)
            return;

        LaunchApplication(app);
    }

    public void LaunchApplication(AppCardViewModel app)
    {
        if (string.IsNullOrWhiteSpace(app.ExePath))
        {
            MessageBox.Show($"Le chemin de l'application '{app.Name}' n'est pas defini.",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (app.IsExeMissing)
        {
            MessageBox.Show($"L'application '{app.Name}' n'a pas ete trouvee :\n{app.ExePath}",
                "Fichier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = app.ExePath,
                Arguments = app.Arguments ?? "",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de lancer '{app.Name}' :\n{ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteEditApp(object? parameter)
    {
        if (parameter is AppCardViewModel app)
        {
            RequestEditApp?.Invoke(app);
        }
    }

    public void ExecuteDeleteApp(object? parameter)
    {
        if (parameter is not AppCardViewModel app)
            return;

        DeleteAppWithConfirmation(app);
    }

    public void DeleteAppWithConfirmation(AppCardViewModel app)
    {
        var result = MessageBox.Show(
            $"Voulez-vous vraiment supprimer '{app.Name}' de LeHub ?",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Deselect if this was selected
            if (_selectedApp == app)
            {
                SelectApp(null);
            }

            DatabaseService.Instance.DeleteApp(app.Id);
            _allApps.Remove(app);
            ApplyFilter();
            LoadTags();
        }
    }

    private void ExecuteToggleFavorite(object? parameter)
    {
        if (parameter is not AppCardViewModel app)
            return;

        app.IsFavorite = !app.IsFavorite;
        DatabaseService.Instance.UpdateFavorite(app.Id, app.IsFavorite);

        if (_showFavoritesOnly)
        {
            ApplyFilter();
        }
    }

    private bool CanMoveUp(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return false;
        var index = _allApps.IndexOf(app);
        return index > 0;
    }

    private void ExecuteMoveUp(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return;
        var index = _allApps.IndexOf(app);
        if (index <= 0) return;

        var other = _allApps[index - 1];
        (app.SortOrder, other.SortOrder) = (other.SortOrder, app.SortOrder);

        DatabaseService.Instance.UpdateSortOrder(app.Id, app.SortOrder);
        DatabaseService.Instance.UpdateSortOrder(other.Id, other.SortOrder);

        _allApps[index] = other;
        _allApps[index - 1] = app;

        ApplyFilter();
    }

    private bool CanMoveDown(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return false;
        var index = _allApps.IndexOf(app);
        return index >= 0 && index < _allApps.Count - 1;
    }

    private void ExecuteMoveDown(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return;
        var index = _allApps.IndexOf(app);
        if (index < 0 || index >= _allApps.Count - 1) return;

        var other = _allApps[index + 1];
        (app.SortOrder, other.SortOrder) = (other.SortOrder, app.SortOrder);

        DatabaseService.Instance.UpdateSortOrder(app.Id, app.SortOrder);
        DatabaseService.Instance.UpdateSortOrder(other.Id, other.SortOrder);

        _allApps[index] = other;
        _allApps[index + 1] = app;

        ApplyFilter();
    }

    private void ExecuteAddPreset(object? _)
    {
        RequestAddPreset?.Invoke();
    }

    private async void ExecuteLaunchPreset(object? parameter)
    {
        if (parameter is not PresetViewModel preset) return;

        foreach (var presetApp in preset.Apps)
        {
            if (presetApp.App == null) continue;

            var appVm = _allApps.FirstOrDefault(a => a.Id == presetApp.AppId);
            if (appVm != null && appVm.CanLaunch)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = appVm.ExePath,
                        Arguments = appVm.Arguments ?? "",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);

                    if (preset.DelayMs > 0)
                    {
                        await Task.Delay(preset.DelayMs);
                    }
                }
                catch
                {
                    // Continue with next app
                }
            }
        }
    }

    private void ExecuteDeletePreset(object? parameter)
    {
        if (parameter is not PresetViewModel preset) return;

        var result = MessageBox.Show(
            $"Voulez-vous vraiment supprimer le preset '{preset.Name}' ?",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            DatabaseService.Instance.DeletePreset(preset.Id);
            Presets.Remove(preset);
        }
    }

    private void ExecuteOpenLocation(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return;

        if (string.IsNullOrWhiteSpace(app.ExePath))
        {
            MessageBox.Show("Le chemin de l'application n'est pas defini.",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(app.ExePath);
        if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
        {
            MessageBox.Show($"Le dossier n'existe pas :\n{directory}",
                "Dossier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Open explorer and select the file
            Process.Start("explorer.exe", $"/select,\"{app.ExePath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir l'emplacement :\n{ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteCopyPath(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return;

        if (!string.IsNullOrWhiteSpace(app.ExePath))
        {
            try
            {
                System.Windows.Clipboard.SetText(app.ExePath);
            }
            catch
            {
                // Clipboard access can fail sometimes
            }
        }
    }

    private void ExecuteLaunchAsAdmin(object? parameter)
    {
        if (parameter is not AppCardViewModel app) return;

        if (string.IsNullOrWhiteSpace(app.ExePath))
        {
            MessageBox.Show($"Le chemin de l'application '{app.Name}' n'est pas defini.",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (app.IsExeMissing)
        {
            MessageBox.Show($"L'application '{app.Name}' n'a pas ete trouvee :\n{app.ExePath}",
                "Fichier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = app.ExePath,
                Arguments = app.Arguments ?? "",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            // User cancelled UAC or other error
            if (!ex.Message.Contains("annul"))
            {
                MessageBox.Show($"Impossible de lancer '{app.Name}' en admin :\n{ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void AddNewApp(AppEntry newApp)
    {
        var id = DatabaseService.Instance.AddApp(newApp);
        newApp.Id = id;
        newApp.SortOrder = _allApps.Count;
        _allApps.Add(new AppCardViewModel(newApp));
        ApplyFilter();
        LoadTags();
    }

    public void UpdateExistingApp(AppCardViewModel cardVm, AppEntry updatedData)
    {
        var model = cardVm.GetModel();

        System.Diagnostics.Debug.WriteLine($"[LeHub] UpdateExistingApp: Original Id={model.Id}, Original Name='{model.Name}'");
        System.Diagnostics.Debug.WriteLine($"[LeHub] UpdateExistingApp: New Name='{updatedData.Name}'");

        // Preserve the original Id and other fields
        updatedData.Id = model.Id;
        updatedData.SortOrder = model.SortOrder;
        updatedData.IsFavorite = model.IsFavorite;
        updatedData.CreatedAt = model.CreatedAt;
        updatedData.UpdatedAt = DateTime.Now;

        // Update the model directly
        model.Name = updatedData.Name;
        model.ExePath = updatedData.ExePath;
        model.Arguments = updatedData.Arguments;
        model.CategoryId = updatedData.CategoryId;
        model.Category = updatedData.Category;
        model.Tags = updatedData.Tags;

        // Save to database
        var rowsAffected = DatabaseService.Instance.UpdateApp(model);
        System.Diagnostics.Debug.WriteLine($"[LeHub] UpdateExistingApp: DB update returned {rowsAffected} rows affected");

        // Update the ViewModel to refresh UI
        cardVm.UpdateFromData(model);

        System.Diagnostics.Debug.WriteLine($"[LeHub] UpdateExistingApp: After UpdateFromData - cardVm.Name='{cardVm.Name}'");

        ApplyFilter();

        // Verify the VM in the filtered list has the updated name
        var vmInList = FilteredApps.FirstOrDefault(a => a.Id == cardVm.Id);
        System.Diagnostics.Debug.WriteLine($"[LeHub] UpdateExistingApp: After ApplyFilter - vmInList?.Name='{vmInList?.Name}', Same instance={ReferenceEquals(vmInList, cardVm)}");

        LoadTags();
    }

    public void HandleFileDrop(string[] files)
    {
        foreach (var file in files)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();

            if (ext == ".exe")
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                RequestAddAppWithData?.Invoke(name, file);
                return;
            }
            else if (ext == ".lnk")
            {
                var info = ShortcutResolverService.Instance.ResolveShortcut(file);
                if (info != null)
                {
                    var name = ShortcutResolverService.Instance.GetAppNameFromPath(file);
                    RequestAddAppWithData?.Invoke(name, info.TargetPath);
                    return;
                }
                else
                {
                    MessageBox.Show("Impossible de lire ce raccourci.",
                        "Format non supporte", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Seuls les fichiers .exe et .lnk sont supportes.",
                    "Format non supporte", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public void AddPreset(Preset preset)
    {
        var id = DatabaseService.Instance.AddPreset(preset);
        preset.Id = id;
        Presets.Add(new PresetViewModel(preset));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PresetViewModel : INotifyPropertyChanged
{
    private readonly Preset _preset;

    public PresetViewModel(Preset preset)
    {
        _preset = preset;
    }

    public int Id => _preset.Id;
    public string Name => _preset.Name;
    public int DelayMs => _preset.DelayMs;
    public List<PresetApp> Apps => _preset.Apps;

    public string AppsDisplay => Apps.Count > 0
        ? string.Join(", ", Apps.Select(a => a.App?.Name ?? "?"))
        : "Aucune app";

    public event PropertyChangedEventHandler? PropertyChanged;
}
