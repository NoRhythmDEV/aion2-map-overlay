using OpenCvSharp;

namespace Aion2MapOverlay.Vision;

public sealed record CameraHeadingFix(double Angle, double Confidence);

// Read the two thin radial edges of the minimap's camera view cone.
// Angles use screen axes: east = 0, south = pi/2. No game state is accessed.
public static class CameraHeading
{
    public static CameraHeadingFix? Detect(Mat pixels, double? centerX = null, double? centerY = null)
    {
        if (pixels.Empty() || pixels.Width < 32 || pixels.Height < 32) return null;
        var profile = Profile(pixels, centerX, centerY);
        return Select(profile);
    }

    public static double[] Profile(Mat pixels, double? centerX = null, double? centerY = null)
    {
        if (pixels.Type() != MatType.CV_8UC3 && pixels.Type() != MatType.CV_8UC1) throw new ArgumentException("Expected grayscale or BGR pixels");
        var grayscale = pixels.Channels() == 1;
        var cx = centerX ?? pixels.Width / 2.0; var cy = centerY ?? pixels.Height / 2.0;
        var scores = new double[360];
        var minRadius = pixels.Width * .16; var maxRadius = pixels.Width * .40;
        double Light(double x, double y)
        {
            var ix = (int)Math.Round(x); var iy = (int)Math.Round(y);
            if (ix < 0 || iy < 0 || ix >= pixels.Width || iy >= pixels.Height) return 0;
            if (grayscale) return pixels.At<byte>(iy, ix);
            var p = pixels.At<Vec3b>(iy, ix);
            return .114 * p.Item0 + .587 * p.Item1 + .299 * p.Item2;
        }
        for (var angle = 0; angle < 360; angle++)
        {
            var radians = angle * Math.PI / 180;
            var dx = Math.Cos(radians); var dy = Math.Sin(radians);
            var values = new List<double>();
            for (var r = minRadius; r <= maxRadius; r += 2)
            {
                var x = cx + dx * r; var y = cy + dy * r;
                var middle = Light(x, y);
                var side = (Light(x - dy * 3, y + dx * 3) + Light(x + dy * 3, y - dx * 3)) / 2;
                values.Add(Math.Clamp(middle - side, 0, 50));
            }
            values.Sort();
            // Require a line along most of the ray rather than a single bright icon.
            scores[angle] = values.Take((int)(values.Count * .8)).Average();
        }
        return scores;
    }

    public static CameraHeadingFix? Select(double[] profile)
    {
        if (profile.Length != 360 || profile.Any(v => !double.IsFinite(v))) throw new ArgumentException("Expected 360 finite radial scores");
        // The visible camera cone is about 114 degrees wide. Allow modest UI/camera variation.
        (double Score, int Start, int Opening) best = default;
        for (var angle = 0; angle < 360; angle++)
            for (var opening = 100; opening <= 125; opening++)
            {
                var a = profile[angle]; var b = profile[(angle + opening) % 360];
                if (Math.Min(a, b) < 8) continue;
                var score = Math.Min(a, b) + .2 * Math.Max(a, b);
                if (score > best.Score) best = (score, angle, opening);
            }
        if (best.Score == 0) return null;
        var heading = (best.Start + best.Opening / 2.0) * Math.PI / 180;
        var alternative = 0.0;
        for (var angle = 0; angle < 360; angle++)
            for (var opening = 100; opening <= 125; opening++)
            {
                var difference = (angle + opening / 2.0) * Math.PI / 180 - heading;
                if (Math.Abs(Math.Atan2(Math.Sin(difference), Math.Cos(difference))) <= Math.PI / 6) continue;
                var a = profile[angle]; var b = profile[(angle + opening) % 360];
                alternative = Math.Max(alternative, Math.Min(a, b) + .2 * Math.Max(a, b));
            }
        var confidence = best.Score / Math.Max(.1, alternative);
        return confidence >= 1.5 ? new(Math.Atan2(Math.Sin(heading), Math.Cos(heading)), confidence) : null;
    }
}
