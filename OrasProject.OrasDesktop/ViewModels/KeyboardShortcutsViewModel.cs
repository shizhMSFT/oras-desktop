using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

public class KeyboardShortcutsViewModel : ViewModelBase
{
    private bool _isVisible = false;

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public void Show() => IsVisible = true;
    public void Hide() => IsVisible = false;
}
