using DisplayDeck.Core.Interop;
using DisplayDeck.Core.Models;

namespace DisplayDeck.Core.Services;

/// <summary>
/// Reads and changes per-monitor display scaling (the Windows Settings "Scale" / DPI
/// setting) via the CCD DisplayConfig device-info API. Fails soft on unsupported
/// displays so the rest of the app keeps working.
/// </summary>
public sealed class DpiScalingService
{
    // The standard Windows scale ladder. curScaleRel/min/max index into this list,
    // offset from the display's recommended scale.
    private static readonly int[] DpiVals =
    {
        100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500,
    };

    /// <summary>Read the current/recommended/available scaling for a GDI device (\\.\DISPLAYn).</summary>
    public DpiScalingInfo GetScaling(string gdiDeviceName)
    {
        if (!TryResolveSource(gdiDeviceName, out var adapterId, out uint sourceId))
            return new DpiScalingInfo { IsSupported = false };

        var packet = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = CcdInterop.DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(),
                adapterId = adapterId,
                id = sourceId,
            },
        };

        if (CcdInterop.DisplayConfigGetDeviceInfo(ref packet) != CcdInterop.ERROR_SUCCESS)
            return new DpiScalingInfo { IsSupported = false };

        int cur = packet.curScaleRel;
        if (cur < packet.minScaleRel) cur = packet.minScaleRel;
        else if (cur > packet.maxScaleRel) cur = packet.maxScaleRel;

        // Recommended sits |minScaleRel| steps up from the bottom of the allowed range.
        int recommendedIndex = Math.Abs(packet.minScaleRel);
        int currentIndex = recommendedIndex + cur;
        int maxIndex = recommendedIndex + packet.maxScaleRel;

        if (recommendedIndex < 0 || recommendedIndex >= DpiVals.Length ||
            currentIndex < 0 || currentIndex >= DpiVals.Length ||
            maxIndex < 0 || maxIndex >= DpiVals.Length)
        {
            return new DpiScalingInfo { IsSupported = false };
        }

        var options = new List<int>();
        for (int i = 0; i <= maxIndex; i++)
            options.Add(DpiVals[i]);

        return new DpiScalingInfo
        {
            IsSupported = true,
            Current = DpiVals[currentIndex],
            Recommended = DpiVals[recommendedIndex],
            Options = options,
        };
    }

    /// <summary>Set the scaling for a GDI device to a percentage from its available options.</summary>
    public ChangeResult SetScaling(string gdiDeviceName, int percent)
    {
        if (!TryResolveSource(gdiDeviceName, out var adapterId, out uint sourceId))
            return new ChangeResult(ChangeStatus.Failed, "Could not locate this display to change its scaling.");

        var info = GetScaling(gdiDeviceName);
        if (!info.IsSupported)
            return new ChangeResult(ChangeStatus.Failed, "Per-monitor scaling isn't available for this display.");

        int targetIndex = Array.IndexOf(DpiVals, percent);
        if (targetIndex < 0 || !info.Options.Contains(percent))
            return new ChangeResult(ChangeStatus.InvalidParameters, $"{percent}% isn't a supported scale for this display.");

        int recommendedIndex = Array.IndexOf(DpiVals, info.Recommended);
        int scaleRel = targetIndex - recommendedIndex;

        var setPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = CcdInterop.DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(),
                adapterId = adapterId,
                id = sourceId,
            },
            scaleRel = scaleRel,
        };

        int r = CcdInterop.DisplayConfigSetDeviceInfo(ref setPacket);
        return r == CcdInterop.ERROR_SUCCESS
            ? ChangeResult.Success
            : new ChangeResult(ChangeStatus.Failed, "Windows rejected the scaling change.");
    }

    /// <summary>Map a GDI device name to its CCD source (adapter LUID + source id).</summary>
    private static bool TryResolveSource(string gdiDeviceName, out LUID adapterId, out uint sourceId)
    {
        adapterId = default;
        sourceId = 0;

        try
        {
            int status = CcdInterop.GetDisplayConfigBufferSizes(
                CcdInterop.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (status != CcdInterop.ERROR_SUCCESS || pathCount == 0)
                return false;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            status = CcdInterop.QueryDisplayConfig(
                CcdInterop.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount, paths,
                ref modeCount, modes,
                IntPtr.Zero);
            if (status != CcdInterop.ERROR_SUCCESS)
                return false;

            for (int i = 0; i < pathCount; i++)
            {
                var path = paths[i];

                var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = CcdInterop.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                        adapterId = path.sourceInfo.adapterId,
                        id = path.sourceInfo.id,
                    },
                };
                if (CcdInterop.DisplayConfigGetDeviceInfo(ref sourceName) != CcdInterop.ERROR_SUCCESS)
                    continue;

                if (string.Equals(sourceName.viewGdiDeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    adapterId = path.sourceInfo.adapterId;
                    sourceId = path.sourceInfo.id;
                    return true;
                }
            }
        }
        catch
        {
            // CCD not available; scaling is best-effort.
        }

        return false;
    }
}
