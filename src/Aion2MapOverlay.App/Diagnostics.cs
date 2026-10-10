using System.IO;
using System.Text.Json;
using Aion2MapOverlay.Data;
using Aion2MapOverlay.Vision;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal static class Diagnostics
{
    public static void Run(string[] screenshots, string output, bool prepareTemplate = true)
    {
        Directory.CreateDirectory(output);
        using var first = Cv2.ImRead(screenshots[0]);
        using var resized = new Mat();
        Cv2.Resize(first, resized, new Size(1600, (int)Math.Round(first.Height * 1600.0 / first.Width)));
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.Title)!);
        if (prepareTemplate)
            using (var title = new Mat(resized, new Rect(10, 13, 205, 32))) Cv2.ImWrite(Paths.Title, title);
        using var tracker = new TerrainTracker(Paths.Reference, Paths.Title);
        var index = QuestlogData.LoadVerteron(Paths.Markers, TerrainTracker.ReferenceSize);
        var results = new List<object>();
        for (var i = 0; i < screenshots.Length; i++)
        {
            using var color = Cv2.ImRead(screenshots[i]);
            using var small = new Mat(); using var gray = new Mat();
            Cv2.Resize(color, small, new Size(1600, (int)Math.Round(color.Height * 1600.0 / color.Width)));
            Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);
            tracker.Reset(); var started = System.Diagnostics.Stopwatch.StartNew();
            var aligned = tracker.Process(gray);
            var elapsed = started.Elapsed.TotalMilliseconds;
            var drawn = 0;
            if (aligned is not null)
            {
                foreach (var marker in index.Markers.Where(m => m.Name == "Sapphire Gemstone"))
                {
                    var p = aligned.Transform.Apply(marker.ReferenceX, marker.ReferenceY);
                    if (p.X < 1600 * .18 || p.X > 1600 * .95 || p.Y < gray.Height * .12 || p.Y > gray.Height * .95) continue;
                    Cv2.Circle(small, new Point((int)p.X, (int)p.Y), 6, new Scalar(255, 190, 30), 2, LineTypes.AntiAlias);
                    drawn++;
                }
            }
            Cv2.ImWrite(Path.Combine(output, $"screenshot-{i + 1}.png"), small);
            results.Add(new { screenshot = Path.GetFileName(screenshots[i]), titleDetected = tracker.IsMapVisible(gray), alignment = aligned, milliseconds = elapsed, sapphireMarkersDrawn = drawn });
        }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
