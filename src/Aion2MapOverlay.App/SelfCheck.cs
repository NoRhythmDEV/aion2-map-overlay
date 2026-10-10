using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aion2MapOverlay.Capture;
using Aion2MapOverlay.Core.Geometry;
using Aion2MapOverlay.Data;
using Aion2MapOverlay.Vision;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal static class SelfCheck
{
    public static void Run(string screenshot, string output)
    {
        Directory.CreateDirectory(output);
        Cv2.SetNumThreads(4);
        var index = QuestlogData.LoadVerteron(Paths.Markers, TerrainTracker.ReferenceSize);
        if (index.Markers.Count != 4733 || index.Markers.Count(m => m.Name == "Sapphire Gemstone") != 192)
            throw new InvalidDataException("Unexpected Verteron marker coverage");
        var random = new Random(42);
        var motion = new MarkerMotion();
        var firstMarker = new PlacedMarker(100, 100, "test", "node");
        var movedMarker = firstMarker with { X = 120 };
        var stamp = Stopwatch.GetTimestamp();
        motion.SetFrame(new([firstMarker], 1600, 900, DateTime.UtcNow), stamp);
        motion.SetFrame(new([movedMarker], 1600, 900, DateTime.UtcNow), stamp);
        var halfway = motion.Position(movedMarker, stamp + (long)(Stopwatch.Frequency * .012));
        if (Math.Abs(halfway.X - 110) > .01) throw new Exception("Marker interpolation midpoint failed");
        motion.Finish();
        if (motion.Position(movedMarker, stamp).X != 120) throw new Exception("Screenshot mode must finish motion");
        var jumpedMarker = movedMarker with { X = 500 };
        motion.SetFrame(new([jumpedMarker], 1600, 900, DateTime.UtcNow), stamp);
        if (motion.Position(jumpedMarker, stamp).X != 500) throw new Exception("Large movements must snap");
        motion.SetFrame(null, stamp);
        motion.SetFrame(new([firstMarker], 1600, 900, DateTime.UtcNow), stamp);
        if (motion.Position(firstMarker, stamp).X != 100) throw new Exception("Lost alignment must reset smoothing");
        for (var i = 0; i < 100; i++)
        {
            var left = random.NextDouble() * 2048; var top = random.NextDouble() * 2048;
            var right = left + random.NextDouble() * 500; var bottom = top + random.NextDouble() * 500;
            var expected = index.Markers.Where(m => m.ReferenceX >= left && m.ReferenceX <= right && m.ReferenceY >= top && m.ReferenceY <= bottom).Select(m => m.Id).ToHashSet();
            if (!expected.SetEquals(index.Query(left, top, right, bottom).Select(m => m.Id))) throw new Exception("Spatial query mismatch");
        }
        var transform = new MapTransform(2.3, -500, 200);
        var projected = transform.Apply(400, 700); var reversed = transform.Inverse(projected.X, projected.Y);
        if (Math.Abs(reversed.X - 400) > 1e-9 || Math.Abs(reversed.Y - 700) > 1e-9) throw new Exception("Coordinate round trip failed");
        using var image = Cv2.ImRead(screenshot, ImreadModes.Grayscale);
        using var gray = new Mat(); Cv2.Resize(image, gray, new OpenCvSharp.Size(1600, (int)Math.Round(image.Height * 1600.0 / image.Width)));
        using var tracker = new TerrainTracker(Paths.Reference, Paths.Title);
        var initial = tracker.Process(gray) ?? throw new Exception("Initial registration failed");
        using var warp = Mat.Eye(2, 3, MatType.CV_64FC1).ToMat(); warp.Set(0, 2, 25.0); warp.Set(1, 2, -12.0);
        using var shifted = new Mat(); Cv2.WarpAffine(gray, shifted, warp, gray.Size());
        var timer = Stopwatch.StartNew();
        var tracked = tracker.Process(shifted, false) ?? throw new Exception("Pan tracking failed");
        var trackingMs = timer.Elapsed.TotalMilliseconds;
        var delta = Math.Sqrt(Math.Pow(tracked.Transform.X - initial.Transform.X - 25, 2) + Math.Pow(tracked.Transform.Y - initial.Transform.Y + 12, 2));
        if (tracked.Method != "tracking" || delta > 2) throw new Exception($"Pan tracking exceeded 2px: {delta}");
        warp.Set(0, 0, 1.08); warp.Set(1, 1, 1.08);
        using var zoomed = new Mat(); Cv2.WarpAffine(gray, zoomed, warp, gray.Size());
        var zoom = tracker.Process(zoomed, false) ?? throw new Exception("Zoom tracking failed");
        if (Math.Abs(zoom.Transform.Scale / initial.Transform.Scale - 1.08) > .01) throw new Exception("Zoom scale mismatch");
        using var blank = Mat.Zeros(gray.Size(), MatType.CV_8UC1).ToMat();
        if (tracker.Process(blank) is not null) throw new Exception("Markers must hide on a closed map");

        var displays = DesktopCapture.Displays();
        using var capture = new DesktopCapture(displays[0]);
        timer.Restart(); using var captured = capture.GrabGray(); var captureMs = timer.Elapsed.TotalMilliseconds;
        if (captured.Empty() || Cv2.Mean(captured).Val0 < 1) throw new Exception("Desktop capture returned an empty frame");
        var icons = IconCache.Load();
        if (icons.Count != 15) throw new Exception("Expected all 15 category icons");
        var window = new MainWindow(); window.PrepareForCheck();
        var content = (FrameworkElement)window.Content; window.Content = null;
        var border = new System.Windows.Controls.Border { Background = window.Background, Child = content, Resources = window.Resources };
        System.Windows.Documents.TextElement.SetForeground(border, window.Foreground);
        System.Windows.Documents.TextElement.SetFontFamily(border, window.FontFamily);
        System.Windows.Documents.TextElement.SetFontSize(border, window.FontSize);
        border.Measure(new System.Windows.Size(760, 900)); border.Arrange(new System.Windows.Rect(0, 0, 760, 900)); border.UpdateLayout();
        var bitmap = new RenderTargetBitmap(760, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(border);
        var tiles = Descendants<System.Windows.Controls.Primitives.ToggleButton>(border).Where(t => t.DataContext is MarkerGroup).ToArray();
        if (tiles.Length != 15) throw new Exception("Resource grid must contain all 15 toggle tiles");
        var tile = tiles[0];
        var groupModel = (MarkerGroup)tile.DataContext;
        var wasEnabled = groupModel.Enabled;
        var peer = new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(tile);
        ((System.Windows.Automation.Provider.IToggleProvider)peer).Toggle();
        if (groupModel.Enabled == wasEnabled) throw new Exception("Resource tile did not toggle its category");
        ((System.Windows.Automation.Provider.IToggleProvider)peer).Toggle();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, "settings-window.png"))) encoder.Save(stream);
        border.Measure(new System.Windows.Size(1000, 900)); border.Arrange(new System.Windows.Rect(0, 0, 1000, 900)); border.UpdateLayout();
        if (Descendants<System.Windows.Controls.Primitives.UniformGrid>(border).Single().Columns != 4)
            throw new Exception("Wide resource grid must switch to four columns");
        groupModel.Enabled = true;
        border.UpdateLayout();
        var wideBitmap = new RenderTargetBitmap(1000, 900, 96, 96, PixelFormats.Pbgra32); wideBitmap.Render(border);
        var wideEncoder = new PngBitmapEncoder(); wideEncoder.Frames.Add(BitmapFrame.Create(wideBitmap));
        using (var stream = File.Create(Path.Combine(output, "settings-window-wide.png"))) wideEncoder.Save(stream);
        groupModel.Enabled = wasEnabled;
        window.Close();
        var overlay = new OverlayWindow(displays[0], icons); overlay.Show(); overlay.EnsureCaptureExcluded();
        if (!overlay.CaptureExcluded) throw new Exception("Capture exclusion unavailable: " + overlay.CaptureExclusionError);
        if (overlay.BeginScreenshotMode()) throw new Exception("Screenshot mode must require a current map frame");
        overlay.Update(new OverlayFrame([new PlacedMarker(800, 450, "gatherable|Sapphire Gemstone")], 1600, 900, DateTime.UtcNow), 20, .9);
        if (!overlay.BeginScreenshotMode() || !overlay.ScreenshotMode || overlay.CaptureExcluded)
            throw new Exception("Screenshot mode did not enable capture inclusion");
        overlay.Update(null, 20, .9);
        if (!overlay.ScreenshotMode) throw new Exception("Screenshot frame was not frozen");
        if (!overlay.EndScreenshotMode() || overlay.ScreenshotMode || !overlay.CaptureExcluded)
            throw new Exception("Screenshot mode did not restore capture exclusion");
        if (overlay.BeginScreenshotMode()) throw new Exception("Resume must discard the frozen frame");
        overlay.Close();
        File.WriteAllText(Path.Combine(output, "self-check.json"), JsonSerializer.Serialize(new {
            spatialQueries = 100, markerCount = index.Markers.Count, toggleGroups = 15,
            panTrackingMilliseconds = trackingMs, panTranslationError = delta, zoomScale = zoom.Transform.Scale / initial.Transform.Scale,
            captureMilliseconds = captureMs, captureWidth = captured.Width, captureHeight = captured.Height,
            mapCloseHidesMarkers = true, iconCount = icons.Count, captureExclusionAccepted = true,
            screenshotModeTransitionsPassed = true, markerInterpolationPassed = true, resourceTileTogglePassed = true
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
