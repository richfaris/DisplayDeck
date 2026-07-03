using DisplayDeck.Core.Models;

namespace DisplayDeck.App.ViewModels;

/// <summary>
/// Implemented by the main view model so individual display cards can request
/// changes that flow through the shared apply/auto-revert pipeline.
/// </summary>
public interface IDisplayApplyHost
{
    void RequestApplyMode(DisplayItemViewModel item, DisplayMode mode);

    void RequestSetPrimary(DisplayItemViewModel item);

    void RequestRotate(DisplayItemViewModel item, DisplayOrientation orientation);
}

/// <summary>A selectable resolution (grouped across refresh rates).</summary>
public sealed record ResolutionOption(int Width, int Height)
{
    public long PixelCount => (long)Width * Height;

    public override string ToString() => $"{Width} × {Height}";
}
