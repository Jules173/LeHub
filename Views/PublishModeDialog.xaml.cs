using System.Windows;

namespace LeHub.Views;

public partial class PublishModeDialog : Window
{
    public string? SelectedMode { get; private set; }

    public PublishModeDialog(string? lastMode = null)
    {
        InitializeComponent();

        // Pre-select the last used mode
        switch (lastMode?.ToLower())
        {
            case "dev":
                DevMode.IsChecked = true;
                break;
            case "preview":
                PreviewMode.IsChecked = true;
                break;
            case "prod":
            default:
                ProdMode.IsChecked = true;
                break;
        }
    }

    private void PublishButton_Click(object sender, RoutedEventArgs e)
    {
        if (DevMode.IsChecked == true)
            SelectedMode = "dev";
        else if (PreviewMode.IsChecked == true)
            SelectedMode = "preview";
        else
            SelectedMode = "prod";

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
