using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayDeck.Core.Models;
using DisplayDeck.Core.Services;

namespace DisplayDeck.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisplayApplyHost
{
    private const int RevertSeconds = 15;

    private readonly DisplayService _displayService = new();
    private readonly DisplayControlService _controlService = new();
    private readonly DispatcherTimer _revertTimer;

    private Action? _revertAction;

    public MainViewModel()
    {
        Displays = new ObservableCollection<DisplayItemViewModel>();
        Profiles = new ProfileService(_displayService, _controlService);
        _revertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _revertTimer.Tick += OnRevertTick;
        Refresh();
    }

    public ObservableCollection<DisplayItemViewModel> Displays { get; }

    /// <summary>Shared profile store used by the Profiles page and the tray menu.</summary>
    public ProfileService Profiles { get; }

    [ObservableProperty] private DisplayItemViewModel? _selectedDisplay;
    [ObservableProperty] private string _summaryText = string.Empty;

    // Toast / status message.
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isStatusError;
    [ObservableProperty] private bool _isStatusVisible;

    // Auto-revert confirmation bar.
    [ObservableProperty] private bool _isConfirmPending;
    [ObservableProperty] private string _confirmMessage = string.Empty;
    [ObservableProperty] private int _countdownSeconds;

    public double MapCanvasWidth { get; } = 620;
    public double MapCanvasHeight { get; } = 340;

    [RelayCommand]
    public void Refresh()
    {
        DisplayDeck.App.Services.Log.Write("Refresh command executed.");
        var displays = _displayService.GetDisplays();

        Displays.Clear();
        foreach (var d in displays)
            Displays.Add(new DisplayItemViewModel(d, this));

        SelectedDisplay = Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault();
        SummaryText = displays.Count == 1 ? "1 display connected" : $"{displays.Count} displays connected";

        ArrangeMap(displays);
    }

    // --- IDisplayApplyHost ---

    public void RequestApplyMode(DisplayItemViewModel item, DisplayMode mode)
    {
        var previous = item.Info.CurrentMode;
        string device = item.Info.DeviceName;

        ApplyWithRevert(
            $"Changed {item.Label} to {mode.ResolutionLabel} @ {mode.RefreshRate} Hz.",
            () => _controlService.ApplyMode(device, mode),
            previous is null ? null : () => _controlService.ApplyMode(device, previous));
    }

    public void RequestSetPrimary(DisplayItemViewModel item)
    {
        var previousPrimary = Displays.FirstOrDefault(d => d.IsPrimary)?.Info.DeviceName;
        string device = item.Info.DeviceName;

        ApplyWithRevert(
            $"Made {item.Label} the primary display.",
            () => _controlService.SetPrimary(device),
            previousPrimary is null ? null : () => _controlService.SetPrimary(previousPrimary));
    }

    public void RequestRotate(DisplayItemViewModel item, DisplayOrientation orientation)
    {
        var previous = item.Info.Orientation;
        string device = item.Info.DeviceName;

        ApplyWithRevert(
            $"Rotated {item.Label} to {Describe(orientation)}.",
            () => _controlService.SetOrientation(device, orientation),
            () => _controlService.SetOrientation(device, previous));
    }

    /// <summary>
    /// Apply a saved profile with the same 15-second auto-revert safety net. The current
    /// configuration is snapshotted first so we can restore it if the user doesn't confirm.
    /// </summary>
    public void ApplyProfileWithRevert(DisplayProfile profile)
    {
        var snapshot = Profiles.CaptureCurrent("Previous configuration");

        ApplyWithRevert(
            $"Applied profile \u201C{profile.Name}\u201D.",
            () => Profiles.Apply(profile),
            () => Profiles.Apply(snapshot));
    }

    /// <summary>Show a transient status toast (used by the Profiles page when saving).</summary>
    public void NotifyStatus(string message, bool isError = false) => ShowStatus(message, isError);

    private void ApplyWithRevert(string description, Func<ChangeResult> apply, Action? revert)
    {
        // Cancel any in-flight confirmation without reverting (user made a newer change).
        StopRevertTimer();

        ChangeResult result;
        try
        {
            result = apply();
        }
        catch (Exception ex)
        {
            DisplayDeck.App.Services.Log.Write($"Apply threw: {ex}");
            ShowStatus($"Failed: {ex.Message}", isError: true);
            return;
        }

        DisplayDeck.App.Services.Log.Write($"Apply '{description}' -> {result.Status}: {result.Message}");

        if (!result.IsSuccess)
        {
            ShowStatus(result.Message, isError: true);
            Refresh();
            return;
        }

        Refresh();

        if (revert is not null)
        {
            _revertAction = revert;
            ConfirmMessage = description;
            CountdownSeconds = RevertSeconds;
            IsConfirmPending = true;
            _revertTimer.Start();
        }
        else
        {
            ShowStatus(description, isError: false);
        }
    }

    [RelayCommand]
    private void KeepChanges()
    {
        StopRevertTimer();
        ShowStatus("Changes kept.", isError: false);
    }

    [RelayCommand]
    private void RevertChanges()
    {
        var revert = _revertAction;
        StopRevertTimer();
        revert?.Invoke();
        Refresh();
        ShowStatus("Reverted to previous settings.", isError: false);
    }

    private void OnRevertTick(object? sender, EventArgs e)
    {
        CountdownSeconds--;
        if (CountdownSeconds <= 0)
            RevertChanges();
    }

    private void StopRevertTimer()
    {
        _revertTimer.Stop();
        IsConfirmPending = false;
        _revertAction = null;
    }

    private async void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = true;
        var shown = message;
        await Task.Delay(TimeSpan.FromSeconds(4));
        if (StatusMessage == shown)
            IsStatusVisible = false;
    }

    private static string Describe(DisplayOrientation o) => o switch
    {
        DisplayOrientation.Landscape => "landscape",
        DisplayOrientation.Portrait => "portrait",
        DisplayOrientation.LandscapeFlipped => "landscape (flipped)",
        DisplayOrientation.PortraitFlipped => "portrait (flipped)",
        _ => o.ToString(),
    };

    private void ArrangeMap(IReadOnlyList<DisplayInfo> displays)
    {
        if (displays.Count == 0)
            return;

        int minX = displays.Min(d => d.PositionX);
        int minY = displays.Min(d => d.PositionY);
        int maxX = displays.Max(d => d.PositionX + Math.Max(1, d.Width));
        int maxY = displays.Max(d => d.PositionY + Math.Max(1, d.Height));

        double spanX = Math.Max(1, maxX - minX);
        double spanY = Math.Max(1, maxY - minY);

        const double padding = 24;
        double availW = MapCanvasWidth - padding * 2;
        double availH = MapCanvasHeight - padding * 2;
        double scale = Math.Min(availW / spanX, availH / spanY);

        double offsetX = padding + (availW - spanX * scale) / 2;
        double offsetY = padding + (availH - spanY * scale) / 2;

        foreach (var vm in Displays)
        {
            var info = vm.Info;
            vm.MapLeft = offsetX + (info.PositionX - minX) * scale;
            vm.MapTop = offsetY + (info.PositionY - minY) * scale;
            vm.MapWidth = Math.Max(28, Math.Max(1, info.Width) * scale);
            vm.MapHeight = Math.Max(20, Math.Max(1, info.Height) * scale);
        }
    }
}
