using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
        // Set focus to the repository filter after successful connection
        Dispatcher.UIThread.Post(() =>
        {
            var repositoryFilterTextBox = this.FindControl<TextBox>("RepositoryFilterTextBox");
            if (repositoryFilterTextBox != null)
            {
                repositoryFilterTextBox.Focus();
                repositoryFilterTextBox.CaretIndex = repositoryFilterTextBox.Text?.Length ?? 0;
            }
        }, DispatcherPriority.Loaded);
        
        // Find the repositories TreeView and add keyboard handler
        var repositoriesTreeView = this.FindControl<TreeView>("RepositoriesTreeView");
        if (repositoriesTreeView != null)
        {
            repositoriesTreeView.AddHandler(KeyDownEvent, OnRepositoryTreeViewKeyDown, RoutingStrategies.Tunnel);
        }
        
        // Find the tags ListBox and add keyboard handler
        var tagsListBox = this.FindControl<ListBox>("TagsListBox");
        if (tagsListBox != null)
        {
            tagsListBox.AddHandler(KeyDownEvent, OnTagsListBoxKeyDown, RoutingStrategies.Tunnel);
        }
        
        // Find the filter textboxes and add keyboard handlers
        var repositoryFilterTextBox = this.FindControl<TextBox>("RepositoryFilterTextBox");
        if (repositoryFilterTextBox != null)
        {
            repositoryFilterTextBox.AddHandler(KeyDownEvent, OnRepositoryFilterKeyDown, RoutingStrategies.Tunnel);
        }
        
        var tagFilterTextBox = this.FindControl<TextBox>("TagFilterTextBox");
        if (tagFilterTextBox != null)
        {
            tagFilterTextBox.AddHandler(KeyDownEvent, OnTagFilterKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private void MainView_AttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Set focus to the registry textbox after the UI is fully rendered
        Dispatcher.UIThread.Post(() =>
        {
            var registryTextBox = this.FindControl<TextBox>("RegistryTextBox");
            if (registryTextBox != null)
            {
                registryTextBox.Focus();
                // Position cursor at the end of the text
                registryTextBox.CaretIndex = registryTextBox.Text?.Length ?? 0;
            }
        }, DispatcherPriority.Loaded);
        
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
    
    private void OnRepositoryTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Prevent the default behavior of collapsing/expanding
            e.Handled = true;
            
            // Force immediate load of tags when Enter is pressed
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ForceLoadSelectedRepository();
            }
            
            // Move focus to the tag filter textbox
            var tagFilterTextBox = this.FindControl<TextBox>("TagFilterTextBox");
            if (tagFilterTextBox != null)
            {
                tagFilterTextBox.Focus();
                tagFilterTextBox.CaretIndex = tagFilterTextBox.Text?.Length ?? 0;
            }
        }
    }
    
    private void OnRepositoryFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true; // Prevent default Enter key behavior
            
            // Select the first item in the filtered list if available
            if (DataContext is MainViewModel viewModel && viewModel.FilteredRepositories.Count > 0)
            {
                var firstRepo = viewModel.FilteredRepositories[0];
                viewModel.SelectedRepository = firstRepo;
                
                // Expand the first repository if it has children
                if (firstRepo.Children.Count > 0)
                {
                    firstRepo.IsExpanded = true;
                }
                
                // Force immediate load when Enter is pressed
                viewModel.ForceLoadSelectedRepository();
                
                // Move focus to the repositories TreeView after selection is processed
                var repositoriesTreeView = this.FindControl<TreeView>("RepositoriesTreeView");
                if (repositoriesTreeView != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        // Focus the TreeView control first
                        repositoriesTreeView.Focus();
                        
                        // Try to find and focus the selected TreeViewItem container
                        var treeViewItem = repositoriesTreeView.ContainerFromIndex(0);
                        if (treeViewItem is Control itemControl)
                        {
                            itemControl.Focus();
                        }
                    }, DispatcherPriority.Background);
                }
            }
        }
    }
    
    private void OnTagFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true; // Prevent default Enter key behavior
            
            // Select the first tag in the filtered list if available
            if (DataContext is MainViewModel viewModel && viewModel.Tags.Count > 0)
            {
                viewModel.SelectedTag = viewModel.Tags[0];
                // Force immediate load when Enter is pressed
                viewModel.ForceLoadSelectedTag();
                
                // Move focus to the tags ListBox after selection is processed
                var tagsListBox = this.FindControl<ListBox>("TagsListBox");
                if (tagsListBox != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        // Focus the ListBox control first
                        tagsListBox.Focus();
                        
                        // Try to find and focus the selected ListBoxItem container
                        var listBoxItem = tagsListBox.ContainerFromIndex(0);
                        if (listBoxItem is Control itemControl)
                        {
                            itemControl.Focus();
                        }
                    }, DispatcherPriority.Background);
                }
            }
        }
    }
    
    private void OnTagsListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Force immediate load of manifest when Enter is pressed
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ForceLoadSelectedTag();
            }
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
