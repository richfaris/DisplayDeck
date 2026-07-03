namespace DisplayDeck.Core.Models;

/// <summary>
/// A snapshot of a single display (adapter output + attached monitor) including its
/// current mode, position, and the full list of modes it supports.
/// </summary>
public sealed class DisplayInfo
{
    /// <summary>GDI device name, e.g. "\\.\DISPLAY1". Stable within a session.</summary>
    public required string DeviceName { get; init; }

    /// <summary>Adapter description, e.g. "NVIDIA GeForce RTX 4080".</summary>
    public string AdapterName { get; init; } = string.Empty;

    /// <summary>Friendly monitor name from EDID, e.g. "DELL U2723QE". May be empty.</summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>Stable hardware identifier from the monitor device (PnP path).</summary>
    public string MonitorDeviceId { get; set; } = string.Empty;

    public bool IsPrimary { get; init; }

    public bool IsActive { get; init; }

    public int PositionX { get; init; }

    public int PositionY { get; init; }

    public DisplayOrientation Orientation { get; init; }

    public DisplayMode? CurrentMode { get; init; }

    public IReadOnlyList<DisplayMode> SupportedModes { get; init; } = Array.Empty<DisplayMode>();

    /// <summary>Best label to show the user: friendly name if known, else adapter/device.</summary>
    public string DisplayLabel =>
        !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName
        : !string.IsNullOrWhiteSpace(AdapterName) ? AdapterName
        : DeviceName;

    /// <summary>The "DISPLAY1" style short number extracted from the device name.</summary>
    public string ShortName
    {
        get
        {
            const string prefix = "\\\\.\\";
            return DeviceName.StartsWith(prefix, StringComparison.Ordinal)
                ? DeviceName[prefix.Length..]
                : DeviceName;
        }
    }

    public int Width => CurrentMode?.Width ?? 0;

    public int Height => CurrentMode?.Height ?? 0;
}
