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

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.RequestAddApp += OnRequestAddApp;
        _viewModel.RequestEditApp += OnRequestEditApp;
        _viewModel.RequestAddAppWithData += OnRequestAddAppWithData;
        _viewModel.RequestAddPreset += OnRequestAddPreset;
        _viewModel.RequestManageTags += OnRequestManageTags;

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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        if (msg == WM_NCHITTEST)
        {
            var point = PointFromScreen(new Point(
                (short)(lParam.ToInt32() & 0xFFFF),
                (short)(lParam.ToInt32() >> 16)));

            const int borderSize = 6;
            var width = ActualWidth;
            var height = ActualHeight;

            // Check corners first (larger hit area)
            if (point.X < borderSize && point.Y < borderSize)
            {
                handled = true;
                return (IntPtr)HTTOPLEFT;
            }
            if (point.X >= width - borderSize && point.Y < borderSize)
            {
                handled = true;
                return (IntPtr)HTTOPRIGHT;
            }
            if (point.X < borderSize && point.Y >= height - borderSize)
            {
                handled = true;
                return (IntPtr)HTBOTTOMLEFT;
            }
            if (point.X >= width - borderSize && point.Y >= height - borderSize)
            {
                handled = true;
                return (IntPtr)HTBOTTOMRIGHT;
            }

            // Then check edges
            if (point.X < borderSize)
            {
                handled = true;
                return (IntPtr)HTLEFT;
            }
            if (point.X >= width - borderSize)
            {
                handled = true;
                return (IntPtr)HTRIGHT;
            }
            if (point.Y < borderSize)
            {
                handled = true;
                return (IntPtr)HTTOP;
            }
            if (point.Y >= height - borderSize)
            {
                handled = true;
                return (IntPtr)HTBOTTOM;
            }
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

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // No action needed
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
}
