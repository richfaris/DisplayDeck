using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace DisplayDeck.App.Services;

/// <summary>
/// Grabs a single still frame from the default webcam for "Fun mode". Deliberately
/// simple and best-effort: it opens the camera, throws away a few warm-up frames so
/// auto-exposure/white-balance can settle, snaps one frame, and hands back a frozen
/// <see cref="BitmapSource"/> that's safe to use from the UI thread.
/// </summary>
public static class CameraService
{
    /// <summary>
    /// Try to take a selfie. Returns the image on success, or a human-readable reason why not.
    /// Never throws — callers can surface <paramref name="error"/> in a toast.
    /// </summary>
    public static (BitmapSource? Image, string? Error) TryCapture()
    {
        try
        {
            // Prefer DirectShow on Windows; it's the most broadly compatible backend.
            using var capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (!capture.IsOpened())
            {
                // Fall back to the default backend in case DirectShow isn't available.
                capture.Open(0);
                if (!capture.IsOpened())
                    return (null, "no camera was found (or access is blocked in Windows privacy settings).");
            }

            using var frame = new Mat();

            // Warm-up: the first few frames are often black while the sensor wakes up.
            for (int i = 0; i < 10; i++)
            {
                if (!capture.Read(frame) || frame.Empty())
                    System.Threading.Thread.Sleep(60);
            }

            if (frame.Empty())
                return (null, "the camera didn't return an image.");

            // Mirror horizontally so it reads like a real mirror/selfie.
            Cv2.Flip(frame, frame, FlipMode.Y);

            Cv2.ImEncode(".bmp", frame, out byte[] buffer);

            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(buffer))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze(); // cross-thread safe

            return (bmp, null);
        }
        catch (Exception ex)
        {
            Log.Write($"CameraService.TryCapture failed: {ex}");
            return (null, ex.Message);
        }
    }
}
