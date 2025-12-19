using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LeHub.Models;
using LeHub.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class PresetFormWindow : Window, INotifyPropertyChanged
{
    private string _presetName = string.Empty;
    private int _delayMs = 200;
    private string _searchText = string.Empty;
    private readonly List<SelectableApp> _allApps = new();

    public PresetFormWindow()
    {
        InitializeComponent();
        DataContext = this;
        AvailableApps = new ObservableCollection<SelectableApp>();
        FilteredApps = new ObservableCollection<SelectableApp>();
    }

    public string PresetName
    {
        get => _presetName;
        set
        {
            _presetName = value;
            OnPropertyChanged();
        }
    }

    public int DelayMs
    {
        get => _delayMs;
        set
        {
            _delayMs = value;
            OnPropertyChanged();
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

    public ObservableCollection<SelectableApp> AvailableApps { get; }
    public ObservableCollection<SelectableApp> FilteredApps { get; }

    public Preset? ResultPreset { get; private set; }

    public void LoadApps(IEnumerable<AppCardViewModel> apps)
    {
        _allApps.Clear();
        AvailableApps.Clear();
        FilteredApps.Clear();

        foreach (var app in apps)
        {
            var selectableApp = new SelectableApp
            {
                Id = app.Id,
                Name = app.Name,
                IsSelected = false
            };
            _allApps.Add(selectableApp);
            AvailableApps.Add(selectableApp);
            FilteredApps.Add(selectableApp);
        }
    }

    private void ApplyFilter()
    {
        FilteredApps.Clear();

        var filtered = _allApps.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(a => a.Name.ToLowerInvariant().Contains(search));
        }

        foreach (var app in filtered)
        {
            FilteredApps.Add(app);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetName))
        {
            MessageBox.Show("Le nom est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        // Get selected apps from all apps (not just filtered)
        var selectedApps = _allApps.Where(a => a.IsSelected).ToList();
        if (selectedApps.Count == 0)
        {
            MessageBox.Show("Selectionnez au moins une application.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultPreset = new Preset
        {
            Name = PresetName.Trim(),
            DelayMs = DelayMs,
            Apps = selectedApps.Select((a, i) => new PresetApp
            {
                AppId = a.Id,
                OrderIndex = i
            }).ToList()
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

public class SelectableApp : INotifyPropertyChanged
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
