using System.Configuration;
using System.Data;
using System.Windows;

namespace Aion2MapOverlay.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 5 && e.Args[0] == "--check-heading")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                using var image = OpenCvSharp.Cv2.ImRead(e.Args[2]);
                using var gray = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.CvtColor(image, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
                var cx = double.Parse(e.Args[3], System.Globalization.CultureInfo.InvariantCulture);
                var cy = double.Parse(e.Args[4], System.Globalization.CultureInfo.InvariantCulture);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var heading = Aion2MapOverlay.Vision.CameraHeading.Detect(gray, cx, cy);
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "heading.json"), System.Text.Json.JsonSerializer.Serialize(new {
                    heading, degrees = heading?.Angle * 180 / Math.PI, milliseconds = timer.Elapsed.TotalMilliseconds
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                if (heading is null) throw new InvalidOperationException("No unambiguous camera cone found");
            }
            catch (Exception error) { System.IO.Directory.CreateDirectory(e.Args[1]); System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString()); Shutdown(1); return; }
            Shutdown(); return;
        }
        if (e.Args.Length == 4 && e.Args[0] == "--verify-update-package")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { AppUpdates.ExtractVerified(e.Args[2], System.IO.Path.Combine(e.Args[1], "payload"), Version.Parse(e.Args[3])); }
            catch (Exception error)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1); return;
            }
            Shutdown(); return;
        }
        if (e.Args.Length == 2 && e.Args[0] is "--smoke-test" or "--download-data")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (e.Args[0] == "--smoke-test") SmokeCheck.Run(e.Args[1]);
                else Task.Run(() => DataSetup.DownloadAsync(new Progress<string>(), CancellationToken.None, e.Args[1])).GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1); return;
            }
            Shutdown(); return;
        }
        if (e.Args.Length >= 3 && e.Args[0] is "--verify" or "--self-check" or "--check-minimap")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (e.Args[0] == "--check-minimap") MinimapCheck.Run(e.Args.Skip(2).ToArray(), e.Args[1]);
                else if (e.Args[0] == "--self-check") SelfCheck.Run(e.Args[2], e.Args[1]);
                else Diagnostics.Run(e.Args.Skip(2).ToArray(), e.Args[1]);
            }
            catch (Exception error)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1); return;
            }
            Shutdown(); return;
        }
        base.OnStartup(e);
    }
}
