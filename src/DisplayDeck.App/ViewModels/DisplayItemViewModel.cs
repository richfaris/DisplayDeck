using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayDeck.Core.Models;

namespace DisplayDeck.App.ViewModels;

/// <summary>Presentation wrapper around a <see cref="DisplayInfo"/> for the Displays view.</summary>
public sealed partial class DisplayItemViewModel : ObservableObject
{
    private readonly IDisplayApplyHost _host;
    private bool _suppressApply;

    public DisplayItemViewModel(DisplayInfo info, IDisplayApplyHost host)
    {
        Info = info;
        _host = host;

        ResolutionOptions = new ObservableCollection<ResolutionOption>(
            info.SupportedModes
                .Select(m => new ResolutionOption(m.Width, m.Height))
                .Distinct()
                .OrderByDescending(r => r.PixelCount));

        RefreshOptions = new ObservableCollection<int>();

        _suppressApply = true;
        SelectedResolution = ResolutionOptions.FirstOrDefault(r =>
            info.CurrentMode != null && r.Width == info.CurrentMode.Width && r.Height == info.CurrentMode.Height)
            ?? ResolutionOptions.FirstOrDefault();
        RebuildRefreshOptions();
        SelectedRefresh = info.CurrentMode?.RefreshRate ?? RefreshOptions.FirstOrDefault();
        _suppressApply = false;
    }

    public DisplayInfo Info { get; }

    public string Label => Info.DisplayLabel;
    public string ShortName => Info.ShortName;
    public string AdapterName => Info.AdapterName;
    public bool IsPrimary => Info.IsPrimary;
    public bool CanMakePrimary => !Info.IsPrimary;
    public DisplayOrientation Orientation => Info.Orientation;

    public string ResolutionText => Info.CurrentMode is { } m ? m.ResolutionLabel : "—";
    public string RefreshText => Info.CurrentMode is { } m ? $"{m.RefreshRate} Hz" : "—";
    public string ColorDepthText => Info.CurrentMode is { } m ? $"{m.BitsPerPixel}-bit" : "—";
    public string PositionText => $"({Info.PositionX}, {Info.PositionY})";
    public int SupportedModeCount => Info.SupportedModes.Count;

    public ObservableCollection<ResolutionOption> ResolutionOptions { get; }
    public ObservableCollection<int> RefreshOptions { get; }

    [ObservableProperty]
    private ResolutionOption? _selectedResolution;

    [ObservableProperty]
    private int _selectedRefresh;

    partial void OnSelectedResolutionChanged(ResolutionOption? value)
    {
        if (_suppressApply)
            return;

        // Preserve refresh where possible when the resolution changes, then apply.
        int previous = SelectedRefresh;
        _suppressApply = true;
        RebuildRefreshOptions();
        SelectedRefresh = RefreshOptions.Contains(previous) ? previous : RefreshOptions.FirstOrDefault();
        _suppressApply = false;

        ApplyCurrentSelection();
    }

    private void RebuildRefreshOptions()
    {
        RefreshOptions.Clear();
        if (SelectedResolution is null)
            return;

        foreach (var hz in Info.SupportedModes
                     .Where(m => m.Width == SelectedResolution.Width && m.Height == SelectedResolution.Height)
                     .Select(m => m.RefreshRate)
                     .Distinct()
                     .OrderByDescending(hz => hz))
        {
            RefreshOptions.Add(hz);
        }
    }

    /// <summary>True when the pending selection differs from the applied mode.</summary>
    private bool HasPendingChange =>
        SelectedResolution is not null && Info.CurrentMode is { } m &&
        (SelectedResolution.Width != m.Width || SelectedResolution.Height != m.Height || SelectedRefresh != m.RefreshRate);

    /// <summary>Apply the current resolution/refresh selection immediately (with auto-revert).</summary>
    private void ApplyCurrentSelection()
    {
        if (SelectedResolution is null || SelectedRefresh <= 0 || !HasPendingChange)
            return;

        int bpp = Info.CurrentMode?.BitsPerPixel ?? 32;
        var mode = new DisplayMode(SelectedResolution.Width, SelectedResolution.Height, SelectedRefresh, bpp);
        _host.RequestApplyMode(this, mode);
    }

    [RelayCommand(CanExecute = nameof(CanMakePrimary))]
    private void MakePrimary() => _host.RequestSetPrimary(this);

    [RelayCommand]
    private void Rotate()
    {
        var next = (DisplayOrientation)(((int)Info.Orientation + 1) % 4);
        _host.RequestRotate(this, next);
    }

    // --- Arrangement-map geometry (filled by MainViewModel). ---

    [ObservableProperty] private double _mapLeft;
    [ObservableProperty] private double _mapTop;
    [ObservableProperty] private double _mapWidth;
    [ObservableProperty] private double _mapHeight;

    partial void OnSelectedRefreshChanged(int value)
    {
        if (_suppressApply)
            return;

        ApplyCurrentSelection();
    }
}
