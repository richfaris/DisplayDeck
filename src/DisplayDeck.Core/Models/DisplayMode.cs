namespace DisplayDeck.Core.Models;

/// <summary>A single supported display mode (resolution + refresh + color depth).</summary>
public sealed record DisplayMode(int Width, int Height, int RefreshRate, int BitsPerPixel)
{
    public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;

    /// <summary>Total pixel count, useful for sorting.</summary>
    public long PixelCount => (long)Width * Height;

    public override string ToString() => $"{Width} × {Height} @ {RefreshRate} Hz";

    public string ResolutionLabel => $"{Width} × {Height}";
}
