namespace DisplayDeck.Core.Models;

/// <summary>
/// Per-monitor DPI scaling state: the current scale, the OS-recommended scale, and the
/// full list of scales Windows allows for this display (e.g. 100, 125, 150 …).
/// </summary>
public sealed class DpiScalingInfo
{
    public bool IsSupported { get; init; }

    public int Current { get; init; } = 100;

    public int Recommended { get; init; } = 100;

    public IReadOnlyList<int> Options { get; init; } = Array.Empty<int>();
}
