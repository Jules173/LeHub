using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LeHub.Models;
using LeHub.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class PresetEditWindow : Window
{
    private int _presetId;
    private readonly ObservableCollection<EditablePresetApp> _selectedApps = new();
    private readonly List<AvailableApp> _allAvailableApps = new();
    private readonly List<AvailableApp> _filteredAvailableApps = new();

    public Preset? ResultPreset { get; private set; }

    public PresetEditWindow()
    {
        InitializeComponent();
        SelectedAppsControl.ItemsSource = _selectedApps;
    }

    public void LoadPreset(PresetViewModel preset, IEnumerable<AppCardViewModel> allApps)
    {
        _presetId = preset.Id;
        NameTextBox.Text = preset.Name;
        DelayTextBox.Text = preset.DelayMs.ToString();

        // Load selected apps in order
        _selectedApps.Clear();
        foreach (var presetApp in preset.Apps.OrderBy(a => a.OrderIndex))
        {
            if (presetApp.App != null)
            {
                _selectedApps.Add(new EditablePresetApp
                {
                    Id = presetApp.AppId,
                    Name = presetApp.App.Name,
                    OrderDisplay = (_selectedApps.Count + 1).ToString()
                });
            }
        }

        // Load available apps (excluding already selected)
        _allAvailableApps.Clear();
        var selectedIds = _selectedApps.Select(a => a.Id).ToHashSet();
        foreach (var app in allApps)
        {
            if (!selectedIds.Contains(app.Id))
            {
                _allAvailableApps.Add(new AvailableApp { Id = app.Id, Name = app.Name });
            }
        }

        RefreshAvailableApps();
    }

    private void RefreshOrderDisplay()
    {
        for (int i = 0; i < _selectedApps.Count; i++)
        {
            _selectedApps[i].OrderDisplay = (i + 1).ToString();
        }

        // Force refresh
        SelectedAppsControl.ItemsSource = null;
        SelectedAppsControl.ItemsSource = _selectedApps;
    }

    private void RefreshAvailableApps()
    {
        var searchText = SearchBox.Text?.ToLowerInvariant() ?? "";
        var selectedIds = _selectedApps.Select(a => a.Id).ToHashSet();

        _filteredAvailableApps.Clear();
        foreach (var app in _allAvailableApps)
        {
            if (!selectedIds.Contains(app.Id) &&
                (string.IsNullOrEmpty(searchText) || app.Name.ToLowerInvariant().Contains(searchText)))
            {
                _filteredAvailableApps.Add(app);
            }
        }

        AvailableAppsControl.ItemsSource = null;
        AvailableAppsControl.ItemsSource = _filteredAvailableApps;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshAvailableApps();
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is EditablePresetApp app)
        {
            var index = _selectedApps.IndexOf(app);
            if (index > 0)
            {
                _selectedApps.Move(index, index - 1);
                RefreshOrderDisplay();
            }
        }
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is EditablePresetApp app)
        {
            var index = _selectedApps.IndexOf(app);
            if (index < _selectedApps.Count - 1)
            {
                _selectedApps.Move(index, index + 1);
                RefreshOrderDisplay();
            }
        }
    }

    private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is EditablePresetApp app)
        {
            _selectedApps.Remove(app);
            RefreshOrderDisplay();
            RefreshAvailableApps();
        }
    }

    private void AvailableApp_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is AvailableApp app)
        {
            _selectedApps.Add(new EditablePresetApp
            {
                Id = app.Id,
                Name = app.Name,
                OrderDisplay = (_selectedApps.Count + 1).ToString()
            });
            RefreshOrderDisplay();
            RefreshAvailableApps();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Le nom est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        if (!int.TryParse(DelayTextBox.Text, out var delayMs) || delayMs < 0)
        {
            MessageBox.Show("Le delai doit etre un nombre positif.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            DelayTextBox.Focus();
            return;
        }

        if (_selectedApps.Count == 0)
        {
            MessageBox.Show("Selectionnez au moins une application.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultPreset = new Preset
        {
            Id = _presetId,
            Name = name,
            DelayMs = delayMs,
            Apps = _selectedApps.Select((a, i) => new PresetApp
            {
                PresetId = _presetId,
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
}

public class EditablePresetApp
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OrderDisplay { get; set; } = string.Empty;
}

public class AvailableApp
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
