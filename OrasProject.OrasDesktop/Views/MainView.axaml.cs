using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OrasProject.OrasDesktop.ViewModels;

namespace OrasProject.OrasDesktop.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        
        // After the control is loaded and initialized
        this.AttachedToVisualTree += MainView_AttachedToVisualTree;
        
        // Subscribe to DataContext changes to hook up ViewModel events
        this.DataContextChanged += MainView_DataContextChanged;
    }

    private void MainView_DataContextChanged(object? sender, EventArgs e)
    {
        // Subscribe to RegistryConnected event when ViewModel is available
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RegistryConnected += OnRegistryConnected;
        }
    }

    private void OnRegistryConnected(object? sender, EventArgs e)
    {
        // Set focus to the repository selector filter after successful connection
        Dispatcher.UIThread.Post(() =>
        {
            var repositorySelectorControl = this.FindControl<RepositorySelectorControl>("RepositorySelectorControl");
            repositorySelectorControl?.FocusFilterTextBox();
        }, DispatcherPriority.Loaded);
    }

    private void MainView_AttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Set focus to the ConnectionControl after the UI is fully rendered
        Dispatcher.UIThread.Post(() =>
        {
            var connectionControl = this.FindControl<ConnectionControl>("ConnectionControl");
            connectionControl?.FocusRegistryTextBox();
        }, DispatcherPriority.Loaded);
    }
    
    // OnRepositoryTreeViewKeyDown and OnRepositoryFilterKeyDown methods removed - now handled by RepositorySelectorControl component
    // OnTagFilterKeyDown and OnTagsListBoxKeyDown methods removed - now handled by TagSelectorControl component
    // ReferrersTreeView_SelectionChanged and related methods removed - now handled by ArtifactView component
}
