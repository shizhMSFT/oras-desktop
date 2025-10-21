using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrasProject.OrasDesktop.Logging;
using OrasProject.OrasDesktop.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace OrasProject.OrasDesktop.Views;

public partial class ReferenceHistoryControl : UserControl
{
    private readonly ILogger<ReferenceHistoryControl> _logger;
    private bool _suppressFocusOpen;

    public static bool EnableLogging
    {
        get => DesktopLoggingOptions.IsEnabled;
        set => DesktopLoggingOptions.IsEnabled = value;
    }

    public ReferenceHistoryControl()
    {
        InitializeComponent();
        _logger = ResolveLogger();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Control initialized; logging to '{LogFilePath}'.", DesktopLoggingOptions.LogFilePath);
        }
        
        // Wire up events after initialization
        var textBox = this.FindControl<TextBox>("ReferenceTextBox");
        if (textBox != null)
        {
            // GotFocus is now wired in XAML
            textBox.LostFocus += ReferenceTextBox_LostFocus;
            textBox.TextChanged += ReferenceTextBox_TextChanged;
            textBox.DoubleTapped += ReferenceTextBox_DoubleTapped;
            // Use AddHandler with Tunnel to catch PointerPressed before TextBox handles it
            textBox.AddHandler(PointerPressedEvent, ReferenceTextBox_PointerPressed, RoutingStrategies.Tunnel);
            textBox.AddHandler(KeyDownEvent, ReferenceTextBox_KeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("KeyDown handler attached via AddHandler.");
            }
        }
        
        // Wire up ListBox selection changed and scroll into view
        this.Loaded += (s, e) =>
        {
            var listBox = this.FindControl<ListBox>("PART_HistoryListBox");
            if (listBox != null)
            {
                listBox.SelectionChanged += HistoryListBox_SelectionChanged;
            }
            
            // Subscribe to SelectedHistoryIndex changes to scroll item into view
            if (DataContext is ReferenceHistoryViewModel viewModel)
            {
                viewModel.WhenAnyValue(x => x.SelectedHistoryIndex)
                    .Subscribe(index =>
                    {
                        if (index >= 0 && index < viewModel.HistoryItems.Count && listBox != null)
                        {
                            listBox.ScrollIntoView(index);
                        }
                    });
                
                // Subscribe to CurrentReference changes to move cursor to end
                viewModel.WhenAnyValue(x => x.CurrentReference)
                    .Subscribe(reference =>
                    {
                        if (!string.IsNullOrEmpty(reference) && textBox != null && textBox.IsFocused)
                        {
                            // Move cursor to end when navigating through history
                            Dispatcher.UIThread.Post(() =>
                            {
                                textBox.CaretIndex = reference.Length;
                            }, DispatcherPriority.Background);
                        }
                    });
                
                // Subscribe to FocusRequested event
                viewModel.FocusRequested += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (textBox != null)
                        {
                            textBox.Focus();
                            textBox.SelectAll();
                            
                            if (_logger.IsEnabled(LogLevel.Information))
                            {
                                _logger.LogInformation("Focus requested via event, textbox focused and text selected.");
                            }
                        }
                    }, DispatcherPriority.Background);
                };
            }
        };
    }

    private void ReferenceTextBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (_suppressFocusOpen)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("GotFocus suppressed to keep popup closed.");
            }
            _suppressFocusOpen = false;
            return;
        }

        // Open dropdown on focus if there's history
        // Note: PointerPressed also handles opening when clicking, this handles keyboard focus (Tab key, etc.)
        if (DataContext is ReferenceHistoryViewModel viewModel && viewModel.HistoryItems.Count > 0)
        {
            if (!viewModel.IsDropDownOpen)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("TextBox focused (keyboard), opening popup.");
                }
                viewModel.IsDropDownOpen = true;
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("TextBox focused, no history so popup stays closed.");
            }
        }
    }

    private void ReferenceTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        // Close dropdown after a short delay to allow click events to process
        // But don't close if the dropdown button was clicked
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is ReferenceHistoryViewModel viewModel)
            {
                // Only close if not interacting with the popup
                if (!IsPointerOverPopup())
                {
                    viewModel.IsDropDownOpen = false;
                }
            }
        }, DispatcherPriority.Background);
    }

    private bool IsPointerOverPopup()
    {
        var popup = this.FindControl<Popup>("PART_Popup");
        return popup?.IsPointerOver ?? false;
    }

    private void ReferenceTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Reset navigation state when user manually types (not when programmatically set by navigation)
        if (DataContext is ReferenceHistoryViewModel viewModel && sender is TextBox textBox)
        {
            // Only reset if the change came from user input, not programmatic navigation
            // Check if we're not currently in a navigation operation
            if (textBox.IsFocused && !viewModel.IsNavigating)
            {
                viewModel.ResetNavigationState();
            }
        }
    }

    private void ReferenceTextBox_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        // On double-tap, close dropdown (if opened by first click) and select all text
        if (sender is TextBox textBox && DataContext is ReferenceHistoryViewModel viewModel)
        {
            // Close dropdown that may have been opened by the first click
            viewModel.IsDropDownOpen = false;
            
            // Select all text
            textBox.SelectAll();
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("TextBox double-tapped, closed dropdown and selected all text.");
            }
            
            e.Handled = true;
        }
    }

    private void ReferenceTextBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only handle single clicks (ClickCount == 1)
        // Double-clicks are handled by DoubleTapped event
        if (e.ClickCount != 1)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Skipping PointerPressed for ClickCount={ClickCount}.", e.ClickCount);
            }
            return;
        }
        
        // Clear suppression flag when clicking - user explicitly wants to interact with the textbox
        _suppressFocusOpen = false;
        
        // Open dropdown when clicking on textbox (like a browser address bar)
        // Always reopen if there's history, even if already focused
        if (sender is TextBox textBox && DataContext is ReferenceHistoryViewModel viewModel)
        {
            if (viewModel.HistoryItems.Count > 0)
            {
                if (!viewModel.IsDropDownOpen)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("TextBox clicked, opening popup.");
                    }
                    viewModel.IsDropDownOpen = true;
                }
                else
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("TextBox clicked, popup already open.");
                    }
                }
            }
        }
    }

    private void HistoryItem_Click(object? sender, RoutedEventArgs e)
    {
        // Reopen dropdown when clicking on textbox if it has focus
        if (sender is TextBox textBox && textBox.IsFocused && DataContext is ReferenceHistoryViewModel viewModel)
        {
            if (viewModel.HistoryItems.Count > 0 && !viewModel.IsDropDownOpen)
            {
                viewModel.IsDropDownOpen = true;
            }
        }
    }

    private void HistoryListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Only process on actual user interaction, not keyboard navigation
        if (sender is ListBox listBox && 
            e.AddedItems.Count > 0 && 
            e.AddedItems[0] is string reference && 
            DataContext is ReferenceHistoryViewModel viewModel)
        {
            // Detect mouse click: pointer is over listbox AND not during keyboard navigation
            var isMouseClick = listBox.IsPointerOver && !viewModel.IsNavigating;
            
            if (isMouseClick)
            {
                // Commit the selection
                viewModel.CurrentReference = reference;
                
                // Close the dropdown explicitly (both view model and popup)
                viewModel.SelectedHistoryIndex = -1;
                viewModel.IsDropDownOpen = false;
                
                var popup = this.FindControl<Popup>("PART_Popup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
                
                // Trigger the load - this is an explicit user action (history selection)
                viewModel.RequestLoad();
                
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("History item clicked; closed popup and triggered load for {Reference}.", reference);
                }
                
                // Focus the textbox and move cursor to end
                var textBox = this.FindControl<TextBox>("ReferenceTextBox");
                if (textBox != null)
                {
                    textBox.Focus();
                    // Move cursor to end of text
                    Dispatcher.UIThread.Post(() =>
                    {
                        textBox.CaretIndex = reference.Length;
                    }, DispatcherPriority.Background);
                }
            }
        }
    }

    private void DeleteHistoryItem_Click(object? sender, RoutedEventArgs e)
    {
        // When delete button is clicked, remove the item from history
        if (sender is Button button && button.Tag is string reference && DataContext is ReferenceHistoryViewModel viewModel)
        {
            viewModel.RemoveHistoryItemCommand.Execute(reference).Subscribe();
            e.Handled = true; // Prevent event bubbling
        }
    }

    private void ReferenceTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ReferenceHistoryViewModel viewModel)
            return;

        if (e.Key != Key.Enter)
        {
            return;
        }

        var popup = this.FindControl<Popup>("PART_Popup");
        bool dropdownWasOpen = viewModel.IsDropDownOpen || (popup?.IsOpen ?? false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Enter pressed. dropdownWasOpen={DropdownWasOpen}, suppressFocusOpen={SuppressFocusOpen}", dropdownWasOpen, _suppressFocusOpen);
        }

        if (dropdownWasOpen)
        {
            // Close the dropdown immediately so it collapses before the command runs
            _suppressFocusOpen = true;
            viewModel.SelectedHistoryIndex = -1;
            viewModel.IsDropDownOpen = false;

            if (popup != null)
            {
                popup.IsOpen = false;
            }

            e.Handled = true;

            // Trigger the load via the view model so ManifestLoader records the source
            viewModel.RequestLoad();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Closed popup and triggered load after Enter.");
            }
            Dispatcher.UIThread.Post(() => _suppressFocusOpen = false, DispatcherPriority.Background);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Enter pressed with popup already closed; deferring to KeyBinding.");
            }
        }
        // If dropdown wasn't open, let the KeyBinding on the control handle Enter normally
    }

    /// <summary>
    /// Programmatically focus the textbox and open dropdown
    /// </summary>
    public void FocusTextBox()
    {
        var textBox = this.FindControl<TextBox>("ReferenceTextBox");
        if (textBox != null && DataContext is ReferenceHistoryViewModel viewModel)
        {
            textBox.Focus();
            textBox.SelectAll();
            
            // Open dropdown if there's history
            if (viewModel.HistoryItems.Count > 0)
            {
                viewModel.IsDropDownOpen = true;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("FocusTextBox helper opened popup.");
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("FocusTextBox helper focused text box but no history present.");
                }
            }
        }
    }

    private static ILogger<ReferenceHistoryControl> ResolveLogger()
    {
        try
        {
            var provider = ServiceLocator.Current;
            var logger = provider.GetService<ILogger<ReferenceHistoryControl>>();
            return logger ?? NullLogger<ReferenceHistoryControl>.Instance;
        }
        catch
        {
            return NullLogger<ReferenceHistoryControl>.Instance;
        }
    }
}
