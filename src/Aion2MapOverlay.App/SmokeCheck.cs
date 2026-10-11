using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal static class SmokeCheck
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        using var image = Mat.Zeros(64, 64, MatType.CV_8UC1).ToMat();
        Cv2.Circle(image, new(32, 32), 10, Scalar.White, -1);
        if (Cv2.CountNonZero(image) < 100) throw new Exception("Native OpenCV runtime unavailable.");
        foreach (var path in new[] { "../outside.dll", "data/map.json", "settings.local.json", "captures/image.png", "C:/app.dll", "/root.dll", "sub/../../app.dll", ".git/config" })
        {
            var rejected = false;
            try { AppUpdates.SafePath(output, path); } catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception("Unsafe update path accepted: " + path);
        }
        var testRoot = Path.Combine(output, "package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var entries = new Dictionary<string, byte[]> { ["aion2-map-overlay.exe"] = [1, 2, 3], ["ApplyUpdate.ps1"] = [4, 5, 6] };
        var files = entries.Select(p => new UpdateFile(p.Key, p.Value.Length, Convert.ToHexString(SHA256.HashData(p.Value)))).ToArray();
        var manifest = new UpdateManifest("0.1.0", files);
        var zipPath = Path.Combine(testRoot, "valid.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var item in entries) { using var stream = zip.CreateEntry(item.Key).Open(); stream.Write(item.Value); }
            using var writer = new StreamWriter(zip.CreateEntry("update-manifest.json").Open()); writer.Write(JsonSerializer.Serialize(manifest));
        }
        AppUpdates.ExtractVerified(zipPath, Path.Combine(testRoot, "verified"), new Version(0, 1, 0));
        var badZipPath = Path.Combine(testRoot, "tampered.zip"); File.Copy(zipPath, badZipPath);
        using (var zip = ZipFile.Open(badZipPath, ZipArchiveMode.Update))
        {
            zip.GetEntry("aion2-map-overlay.exe")!.Delete();
            using var stream = zip.CreateEntry("aion2-map-overlay.exe").Open(); stream.Write([9, 9, 9]);
        }
        var tamperRejected = false;
        try { AppUpdates.ExtractVerified(badZipPath, Path.Combine(testRoot, "tampered"), new Version(0, 1, 0)); }
        catch (InvalidDataException) { tamperRejected = true; }
        if (!tamperRejected) throw new Exception("Tampered update accepted.");
        var originalDataRoot = Paths.DataRoot;
        Paths.DataRoot = Path.Combine(testRoot, "no-data");
        try
        {
            var window = new MainWindow(); window.PrepareForCheck();
            var setup = (System.Windows.FrameworkElement)window.FindName("SetupPanel");
            var start = (System.Windows.Controls.Button)window.FindName("StartButton");
            if (setup.Visibility != System.Windows.Visibility.Visible || start.IsEnabled) throw new Exception("First-run setup must appear before tracking is enabled.");
            var content = (System.Windows.FrameworkElement)window.Content; window.Content = null;
            var border = new System.Windows.Controls.Border { Background = window.Background, Child = content, Resources = window.Resources };
            System.Windows.Documents.TextElement.SetForeground(border, window.Foreground);
            System.Windows.Documents.TextElement.SetFontFamily(border, window.FontFamily);
            System.Windows.Documents.TextElement.SetFontSize(border, window.FontSize);
            border.Measure(new System.Windows.Size(760, 1020)); border.Arrange(new System.Windows.Rect(0, 0, 760, 1020)); border.UpdateLayout();
            var preview = new System.Windows.Media.Imaging.RenderTargetBitmap(760, 1020, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); preview.Render(border);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));
            using (var stream = File.Create(Path.Combine(output, "first-run-ui.png"))) encoder.Save(stream);
            window.Close();
        }
        finally { Paths.DataRoot = originalDataRoot; }
        File.WriteAllText(Path.Combine(output, "smoke-test.json"), JsonSerializer.Serialize(new {
            version = AppUpdates.CurrentVersion.ToString(), nativeOpenCvPassed = true,
            unsafePathsRejected = 8, validPackageAccepted = true, tamperedPackageRejected = true, firstRunSetupPassed = true
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
