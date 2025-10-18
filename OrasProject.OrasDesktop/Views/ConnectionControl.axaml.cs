using Avalonia.Controls;
using Avalonia.Threading;

namespace OrasProject.OrasDesktop.Views;

public partial class ConnectionControl : UserControl
{
    public ConnectionControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Focuses the registry textbox
    /// </summary>
    public void FocusRegistryTextBox()
    {
        var registryTextBox = this.FindControl<TextBox>("RegistryTextBox");
        if (registryTextBox != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                registryTextBox.Focus();
                registryTextBox.CaretIndex = registryTextBox.Text?.Length ?? 0;
            }, DispatcherPriority.Loaded);
        }
    }
}
