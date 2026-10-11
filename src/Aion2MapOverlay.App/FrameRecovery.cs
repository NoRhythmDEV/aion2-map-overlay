using System.IO;
using Aion2MapOverlay.Capture;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal sealed class FrameRecovery
{
    public int Failures { get; private set; }
    public void Succeeded() => Failures = 0;
    public bool TryRetry(Exception error, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;
        if (error is not (ScreenCaptureException or OpenCVException)) return false;
        Failures++;
        delay = TimeSpan.FromMilliseconds(Math.Min(3000, Failures * 500));
        return Failures <= 8;
    }
}

internal static class RuntimeLog
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "logs", "overlay.log");
    public static void Write(string context, Exception error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1024 * 1024) File.Move(FilePath, FilePath + ".previous", true);
            File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} v{AppUpdates.CurrentVersion} {context}\n{error}\n\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
