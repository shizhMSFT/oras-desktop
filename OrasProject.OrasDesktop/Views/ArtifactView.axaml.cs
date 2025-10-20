using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using OrasProject.OrasDesktop.ViewModels;
using ReactiveUI;

namespace OrasProject.OrasDesktop.Views;

public partial class ArtifactView : ReactiveUserControl<ArtifactViewModel>
{
    public ArtifactView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Wire up the ManifestViewer TextBlock reference to the JsonViewerViewModel
            if (ViewModel != null)
            {
                var manifestViewer = this.FindControl<ContentControl>("ManifestViewer");
                ViewModel.JsonViewer.ManifestViewer = manifestViewer;
            }
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ReferrersTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TreeView treeView || treeView.SelectedItem is null)
        {
            return;
        }

        var container = treeView.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .FirstOrDefault(item => Equals(item.DataContext, treeView.SelectedItem));

        if (container == null)
        {
            return;
        }

        container.IsExpanded = true;

        var parent = container.GetVisualParent<TreeViewItem>();
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.GetVisualParent<TreeViewItem>();
        }
    }
}
