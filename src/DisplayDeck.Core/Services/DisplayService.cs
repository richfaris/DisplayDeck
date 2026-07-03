using DisplayDeck.Core.Interop;
using DisplayDeck.Core.Models;

namespace DisplayDeck.Core.Services;

/// <summary>
/// Enumerates the system's displays and their supported modes using the classic
/// GDI display APIs. Friendly EDID names are resolved via the CCD API where possible.
/// </summary>
public sealed class DisplayService
{
    /// <summary>Enumerate all displays currently attached to the desktop.</summary>
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var results = new List<DisplayInfo>();
        var friendlyNames = MonitorNameResolver.GetFriendlyNames();

        uint deviceIndex = 0;
        while (true)
        {
            var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!NativeMethods.EnumDisplayDevices(null, deviceIndex, ref adapter, 0))
                break;

            deviceIndex++;

            bool attached = adapter.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop);
            if (!attached)
                continue;

            bool isPrimary = adapter.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice);

            var current = GetCurrentMode(adapter.DeviceName, out int posX, out int posY, out var orientation);
            var modes = GetSupportedModes(adapter.DeviceName);

            var (monitorName, monitorId) = GetMonitorSubDevice(adapter.DeviceName);
            friendlyNames.TryGetValue(adapter.DeviceName, out var edidName);

            var info = new DisplayInfo
            {
                DeviceName = adapter.DeviceName,
                AdapterName = adapter.DeviceString,
                FriendlyName = !string.IsNullOrWhiteSpace(edidName) ? edidName! : monitorName,
                MonitorDeviceId = monitorId,
                IsPrimary = isPrimary,
                IsActive = true,
                PositionX = posX,
                PositionY = posY,
                Orientation = orientation,
                CurrentMode = current,
                SupportedModes = modes,
            };

            results.Add(info);
        }

        return results
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string name, string id) GetMonitorSubDevice(string adapterName)
    {
        var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (NativeMethods.EnumDisplayDevices(adapterName, 0, ref monitor, 0))
            return (monitor.DeviceString, monitor.DeviceID);
        return (string.Empty, string.Empty);
    }

    private static DisplayMode? GetCurrentMode(string deviceName, out int posX, out int posY, out DisplayOrientation orientation)
    {
        var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        posX = 0;
        posY = 0;
        orientation = DisplayOrientation.Landscape;

        if (!NativeMethods.EnumDisplaySettingsEx(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm, 0))
            return null;

        posX = dm.dmPositionX;
        posY = dm.dmPositionY;
        orientation = (DisplayOrientation)dm.dmDisplayOrientation;

        return new DisplayMode(
            (int)dm.dmPelsWidth,
            (int)dm.dmPelsHeight,
            (int)dm.dmDisplayFrequency,
            (int)dm.dmBitsPerPel);
    }

    private static IReadOnlyList<DisplayMode> GetSupportedModes(string deviceName)
    {
        var set = new HashSet<DisplayMode>();
        int modeNum = 0;
        while (true)
        {
            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (!NativeMethods.EnumDisplaySettingsEx(deviceName, modeNum, ref dm, 0))
                break;

            modeNum++;

            // Skip interlaced / dubious very-low modes; keep >= 8bpp.
            if (dm.dmBitsPerPel < 8)
                continue;

            set.Add(new DisplayMode(
                (int)dm.dmPelsWidth,
                (int)dm.dmPelsHeight,
                (int)dm.dmDisplayFrequency,
                (int)dm.dmBitsPerPel));
        }

        return set
            .OrderByDescending(m => m.PixelCount)
            .ThenByDescending(m => m.RefreshRate)
            .ThenByDescending(m => m.BitsPerPixel)
            .ToList();
    }
}
