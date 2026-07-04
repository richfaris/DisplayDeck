using System.Text.Json;
using DisplayDeck.Core.Models;

namespace DisplayDeck.Core.Services;

/// <summary>
/// Persists and applies named display profiles. Each profile is a single ".ddp"
/// JSON file under %LOCALAPPDATA%\DisplayDeck\Profiles.
/// </summary>
public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly DisplayService _displays;
    private readonly DisplayControlService _control;
    private readonly DpiScalingService _dpi;

    public ProfileService(DisplayService displays, DisplayControlService control, DpiScalingService dpi)
    {
        _displays = displays;
        _control = control;
        _dpi = dpi;
    }

    /// <summary>Folder where profiles live. Created on demand.</summary>
    public string ProfilesDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DisplayDeck", "Profiles");

    /// <summary>Capture the current display configuration into a new (unsaved) profile.</summary>
    public DisplayProfile CaptureCurrent(string name)
    {
        var displays = _displays.GetDisplays();
        var profile = new DisplayProfile { Name = string.IsNullOrWhiteSpace(name) ? "Untitled" : name.Trim() };

        foreach (var d in displays)
        {
            profile.Displays.Add(new DisplayProfileEntry
            {
                DeviceName = d.DeviceName,
                MonitorDeviceId = d.MonitorDeviceId,
                FriendlyName = d.DisplayLabel,
                Width = d.CurrentMode?.Width ?? 0,
                Height = d.CurrentMode?.Height ?? 0,
                RefreshRate = d.CurrentMode?.RefreshRate ?? 0,
                BitsPerPixel = d.CurrentMode?.BitsPerPixel ?? 32,
                PositionX = d.PositionX,
                PositionY = d.PositionY,
                Orientation = d.Orientation,
                IsPrimary = d.IsPrimary,
                ScalingPercent = d.SupportsScaling ? d.ScalingPercent : 0,
            });
        }

        return profile;
    }

    /// <summary>Load all saved profiles, most recently created first.</summary>
    public IReadOnlyList<DisplayProfile> LoadAll()
    {
        Directory.CreateDirectory(ProfilesDirectory);
        var list = new List<DisplayProfile>();

        foreach (var file in Directory.EnumerateFiles(ProfilesDirectory, "*.ddp"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<DisplayProfile>(json, JsonOptions);
                if (profile is not null)
                    list.Add(profile);
            }
            catch
            {
                // Ignore a corrupt/partial file rather than failing the whole list.
            }
        }

        return list
            .OrderByDescending(p => p.Created, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Save (create or overwrite) a profile.</summary>
    public void Save(DisplayProfile profile)
    {
        Directory.CreateDirectory(ProfilesDirectory);
        string path = PathFor(profile);
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
    }

    public void Delete(DisplayProfile profile)
    {
        string path = PathFor(profile);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Rename(DisplayProfile profile, string newName)
    {
        profile.Name = string.IsNullOrWhiteSpace(newName) ? profile.Name : newName.Trim();
        Save(profile);
    }

    /// <summary>Apply a saved profile to the currently-connected displays.</summary>
    public ChangeResult Apply(DisplayProfile profile)
    {
        var current = _displays.GetDisplays();
        var result = _control.ApplyProfile(profile, current);

        // Scaling is a separate CCD operation from the mode/position commit; apply it
        // best-effort for each profile display that stored a scale and is connected.
        foreach (var e in profile.Displays)
        {
            if (e.ScalingPercent <= 0)
                continue;

            var device = ResolveDeviceName(e, current);
            if (device is not null)
                _dpi.SetScaling(device, e.ScalingPercent);
        }

        return result;
    }

    /// <summary>Match a saved entry to a live device name: prefer hardware id, fall back to GDI name.</summary>
    private static string? ResolveDeviceName(DisplayProfileEntry entry, IReadOnlyList<DisplayInfo> current)
    {
        var d = current.FirstOrDefault(c =>
                    !string.IsNullOrEmpty(entry.MonitorDeviceId) &&
                    string.Equals(c.MonitorDeviceId, entry.MonitorDeviceId, StringComparison.OrdinalIgnoreCase))
                ?? current.FirstOrDefault(c =>
                    string.Equals(c.DeviceName, entry.DeviceName, StringComparison.OrdinalIgnoreCase));
        return d?.DeviceName;
    }

    /// <summary>
    /// True when every display in the profile already matches the current live configuration
    /// (mode, orientation, position, primary) — i.e. applying it would change nothing.
    /// </summary>
    public bool MatchesCurrent(DisplayProfile profile)
    {
        var current = _displays.GetDisplays();

        foreach (var e in profile.Displays)
        {
            var d = current.FirstOrDefault(c =>
                        !string.IsNullOrEmpty(e.MonitorDeviceId) &&
                        string.Equals(c.MonitorDeviceId, e.MonitorDeviceId, StringComparison.OrdinalIgnoreCase))
                    ?? current.FirstOrDefault(c =>
                        string.Equals(c.DeviceName, e.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (d is null)
                return false; // a display in the profile isn't present -> not an exact match

            int w = d.CurrentMode?.Width ?? 0;
            int h = d.CurrentMode?.Height ?? 0;
            int hz = d.CurrentMode?.RefreshRate ?? 0;

            if (w != e.Width || h != e.Height || hz != e.RefreshRate) return false;
            if ((int)d.Orientation != (int)e.Orientation) return false;
            if (d.PositionX != e.PositionX || d.PositionY != e.PositionY) return false;
            if (d.IsPrimary != e.IsPrimary) return false;
            // Only compare scaling when the profile captured it (older profiles store 0).
            if (e.ScalingPercent > 0 && d.SupportsScaling && d.ScalingPercent != e.ScalingPercent) return false;
        }

        return true;
    }

    private string PathFor(DisplayProfile profile) =>
        Path.Combine(ProfilesDirectory, $"{profile.Id}.ddp");
}
