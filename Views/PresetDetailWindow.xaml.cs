using System.Collections.ObjectModel;
using System.Windows;
using LeHub.Models;
using LeHub.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class PresetDetailWindow : Window
{
    private readonly PresetViewModel _preset;
    private readonly Action<PresetViewModel>? _onRun;
    private readonly Action<PresetViewModel>? _onRunWithLogs;
    private readonly Action<PresetViewModel>? _onEdit;
    private readonly Action<PresetViewModel>? _onDuplicate;
    private readonly Action<PresetViewModel>? _onDelete;

    public PresetDetailWindow(
        PresetViewModel preset,
        Action<PresetViewModel>? onRun = null,
        Action<PresetViewModel>? onEdit = null,
        Action<PresetViewModel>? onDuplicate = null,
        Action<PresetViewModel>? onDelete = null,
        Action<PresetViewModel>? onRunWithLogs = null)
    {
        InitializeComponent();

        _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        _onRun = onRun;
        _onEdit = onEdit;
        _onDuplicate = onDuplicate;
        _onDelete = onDelete;
        _onRunWithLogs = onRunWithLogs;

        LoadPresetData();
    }

    private void LoadPresetData()
    {
        try
        {
            PresetNameText.Text = _preset.Name ?? "Sans nom";
            DelayText.Text = $"{_preset.DelayMs} ms";

            var apps = _preset.Apps ?? new List<PresetApp>();
            AppCountText.Text = apps.Count == 1
                ? "1 application"
                : $"{apps.Count} applications";

            var orderedApps = apps
                .Where(a => a != null)
                .OrderBy(a => a.OrderIndex)
                .Select((a, i) => new PresetAppDisplay
                {
                    OrderDisplay = (i + 1).ToString(),
                    AppName = a.App?.Name ?? "Application supprimee",
                    IsMissing = a.App == null || string.IsNullOrEmpty(a.App.ExePath) || !System.IO.File.Exists(a.App.ExePath)
                })
                .ToList();

            AppsListControl.ItemsSource = orderedApps;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeHub] PresetDetailWindow.LoadPresetData error: {ex.Message}");
            MessageBox.Show($"Erreur lors du chargement des donnees: {ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        _onRun?.Invoke(_preset);
        Close();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        _onEdit?.Invoke(_preset);
        Close();
    }

    private void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        _onDuplicate?.Invoke(_preset);
        Close();
    }

    private void RunWithLogsButton_Click(object sender, RoutedEventArgs e)
    {
        _onRunWithLogs?.Invoke(_preset);
        Close();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Voulez-vous vraiment supprimer le preset '{_preset.Name}' ?",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _onDelete?.Invoke(_preset);
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class PresetAppDisplay
{
    public string OrderDisplay { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public bool IsMissing { get; set; }
}
