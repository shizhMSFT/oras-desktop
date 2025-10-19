using Avalonia.Controls;
using Avalonia.Input;

namespace OrasProject.OrasDesktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Handle F4 globally at window level
        this.KeyDown += MainWindow_KeyDown;
    }
    
    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // F4 to focus reference textbox
        if (e.Key == Key.F4)
        {
            var mainView = this.FindControl<MainView>("MainViewControl");
            var referenceControl = mainView?.FindControl<ReferenceHistoryControl>("ReferenceHistoryControl");
            if (referenceControl != null)
            {
                referenceControl.FocusTextBox();
                e.Handled = true;
            }
        }
    }
}
