using System.Windows;
using System.Windows.Media;
using LeHub.Models;
using LeHub.Services;
using LeHub.ViewModels;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace LeHub.Views;

public partial class ProjectTaskLogWindow : Window
{
    private readonly ProjectCardViewModel _project;
    private readonly ProjectTask _task;
    private readonly bool _addToHub;
    private readonly string? _publishMode;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public string? ArtifactPath { get; private set; }
    public bool TaskSucceeded { get; private set; }

    public ProjectTaskLogWindow(ProjectCardViewModel project, ProjectTask task, bool addToHub = false, string? publishMode = null)
    {
        InitializeComponent();

        _project = project;
        _task = task;
        _addToHub = addToHub;
        _publishMode = publishMode;

        TaskTitle.Text = GetTaskTitle(task);
        ProjectName.Text = project.Name;
        Title = $"{GetTaskTitle(task)} - {project.Name}";

        Loaded += OnLoaded;
    }

    private string GetTaskTitle(ProjectTask task) => task switch
    {
        ProjectTask.Build => "Build",
        ProjectTask.Run => "Execution",
        ProjectTask.Test => "Tests",
        ProjectTask.Publish => "Publication",
        _ => "Tache"
    };

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync();
    }

    private async Task RunTaskAsync()
    {
        _isRunning = true;
        _cts = new CancellationTokenSource();

        var runner = new ProjectTaskRunner();
        runner.OutputReceived += OnOutputReceived;
        runner.ErrorReceived += OnErrorReceived;
        runner.TaskCompleted += OnTaskCompleted;

        AppendLog($"Demarrage de {GetTaskTitle(_task).ToLower()}...\n");
        AppendLog($"Projet: {_project.RootPath}\n");
        AppendLog(new string('-', 50) + "\n");

        try
        {
            var result = await runner.RunTaskAsync(
                _project.GetModel(),
                _task,
                _publishMode,
                _cts.Token);

            TaskSucceeded = result.Success;
            ArtifactPath = result.ArtifactPath;

            if (result.Success)
            {
                AppendLog($"\n{new string('-', 50)}");
                AppendLog($"\nTermine avec succes (code: {result.ExitCode})");

                if (!string.IsNullOrEmpty(result.ArtifactPath))
                {
                    AppendLog($"\nArtefact: {result.ArtifactPath}");
                }

                SetStatus("Succes", "#50a060");
            }
            else
            {
                AppendLog($"\n{new string('-', 50)}");
                AppendLog($"\nEchec (code: {result.ExitCode})");
                SetStatus("Echec", "#d05050");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("\n\nOperation annulee par l'utilisateur.");
            SetStatus("Annule", "#d0a040");
        }
        catch (Exception ex)
        {
            AppendLog($"\n\nErreur: {ex.Message}");
            SetStatus("Erreur", "#d05050");
        }
        finally
        {
            _isRunning = false;
            runner.OutputReceived -= OnOutputReceived;
            runner.ErrorReceived -= OnErrorReceived;
            runner.TaskCompleted -= OnTaskCompleted;

            Dispatcher.Invoke(() =>
            {
                CancelButton.IsEnabled = false;
                CloseButton.IsEnabled = true;
            });
        }
    }

    private void OnOutputReceived(string output)
    {
        Dispatcher.Invoke(() => AppendLog(output));
    }

    private void OnErrorReceived(string error)
    {
        Dispatcher.Invoke(() => AppendLog(error, isError: true));
    }

    private void OnTaskCompleted(int exitCode)
    {
        // Handled in RunTaskAsync
    }

    private void AppendLog(string text, bool isError = false)
    {
        LogTextBox.AppendText(text);
        LogTextBox.ScrollToEnd();
        LogScrollViewer.ScrollToEnd();
    }

    private void SetStatus(string text, string color)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
            CancelButton.IsEnabled = false;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = TaskSucceeded;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isRunning)
        {
            var result = System.Windows.MessageBox.Show(
                "Une tache est en cours. Voulez-vous l'annuler ?",
                "Tache en cours",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _cts?.Cancel();
            }
            else
            {
                e.Cancel = true;
            }
        }

        base.OnClosing(e);
    }
}
