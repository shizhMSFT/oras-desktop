using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OrasProject.OrasDesktop.ViewModels;

namespace OrasProject.OrasDesktop.Views;

public partial class RepositorySelectorControl : UserControl
{
    public RepositorySelectorControl()
    {
        InitializeComponent();
        
        // Wire up keyboard event handlers
        var filterTextBox = this.FindControl<TextBox>("RepositoryFilterTextBox");
        if (filterTextBox != null)
        {
            filterTextBox.AddHandler(KeyDownEvent, OnRepositoryFilterKeyDown, RoutingStrategies.Tunnel);
        }
        
        var treeView = this.FindControl<TreeView>("RepositoriesTreeView");
        if (treeView != null)
        {
            treeView.AddHandler(KeyDownEvent, OnRepositoryTreeViewKeyDown, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>
    /// Focus the repository filter textbox
    /// </summary>
    public void FocusFilterTextBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var filterTextBox = this.FindControl<TextBox>("RepositoryFilterTextBox");
            if (filterTextBox != null)
            {
                filterTextBox.Focus();
                filterTextBox.CaretIndex = filterTextBox.Text?.Length ?? 0;
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnRepositoryFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is RepositorySelectorViewModel viewModel)
        {
            e.Handled = true; // Prevent default Enter key behavior
            
            // Select the first item in the filtered list if available
            if (viewModel.FilteredRepositories.Count > 0)
            {
                var firstRepo = viewModel.FilteredRepositories[0];
                viewModel.SelectedRepository = firstRepo;
                
                // Expand the first repository if it has children
                if (firstRepo.Children.Count > 0)
                {
                    firstRepo.IsExpanded = true;
                }
                
                // Force immediate load when Enter is pressed
                viewModel.LoadSelectedRepository();
                
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

    private void OnRepositoryTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is RepositorySelectorViewModel viewModel)
        {
            // Prevent the default behavior of collapsing/expanding
            e.Handled = true;
            
            // Don't reload tags - just move focus to the tag filter
            // The repository is already selected and tags are already loaded
            
            // Move focus to the TagSelectorControl's filter textbox
            Dispatcher.UIThread.Post(() =>
            {
                // Navigate up to find the parent MainView, then find TagSelectorControl
                var parent = this.Parent;
                while (parent != null && parent is not UserControl)
                {
                    parent = parent.Parent;
                }
                
                if (parent is Control parentControl)
                {
                    var tagSelectorControl = parentControl.FindControl<Control>("TagSelectorControl");
                    if (tagSelectorControl != null)
                    {
                        // Find the TagFilterTextBox inside the TagSelectorControl
                        var tagFilterTextBox = tagSelectorControl.FindControl<TextBox>("TagFilterTextBox");
                        if (tagFilterTextBox != null)
                        {
                            tagFilterTextBox.Focus();
                            tagFilterTextBox.CaretIndex = tagFilterTextBox.Text?.Length ?? 0;
                        }
                    }
                }
            }, DispatcherPriority.Background);
        }
    }
}
