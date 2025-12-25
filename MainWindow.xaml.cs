using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using LeHub.ViewModels;
using LeHub.Views;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace LeHub;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ProjectsViewModel _projectsVM;

    // Expose ViewModels for binding
    public MainViewModel AppsVM => _viewModel;
    public ProjectsViewModel ProjectsVM => _projectsVM;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        _projectsVM = new ProjectsViewModel();
        DataContext = this; // Use this as DataContext to expose both ViewModels

        _viewModel.RequestAddApp += OnRequestAddApp;
        _viewModel.RequestEditApp += OnRequestEditApp;
        _viewModel.RequestAddAppWithData += OnRequestAddAppWithData;
        _viewModel.RequestAddPreset += OnRequestAddPreset;
        _viewModel.RequestManageTags += OnRequestManageTags;
        _viewModel.RequestRunPresetWithLogs += OnRequestRunPresetWithLogs;

        _projectsVM.RequestAddProject += OnRequestAddProject;
        _projectsVM.RequestEditProject += OnRequestEditProject;
        _projectsVM.RequestDeleteProject += OnRequestDeleteProject;
        _projectsVM.RequestRunTask += OnRequestRunTask;
        _projectsVM.RequestPublishMode += OnRequestPublishMode;

        SourceInitialized += MainWindow_SourceInitialized;
    }

    // Win32 interop for proper window resizing
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;

        // Handle maximize to respect taskbar
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var work = monitorInfo.rcWork;
                    var monitor_rect = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.x = work.left - monitor_rect.left;
                    mmi.ptMaxPosition.y = work.top - monitor_rect.top;
                    mmi.ptMaxSize.x = work.right - work.left;
                    mmi.ptMaxSize.y = work.bottom - work.top;
                    Marshal.StructureToPtr(mmi, lParam, true);
                }
            }
            handled = false;
        }

        return IntPtr.Zero;
    }

    // Title bar event handlers
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // Update maximize icon based on window state
        if (MaximizeIcon != null)
        {
            if (WindowState == WindowState.Maximized)
            {
                // Restore icon (two overlapping rectangles)
                MaximizeIcon.Data = System.Windows.Media.Geometry.Parse("M2,0 L10,0 L10,8 L2,8 Z M0,2 L8,2 L8,10 L0,10 Z");
                MaximizeButton.ToolTip = "Restaurer";
            }
            else
            {
                // Maximize icon (single rectangle)
                MaximizeIcon.Data = System.Windows.Media.Geometry.Parse("M0,0 L10,0 L10,10 L0,10 Z");
                MaximizeButton.ToolTip = "Agrandir";
            }
        }
    }

    private void OnRequestAddApp()
    {
        var form = new AppFormWindow
        {
            Owner = this,
            IsEditMode = false
        };

        if (form.ShowDialog() == true && form.ResultApp != null)
        {
            _viewModel.AddNewApp(form.ResultApp);
        }
    }

    private void OnRequestEditApp(AppCardViewModel appVm)
    {
        var form = new AppFormWindow
        {
            Owner = this
        };
        form.LoadFromApp(appVm.GetModel());

        if (form.ShowDialog() == true && form.ResultApp != null)
        {
            _viewModel.UpdateExistingApp(appVm, form.ResultApp);
        }
    }

    private void OnRequestAddAppWithData(string name, string exePath)
    {
        var form = new AppFormWindow
        {
            Owner = this,
            IsEditMode = false
        };
        form.PreFill(name, exePath);

        if (form.ShowDialog() == true && form.ResultApp != null)
        {
            _viewModel.AddNewApp(form.ResultApp);
        }
    }

    private void OnRequestAddPreset()
    {
        var form = new PresetFormWindow
        {
            Owner = this
        };
        form.LoadApps(_viewModel.AllApps);

        if (form.ShowDialog() == true && form.ResultPreset != null)
        {
            _viewModel.AddPreset(form.ResultPreset);
        }
    }

    private void OnRequestManageTags()
    {
        var window = new TagsManagerWindow
        {
            Owner = this
        };
        window.ShowDialog();
        // Reload tags after closing
        _viewModel.LoadTags();
        _viewModel.LoadApps();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+F focuses search
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        // Ctrl+N add new app
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnRequestAddApp();
            e.Handled = true;
            return;
        }

        // Escape = deselect
        if (e.Key == Key.Escape)
        {
            _viewModel.SelectApp(null);
            e.Handled = true;
            return;
        }

        // The following shortcuts require a selected app
        var selectedApp = _viewModel.SelectedApp;
        if (selectedApp == null)
        {
            // No selection - inform user if they try to use keyboard shortcuts
            if (e.Key == Key.Enter || e.Key == Key.Delete ||
                (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control))
            {
                MessageBox.Show("Selectionne une application d'abord (clic sur une carte).",
                    "Aucune selection", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
            }
            return;
        }

        // Enter = launch selected app
        if (e.Key == Key.Enter)
        {
            _viewModel.LaunchApplication(selectedApp);
            e.Handled = true;
            return;
        }

        // Delete = delete selected app with confirmation
        if (e.Key == Key.Delete)
        {
            _viewModel.DeleteAppWithConfirmation(selectedApp);
            e.Handled = true;
            return;
        }

        // Ctrl+E = edit selected app
        if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnRequestEditApp(selectedApp);
            e.Handled = true;
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is AppCardViewModel app)
        {
            // Single click = select
            _viewModel.SelectApp(app);

            // Double-click = launch
            if (e.ClickCount == 2)
            {
                _viewModel.LaunchApplication(app);
                e.Handled = true;
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".exe" || ext == ".lnk")
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
        }
        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                _viewModel.HandleFileDrop(files);
            }
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is AppCardViewModel app)
        {
            ShowAppContextMenu(element, app);
            e.Handled = true;
        }
    }

    private void Card_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is AppCardViewModel app)
        {
            // Select the app on right-click too
            _viewModel.SelectApp(app);
            ShowAppContextMenu(element, app);
            e.Handled = true;
        }
    }

    private void ShowAppContextMenu(FrameworkElement placementTarget, AppCardViewModel app)
    {
        var menu = new ContextMenu();

        var launchItem = new MenuItem { Header = "Lancer", FontWeight = FontWeights.SemiBold };
        launchItem.Click += (_, _) => _viewModel.ExecuteLaunchApp(app);
        menu.Items.Add(launchItem);

        menu.Items.Add(new Separator());

        var editItem = new MenuItem { Header = "Modifier" };
        editItem.Click += (_, _) => OnRequestEditApp(app);
        menu.Items.Add(editItem);

        var openLocationItem = new MenuItem { Header = "Ouvrir l'emplacement" };
        openLocationItem.Click += (_, _) => _viewModel.OpenLocationCommand.Execute(app);
        menu.Items.Add(openLocationItem);

        var copyPathItem = new MenuItem { Header = "Copier le chemin" };
        copyPathItem.Click += (_, _) => _viewModel.CopyPathCommand.Execute(app);
        menu.Items.Add(copyPathItem);

        menu.Items.Add(new Separator());

        var adminItem = new MenuItem { Header = "Lancer en admin" };
        adminItem.Click += (_, _) => _viewModel.LaunchAsAdminCommand.Execute(app);
        menu.Items.Add(adminItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem
        {
            Header = "Supprimer",
            Foreground = (System.Windows.Media.Brush)FindResource("AccentRedBrush"),
            FontWeight = FontWeights.SemiBold
        };
        deleteItem.Click += (_, _) => _viewModel.ExecuteDeleteApp(app);
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = placementTarget;
        menu.IsOpen = true;
    }

    // ========== Navigation Handlers ==========
    private void NavApps_Checked(object sender, RoutedEventArgs e)
    {
        if (AppsPanel != null && ProjectsPanel != null && PresetsPanel != null)
        {
            AppsPanel.Visibility = Visibility.Visible;
            ProjectsPanel.Visibility = Visibility.Collapsed;
            PresetsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void NavProjects_Checked(object sender, RoutedEventArgs e)
    {
        if (AppsPanel != null && ProjectsPanel != null && PresetsPanel != null)
        {
            AppsPanel.Visibility = Visibility.Collapsed;
            ProjectsPanel.Visibility = Visibility.Visible;
            PresetsPanel.Visibility = Visibility.Collapsed;
        }
    }

    // ========== Projects Event Handlers ==========
    private void OnRequestAddProject()
    {
        var form = new Views.ProjectFormWindow { Owner = this };
        if (form.ShowDialog() == true && form.ResultProject != null)
        {
            // Save to database (AddProject handles tag linking internally)
            var projectId = Services.DatabaseService.Instance.AddProject(form.ResultProject);
            form.ResultProject.Id = projectId;

            _projectsVM.AddProjectToList(form.ResultProject);
        }
    }

    private void OnRequestEditProject(ProjectCardViewModel project)
    {
        var form = new Views.ProjectFormWindow { Owner = this };
        form.LoadFromProject(project.GetModel());

        if (form.ShowDialog() == true && form.ResultProject != null)
        {
            Services.DatabaseService.Instance.UpdateProject(form.ResultProject);
            project.UpdateFromData(form.ResultProject);
        }
    }

    private void OnRequestDeleteProject(ProjectCardViewModel project)
    {
        var hasLinkedApp = Services.DatabaseService.Instance.ProjectHasLinkedApp(project.Id);
        var dialog = new Views.DeleteProjectDialog(project.Name, hasLinkedApp) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.Choice != Views.DeleteProjectChoice.Cancel)
        {
            var deleteLinkedApp = dialog.Choice == Views.DeleteProjectChoice.DeleteBoth;
            Services.DatabaseService.Instance.DeleteProject(project.Id, deleteLinkedApp);
            _projectsVM.RemoveProject(project);

            // Refresh apps if we deleted the linked app
            if (deleteLinkedApp)
            {
                _viewModel.LoadApps();
            }
        }
    }

    private void OnRequestRunTask(ProjectCardViewModel project, Services.ProjectTask task, bool addToHub)
    {
        var logWindow = new Views.ProjectTaskLogWindow(project, task, addToHub) { Owner = this };
        var result = logWindow.ShowDialog();

        // If publish succeeded and addToHub is true, add/update the app in Hub
        if (result == true && addToHub && task == Services.ProjectTask.Publish && !string.IsNullOrEmpty(logWindow.ArtifactPath))
        {
            AddOrUpdateAppFromProject(project, logWindow.ArtifactPath);
        }
    }

    private void OnRequestPublishMode(ProjectCardViewModel project)
    {
        var dialog = new Views.PublishModeDialog(project.LastPublishMode) { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedMode))
        {
            // Save the selected mode
            Services.DatabaseService.Instance.UpdateProjectPublishMode(project.Id, dialog.SelectedMode);
            project.LastPublishMode = dialog.SelectedMode;

            // Run publish with the selected mode (addToHub = true since this was triggered from PublishAndAddToHub)
            var logWindow = new Views.ProjectTaskLogWindow(project, Services.ProjectTask.Publish, true, dialog.SelectedMode) { Owner = this };
            var result = logWindow.ShowDialog();

            if (result == true && !string.IsNullOrEmpty(logWindow.ArtifactPath))
            {
                AddOrUpdateAppFromProject(project, logWindow.ArtifactPath);
            }
        }
    }

    private void AddOrUpdateAppFromProject(ProjectCardViewModel project, string artifactPath)
    {
        // Upsert logic: Check if app already exists by source_project_id
        var existingApp = Services.DatabaseService.Instance.GetAppBySourceProject(project.Id);

        if (existingApp != null)
        {
            // Update existing app
            existingApp.Name = project.Name;
            existingApp.ExePath = artifactPath;
            existingApp.UpdatedAt = DateTime.Now;
            Services.DatabaseService.Instance.UpdateApp(existingApp);
        }
        else
        {
            // Create new app
            var newApp = new Models.AppEntry
            {
                Name = project.Name,
                ExePath = artifactPath,
                SourceProjectId = project.Id,
                GeneratedByLeHub = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            Services.DatabaseService.Instance.AddApp(newApp);
        }

        // Refresh apps list
        _viewModel.LoadApps();

        MessageBox.Show($"L'application '{project.Name}' a ete ajoutee/mise a jour dans le Hub.",
            "Publie avec succes", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AddProject_Click(object sender, RoutedEventArgs e)
    {
        OnRequestAddProject();
    }

    private void ProjectCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ProjectCardViewModel project)
        {
            _projectsVM.SelectProject(project);

            if (e.ClickCount == 2)
            {
                // Double-click = open folder
                _projectsVM.OpenFolderCommand.Execute(project);
                e.Handled = true;
            }
        }
    }

    private void ProjectCard_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ProjectCardViewModel project)
        {
            _projectsVM.SelectProject(project);
            ShowProjectContextMenu(element, project);
            e.Handled = true;
        }
    }

    private void ProjectMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ProjectCardViewModel project)
        {
            ShowProjectContextMenu(element, project);
            e.Handled = true;
        }
    }

    private void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ProjectCardViewModel project)
        {
            _projectsVM.OpenFolderCommand.Execute(project);
        }
    }

    private void BuildProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ProjectCardViewModel project)
        {
            _projectsVM.BuildCommand.Execute(project);
        }
    }

    private void ShowProjectContextMenu(FrameworkElement placementTarget, ProjectCardViewModel project)
    {
        var menu = new ContextMenu();

        // Open folder
        var openFolderItem = new MenuItem { Header = "Ouvrir le dossier" };
        openFolderItem.Click += (_, _) => _projectsVM.OpenFolderCommand.Execute(project);
        menu.Items.Add(openFolderItem);

        // Open in VS Code
        if (Services.ToolDetectionService.Instance.IsVSCodeAvailable())
        {
            var vscodeItem = new MenuItem { Header = "Ouvrir dans VS Code" };
            vscodeItem.Click += (_, _) => _projectsVM.OpenVSCodeCommand.Execute(project);
            menu.Items.Add(vscodeItem);
        }

        // Open in Visual Studio (for .NET projects)
        if (project.ProjectType == Models.ProjectType.DotNet &&
            Services.ToolDetectionService.Instance.FindVisualStudio() != null)
        {
            var vsItem = new MenuItem { Header = "Ouvrir dans Visual Studio" };
            vsItem.Click += (_, _) => _projectsVM.OpenVisualStudioCommand.Execute(project);
            menu.Items.Add(vsItem);
        }

        menu.Items.Add(new Separator());

        // Build
        var buildItem = new MenuItem { Header = "Build" };
        buildItem.Click += (_, _) => _projectsVM.BuildCommand.Execute(project);
        menu.Items.Add(buildItem);

        // Run
        var runItem = new MenuItem { Header = "Executer" };
        runItem.Click += (_, _) => _projectsVM.RunCommand.Execute(project);
        menu.Items.Add(runItem);

        // Test
        var testItem = new MenuItem { Header = "Tester" };
        testItem.Click += (_, _) => _projectsVM.TestCommand.Execute(project);
        menu.Items.Add(testItem);

        menu.Items.Add(new Separator());

        // Publish
        var publishItem = new MenuItem { Header = "Publier" };
        publishItem.Click += (_, _) => _projectsVM.PublishCommand.Execute(project);
        menu.Items.Add(publishItem);

        // Publish + Add to Hub
        var publishHubItem = new MenuItem { Header = "Publier + Ajouter au Hub", FontWeight = FontWeights.SemiBold };
        publishHubItem.Click += (_, _) => _projectsVM.PublishAndAddToHubCommand.Execute(project);
        menu.Items.Add(publishHubItem);

        menu.Items.Add(new Separator());

        // Edit
        var editItem = new MenuItem { Header = "Modifier" };
        editItem.Click += (_, _) => _projectsVM.EditProjectCommand.Execute(project);
        menu.Items.Add(editItem);

        // Delete
        var deleteItem = new MenuItem
        {
            Header = "Supprimer",
            Foreground = (System.Windows.Media.Brush)FindResource("AccentRedBrush"),
            FontWeight = FontWeights.SemiBold
        };
        deleteItem.Click += (_, _) => _projectsVM.DeleteProjectCommand.Execute(project);
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = placementTarget;
        menu.IsOpen = true;
    }

    // ========== Preset Event Handlers ==========
    private void NavPresets_Checked(object sender, RoutedEventArgs e)
    {
        if (AppsPanel != null && ProjectsPanel != null && PresetsPanel != null)
        {
            AppsPanel.Visibility = Visibility.Collapsed;
            ProjectsPanel.Visibility = Visibility.Collapsed;
            PresetsPanel.Visibility = Visibility.Visible;
        }
    }

    private void PresetItem_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement element && element.Tag is PresetViewModel preset)
            {
                OpenPresetDetailWindow(preset);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeHub] PresetItem_Click error: {ex.Message}");
            MessageBox.Show($"Erreur lors de l'ouverture du preset: {ex.Message}",
                "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PresetCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is Border border && border.Tag is PresetViewModel preset)
            {
                _viewModel.SelectPreset(preset);

                if (e.ClickCount == 2)
                {
                    OpenPresetDetailWindow(preset);
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LeHub] PresetCard_MouseLeftButtonDown error: {ex.Message}");
        }
    }

    private void OpenPresetDetailWindow(PresetViewModel preset)
    {
        if (preset == null)
        {
            MessageBox.Show("Le preset est invalide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var detailWindow = new Views.PresetDetailWindow(
            preset,
            onRun: p => _viewModel.ExecuteLaunchPreset(p),
            onEdit: p => OpenPresetEditWindow(p),
            onDuplicate: p => DuplicatePreset(p),
            onDelete: p =>
            {
                Services.DatabaseService.Instance.DeletePreset(p.Id);
                _viewModel.LoadPresets();
            },
            onRunWithLogs: p => OnRequestRunPresetWithLogs(p)
        )
        { Owner = this };

        detailWindow.ShowDialog();
    }

    private void OpenPresetEditWindow(PresetViewModel preset)
    {
        if (preset == null) return;

        var editWindow = new Views.PresetEditWindow { Owner = this };
        editWindow.LoadPreset(preset, _viewModel.AllApps);

        if (editWindow.ShowDialog() == true && editWindow.ResultPreset != null)
        {
            Services.DatabaseService.Instance.UpdatePreset(editWindow.ResultPreset);
            _viewModel.LoadPresets();
        }
    }

    private void EditPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is PresetViewModel preset)
        {
            OpenPresetEditWindow(preset);
        }
    }

    private void DuplicatePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is PresetViewModel preset)
        {
            DuplicatePreset(preset);
        }
    }

    private void DuplicatePreset(PresetViewModel preset)
    {
        if (preset == null) return;

        var newName = $"{preset.Name} (copie)";
        var newId = Services.DatabaseService.Instance.DuplicatePreset(preset.Id, newName);
        if (newId > 0)
        {
            _viewModel.LoadPresets();
            MessageBox.Show($"Le preset '{newName}' a ete cree.", "Preset duplique",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Erreur lors de la duplication du preset.", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnRequestRunPresetWithLogs(PresetViewModel preset)
    {
        if (preset == null) return;

        var logWindow = new Views.PresetRunLogWindow(preset, _viewModel.AllApps) { Owner = this };
        logWindow.ShowDialog();
    }

    private void RunPresetWithLogs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is PresetViewModel preset)
        {
            OnRequestRunPresetWithLogs(preset);
        }
    }
}
