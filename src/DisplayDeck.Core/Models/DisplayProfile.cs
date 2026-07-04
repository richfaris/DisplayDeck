namespace DisplayDeck.Core.Models;

/// <summary>
/// One display's settings inside a saved <see cref="DisplayProfile"/>. Stores enough
/// to match the display again later and to reproduce its full configuration.
/// </summary>
public sealed class DisplayProfileEntry
{
    /// <summary>GDI device name captured at save time, e.g. "\\.\DISPLAY1".</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Stable PnP/EDID identifier — the preferred key for matching across sessions.</summary>
    public string MonitorDeviceId { get; set; } = string.Empty;

    /// <summary>Human-friendly label shown in the UI, e.g. "DELL U2723QE".</summary>
    public string FriendlyName { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; } = 32;

    public int PositionX { get; set; }
    public int PositionY { get; set; }

    public DisplayOrientation Orientation { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>Per-monitor scaling percentage (0 = not captured, for older profiles).</summary>
    public int ScalingPercent { get; set; }

    public string ResolutionLabel => $"{Width} × {Height}";
}

/// <summary>
/// A named snapshot of the full multi-monitor configuration: each display's
/// resolution, refresh, color depth, orientation, position, and which one is primary.
/// Persisted as a single ".ddp" JSON file.
/// </summary>
public sealed class DisplayProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Untitled";

    /// <summary>ISO-8601 timestamp of when the profile was created/saved.</summary>
    public string Created { get; set; } = DateTimeOffset.Now.ToString("o");

    public List<DisplayProfileEntry> Displays { get; set; } = new();

    /// <summary>A one-line human summary, e.g. "2 displays · 3840×2160, 1920×1080".</summary>
    public string Summary
    {
        get
        {
            if (Displays.Count == 0)
                return "No displays";

            string count = Displays.Count == 1 ? "1 display" : $"{Displays.Count} displays";
            string modes = string.Join(", ", Displays
                .OrderByDescending(d => d.IsPrimary)
                .Select(d => d.ResolutionLabel));
            return $"{count} · {modes}";
        }
    }
}
