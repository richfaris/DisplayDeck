using System.IO;

namespace DisplayDeck.App.Services;

/// <summary>Minimal file logger for diagnostics: %LOCALAPPDATA%\DisplayDeck\log.txt.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DisplayDeck", "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
