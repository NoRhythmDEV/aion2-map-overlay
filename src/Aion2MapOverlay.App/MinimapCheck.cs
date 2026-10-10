using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Aion2MapOverlay.Capture;
using Aion2MapOverlay.Data;
using Aion2MapOverlay.Vision;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

// Offline check against user-supplied images, using the same matcher as live guidance.
internal static class MinimapCheck
{
    public static void Run(string[] screenshots, string output)
    {
        Directory.CreateDirectory(output);
        Cv2.SetNumThreads(4);
        using var tracker = new MinimapTracker(Paths.Reference);
        var index = QuestlogData.LoadVerteron(Paths.Markers, TerrainTracker.ReferenceSize);
        var enabled = index.Markers.Where(m => m.Name == "Sapphire Gemstone").Select(m => m.GroupKey).ToHashSet();
        var results = new List<object>();
        foreach (var path in screenshots)
        {
            using var image = Cv2.ImRead(path);
            if (image.Empty()) throw new InvalidDataException("Screenshot could not be read: " + path);
            var display = new DisplayArea(0, 0, image.Width, image.Height);
            foreach (var large in new[] { false, true })
            {
                var area = Navigation.MinimapArea(display, large);
                using var crop = new Mat(image, new Rect(area.X, area.Y, area.Width, area.Height));
                using var gray = new Mat(); Cv2.CvtColor(crop, gray, ColorConversionCodes.BGR2GRAY);
                var timer = Stopwatch.StartNew();
                var fix = tracker.Process(gray);
                var milliseconds = timer.Elapsed.TotalMilliseconds;
                var target = fix is null ? null : Navigation.Nearest(index.Markers, enabled, fix);
                using var preview = crop.Clone();
                var center = new Point(area.Width / 2, area.Height / 2);
                Cv2.DrawMarker(preview, center, Scalar.Cyan, MarkerTypes.Cross, 16, 1);
                if (fix is not null && target is not null)
                {
                    var guide = Navigation.Guide(target, fix, display, area, large, DateTime.UtcNow);
                    var radius = Math.Min(area.Width, area.Height) * .43;
                    Cv2.Circle(preview, center, (int)radius, Scalar.Cyan, 2);
                    var tip = new Point((int)(center.X + Math.Cos(guide.Angle) * radius), (int)(center.Y + Math.Sin(guide.Angle) * radius));
                    Cv2.ArrowedLine(preview, center, tip, Scalar.Cyan, 2, tipLength: .2);
                }
                Cv2.ImWrite(Path.Combine(output, Path.GetFileNameWithoutExtension(path) + (large ? "-large.png" : "-default.png")), preview);
                results.Add(new { screenshot = Path.GetFileName(path), large, aligned = fix is not null,
                    fix, milliseconds, target = target?.Name, targetId = target?.Id });
            }
        }
        using var blank = Mat.Zeros(400, 300, MatType.CV_8UC1).ToMat();
        if (tracker.Process(blank) is not null) throw new Exception("Blank minimap must not produce guidance");
        File.WriteAllText(Path.Combine(output, "minimap-check.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
