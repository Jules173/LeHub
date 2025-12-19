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

    public PresetFormWindow()
    {
        InitializeComponent();
        DataContext = this;
        AvailableApps = new ObservableCollection<SelectableApp>();
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

    public ObservableCollection<SelectableApp> AvailableApps { get; }

    public Preset? ResultPreset { get; private set; }

    public void LoadApps(IEnumerable<AppCardViewModel> apps)
    {
        AvailableApps.Clear();
        foreach (var app in apps)
        {
            AvailableApps.Add(new SelectableApp
            {
                Id = app.Id,
                Name = app.Name,
                IsSelected = false
            });
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

        var selectedApps = AvailableApps.Where(a => a.IsSelected).ToList();
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
