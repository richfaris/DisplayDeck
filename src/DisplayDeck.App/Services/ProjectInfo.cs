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
    public string RepositoryUrl { get; set; } = "";
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

        // Prefer the live git remote (self-updating), then the baked value, then last-known.
        string repo = DetectRepositoryUrl(source);
        if (string.IsNullOrEmpty(repo)) repo = ReadMeta("Repository") ?? "";
        if (string.IsNullOrEmpty(repo)) repo = data.RepositoryUrl;

        bool changed =
            !string.Equals(data.RunLocation, runLocation, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(data.SourceRoot, source, StringComparison.OrdinalIgnoreCase);

        bool repoChanged =
            !string.IsNullOrEmpty(repo) &&
            !string.Equals(data.RepositoryUrl, repo, StringComparison.OrdinalIgnoreCase);

        if (changed || firstEver || repoChanged)
        {
            data.Builder = builder;
            data.AIAssistant = ai;
            if (!string.IsNullOrEmpty(repo)) data.RepositoryUrl = repo;
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
            // Keep builder/AI/repo fresh even if paths are unchanged.
            data.Builder = builder;
            data.AIAssistant = ai;
            if (!string.IsNullOrEmpty(repo)) data.RepositoryUrl = repo;
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

    /// <summary>
    /// Read the "origin" remote from the source tree's .git/config and normalize it to a
    /// browsable https URL. Returns "" when there's no git repo (e.g. a published build).
    /// This keeps the repo link self-updating without hard-coding it.
    /// </summary>
    private static string DetectRepositoryUrl(string sourceRoot)
    {
        try
        {
            if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
                return "";

            string config = Path.Combine(sourceRoot, ".git", "config");
            if (!File.Exists(config))
                return "";

            bool inOrigin = false;
            foreach (string raw in File.ReadAllLines(config))
            {
                string line = raw.Trim();
                if (line.StartsWith("[remote ", StringComparison.OrdinalIgnoreCase))
                    inOrigin = line.Contains("\"origin\"", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("[", StringComparison.Ordinal))
                    inOrigin = false;
                else if (inOrigin && line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
                {
                    int eq = line.IndexOf('=');
                    if (eq >= 0)
                        return NormalizeGitUrl(line[(eq + 1)..].Trim());
                }
            }
        }
        catch
        {
            // Best-effort detection.
        }
        return "";
    }

    private static string NormalizeGitUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        url = url.Trim();

        // git@github.com:owner/repo(.git) -> https://github.com/owner/repo
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url["git@".Length..].Replace(":", "/");
        else if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url["ssh://".Length..].Replace("git@", "");

        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        return url;
    }
}
