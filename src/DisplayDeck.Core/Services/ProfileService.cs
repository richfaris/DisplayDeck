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

    public ProfileService(DisplayService displays, DisplayControlService control)
    {
        _displays = displays;
        _control = control;
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
        return _control.ApplyProfile(profile, current);
    }

    private string PathFor(DisplayProfile profile) =>
        Path.Combine(ProfilesDirectory, $"{profile.Id}.ddp");
}
