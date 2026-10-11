using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Aion2MapOverlay.Capture;

public sealed record DisplayArea(int X, int Y, int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height} at {X}, {Y}";
}

// Initial capture backend: bounded, downsampled desktop snapshots, no video encoding.
// Deliberately isolated so a GPU-backed backend can replace it after profiling.
public sealed class ScreenCaptureException(string operation, int nativeError) : IOException(
    $"{operation} failed (Windows error {nativeError}).")
{
    public int NativeError { get; } = nativeError;
}

public sealed class DesktopCapture : IDisposable
{
    private nint screen, memory, bitmap, oldBitmap, pixels;
    private bool disposed;
    public int Width { get; }
    public int Height { get; }
    public DisplayArea Display { get; }

    public static IReadOnlyList<DisplayArea> Displays()
    {
        var displays = new List<DisplayArea>();
        MonitorCallback callback = (nint monitor, nint dc, ref NativeRect rect, nint data) =>
        { displays.Add(new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)); return true; };
        EnumDisplayMonitors(0, 0, callback, 0);
        return displays;
    }

    public DesktopCapture(DisplayArea display, int maximumWidth = 1600)
    {
        if (display.Width <= 0 || display.Height <= 0 || maximumWidth <= 0) throw new ArgumentOutOfRangeException(nameof(display));
        Display = display; Width = Math.Min(maximumWidth, display.Width);
        Height = Math.Max(1, (int)Math.Round(display.Height * (double)Width / display.Width));
        // Initialize on the first frame so temporary desktop unavailability can be retried.
    }

    private void Initialize()
    {
        if (memory != 0) return;
        try
        {
            screen = GetDC(0);
            if (screen == 0) throw new ScreenCaptureException("Desktop access", Marshal.GetLastWin32Error());
            memory = CreateCompatibleDC(screen);
            if (memory == 0) throw new ScreenCaptureException("Capture context creation", Marshal.GetLastWin32Error());
            var info = new BitmapInfo { Size = 40, Width = Width, Height = -Height, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(screen, ref info, 0, out pixels, 0, 0);
            if (bitmap == 0 || pixels == 0) throw new ScreenCaptureException("Capture buffer creation", Marshal.GetLastWin32Error());
            oldBitmap = SelectObject(memory, bitmap);
            if (oldBitmap == 0 || oldBitmap == -1) throw new ScreenCaptureException("Capture buffer selection", Marshal.GetLastWin32Error());
            SetStretchBltMode(memory, 3);
        }
        catch { Reset(); throw; }
    }

    public Mat GrabGray()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Initialize();
        if (!StretchBlt(memory, 0, 0, Width, Height, screen, Display.X, Display.Y, Display.Width, Display.Height, 0x00CC0020))
            throw new ScreenCaptureException("Screen capture", Marshal.GetLastWin32Error());
        using var color = Mat.FromPixelData(Height, Width, MatType.CV_8UC4, pixels);
        var gray = new Mat();
        try { Cv2.CvtColor(color, gray, ColorConversionCodes.BGRA2GRAY); return gray; }
        catch { gray.Dispose(); throw; }
    }

    public void Reset()
    {
        if (oldBitmap != 0 && oldBitmap != -1) SelectObject(memory, oldBitmap);
        if (bitmap != 0) DeleteObject(bitmap);
        if (memory != 0) DeleteDC(memory);
        if (screen != 0) ReleaseDC(0, screen);
        screen = memory = bitmap = oldBitmap = pixels = 0;
    }

    public void Dispose() { Reset(); disposed = true; }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPels, YPels; public uint Used, Important; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect rect, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool StretchBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, int sw, int sh, uint operation);
}
