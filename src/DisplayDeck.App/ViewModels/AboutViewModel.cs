using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayDeck.App.Services;

namespace DisplayDeck.App.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    private readonly ProjectInfoData _info;

    public AboutViewModel()
    {
        _info = ProjectInfo.Load();
    }

    public string Builder => _info.Builder;
    public string AIAssistant => string.IsNullOrWhiteSpace(_info.AIAssistant) ? "—" : _info.AIAssistant;
    public string RepositoryUrl => string.IsNullOrWhiteSpace(_info.RepositoryUrl) ? "—" : _info.RepositoryUrl;
    public bool HasRepository => !string.IsNullOrWhiteSpace(_info.RepositoryUrl);
    public string SourceRoot => _info.SourceRoot;
    public string RunLocation => _info.RunLocation;
    public string FirstSeen => _info.FirstSeen;
    public string LastUpdated => _info.LastUpdated;
    public bool LocationChanged => _info.LocationChanged;

    public bool HasSource => Directory.Exists(_info.SourceRoot);

    public string SolutionPath
    {
        get
        {
            try
            {
                if (Directory.Exists(_info.SourceRoot))
                {
                    var sln = Directory.EnumerateFiles(_info.SourceRoot, "*.*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f =>
                            f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
                    if (sln is not null)
                        return sln;
                }
            }
            catch { /* ignore */ }
            return "—";
        }
    }

    [RelayCommand]
    private void OpenRepository()
    {
        if (!HasRepository)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(_info.RepositoryUrl) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private void OpenSource() => OpenFolder(SourceRoot);

    [RelayCommand]
    private void OpenRunLocation() => OpenFolder(RunLocation);

    [RelayCommand]
    private void OpenInCursor()
    {
        if (!Directory.Exists(SourceRoot))
            return;

        try
        {
            // Requires the Cursor CLI ("cursor") on PATH.
            Process.Start(new ProcessStartInfo("cursor", $"\"{SourceRoot}\"") { UseShellExecute = true });
        }
        catch
        {
            OpenFolder(SourceRoot);
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }
}
