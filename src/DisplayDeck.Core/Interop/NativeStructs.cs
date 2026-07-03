using System.Runtime.InteropServices;

namespace DisplayDeck.Core.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DISPLAY_DEVICE
{
    [MarshalAs(UnmanagedType.U4)]
    public int cb;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;

    [MarshalAs(UnmanagedType.U4)]
    public DisplayDeviceStateFlags StateFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

[Flags]
internal enum DisplayDeviceStateFlags : uint
{
    AttachedToDesktop = 0x00000001,
    MultiDriver = 0x00000002,
    PrimaryDevice = 0x00000004,
    MirroringDriver = 0x00000008,
    VgaCompatible = 0x00000010,
    Removable = 0x00000020,
    ModesPruned = 0x08000000,
    Remote = 0x04000000,
    Disconnect = 0x02000000,
    // For monitor sub-devices
    Active = 0x00000001,
    Attached = 0x00000002,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DEVMODE
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmDeviceName;

    public ushort dmSpecVersion;
    public ushort dmDriverVersion;
    public ushort dmSize;
    public ushort dmDriverExtra;
    public uint dmFields;

    // POINTL / union: for display devices this is position + orientation
    public int dmPositionX;
    public int dmPositionY;
    public uint dmDisplayOrientation;
    public uint dmDisplayFixedOutput;

    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmFormName;

    public ushort dmLogPixels;
    public uint dmBitsPerPel;
    public uint dmPelsWidth;
    public uint dmPelsHeight;
    public uint dmDisplayFlags; // union with dmNup
    public uint dmDisplayFrequency;
    public uint dmICMMethod;
    public uint dmICMIntent;
    public uint dmMediaType;
    public uint dmDitherType;
    public uint dmReserved1;
    public uint dmReserved2;
    public uint dmPanningWidth;
    public uint dmPanningHeight;
}

/// <summary>Fields of DEVMODE that are considered valid (dmFields bitmask).</summary>
[Flags]
internal enum DevModeFields : uint
{
    Position = 0x00000020,
    DisplayOrientation = 0x00000080,
    BitsPerPixel = 0x00040000,
    PelsWidth = 0x00080000,
    PelsHeight = 0x00100000,
    DisplayFlags = 0x00200000,
    DisplayFrequency = 0x00400000,
    DisplayFixedOutput = 0x20000000,
}

internal enum DisplayOrientationValue : uint
{
    Default = 0, // Landscape
    Rotate90 = 1, // Portrait
    Rotate180 = 2, // Landscape (flipped)
    Rotate270 = 3, // Portrait (flipped)
}
