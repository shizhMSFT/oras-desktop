using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OrasProject.OrasDesktop.ViewModels;

namespace OrasProject.OrasDesktop.Views;

public partial class MainView : UserControl
{
    private bool _isShiftPressed = false;
    
    public MainView()
    {
        InitializeComponent();
        
        // After the control is loaded and initialized
        this.AttachedToVisualTree += MainView_AttachedToVisualTree;
    }

    private void MainView_AttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Find the connect button by name
        var connectButton = this.FindControl<Button>("ConnectButton");
        if (connectButton != null)
        {
            // Listen for keyboard events on the button to track shift key state
            connectButton.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            connectButton.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
            connectButton.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            
            // Override the default click behavior
            connectButton.Click += ConnectButton_Click;
        }
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _isShiftPressed = true;
        }
    }
    
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _isShiftPressed = false;
        }
    }
    
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Capture shift state from pointer events as well
        _isShiftPressed = (e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
    }

    private void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        // Get the DataContext and cast to MainViewModel
        if (DataContext is MainViewModel viewModel)
        {
            // Execute command with shift key state
            viewModel.ForceLoginCommand.Execute(_isShiftPressed);
            
            // Stop the event from continuing to bubble up (prevents the Command from executing)
            e.Handled = true;
        }
    }
    
    private void ReferenceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel viewModel)
        {
            // Execute the command to load the manifest by reference
            viewModel.LoadManifestByReferenceCommand.Execute();
            e.Handled = true;
        }
    }
}
