using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal static class IconCache
{
    public static Dictionary<string, ImageSource> Load()
    {
        var result = new Dictionary<string, ImageSource>();
        var manifest = Path.Combine(Paths.Icons, "manifest.json");
        if (File.Exists(manifest))
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifest))!)
            {
                using var pixels = Cv2.ImRead(Path.Combine(Paths.Icons, pair.Value), ImreadModes.Unchanged);
                if (pixels.Empty()) continue;
                Cv2.ImEncode(".png", pixels, out var png);
                using var stream = new MemoryStream(png);
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream; image.EndInit(); image.Freeze(); result[pair.Key] = image;
            }
        var geometry = Geometry.Parse("M 12,1 L 23,7 L 23,20 L 12,26 L 1,20 L 1,7 Z M 1,7 L 12,13 L 23,7 M 12,13 L 12,26");
        var drawing = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(157, 107, 229)), new Pen(Brushes.White, 1.5), geometry);
        var cube = new DrawingImage(drawing); cube.Freeze(); result["collectable|Hidden Cube"] = cube;
        return result;
    }
}
