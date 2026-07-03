using DisplayDeck.Core.Interop;

namespace DisplayDeck.Core.Services;

/// <summary>
/// Resolves friendly EDID monitor names (e.g. "DELL U2723QE") keyed by GDI device
/// name (e.g. "\\.\DISPLAY1") using the CCD API. Fails soft: returns an empty map.
/// </summary>
internal static class MonitorNameResolver
{
    public static Dictionary<string, string> GetFriendlyNames()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            int status = CcdInterop.GetDisplayConfigBufferSizes(
                CcdInterop.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (status != CcdInterop.ERROR_SUCCESS || pathCount == 0)
                return map;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            status = CcdInterop.QueryDisplayConfig(
                CcdInterop.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount, paths,
                ref modeCount, modes,
                IntPtr.Zero);
            if (status != CcdInterop.ERROR_SUCCESS)
                return map;

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

                var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = CcdInterop.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = path.targetInfo.adapterId,
                        id = path.targetInfo.id,
                    },
                };

                string friendly = CcdInterop.DisplayConfigGetDeviceInfo(ref targetName) == CcdInterop.ERROR_SUCCESS
                    ? targetName.monitorFriendlyDeviceName
                    : string.Empty;

                var gdi = sourceName.viewGdiDeviceName;
                if (!string.IsNullOrWhiteSpace(gdi) && !string.IsNullOrWhiteSpace(friendly))
                    map[gdi] = friendly;
            }
        }
        catch
        {
            // CCD not available or unexpected shape; friendly names are best-effort.
        }

        return map;
    }
}
