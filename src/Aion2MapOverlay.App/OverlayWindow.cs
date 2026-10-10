using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2MapOverlay.Capture;

namespace Aion2MapOverlay.App;

internal sealed record PlacedMarker(double X, double Y, string Key);
internal sealed record OverlayFrame(PlacedMarker[] Markers, int Width, int Height, DateTime CapturedAt);

internal sealed class OverlayWindow : Window
{
    private readonly Surface surface;
    private readonly DispatcherTimer expiry;
    public bool CaptureExcluded { get; private set; }
    public int CaptureExclusionError { get; private set; }

    public OverlayWindow(DisplayArea display, Dictionary<string, ImageSource> icons)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        surface = new(icons); Content = surface;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(handle, -20, GetWindowLongPtr(handle, -20) | 0x08000000 | 0x00000020 | 0x00000080);
            CaptureExcluded = SetWindowDisplayAffinity(handle, 0x11);
            SetWindowPos(handle, -1, display.X, display.Y, display.Width, display.Height, 0x0010);
        };
        Loaded += (_, _) => { SetWindowPos(new WindowInteropHelper(this).Handle, -1, display.X, display.Y, display.Width, display.Height, 0x0010); EnsureCaptureExcluded(); };
        expiry = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Render, (_, _) => surface.InvalidateVisual(), Dispatcher);
        Closed += (_, _) => expiry.Stop();
    }

    public void Update(OverlayFrame? frame, double size, double opacity)
    { surface.Frame = frame; surface.IconSize = size; surface.IconOpacity = opacity; surface.InvalidateVisual(); }

    public void EnsureCaptureExcluded()
    {
        CaptureExcluded = SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11);
        CaptureExclusionError = CaptureExcluded ? 0 : Marshal.GetLastWin32Error();
    }

    public static bool IsGameForeground()
    {
        var text = new StringBuilder(512); GetWindowText(GetForegroundWindow(), text, text.Capacity);
        var title = text.ToString();
        return title.Contains("AION", StringComparison.OrdinalIgnoreCase) && !title.Contains("Map Overlay", StringComparison.OrdinalIgnoreCase);
    }

    public static bool Exclude(Window window) => SetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, 0x11);

    private sealed class Surface(Dictionary<string, ImageSource> icons) : FrameworkElement
    {
        private readonly Brush backdrop = new SolidColorBrush(Color.FromArgb(130, 0, 0, 0));
        public OverlayFrame? Frame { get; set; }
        public double IconSize { get; set; } = 20;
        public double IconOpacity { get; set; } = .9;
        protected override void OnRender(DrawingContext dc)
        {
            var frame = Frame;
            if (frame is null || DateTime.UtcNow - frame.CapturedAt > TimeSpan.FromMilliseconds(300) || !IsGameForeground()) return;
            var fx = ActualWidth / frame.Width; var fy = ActualHeight / frame.Height;
            dc.PushClip(new RectangleGeometry(new Rect(ActualWidth * .18, ActualHeight * .12, ActualWidth * .76, ActualHeight * .82)));
            dc.PushOpacity(IconOpacity);
            foreach (var marker in frame.Markers)
            {
                var x = marker.X * fx; var y = marker.Y * fy;
                dc.DrawEllipse(backdrop, null, new Point(x, y), IconSize / 2 + 1, IconSize / 2 + 1);
                if (icons.TryGetValue(marker.Key, out var image)) dc.DrawImage(image, new Rect(x - IconSize / 2, y - IconSize / 2, IconSize, IconSize));
                else dc.DrawEllipse(Brushes.Cyan, null, new Point(x, y), IconSize / 3, IconSize / 3);
            }
            dc.Pop(); dc.Pop();
        }
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowDisplayAffinity(nint handle, uint affinity);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint handle, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, StringBuilder title, int length);
}
