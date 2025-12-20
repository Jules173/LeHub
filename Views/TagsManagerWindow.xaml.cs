using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LeHub.Models;
using LeHub.Services;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LeHub.Views;

public partial class TagsManagerWindow : Window, INotifyPropertyChanged
{
    private string _newTagName = string.Empty;

    public TagsManagerWindow()
    {
        InitializeComponent();
        DataContext = this;
        Tags = new ObservableCollection<Tag>();
        LoadTags();
    }

    public string NewTagName
    {
        get => _newTagName;
        set
        {
            _newTagName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Tag> Tags { get; }

    private void LoadTags()
    {
        Tags.Clear();
        var tags = DatabaseService.Instance.GetAllTags();
        foreach (var tag in tags)
        {
            Tags.Add(tag);
        }
    }

    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        AddNewTag();
    }

    private void NewTagTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddNewTag();
        }
    }

    private void AddNewTag()
    {
        if (string.IsNullOrWhiteSpace(NewTagName))
            return;

        var tagName = NewTagName.Trim();

        // Check for duplicates
        if (Tags.Any(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Ce tag existe deja.", "Doublon", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var id = DatabaseService.Instance.AddTag(tagName);
        Tags.Add(new Tag { Id = id, Name = tagName });
        NewTagName = string.Empty;
    }

    private void RenameTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is Tag tag)
        {
            var inputDialog = new InputDialog("Renommer le tag", "Nouveau nom:");
            inputDialog.InputValue = tag.Name;
            inputDialog.Owner = this;

            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputValue))
            {
                var newName = inputDialog.InputValue.Trim();

                // Check for duplicates
                if (Tags.Any(t => t.Id != tag.Id && t.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Un tag avec ce nom existe deja.", "Doublon", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DatabaseService.Instance.UpdateTag(tag.Id, newName);
                tag.Name = newName;
                LoadTags(); // Refresh
            }
        }
    }

    private void DeleteTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is Tag tag)
        {
            var result = MessageBox.Show(
                $"Voulez-vous vraiment supprimer le tag '{tag.Name}' ?\nIl sera retire de toutes les applications.",
                "Confirmer la suppression",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DatabaseService.Instance.DeleteTag(tag.Id);
                Tags.Remove(tag);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
