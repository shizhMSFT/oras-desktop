using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OrasProject.OrasDesktop.ViewModels;

namespace OrasProject.OrasDesktop.Views;

public partial class KeyboardShortcutsOverlay : UserControl
{
    public KeyboardShortcutsOverlay()
    {
        InitializeComponent();
    }

    private void OnOverlayClicked(object? sender, PointerPressedEventArgs e)
    {
        // Close overlay when clicking on the dark background
        if (DataContext is KeyboardShortcutsViewModel vm)
        {
            vm.Hide();
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is KeyboardShortcutsViewModel vm)
        {
            vm.Hide();
        }
    }
}
