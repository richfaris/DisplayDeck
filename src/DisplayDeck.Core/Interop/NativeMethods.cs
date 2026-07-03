using System.Runtime.InteropServices;

namespace DisplayDeck.Core.Interop;

/// <summary>
/// P/Invoke declarations for the classic GDI display APIs (EnumDisplayDevices /
/// EnumDisplaySettings / ChangeDisplaySettingsEx). These are used to enumerate
/// adapters, monitors and their supported modes, and to apply mode changes.
/// </summary>
internal static class NativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplaySettingsEx(
        string lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        IntPtr lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    public const int ENUM_CURRENT_SETTINGS = -1;
    public const int ENUM_REGISTRY_SETTINGS = -2;

    // EnumDisplaySettingsEx flags
    public const uint EDS_RAWMODE = 0x00000002;

    // EnumDisplayDevices flags
    public const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;

    // ChangeDisplaySettings flags
    public const uint CDS_UPDATEREGISTRY = 0x00000001;
    public const uint CDS_TEST = 0x00000002;
    public const uint CDS_FULLSCREEN = 0x00000004;
    public const uint CDS_GLOBAL = 0x00000008;
    public const uint CDS_SET_PRIMARY = 0x00000010;
    public const uint CDS_NORESET = 0x10000000;
    public const uint CDS_RESET = 0x40000000;

    // ChangeDisplaySettings return codes
    public const int DISP_CHANGE_SUCCESSFUL = 0;
    public const int DISP_CHANGE_RESTART = 1;
    public const int DISP_CHANGE_FAILED = -1;
    public const int DISP_CHANGE_BADMODE = -2;
    public const int DISP_CHANGE_NOTUPDATED = -3;
    public const int DISP_CHANGE_BADFLAGS = -4;
    public const int DISP_CHANGE_BADPARAM = -5;
    public const int DISP_CHANGE_BADDUALVIEW = -6;
}
