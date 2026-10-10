using Aion2MapOverlay.Core.Geometry;
using OpenCvSharp;
using OpenCvSharp.Features2D;

namespace Aion2MapOverlay.Vision;

public sealed record Alignment(MapTransform Transform, int Inliers, double MedianError, string Method);

public sealed class TerrainTracker : IDisposable
{
    public const int ReferenceSize = 2048;
    private readonly SIFT sift = SIFT.Create(6500);
    private readonly Mat referenceDescriptors = new();
    private readonly KeyPoint[] referencePoints;
    private readonly FlannBasedMatcher matcher = new();
    private Mat? previous;
    private Point2f[] tracked = [];
    private Alignment? last;
    private DateTime lastFull;
    private readonly Mat? title;

    public TerrainTracker(string referencePath, string titlePath)
    {
        using var reference = Cv2.ImRead(referencePath, ImreadModes.Grayscale);
        if (reference.Empty()) throw new FileNotFoundException("Reference map could not be loaded", referencePath);
        using var resized = new Mat();
        Cv2.Resize(reference, resized, new(ReferenceSize, ReferenceSize));
        sift.DetectAndCompute(resized, null, out referencePoints, referenceDescriptors);
        matcher.Add([referenceDescriptors]); matcher.Train();
        if (File.Exists(titlePath)) title = Cv2.ImRead(titlePath, ImreadModes.Grayscale);
    }

    public bool IsMapVisible(Mat gray)
    {
        if (title is null || title.Empty()) return false;
        using var region = new Mat(gray, new Rect(0, 0, Math.Min(gray.Width, 260), Math.Min(gray.Height, 75)));
        var best = -1.0;
        foreach (var factor in new[] { .8, .9, 1.0, 1.1, 1.2 })
        {
            using var template = new Mat();
            Cv2.Resize(title, template, new(), factor, factor);
            if (template.Width >= region.Width || template.Height >= region.Height) continue;
            using var response = new Mat();
            Cv2.MatchTemplate(region, template, response, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(response, out double _, out double max);
            best = Math.Max(best, max);
        }
        return best >= .67;
    }

    public Alignment? Process(Mat gray, bool requireMapTitle = true)
    {
        if (requireMapTitle && !IsMapVisible(gray)) { Reset(); return null; }
        Alignment? result = null;
        if (previous is not null && last is not null && tracked.Length >= 25 &&
            DateTime.UtcNow - lastFull < TimeSpan.FromSeconds(2))
        {
            Point2f[] next = [];
            Cv2.CalcOpticalFlowPyrLK(previous, gray, tracked, ref next, out byte[] status,
                out float[] errors, new Size(25, 25), 3);
            var valid = Enumerable.Range(0, next.Length).Where(i => status[i] != 0 && errors[i] < 35 &&
                next[i].X > gray.Width * .18 && next[i].X < gray.Width * .94 && next[i].Y > gray.Height * .12 && next[i].Y < gray.Height * .94).ToArray();
            var from = valid.Select(i => tracked[i]).ToArray();
            var to = valid.Select(i => next[i]).ToArray();
            var delta = Estimate(from, to, .7, 1.4, "tracking", out var keep);
            if (delta is not null && delta.Inliers >= Math.Max(25, tracked.Length * .45))
            {
                tracked = keep.Select(i => to[i]).ToArray();
                result = delta with { Transform = last.Transform.Then(delta.Transform) };
            }
        }
        if (result is null)
        {
            using var mask = Mat.Zeros(gray.Size(), MatType.CV_8UC1).ToMat();
            Cv2.Rectangle(mask, new Rect((int)(gray.Width * .18), (int)(gray.Height * .12), (int)(gray.Width * .76), (int)(gray.Height * .82)), Scalar.White, -1);
            using var descriptors = new Mat();
            sift.DetectAndCompute(gray, mask, out var points, descriptors);
            if (!descriptors.Empty())
            {
                var pairs = matcher.KnnMatch(descriptors, 2)
                    .Where(p => p.Length == 2 && p[0].Distance < .70 * p[1].Distance)
                    .Select(p => p[0]).GroupBy(p => p.TrainIdx).Select(g => g.MinBy(p => p.Distance)).ToArray();
                var from = pairs.Select(p => referencePoints[p.TrainIdx].Pt).ToArray();
                var to = pairs.Select(p => points[p.QueryIdx].Pt).ToArray();
                result = Estimate(from, to, .15, 12, "terrain match", out var keep);
                if (result is not null) tracked = keep.Select(i => to[i]).ToArray();
            }
            lastFull = DateTime.UtcNow;
        }
        previous?.Dispose(); previous = result is null ? null : gray.Clone(); last = result;
        return result;
    }

    private static Alignment? Estimate(Point2f[] from, Point2f[] to, double minScale, double maxScale, string method, out int[] keep)
    {
        keep = [];
        if (from.Length < 25) return null;
        using var input = InputArray.Create(from); using var output = InputArray.Create(to);
        using var inlierMask = new Mat();
        using var affine = Cv2.EstimateAffinePartial2D(input, output, inlierMask, RobustEstimationAlgorithms.RANSAC, 2.5, 2000, .995, 10);
        if (affine is null || affine.Empty()) return null;
        var a = affine.At<double>(0, 0); var b = affine.At<double>(1, 0);
        if (a < minScale || a > maxScale || Math.Abs(b) > a * .015) return null;
        var transform = new MapTransform(a, affine.At<double>(0, 2), affine.At<double>(1, 2));
        keep = Enumerable.Range(0, from.Length).Where(i => inlierMask.At<byte>(i) != 0).ToArray();
        if (keep.Length < 25 || keep.Length < from.Length * .25) return null;
        var errors = keep.Select(i => { var p = transform.Apply(from[i].X, from[i].Y); return Math.Sqrt(Math.Pow(p.X - to[i].X, 2) + Math.Pow(p.Y - to[i].Y, 2)); }).Order().ToArray();
        var median = errors[errors.Length / 2];
        var spanX = keep.Max(i => to[i].X) - keep.Min(i => to[i].X);
        var spanY = keep.Max(i => to[i].Y) - keep.Min(i => to[i].Y);
        if (median > 2 || spanX < 120 || spanY < 100) return null;
        return new(transform, keep.Length, median, method);
    }

    public void Reset() { previous?.Dispose(); previous = null; last = null; tracked = []; }
    public void Dispose() { Reset(); title?.Dispose(); matcher.Dispose(); referenceDescriptors.Dispose(); sift.Dispose(); }
}
