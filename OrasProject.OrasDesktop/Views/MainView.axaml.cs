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


    private void ReferrersTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Auto-expand selected referrer nodes
        if (sender is TreeView treeView && e.AddedItems.Count > 0)
        {
            var selectedItem = e.AddedItems[0];
            if (selectedItem != null)
            {
                // Find the TreeViewItem container for the selected item
                Dispatcher.UIThread.Post(() =>
                {
                    var container = FindTreeViewItemContainer(treeView, selectedItem);
                    if (container != null)
                    {
                        container.IsExpanded = true;
                    }
                }, DispatcherPriority.Loaded);
            }
        }
    }

    private TreeViewItem? FindTreeViewItemContainer(TreeView treeView, object item)
    {
        // Try to find the container in the visual tree
        foreach (var rootItem in treeView.GetRealizedContainers())
        {
            if (rootItem is TreeViewItem rootContainer)
            {
                if (rootContainer.DataContext == item)
                {
                    return rootContainer;
                }
                
                // Recursively search children
                var found = FindTreeViewItemInChildren(rootContainer, item);
                if (found != null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    private TreeViewItem? FindTreeViewItemInChildren(TreeViewItem parent, object item)
    {
        foreach (var child in parent.GetRealizedContainers())
        {
            if (child is TreeViewItem childContainer)
            {
                if (childContainer.DataContext == item)
                {
                    return childContainer;
                }
                
                // Recursively search nested children
                var found = FindTreeViewItemInChildren(childContainer, item);
                if (found != null)
                {
                    return found;
                }
            }
        }
        return null;
    }
    
}
