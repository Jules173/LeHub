using System.Windows;

namespace LeHub.Views;

public enum DeleteProjectChoice
{
    Cancel,
    DeleteBoth,
    KeepApp
}

public partial class DeleteProjectDialog : Window
{
    public DeleteProjectChoice Choice { get; private set; } = DeleteProjectChoice.Cancel;

    public DeleteProjectDialog(string projectName, bool hasLinkedApp)
    {
        InitializeComponent();

        TitleText.Text = $"Supprimer \"{projectName}\" ?";

        if (hasLinkedApp)
        {
            DescriptionText.Text = "Ce projet a une application associee dans le Hub.";
            DeleteBothOption.Visibility = Visibility.Visible;
            KeepAppOption.Visibility = Visibility.Visible;
        }
        else
        {
            DescriptionText.Text = "Voulez-vous vraiment supprimer ce projet ?";
            DeleteBothOption.Visibility = Visibility.Collapsed;
            KeepAppOption.Visibility = Visibility.Collapsed;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeleteBothOption.IsChecked == true)
            Choice = DeleteProjectChoice.DeleteBoth;
        else if (KeepAppOption.IsChecked == true)
            Choice = DeleteProjectChoice.KeepApp;
        else
            Choice = DeleteProjectChoice.DeleteBoth; // Default for projects without linked app

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = DeleteProjectChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
