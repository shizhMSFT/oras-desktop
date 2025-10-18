using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// Context menu view model for annotation operations.
/// Supports copying annotation key, value, or both.
/// </summary>
public class AnnotationContextMenuViewModel : ViewModelBase
{
    private string _key;
    private string _value;

    public AnnotationContextMenuViewModel(string key, string value)
    {
        _key = key;
        _value = value;

        CopyKeyCommand = ReactiveCommand.CreateFromTask(CopyKeyAsync);
        CopyValueCommand = ReactiveCommand.CreateFromTask(CopyValueAsync);
        CopyBothCommand = ReactiveCommand.CreateFromTask(CopyBothAsync);
    }

    public string Key
    {
        get => _key;
        set => this.RaiseAndSetIfChanged(ref _key, value);
    }

    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public ReactiveCommand<Unit, Unit> CopyKeyCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyValueCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyBothCommand { get; }

    private async Task CopyKeyAsync()
    {
        await CopyToClipboardAsync(Key);
    }

    private async Task CopyValueAsync()
    {
        await CopyToClipboardAsync(Value);
    }

    private async Task CopyBothAsync()
    {
        await CopyToClipboardAsync($"{Key}: {Value}");
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.Clipboard != null)
                {
                    await mainWindow.Clipboard.SetTextAsync(text);
                }
            }
        }
        catch (Exception)
        {
            // Silently fail clipboard operations
        }
    }
}
