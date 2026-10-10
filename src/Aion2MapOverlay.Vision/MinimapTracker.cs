using OpenCvSharp;
using OpenCvSharp.Features2D;

namespace Aion2MapOverlay.Vision;

public sealed record MinimapFix(double PlayerX, double PlayerY, double Rotation, int Inliers, double MedianError);

// Native-resolution minimap terrain matching. Coordinates use the existing 2048px reference.
public sealed class MinimapTracker : IDisposable
{
    private readonly SIFT sift = SIFT.Create(30000, 3, .025);
    private readonly Mat descriptors = new();
    private readonly KeyPoint[] points;
    private readonly FlannBasedMatcher matcher = new();

    public MinimapTracker(string referencePath)
    {
        using var original = Cv2.ImRead(referencePath, ImreadModes.Grayscale);
        if (original.Empty()) throw new FileNotFoundException("Minimap reference missing", referencePath);
        using var reference = new Mat(); Cv2.Resize(original, reference, new(4096, 4096));
        sift.DetectAndCompute(reference, null, out points, descriptors);
        matcher.Add([descriptors]); matcher.Train();
    }

    public MinimapFix? Process(Mat gray)
    {
        using var current = new Mat();
        sift.DetectAndCompute(gray, null, out var features, current);
        if (current.Empty()) return null;
        var pairs = matcher.KnnMatch(current, 2)
            .Where(p => p.Length == 2 && p[0].Distance < .75 * p[1].Distance)
            .Select(p => p[0]).GroupBy(p => p.TrainIdx).Select(g => g.MinBy(p => p.Distance)).ToArray();
        if (pairs.Length < 10) return null;
        var from = pairs.Select(p => points[p.TrainIdx].Pt).ToArray();
        var to = pairs.Select(p => features[p.QueryIdx].Pt).ToArray();
        using var input = InputArray.Create(from); using var target = InputArray.Create(to); using var mask = new Mat();
        using var affine = Cv2.EstimateAffinePartial2D(input, target, mask, RobustEstimationAlgorithms.RANSAC, 2.5, 5000, .999, 10);
        if (affine is null || affine.Empty()) return null;
        var keep = Enumerable.Range(0, pairs.Length).Where(i => mask.At<byte>(i) != 0).ToArray();
        if (keep.Length < 10 || keep.Length < pairs.Length * .4) return null;
        var a = affine.At<double>(0, 0); var b = affine.At<double>(1, 0);
        var x = affine.At<double>(0, 2); var y = affine.At<double>(1, 2);
        var scale = Math.Sqrt(a * a + b * b);
        var rotation = Math.Atan2(b, a);
        // These presets are north-up. Reject rotating/misregistered terrain rather than guess.
        if (scale < .2 || scale > 12 || Math.Abs(rotation) > Math.PI / 36) return null;
        var errors = keep.Select(i => Math.Sqrt(Math.Pow(a * from[i].X - b * from[i].Y + x - to[i].X, 2) + Math.Pow(b * from[i].X + a * from[i].Y + y - to[i].Y, 2))).Order().ToArray();
        var spanX = keep.Max(i => to[i].X) - keep.Min(i => to[i].X);
        var spanY = keep.Max(i => to[i].Y) - keep.Min(i => to[i].Y);
        if (errors[errors.Length / 2] > 1.5 || spanX < gray.Width * .35 || spanY < gray.Height * .25) return null;
        var cx = gray.Width / 2.0; var cy = gray.Height / 2.0;
        var playerX = (a * (cx - x) + b * (cy - y)) / (a * a + b * b) / 2;
        var playerY = (-b * (cx - x) + a * (cy - y)) / (a * a + b * b) / 2;
        if (!double.IsFinite(playerX) || !double.IsFinite(playerY) || playerX < 0 || playerX > 2048 || playerY < 0 || playerY > 2048) return null;
        return new(playerX, playerY, rotation, keep.Length, errors[errors.Length / 2]);
    }

    public void Dispose() { sift.Dispose(); descriptors.Dispose(); matcher.Dispose(); }
}
