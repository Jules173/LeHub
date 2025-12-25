using System.Diagnostics;
using System.Text;
using System.Windows;
using LeHub.Models;
using LeHub.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace LeHub.Views;

public partial class PresetRunLogWindow : Window
{
    private readonly PresetViewModel _preset;
    private readonly List<AppCardViewModel> _allApps;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly StringBuilder _logBuilder = new();

    public PresetRunLogWindow(PresetViewModel preset, List<AppCardViewModel> allApps)
    {
        InitializeComponent();

        _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        _allApps = allApps ?? new List<AppCardViewModel>();

        PresetNameText.Text = preset.Name;
        Loaded += PresetRunLogWindow_Loaded;
    }

    private async void PresetRunLogWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RunPresetAsync();
    }

    private async Task RunPresetAsync()
    {
        _isRunning = true;
        _cts = new CancellationTokenSource();
        StopButton.IsEnabled = true;
        CloseButton.IsEnabled = false;

        var apps = _preset.Apps?.Where(a => a != null).OrderBy(a => a.OrderIndex).ToList() ?? new List<PresetApp>();
        var totalApps = apps.Count;
        var launchedCount = 0;
        var skippedCount = 0;

        AppendLog($"Demarrage du preset '{_preset.Name}'");
        AppendLog($"  - {totalApps} application(s) configuree(s)");
        AppendLog($"  - Delai entre les apps: {_preset.DelayMs} ms");
        AppendLog("");

        ProgressBar.Maximum = totalApps;

        try
        {
            foreach (var presetApp in apps)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    AppendLog("");
                    AppendLog("[ARRETE] Execution interrompue par l'utilisateur.");
                    break;
                }

                var currentIndex = apps.IndexOf(presetApp) + 1;
                ProgressBar.Value = currentIndex;
                ProgressText.Text = $"{currentIndex}/{totalApps} applications";

                // Try to get app from in-memory list first
                var appVm = _allApps.FirstOrDefault(a => a.Id == presetApp.AppId);

                string appName;
                string? exePath;
                string? arguments;

                if (appVm != null)
                {
                    appName = appVm.Name;
                    exePath = appVm.ExePath;
                    arguments = appVm.Arguments;
                }
                else if (presetApp.App != null)
                {
                    appName = presetApp.App.Name;
                    exePath = presetApp.App.ExePath;
                    arguments = presetApp.App.Arguments;
                }
                else
                {
                    AppendLog($"[{currentIndex}] Application ID {presetApp.AppId}: SUPPRIMEE - ignoree");
                    skippedCount++;
                    continue;
                }

                // Check if file exists
                if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
                {
                    AppendLog($"[{currentIndex}] {appName}: INTROUVABLE - ignoree");
                    AppendLog($"       Chemin: {exePath ?? "(vide)"}");
                    skippedCount++;
                    continue;
                }

                // Launch app
                try
                {
                    AppendLog($"[{currentIndex}] {appName}: Lancement...");

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments ?? "",
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                    launchedCount++;
                    AppendLog($"       OK");

                    // Wait delay before next app (if not last)
                    if (currentIndex < totalApps && _preset.DelayMs > 0)
                    {
                        StatusText.Text = $"Attente {_preset.DelayMs} ms...";
                        await Task.Delay(_preset.DelayMs, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    AppendLog("");
                    AppendLog("[ARRETE] Execution interrompue par l'utilisateur.");
                    break;
                }
                catch (Exception ex)
                {
                    AppendLog($"       ERREUR: {ex.Message}");
                    skippedCount++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("");
            AppendLog("[ARRETE] Execution interrompue par l'utilisateur.");
        }
        catch (Exception ex)
        {
            AppendLog("");
            AppendLog($"[ERREUR] Erreur inattendue: {ex.Message}");
        }

        // Summary
        AppendLog("");
        AppendLog("=== RESUME ===");
        AppendLog($"  Lancees: {launchedCount}");
        AppendLog($"  Ignorees: {skippedCount}");
        AppendLog($"  Total: {totalApps}");

        _isRunning = false;
        StatusText.Text = _cts.Token.IsCancellationRequested ? "Arrete" : "Termine";
        StopButton.IsEnabled = false;
        CloseButton.IsEnabled = true;
    }

    private void AppendLog(string message)
    {
        _logBuilder.AppendLine(message);
        LogText.Text = _logBuilder.ToString();

        // Auto-scroll to bottom
        LogScrollViewer.ScrollToEnd();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StopButton.IsEnabled = false;
        StatusText.Text = "Arret en cours...";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            var result = MessageBox.Show(
                "L'execution est en cours. Voulez-vous l'arreter et fermer?",
                "Confirmer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _cts?.Cancel();
            }
            else
            {
                return;
            }
        }

        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnClosing(e);
    }
}
