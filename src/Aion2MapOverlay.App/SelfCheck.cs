using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aion2MapOverlay.Capture;
using Aion2MapOverlay.Core.Geometry;
using Aion2MapOverlay.Core.Markers;
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
        var nodeA = new MapMarker("a", "Sapphire Gemstone", "gatherable", "test", 110, 100, null);
        var nodeB = nodeA with { Id = "b", ReferenceX = 110.5 };
        var collectible = nodeA with { Id = "c", Type = "collectable", ReferenceX = 100 };
        var fix = new MinimapFix(100, 100, 0, 20, .2);
        var selectedNodes = new HashSet<string> { nodeA.GroupKey, collectible.GroupKey };
        if (Navigation.Nearest([nodeA, nodeB, collectible], selectedNodes, fix)?.Id != "a") throw new Exception("Navigation must choose the nearest selected gatherable");
        if (Navigation.Nearest([nodeA, nodeB], selectedNodes, fix, nodeB)?.Id != "b") throw new Exception("Nearly tied navigation targets must remain stable");
        if (Navigation.Nearest([nodeA], [], fix) is not null) throw new Exception("Disabled resources must not receive guidance");
        var otherDisplay = new DisplayArea(-2560, 40, 2560, 1440);
        var minimapArea = Navigation.MinimapArea(otherDisplay, true);
        var guide = Navigation.Guide(nodeA, fix, otherDisplay, minimapArea, true, DateTime.UtcNow, 0);
        if (Math.Abs(guide.Angle + Math.PI / 2) > .0001 || guide.CenterX != .5 || guide.CenterY != .60)
            throw new Exception("Navigation direction or multi-monitor coordinates incorrect");
        var minimapFrame = Navigation.ProjectMinimap(new MarkerIndex([nodeA, nodeB, collectible]), selectedNodes, fix with { Scale = 4 }, otherDisplay, minimapArea, true, DateTime.UtcNow);
        var minimapProjected = minimapFrame.Markers.Single(m => m.Id == "a");
        var expectedX = (minimapArea.X - otherDisplay.X + minimapArea.Width / 2.0 + 40) / otherDisplay.Width;
        if (Math.Abs(minimapProjected.X - expectedX) > .000001 || minimapFrame.Markers.Length != 3)
            throw new Exception("Minimap marker projection or selection incorrect");
        foreach (var heading in new[] { -Math.PI, -Math.PI / 2, 0, Math.PI / 2, Math.PI - .01 })
        {
            var aheadNode = nodeA with { ReferenceX = fix.PlayerX + 10 * Math.Cos(heading), ReferenceY = fix.PlayerY + 10 * Math.Sin(heading) };
            var aheadGuide = Navigation.Guide(aheadNode, fix, otherDisplay, minimapArea, true, DateTime.UtcNow, heading);
            if (Math.Abs(aheadGuide.Angle + Math.PI / 2) > .000001) throw new Exception("A node ahead of the camera must point up");
            var rightNode = nodeA with { ReferenceX = fix.PlayerX + 10 * Math.Cos(heading + Math.PI / 2), ReferenceY = fix.PlayerY + 10 * Math.Sin(heading + Math.PI / 2) };
            if (Math.Abs(Navigation.Guide(rightNode, fix, otherDisplay, minimapArea, true, DateTime.UtcNow, heading).Angle) > .000001)
                throw new Exception("A node to the camera's right must point right");
        }
        CameraHeadingChecks.Run();
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
        capture.Reset();
        using var recaptured = capture.GrabGray();
        if (recaptured.Empty() || recaptured.Size() != captured.Size()) throw new Exception("Capture reset must recreate a valid frame");
        if (captured.Empty() || Cv2.Mean(captured).Val0 < 1) throw new Exception("Desktop capture returned an empty frame");
        var icons = IconCache.Load();
        if (icons.Count != 15) throw new Exception("Expected all 15 category icons");
        var window = new MainWindow(); window.PrepareForCheck();
        var guideDialog = HudGuide.Create(window);
        var guideContent = (FrameworkElement)guideDialog.Content; guideDialog.Content = null;
        var guideBorder = new System.Windows.Controls.Border { Background = guideDialog.Background, Child = guideContent, Resources = guideDialog.Resources };
        System.Windows.Documents.TextElement.SetForeground(guideBorder, guideDialog.Foreground);
        System.Windows.Documents.TextElement.SetFontFamily(guideBorder, guideDialog.FontFamily);
        System.Windows.Documents.TextElement.SetFontSize(guideBorder, guideDialog.FontSize);
        guideBorder.Measure(new System.Windows.Size(500, double.PositiveInfinity));
        var guideHeight = (int)Math.Ceiling(guideBorder.DesiredSize.Height);
        guideBorder.Arrange(new System.Windows.Rect(0, 0, 500, guideHeight)); guideBorder.UpdateLayout();
        var guideBitmap = new RenderTargetBitmap(500, guideHeight, 96, 96, PixelFormats.Pbgra32); guideBitmap.Render(guideBorder);
        var guideEncoder = new PngBitmapEncoder(); guideEncoder.Frames.Add(BitmapFrame.Create(guideBitmap));
        using (var stream = File.Create(Path.Combine(output, "hud-guide.png"))) guideEncoder.Save(stream);
        guideDialog.Close();
        var content = (FrameworkElement)window.Content; window.Content = null;
        var border = new System.Windows.Controls.Border { Background = window.Background, Child = content, Resources = window.Resources };
        System.Windows.Documents.TextElement.SetForeground(border, window.Foreground);
        System.Windows.Documents.TextElement.SetFontFamily(border, window.FontFamily);
        System.Windows.Documents.TextElement.SetFontSize(border, window.FontSize);
        border.Measure(new System.Windows.Size(760, 1020)); border.Arrange(new System.Windows.Rect(0, 0, 760, 1020)); border.UpdateLayout();
        var bitmap = new RenderTargetBitmap(760, 1020, 96, 96, PixelFormats.Pbgra32); bitmap.Render(border);
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
        border.Measure(new System.Windows.Size(1000, 1020)); border.Arrange(new System.Windows.Rect(0, 0, 1000, 1020)); border.UpdateLayout();
        if (Descendants<System.Windows.Controls.Primitives.UniformGrid>(border).Single().Columns != 4)
            throw new Exception("Wide resource grid must switch to four columns");
        groupModel.Enabled = true;
        border.UpdateLayout();
        var wideBitmap = new RenderTargetBitmap(1000, 1020, 96, 96, PixelFormats.Pbgra32); wideBitmap.Render(border);
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
        overlay.Update(null, 20, .9, new GuidanceFrame(.5, .6, .045, -Math.PI / 2, "Sapphire", nodeA.GroupKey, true, DateTime.UtcNow));
        if (!overlay.BeginScreenshotMode()) throw new Exception("Gameplay guidance must enter screenshot mode");
        var surface = (FrameworkElement)overlay.Content;
        surface.Measure(new System.Windows.Size(1000, 600)); surface.Arrange(new System.Windows.Rect(0, 0, 1000, 600));
        var guidanceBitmap = new RenderTargetBitmap(1000, 600, 96, 96, PixelFormats.Pbgra32); guidanceBitmap.Render(surface);
        var tipPixel = new byte[4]; guidanceBitmap.CopyPixels(new Int32Rect(500, 310, 1, 1), tipPixel, 4, 0);
        if (tipPixel[0] < 150 || tipPixel[1] < 80 || tipPixel[2] > 100 || tipPixel[3] == 0)
            throw new Exception("Frozen gameplay screenshot must include the up-facing ring pointer");
        var guidanceEncoder = new PngBitmapEncoder(); guidanceEncoder.Frames.Add(BitmapFrame.Create(guidanceBitmap));
        using (var stream = File.Create(Path.Combine(output, "guidance-overlay.png"))) guidanceEncoder.Save(stream);
        if (!overlay.EndScreenshotMode() || overlay.BeginScreenshotMode())
            throw new Exception("Gameplay guidance screenshot mode must discard its frame on resume");
        overlay.Update(null, 20, .9, null, minimapFrame with { CapturedAt = DateTime.UtcNow });
        if (!overlay.BeginScreenshotMode() || !overlay.EndScreenshotMode()) throw new Exception("Minimap-only screenshot mode failed");
        overlay.Update(null, 20, .9, new GuidanceFrame(.5, .6, .045, 0, "Sapphire", nodeA.GroupKey, true, DateTime.UtcNow.AddSeconds(-2)));
        if (overlay.BeginScreenshotMode()) throw new Exception("Stale gameplay frames must not enter screenshot mode");
        overlay.Close();
        File.WriteAllText(Path.Combine(output, "self-check.json"), JsonSerializer.Serialize(new {
            spatialQueries = 100, markerCount = index.Markers.Count, toggleGroups = 15,
            panTrackingMilliseconds = trackingMs, panTranslationError = delta, zoomScale = zoom.Transform.Scale / initial.Transform.Scale,
            captureMilliseconds = captureMs, captureWidth = captured.Width, captureHeight = captured.Height,
            mapCloseHidesMarkers = true, iconCount = icons.Count, captureExclusionAccepted = true,
            screenshotModeTransitionsPassed = true, markerInterpolationPassed = true, resourceTileTogglePassed = true,
            navigationSelectionAndGeometryPassed = true, cameraRelativeHeadingPassed = true, gameplayScreenshotPassed = true
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
