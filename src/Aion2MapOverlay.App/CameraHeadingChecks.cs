using Aion2MapOverlay.Vision;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal static class CameraHeadingChecks
{
    public static void Run()
    {
        foreach (var height in new[] { 198, 486 })
            foreach (var degrees in new[] { 0, 47, 90, 180, 270, 359 })
            {
                using var map = new Mat(height, 328, MatType.CV_8UC1, Scalar.All(75));
                var center = new Point(164, height / 2);
                foreach (var edge in new[] { degrees - 57, degrees + 57 })
                {
                    var radians = edge * Math.PI / 180;
                    Cv2.Line(map, center, new Point(center.X + (int)(150 * Math.Cos(radians)), center.Y + (int)(150 * Math.Sin(radians))), Scalar.All(225), 1);
                }
                // Icons cover the center and isolated bright points lie elsewhere on the map.
                Cv2.Circle(map, center, 35, Scalar.White, -1);
                Cv2.Circle(map, new Point(250, 55), 8, Scalar.White, -1);
                var detected = CameraHeading.Detect(map) ?? throw new Exception($"Camera cone missed: {degrees} degrees, height {height}");
                var error = detected.Angle - degrees * Math.PI / 180;
                if (Math.Abs(Math.Atan2(Math.Sin(error), Math.Cos(error))) > Math.PI / 60)
                    throw new Exception("Camera heading error exceeds 3 degrees");
            }
        using var blank = Mat.Zeros(486, 328, MatType.CV_8UC1).ToMat();
        if (CameraHeading.Detect(blank) is not null) throw new Exception("Blank minimap must not report heading");
        Cv2.Line(blank, new(164, 243), new(314, 243), Scalar.White, 1);
        if (CameraHeading.Detect(blank) is not null) throw new Exception("One visible cone edge must not guess a heading");
        var ambiguous = new double[360]; ambiguous[0] = ambiguous[114] = ambiguous[180] = ambiguous[294] = 30;
        if (CameraHeading.Select(ambiguous) is not null) throw new Exception("Ambiguous camera cones must hide guidance");
    }
}
