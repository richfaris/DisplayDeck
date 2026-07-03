using DisplayDeck.Core.Interop;
using DisplayDeck.Core.Models;

namespace DisplayDeck.Core.Services;

/// <summary>
/// Applies changes to displays: resolution/refresh/color depth, orientation,
/// primary monitor, and positions. Wraps ChangeDisplaySettingsEx.
/// </summary>
public sealed class DisplayControlService
{
    private const uint FieldsMode =
        (uint)(DevModeFields.PelsWidth | DevModeFields.PelsHeight |
               DevModeFields.DisplayFrequency | DevModeFields.BitsPerPixel);

    /// <summary>Apply resolution / refresh / color depth to a single display.</summary>
    public ChangeResult ApplyMode(string deviceName, DisplayMode mode, bool test = false)
    {
        if (!TryGetCurrentDevMode(deviceName, out var dm))
            return new ChangeResult(ChangeStatus.Failed, $"Could not read current settings for {deviceName}.");

        dm.dmPelsWidth = (uint)mode.Width;
        dm.dmPelsHeight = (uint)mode.Height;
        dm.dmDisplayFrequency = (uint)mode.RefreshRate;
        dm.dmBitsPerPel = (uint)mode.BitsPerPixel;
        dm.dmFields = FieldsMode;

        uint flags = test ? NativeMethods.CDS_TEST : NativeMethods.CDS_UPDATEREGISTRY;
        int r = NativeMethods.ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
        return MapResult(r);
    }

    /// <summary>Rotate a display, swapping width/height when parity changes.</summary>
    public ChangeResult SetOrientation(string deviceName, DisplayOrientation orientation)
    {
        if (!TryGetCurrentDevMode(deviceName, out var dm))
            return new ChangeResult(ChangeStatus.Failed, $"Could not read current settings for {deviceName}.");

        bool currentlyPortrait = dm.dmDisplayOrientation % 2 == 1;
        bool targetPortrait = (uint)orientation % 2 == 1;

        dm.dmDisplayOrientation = (uint)orientation;
        dm.dmFields = (uint)(DevModeFields.DisplayOrientation | DevModeFields.PelsWidth | DevModeFields.PelsHeight);

        if (currentlyPortrait != targetPortrait)
            (dm.dmPelsWidth, dm.dmPelsHeight) = (dm.dmPelsHeight, dm.dmPelsWidth);

        int r = NativeMethods.ChangeDisplaySettingsEx(
            deviceName, ref dm, IntPtr.Zero, NativeMethods.CDS_UPDATEREGISTRY, IntPtr.Zero);
        return MapResult(r);
    }

    /// <summary>
    /// Make <paramref name="deviceName"/> the primary display. All displays are shifted
    /// so the new primary sits at (0,0), then committed atomically.
    /// </summary>
    public ChangeResult SetPrimary(string deviceName)
    {
        if (!TryGetCurrentDevMode(deviceName, out var targetDm))
            return new ChangeResult(ChangeStatus.Failed, $"Could not read current settings for {deviceName}.");

        int offsetX = targetDm.dmPositionX;
        int offsetY = targetDm.dmPositionY;

        foreach (var device in GetAttachedDeviceNames())
        {
            if (!TryGetCurrentDevMode(device, out var dm))
                continue;

            dm.dmPositionX -= offsetX;
            dm.dmPositionY -= offsetY;
            dm.dmFields = (uint)DevModeFields.Position;

            uint flags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
            if (string.Equals(device, deviceName, StringComparison.OrdinalIgnoreCase))
                flags |= NativeMethods.CDS_SET_PRIMARY;

            NativeMethods.ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
        }

        int r = NativeMethods.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        return MapResult(r);
    }

    /// <summary>
    /// Apply a full saved profile atomically: for every profile display that maps to a
    /// currently-attached device, set its mode, position and orientation with NORESET,
    /// flag the primary, then commit everything in a single ChangeDisplaySettingsEx.
    /// </summary>
    /// <param name="profile">The profile to apply.</param>
    /// <param name="current">Displays currently attached (used to match by hardware id / device name).</param>
    public ChangeResult ApplyProfile(DisplayProfile profile, IReadOnlyList<DisplayInfo> current)
    {
        if (profile.Displays.Count == 0)
            return new ChangeResult(ChangeStatus.InvalidParameters, "This profile has no displays to apply.");

        int applied = 0;

        foreach (var entry in profile.Displays)
        {
            string? device = ResolveDevice(entry, current);
            if (device is null)
                continue; // display from the profile isn't connected right now — skip it.

            if (!TryGetCurrentDevMode(device, out var dm))
                continue;

            bool portrait = (uint)entry.Orientation % 2 == 1;

            dm.dmPelsWidth = (uint)(portrait ? entry.Height : entry.Width);
            dm.dmPelsHeight = (uint)(portrait ? entry.Width : entry.Height);
            dm.dmDisplayFrequency = (uint)entry.RefreshRate;
            dm.dmBitsPerPel = (uint)(entry.BitsPerPixel <= 0 ? 32 : entry.BitsPerPixel);
            dm.dmDisplayOrientation = (uint)entry.Orientation;
            dm.dmPositionX = entry.PositionX;
            dm.dmPositionY = entry.PositionY;
            dm.dmFields = (uint)(DevModeFields.PelsWidth | DevModeFields.PelsHeight |
                                 DevModeFields.DisplayFrequency | DevModeFields.BitsPerPixel |
                                 DevModeFields.DisplayOrientation | DevModeFields.Position);

            uint flags = NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET;
            if (entry.IsPrimary)
                flags |= NativeMethods.CDS_SET_PRIMARY;

            NativeMethods.ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
            applied++;
        }

        if (applied == 0)
            return new ChangeResult(ChangeStatus.Failed, "None of the profile's displays are currently connected.");

        int r = NativeMethods.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        return MapResult(r);
    }

    /// <summary>Match a saved entry to a live device: prefer stable hardware id, fall back to device name.</summary>
    private static string? ResolveDevice(DisplayProfileEntry entry, IReadOnlyList<DisplayInfo> current)
    {
        if (!string.IsNullOrWhiteSpace(entry.MonitorDeviceId))
        {
            var byId = current.FirstOrDefault(d =>
                string.Equals(d.MonitorDeviceId, entry.MonitorDeviceId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
                return byId.DeviceName;
        }

        var byName = current.FirstOrDefault(d =>
            string.Equals(d.DeviceName, entry.DeviceName, StringComparison.OrdinalIgnoreCase));
        return byName?.DeviceName;
    }

    /// <summary>Reposition multiple displays at once (for drag-to-arrange).</summary>
    public ChangeResult SetPositions(IReadOnlyDictionary<string, (int X, int Y)> positions)
    {
        foreach (var (device, pos) in positions)
        {
            if (!TryGetCurrentDevMode(device, out var dm))
                continue;

            dm.dmPositionX = pos.X;
            dm.dmPositionY = pos.Y;
            dm.dmFields = (uint)DevModeFields.Position;

            NativeMethods.ChangeDisplaySettingsEx(
                device, ref dm, IntPtr.Zero,
                NativeMethods.CDS_UPDATEREGISTRY | NativeMethods.CDS_NORESET, IntPtr.Zero);
        }

        int r = NativeMethods.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        return MapResult(r);
    }

    private static bool TryGetCurrentDevMode(string deviceName, out DEVMODE dm)
    {
        dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        return NativeMethods.EnumDisplaySettingsEx(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm, 0);
    }

    private static IEnumerable<string> GetAttachedDeviceNames()
    {
        uint index = 0;
        while (true)
        {
            var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!NativeMethods.EnumDisplayDevices(null, index, ref adapter, 0))
                break;
            index++;

            if (adapter.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop))
                yield return adapter.DeviceName;
        }
    }

    private static ChangeResult MapResult(int code) => code switch
    {
        NativeMethods.DISP_CHANGE_SUCCESSFUL => ChangeResult.Success,
        NativeMethods.DISP_CHANGE_RESTART => new ChangeResult(ChangeStatus.NeedsRestart, "Applied — a restart is required to fully take effect."),
        NativeMethods.DISP_CHANGE_BADMODE => new ChangeResult(ChangeStatus.BadMode, "This display mode is not supported by the monitor."),
        NativeMethods.DISP_CHANGE_BADPARAM => new ChangeResult(ChangeStatus.InvalidParameters, "Invalid parameters for the display change."),
        NativeMethods.DISP_CHANGE_BADFLAGS => new ChangeResult(ChangeStatus.InvalidParameters, "Invalid flags for the display change."),
        NativeMethods.DISP_CHANGE_BADDUALVIEW => new ChangeResult(ChangeStatus.Failed, "The change failed due to a dual-view configuration."),
        NativeMethods.DISP_CHANGE_NOTUPDATED => new ChangeResult(ChangeStatus.Failed, "Unable to write settings to the registry."),
        _ => new ChangeResult(ChangeStatus.Failed, "The display change failed."),
    };
}
