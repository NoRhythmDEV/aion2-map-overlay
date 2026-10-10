using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Aion2MapOverlay.App;

// Ordinary Windows global hotkey on our own window; no keyboard or game hooks.
internal sealed class ScreenshotHotkey : IDisposable
{
    private const int Id = 0xA102;
    private readonly HwndSource source;
    private readonly Action toggle;
    public bool Registered { get; }

    public ScreenshotHotkey(nint handle, Action toggle)
    {
        this.toggle = toggle;
        source = HwndSource.FromHwnd(handle);
        source.AddHook(OnMessage);
        Registered = RegisterHotKey(handle, Id, 0x4000, 0x77); // MOD_NOREPEAT, F8
    }

    private nint OnMessage(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == Id) { handled = true; toggle(); }
        return 0;
    }

    public void Dispose()
    {
        if (Registered) UnregisterHotKey(source.Handle, Id);
        source.RemoveHook(OnMessage);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
}
