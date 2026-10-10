using System.Diagnostics;
using System.Windows;

namespace Aion2MapOverlay.App;

// Render-only interpolation: recognition always receives the original screen pixels.
internal sealed class MarkerMotion
{
    private Dictionary<string, Point> starts = [];
    private Dictionary<string, Point> targets = [];
    private long started;
    private bool finished;

    public void SetFrame(OverlayFrame? frame, long now)
    {
        var nextStarts = new Dictionary<string, Point>();
        var nextTargets = new Dictionary<string, Point>();
        if (frame is not null)
            foreach (var marker in frame.Markers)
            {
                if (marker.Id is null) continue;
                var target = new Point(marker.X, marker.Y);
                var previous = targets.ContainsKey(marker.Id) ? Position(marker, now) : target;
                // Do not slide through the map after a large pan or a new registration.
                nextStarts[marker.Id] = (target - previous).Length <= 80 ? previous : target;
                nextTargets[marker.Id] = target;
            }
        starts = nextStarts; targets = nextTargets; started = now; finished = false;
    }

    public Point Position(PlacedMarker marker, long now)
    {
        if (marker.Id is null || !targets.TryGetValue(marker.Id, out var target)) return new(marker.X, marker.Y);
        var amount = finished ? 1 : Math.Clamp((now - started) / (double)Stopwatch.Frequency / .024, 0, 1);
        var start = starts[marker.Id];
        return start + (target - start) * amount;
    }

    public void Finish() => finished = true;
}
