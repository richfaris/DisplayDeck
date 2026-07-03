namespace DisplayDeck.Core.Models;

/// <summary>Screen rotation, matching the Win32 DMDO_* values.</summary>
public enum DisplayOrientation
{
    Landscape = 0,
    Portrait = 1,
    LandscapeFlipped = 2,
    PortraitFlipped = 3,
}
