using System.IO;
using System.Reflection;
using System.Text.Json;

namespace DisplayDeck.App.Services;

/// <summary>
/// Persisted "where did this app come from" record, so it can always be found and
/// improved later. Auto-updates when the app is launched from a new location.
/// </summary>
public sealed class ProjectInfoData
{
    public string Builder { get; set; } = "Cursor";
    public string AIAssistant { get; set; } = "";
    public string SourceRoot { get; set; } = "";
    public string RunLocation { get; set; } = "";
    public string FirstSeen { get; set; } = "";
    public string LastUpdated { get; set; } = "";

    /// <summary>Set at load time when the run/source location changed since last launch.</summary>
    public bool LocationChanged { get; set; }
}

/// <summary>Loads, reconciles and persists <see cref="ProjectInfoData"/>.</summary>
public static class ProjectInfo
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DisplayDeck", "project-info.json");

    public static ProjectInfoData Load()
    {
        string builder = ReadMeta("Builder") ?? "Cursor";
        string ai = ReadMeta("AIAssistant") ?? "";
        string source = ReadMeta("SourceRoot") ?? "(unknown — rebuild from source to record)";
        string runLocation = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        ProjectInfoData? existing = TryReadStore();
        bool firstEver = existing is null;
        ProjectInfoData data = existing ?? new ProjectInfoData { FirstSeen = now };

        bool changed =
            !string.Equals(data.RunLocation, runLocation, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(data.SourceRoot, source, StringComparison.OrdinalIgnoreCase);

        if (changed || firstEver)
        {
            data.Builder = builder;
            data.AIAssistant = ai;
            data.SourceRoot = source;
            data.RunLocation = runLocation;
            data.LastUpdated = now;
            if (string.IsNullOrEmpty(data.FirstSeen))
                data.FirstSeen = now;

            // Only flag as "moved" when a genuine change happened after the first run.
            data.LocationChanged = changed && !firstEver;
            Save(data);
        }
        else
        {
            // Keep builder/AI fresh even if paths are unchanged.
            data.Builder = builder;
            data.AIAssistant = ai;
            data.LocationChanged = false;
        }

        return data;
    }

    private static ProjectInfoData? TryReadStore()
    {
        try
        {
            if (!File.Exists(StorePath))
                return null;
            return JsonSerializer.Deserialize<ProjectInfoData>(File.ReadAllText(StorePath));
        }
        catch
        {
            return null;
        }
    }

    private static void Save(ProjectInfoData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    private static string? ReadMeta(string key) =>
        Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
}
