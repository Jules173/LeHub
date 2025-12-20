using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LeHub.Views;

public partial class InputDialog : Window, INotifyPropertyChanged
{
    private string _inputValue = string.Empty;

    public InputDialog(string title, string prompt)
    {
        InitializeComponent();
        DataContext = this;
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Focus();
    }

    public string InputValue
    {
        get => _inputValue;
        set
        {
            _inputValue = value;
            OnPropertyChanged();
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
