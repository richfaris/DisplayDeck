using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayDeck.Core.Models;
using DisplayDeck.Core.Services;

namespace DisplayDeck.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisplayApplyHost
{
    private const int RevertSeconds = 8;

    private readonly DisplayService _displayService = new();
    private readonly DisplayControlService _controlService = new();
    private readonly DpiScalingService _scalingService = new();
    private readonly DispatcherTimer _revertTimer;

    private Action? _revertAction;

    public MainViewModel()
    {
        Displays = new ObservableCollection<DisplayItemViewModel>();
        Profiles = new ProfileService(_displayService, _controlService, _scalingService);
        _revertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _revertTimer.Tick += OnRevertTick;
        Refresh();
    }

    public ObservableCollection<DisplayItemViewModel> Displays { get; }

    /// <summary>Shared profile store used by the Profiles page and the tray menu.</summary>
    public ProfileService Profiles { get; }

    [ObservableProperty] private DisplayItemViewModel? _selectedDisplay;
    [ObservableProperty] private string _summaryText = string.Empty;

    /// <summary>The global hotkey that actually registered (may be a fallback). Shown in the
    /// always-visible Shortcuts card so it's never a mystery which combo is live.</summary>
    [ObservableProperty] private string _hotkeyGesture = "Ctrl + Alt + D";

    // Fun mode: snap a webcam selfie and show it inside every monitor tile on the map.
    [ObservableProperty] private bool _isFunMode;
    [ObservableProperty] private System.Windows.Media.Imaging.BitmapSource? _funModeImage;
    private bool _suppressFunOffMessage;

    partial void OnIsFunModeChanged(bool value)
    {
        if (value)
        {
            EnterFunModeAsync();
            return;
        }

        FunModeImage = null;
        if (!_suppressFunOffMessage)
            ShowStatus("Fun mode off.", isError: false);
    }

    private async void EnterFunModeAsync()
    {
        ShowStatus("Smile! Grabbing a photo from your camera\u2026", isError: false);

        var (image, error) = await Task.Run(DisplayDeck.App.Services.CameraService.TryCapture);

        if (image is null)
        {
            _suppressFunOffMessage = true;
            IsFunMode = false;
            _suppressFunOffMessage = false;
            ShowStatus($"Fun mode: {error ?? "couldn't access the camera."}", isError: true);
            return;
        }

        FunModeImage = image;
        ShowStatus("Fun mode on \u2014 that's you on every screen! \U0001F389", isError: false);
    }

    // Toast / status message.
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isStatusError;
    [ObservableProperty] private bool _isStatusVisible;

    // Auto-revert confirmation bar.
    [ObservableProperty] private bool _isConfirmPending;
    [ObservableProperty] private string _confirmMessage = string.Empty;
    [ObservableProperty] private int _countdownSeconds;

    public double MapCanvasWidth { get; } = 620;
    public double MapCanvasHeight { get; } = 300;

    // Last transform used by ArrangeMap, so drag positions can be converted back to
    // real desktop coordinates (map px -> real px).
    private double _mapScale = 1;
    private double _mapOffsetX;
    private double _mapOffsetY;
    private int _mapMinX;
    private int _mapMinY;

    [RelayCommand]
    public void Refresh()
    {
        DisplayDeck.App.Services.Log.Write("Refresh command executed.");
        var displays = _displayService.GetDisplays();

        // Order the cards to mirror the physical layout (left-to-right, then top-to-bottom)
        // so they stay in the same relationship as the arrangement map after a rearrange.
        var ordered = displays
            .OrderBy(d => d.PositionX)
            .ThenBy(d => d.PositionY)
            .ToList();

        Displays.Clear();
        foreach (var d in ordered)
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
        string device = item.Info.DeviceName;

        // No auto-revert: making a display primary can't black out a screen — every
        // monitor stays on, so it's always trivially reversible.
        ApplyWithRevert(
            $"Made {item.Label} the primary display.",
            () => _controlService.SetPrimary(device),
            revert: null);
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

    public void RequestSetScaling(DisplayItemViewModel item, int percent)
    {
        int previous = item.Info.ScalingPercent;
        if (percent == previous)
            return;

        string device = item.Info.DeviceName;

        ApplyWithRevert(
            $"Set {item.Label} scaling to {percent}%.",
            () => _scalingService.SetScaling(device, percent),
            () => _scalingService.SetScaling(device, previous));
    }

    /// <summary>
    /// Apply a saved profile. When every monitor the profile references is currently
    /// connected, the machine has already run in exactly this state, so we skip the
    /// confirm timer. If the hardware differs from when it was saved, we keep the
    /// 8-second auto-revert as a safety net (a stored mode could blank an unfamiliar panel).
    /// </summary>
    public void ApplyProfileWithRevert(DisplayProfile profile)
    {
        // If the profile already matches the live setup, applying it would do nothing —
        // tell the user instead of popping a misleading "keep changes?" dialog.
        if (Profiles.MatchesCurrent(profile))
        {
            ShowStatus($"\u201C{profile.Name}\u201D already matches your current setup \u2014 nothing to change.", isError: false);
            return;
        }

        Action? revert = null;
        if (!Profiles.AppliesToCurrentHardware(profile))
        {
            var snapshot = Profiles.CaptureCurrent("Previous configuration");
            revert = () => Profiles.Apply(snapshot);
        }

        ApplyWithRevert(
            $"Applied profile \u201C{profile.Name}\u201D.",
            () => Profiles.Apply(profile),
            revert);
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

        // Remember the transform so drag deltas can be mapped back to real coordinates.
        _mapScale = scale;
        _mapOffsetX = offsetX;
        _mapOffsetY = offsetY;
        _mapMinX = minX;
        _mapMinY = minY;

        foreach (var vm in Displays)
        {
            var info = vm.Info;
            vm.MapLeft = offsetX + (info.PositionX - minX) * scale;
            vm.MapTop = offsetY + (info.PositionY - minY) * scale;
            vm.MapWidth = Math.Max(28, Math.Max(1, info.Width) * scale);
            vm.MapHeight = Math.Max(20, Math.Max(1, info.Height) * scale);
        }
    }

    /// <summary>Re-snap the map tiles to the live desktop layout (undo a partial drag).</summary>
    public void SnapMapBack() => ArrangeMap(Displays.Select(d => d.Info).ToList());

    /// <summary>
    /// Commit a drag on the arrangement map. The dragged tile's map position is converted
    /// back to real desktop pixels, snapped so it sits flush against its nearest neighbour
    /// (Windows requires a gap-free, non-overlapping desktop), then applied to all displays.
    /// No auto-revert: repositioning never turns a monitor off, so it's always reversible.
    /// </summary>
    public void CommitDragArrange(DisplayItemViewModel dragged)
    {
        if (Displays.Count < 2 || _mapScale <= 0)
        {
            SnapMapBack();
            return;
        }

        // Tentative real top-left of the dragged monitor, derived from its map position.
        double tentX = _mapMinX + (dragged.MapLeft - _mapOffsetX) / _mapScale;
        double tentY = _mapMinY + (dragged.MapTop - _mapOffsetY) / _mapScale;

        int dw = Math.Max(1, dragged.Info.Width);
        int dh = Math.Max(1, dragged.Info.Height);
        double dcx = tentX + dw / 2.0;
        double dcy = tentY + dh / 2.0;

        // Anchor to the nearest other monitor (by centre distance).
        DisplayItemViewModel? anchor = null;
        double best = double.MaxValue;
        foreach (var other in Displays)
        {
            if (ReferenceEquals(other, dragged))
                continue;

            double ocx = other.Info.PositionX + Math.Max(1, other.Info.Width) / 2.0;
            double ocy = other.Info.PositionY + Math.Max(1, other.Info.Height) / 2.0;
            double dist = (dcx - ocx) * (dcx - ocx) + (dcy - ocy) * (dcy - ocy);
            if (dist < best)
            {
                best = dist;
                anchor = other;
            }
        }

        if (anchor is null)
        {
            SnapMapBack();
            return;
        }

        int ow = Math.Max(1, anchor.Info.Width);
        int oh = Math.Max(1, anchor.Info.Height);
        int ox = anchor.Info.PositionX;
        int oy = anchor.Info.PositionY;
        double anchorCx = ox + ow / 2.0;
        double anchorCy = oy + oh / 2.0;

        // Snap flush to whichever edge of the anchor the drag points toward.
        int nx, ny;
        if (Math.Abs(dcx - anchorCx) >= Math.Abs(dcy - anchorCy))
        {
            nx = dcx >= anchorCx ? ox + ow : ox - dw; // right or left
            ny = oy;                                   // top-aligned
        }
        else
        {
            ny = dcy >= anchorCy ? oy + oh : oy - dh;  // below or above
            nx = ox;                                   // left-aligned
        }

        var positions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in Displays)
            positions[d.Info.DeviceName] = (d.Info.PositionX, d.Info.PositionY);
        positions[dragged.Info.DeviceName] = (nx, ny);

        // Windows anchors the primary display at (0,0); normalise everything relative to it.
        var primary = Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays[0];
        var (px, py) = positions[primary.Info.DeviceName];
        if (px != 0 || py != 0)
        {
            foreach (var key in positions.Keys.ToList())
            {
                var v = positions[key];
                positions[key] = (v.X - px, v.Y - py);
            }
        }

        bool changed = Displays.Any(d =>
        {
            var (x, y) = positions[d.Info.DeviceName];
            return x != d.Info.PositionX || y != d.Info.PositionY;
        });

        if (!changed)
        {
            SnapMapBack();
            return;
        }

        ApplyWithRevert(
            $"Rearranged {dragged.Label}.",
            () => _controlService.SetPositions(positions),
            revert: null);
    }
}
