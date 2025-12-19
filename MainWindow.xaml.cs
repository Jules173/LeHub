using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LeHub.ViewModels;
using LeHub.Views;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;

namespace LeHub;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.RequestAddApp += OnRequestAddApp;
        _viewModel.RequestEditApp += OnRequestEditApp;
        _viewModel.RequestAddAppWithData += OnRequestAddAppWithData;
        _viewModel.RequestAddPreset += OnRequestAddPreset;
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

        // Get selected app from focused element
        var selectedApp = GetSelectedApp();
        if (selectedApp == null) return;

        // Enter = launch
        if (e.Key == Key.Enter)
        {
            _viewModel.LaunchApplication(selectedApp);
            e.Handled = true;
            return;
        }

        // Delete = delete with confirmation
        if (e.Key == Key.Delete)
        {
            _viewModel.DeleteAppWithConfirmation(selectedApp);
            e.Handled = true;
            return;
        }

        // Ctrl+E = edit
        if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnRequestEditApp(selectedApp);
            e.Handled = true;
        }
    }

    private AppCardViewModel? GetSelectedApp()
    {
        // Try to get from focused element
        if (Keyboard.FocusedElement is FrameworkElement fe)
        {
            var current = fe;
            while (current != null)
            {
                if (current.DataContext is AppCardViewModel app)
                    return app;
                current = current.Parent as FrameworkElement;
            }
        }

        // Fallback: return first app
        return _viewModel.FilteredApps.FirstOrDefault();
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to launch
        if (e.ClickCount == 2)
        {
            if (sender is Border border && border.DataContext is AppCardViewModel app)
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
            Foreground = (System.Windows.Media.Brush)FindResource("AccentRedBrush")
        };
        deleteItem.Click += (_, _) => _viewModel.ExecuteDeleteApp(app);
        menu.Items.Add(deleteItem);

        menu.PlacementTarget = placementTarget;
        menu.IsOpen = true;
    }
}
