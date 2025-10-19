using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OrasProject.OrasDesktop.ViewModels;
using System;

namespace OrasProject.OrasDesktop.Views;

public partial class TagSelectorControl : UserControl
{
    public TagSelectorControl()
    {
        InitializeComponent();
        
        // Wire up event handlers
        var tagFilterTextBox = this.FindControl<TextBox>("TagFilterTextBox");
        if (tagFilterTextBox != null)
        {
            tagFilterTextBox.AddHandler(KeyDownEvent, OnTagFilterKeyDown, RoutingStrategies.Tunnel);
        }
        
        var tagsListBox = this.FindControl<ListBox>("TagsListBox");
        if (tagsListBox != null)
        {
            tagsListBox.AddHandler(KeyDownEvent, OnTagsListBoxKeyDown, RoutingStrategies.Tunnel);
        }
    }
    
    private void OnTagFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TagSelectorViewModel viewModel)
        {
            e.Handled = true;
            
            // Select and load the first tag if available
            if (viewModel.Tags.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[TagSelectorControl] Enter pressed on filter box. Calling SelectAndLoadTag for tag: {viewModel.Tags[0].Name}");
                viewModel.SelectAndLoadTag(viewModel.Tags[0]);
                
                // Move focus to the tags ListBox
                var tagsListBox = this.FindControl<ListBox>("TagsListBox");
                if (tagsListBox != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        tagsListBox.Focus();
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
        if (e.Key == Key.Enter && DataContext is TagSelectorViewModel viewModel)
        {
            e.Handled = true;
            
            // Load the currently selected tag
            if (viewModel.SelectedTag != null)
            {
                viewModel.LoadSelectedTag();
            }
        }
    }
}
