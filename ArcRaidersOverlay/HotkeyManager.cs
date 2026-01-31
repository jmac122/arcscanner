using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ArcRaidersOverlay;

public class HotkeyManager : IDisposable
{
    #region Win32 API

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier keys
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    // Windows messages
    public const int WM_HOTKEY = 0x0312;

    #endregion

    public const int SCAN_HOTKEY_ID = 9001;

    private readonly IntPtr _hwnd;
    private readonly List<int> _registeredHotkeys = new();
    private bool _disposed;

    public event EventHandler<int>? HotkeyPressed;

    public HotkeyManager(IntPtr windowHandle)
    {
        _hwnd = windowHandle;
    }

    public bool RegisterHotkey(int id, ModifierKeys modifiers, Key key)
    {
        uint mod = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mod |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) mod |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mod |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mod |= MOD_WIN;

        var vk = KeyInterop.VirtualKeyFromKey(key);

        if (RegisterHotKey(_hwnd, id, mod, (uint)vk))
        {
            _registeredHotkeys.Add(id);
            System.Diagnostics.Debug.WriteLine($"Registered hotkey {id}: {modifiers}+{key}");
            return true;
        }

        System.Diagnostics.Debug.WriteLine($"Failed to register hotkey {id}");
        return false;
    }

    public bool UnregisterHotkey(int id)
    {
        if (UnregisterHotKey(_hwnd, id))
        {
            _registeredHotkeys.Remove(id);
            return true;
        }
        return false;
    }

    public void HandleHotkeyMessage(int id)
    {
        HotkeyPressed?.Invoke(this, id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _registeredHotkeys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }
        _registeredHotkeys.Clear();

        GC.SuppressFinalize(this);
    }
}
