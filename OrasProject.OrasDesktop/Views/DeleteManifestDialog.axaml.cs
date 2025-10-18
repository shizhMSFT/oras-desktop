using Avalonia.Controls;

namespace OrasProject.OrasDesktop.Views;

public partial class DeleteManifestDialog : Window
{
    public DeleteManifestDialog()
    {
        InitializeComponent();
    }

    public DeleteManifestDialog(string manifestReference) : this()
    {
        QuestionText.Text = "Are you sure you want to delete the manifest:";
        ReferenceText.Text = manifestReference;
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
}
