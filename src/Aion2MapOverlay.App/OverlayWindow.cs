using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2MapOverlay.Capture;

namespace Aion2MapOverlay.App;

internal sealed record PlacedMarker(double X, double Y, string Key, string? Id = null);
internal sealed record OverlayFrame(PlacedMarker[] Markers, int Width, int Height, DateTime CapturedAt);

internal sealed class OverlayWindow : Window
{
    private readonly Surface surface;
    private readonly DispatcherTimer expiry;
    public bool CaptureExcluded { get; private set; }
    public int CaptureExclusionError { get; private set; }
    public bool ScreenshotMode => surface.Frozen;

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
        expiry = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            if ((surface.Frame is not null || surface.Guidance is not null) && !surface.Frozen) surface.InvalidateVisual();
        }, Dispatcher);
        Closed += (_, _) => expiry.Stop();
    }

    public void Update(OverlayFrame? frame, double size, double opacity, GuidanceFrame? guidance = null)
    {
        if (surface.Frozen) return;
        surface.SetFrame(frame); surface.Guidance = guidance; surface.IconSize = size; surface.IconOpacity = opacity; surface.InvalidateVisual();
    }

    public void ClearGuidance() { surface.Guidance = null; surface.InvalidateVisual(); }

    public bool BeginScreenshotMode()
    {
        if (surface.Frame is null || DateTime.UtcNow - surface.Frame.CapturedAt > TimeSpan.FromMilliseconds(500)) return false;
        if (!SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0)) return false;
        CaptureExcluded = false;
        surface.Frozen = true;
        surface.FinishMotion();
        surface.InvalidateVisual();
        return true;
    }

    public bool EndScreenshotMode()
    {
        EnsureCaptureExcluded();
        if (!CaptureExcluded) return false;
        surface.Frozen = false;
        surface.SetFrame(null);
        surface.InvalidateVisual();
        return true;
    }

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
        public bool Frozen { get; set; }
        public GuidanceFrame? Guidance { get; set; }
        private readonly MarkerMotion motion = new();
        public void SetFrame(OverlayFrame? frame)
        {
            motion.SetFrame(frame, System.Diagnostics.Stopwatch.GetTimestamp());
            Frame = frame;
        }
        public void FinishMotion() => motion.Finish();
        protected override void OnRender(DrawingContext dc)
        {
            if (!Frozen && !IsGameForeground()) return;
            if (!Frozen && Guidance is { } guide && DateTime.UtcNow - guide.CapturedAt < TimeSpan.FromMilliseconds(500))
                DrawGuide(dc, guide);
            var frame = Frame;
            if (frame is null || (!Frozen && (DateTime.UtcNow - frame.CapturedAt > TimeSpan.FromMilliseconds(300) || !IsGameForeground()))) return;
            var fx = ActualWidth / frame.Width; var fy = ActualHeight / frame.Height;
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            dc.PushClip(new RectangleGeometry(new Rect(ActualWidth * .18, ActualHeight * .12, ActualWidth * .76, ActualHeight * .82)));
            dc.PushOpacity(IconOpacity);
            foreach (var marker in frame.Markers)
            {
                var position = motion.Position(marker, now);
                var x = position.X * fx; var y = position.Y * fy;
                dc.DrawEllipse(backdrop, null, new Point(x, y), IconSize / 2 + 1, IconSize / 2 + 1);
                if (icons.TryGetValue(marker.Key, out var image)) dc.DrawImage(image, new Rect(x - IconSize / 2, y - IconSize / 2, IconSize, IconSize));
                else dc.DrawEllipse(Brushes.Cyan, null, new Point(x, y), IconSize / 3, IconSize / 3);
            }
            dc.Pop(); dc.Pop();
        }

        private void DrawGuide(DrawingContext dc, GuidanceFrame guide)
        {
            var center = new Point(guide.CenterX * ActualWidth, guide.CenterY * ActualHeight);
            var radius = guide.Radius * ActualWidth;
            var accent = Brushes.DeepSkyBlue;
            dc.DrawEllipse(null, new Pen(backdrop, 4), center, radius, radius);
            dc.DrawEllipse(null, new Pen(accent, 1.5), center, radius, radius);
            var direction = new Vector(Math.Cos(guide.Angle), Math.Sin(guide.Angle));
            var side = new Vector(-direction.Y, direction.X);
            var tip = center + direction * radius;
            var triangle = new StreamGeometry();
            using (var context = triangle.Open())
            {
                context.BeginFigure(tip + direction * 7, true, true);
                context.LineTo(tip - direction * 9 + side * 7, true, false);
                context.LineTo(tip - direction * 9 - side * 7, true, false);
            }
            triangle.Freeze(); dc.DrawGeometry(accent, new Pen(Brushes.Black, 1), triangle);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var label = new FormattedText(guide.Name, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.White, dpi);
            var box = new Rect(center.X - label.Width / 2 - 8, center.Y + radius + 8, label.Width + 16, 24);
            dc.DrawRoundedRectangle(backdrop, null, box, 5, 5);
            dc.DrawText(label, new Point(box.X + 8, box.Y + 3));
            var north = new FormattedText("N", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, accent, dpi);
            dc.DrawText(north, new Point(center.X - north.Width / 2, center.Y - radius - 15));
        }
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowDisplayAffinity(nint handle, uint affinity);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint handle, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, StringBuilder title, int length);
}
