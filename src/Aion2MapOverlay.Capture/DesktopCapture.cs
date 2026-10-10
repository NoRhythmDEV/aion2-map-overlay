using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Aion2MapOverlay.Capture;

public sealed record DisplayArea(int X, int Y, int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height} at {X}, {Y}";
}

// Initial capture backend: bounded, downsampled desktop snapshots, no video encoding.
// Deliberately isolated so a GPU-backed backend can replace it after profiling.
public sealed class DesktopCapture : IDisposable
{
    private readonly nint screen, memory, bitmap, oldBitmap, pixels;
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
        Display = display; Width = Math.Min(maximumWidth, display.Width);
        Height = (int)Math.Round(display.Height * (double)Width / display.Width);
        screen = GetDC(0); memory = CreateCompatibleDC(screen);
        var info = new BitmapInfo { Size = 40, Width = Width, Height = -Height, Planes = 1, BitCount = 32 };
        bitmap = CreateDIBSection(screen, ref info, 0, out pixels, 0, 0);
        if (screen == 0 || memory == 0 || bitmap == 0) { Dispose(); throw new InvalidOperationException("Windows screen capture could not be initialized."); }
        oldBitmap = SelectObject(memory, bitmap);
        SetStretchBltMode(memory, 3);
    }

    public Mat GrabGray()
    {
        if (!StretchBlt(memory, 0, 0, Width, Height, screen, Display.X, Display.Y, Display.Width, Display.Height, 0x00CC0020))
            throw new InvalidOperationException("Windows screen capture failed.");
        using var color = Mat.FromPixelData(Height, Width, MatType.CV_8UC4, pixels);
        var gray = new Mat(); Cv2.CvtColor(color, gray, ColorConversionCodes.BGRA2GRAY); return gray;
    }

    public void Dispose()
    {
        if (oldBitmap != 0) SelectObject(memory, oldBitmap);
        if (bitmap != 0) DeleteObject(bitmap);
        if (memory != 0) DeleteDC(memory);
        if (screen != 0) ReleaseDC(0, screen);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPels, YPels; public uint Used, Important; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect rect, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, int sw, int sh, uint operation);
}
