using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DisplayDeck.App.Services;

/// <summary>
/// Registers a system-wide hotkey to summon the app. Tries a preferred gesture and
/// falls back through alternatives if it is already taken by another process.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xB007;

    [Flags]
    private enum Mod : uint
    {
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000,
    }

    private sealed record Candidate(Mod Modifiers, uint Vk, string Display);

    // VK codes
    private const uint VK_D = 0x44;

    private static readonly Candidate[] Candidates =
    {
        new(Mod.Control | Mod.Alt, VK_D, "Ctrl + Alt + D"),
        new(Mod.Control | Mod.Shift, VK_D, "Ctrl + Shift + D"),
        new(Mod.Win | Mod.Alt, VK_D, "Win + Alt + D"),
        new(Mod.Control | Mod.Alt | Mod.Shift, VK_D, "Ctrl + Alt + Shift + D"),
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Window _window;
    private HwndSource? _source;
    private IntPtr _handle;
    private bool _registered;

    public event Action? Pressed;

    /// <summary>The gesture that was successfully registered, or null if none.</summary>
    public string? ActiveGesture { get; private set; }

    public HotkeyManager(Window window)
    {
        _window = window;
        if (window.IsInitialized && new WindowInteropHelper(window).Handle != IntPtr.Zero)
            Attach();
        else
            window.SourceInitialized += (_, _) => Attach();
    }

    private void Attach()
    {
        _handle = new WindowInteropHelper(_window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
        Log.Write($"HotkeyManager attaching to hwnd 0x{_handle.ToInt64():X}");

        foreach (var c in Candidates)
        {
            uint mods = (uint)(c.Modifiers | Mod.NoRepeat);
            if (RegisterHotKey(_handle, HotkeyId, mods, c.Vk))
            {
                _registered = true;
                ActiveGesture = c.Display;
                Log.Write($"Registered global hotkey: {c.Display}");
                return;
            }

            int err = Marshal.GetLastWin32Error();
            Log.Write($"Failed to register {c.Display} (win32 error {err}).");
        }

        Log.Write("Could not register ANY global hotkey. Tray icon will still work.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Log.Write("Hotkey pressed.");
            Pressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered && _handle != IntPtr.Zero)
            UnregisterHotKey(_handle, HotkeyId);
        _source?.RemoveHook(WndProc);
        _registered = false;
    }
}
