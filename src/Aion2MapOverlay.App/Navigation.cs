using Aion2MapOverlay.Capture;
using Aion2MapOverlay.Core.Markers;
using Aion2MapOverlay.Vision;

namespace Aion2MapOverlay.App;

internal sealed record GuidanceFrame(double CenterX, double CenterY, double Radius, double Angle, string Name, string GroupKey, bool Large, DateTime CapturedAt);

internal static class Navigation
{
    public static DisplayArea MinimapArea(DisplayArea display, bool large)
    {
        return new(display.X + (int)(display.Width * (large ? .859 : .868)),
            display.Y + (int)(display.Height * .089),
            (int)(display.Width * (large ? .130 : .122)),
            (int)(display.Height * (large ? .338 : .138)));
    }

    public static MapMarker? Nearest(IEnumerable<MapMarker> markers, HashSet<string> enabled, MinimapFix fix, MapMarker? previous = null)
    {
        var candidates = markers.Where(m => m.Type == "gatherable" && enabled.Contains(m.GroupKey));
        static double Distance(MapMarker m, MinimapFix f) => Math.Pow(m.ReferenceX - f.PlayerX, 2) + Math.Pow(m.ReferenceY - f.PlayerY, 2);
        var nearest = candidates.MinBy(m => Distance(m, fix));
        // Keep the current target when two nodes are almost tied; prevent pointer flicker.
        if (nearest is not null && previous is not null && enabled.Contains(previous.GroupKey) &&
            Distance(previous, fix) <= Distance(nearest, fix) * 1.21) return previous;
        return nearest;
    }

    public static GuidanceFrame Guide(MapMarker target, MinimapFix fix, DisplayArea display, DisplayArea area, bool large, DateTime capturedAt)
    {
        return new((area.X - display.X + area.Width / 2.0) / display.Width,
            (area.Y - display.Y + area.Height / 2.0) / display.Height,
            Math.Min(area.Width, area.Height) * .43 / display.Width,
            Math.Atan2(target.ReferenceY - fix.PlayerY, target.ReferenceX - fix.PlayerX) + fix.Rotation,
            target.Name, target.GroupKey, large, capturedAt);
    }
}

internal sealed class NavigationSession(MarkerIndex index, DisplayArea display) : IDisposable
{
    private MinimapTracker? tracker;
    private DesktopCapture? capture;
    private MapMarker? target;

    public (GuidanceFrame? Frame, string Status) Process(bool large, HashSet<string> enabled)
    {
        if (!index.Markers.Any(m => m.Type == "gatherable" && enabled.Contains(m.GroupKey)))
            return (null, "Direction: select at least one gatherable.");
        tracker ??= new MinimapTracker(Paths.Reference);
        var area = Navigation.MinimapArea(display, large);
        if (capture?.Display != area)
        {
            capture?.Dispose(); capture = new DesktopCapture(area); target = null;
        }
        var capturedAt = DateTime.UtcNow;
        using var gray = capture.GrabGray();
        var fix = tracker.Process(gray);
        if (fix is null) { target = null; return (null, "Direction: minimap alignment lost. Check your minimap preset."); }
        target = Navigation.Nearest(index.Markers, enabled, fix, target);
        return target is null ? (null, "Direction: no selected gatherable found.") :
            (Navigation.Guide(target, fix, display, area, large, capturedAt), $"Direction: {target.Name} · {fix.Inliers} terrain points");
    }

    public void Dispose()
    {
        tracker?.Dispose(); tracker = null; capture?.Dispose(); capture = null; target = null;
    }
}
