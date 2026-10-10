namespace Aion2MapOverlay.Core.Markers;

public sealed record MapMarker(string Id, string Name, string Type, string Category,
    double ReferenceX, double ReferenceY, string? Icon)
{
    public string GroupKey => Type + "|" + Name;
}

public sealed class MarkerIndex
{
    private const double CellSize = 128;
    private readonly Dictionary<(int, int), List<MapMarker>> cells = [];
    public IReadOnlyList<MapMarker> Markers { get; }

    public MarkerIndex(IReadOnlyList<MapMarker> markers)
    {
        Markers = markers;
        foreach (var marker in markers)
        {
            var cell = ((int)Math.Floor(marker.ReferenceX / CellSize), (int)Math.Floor(marker.ReferenceY / CellSize));
            if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = [];
            list.Add(marker);
        }
    }

    public IEnumerable<MapMarker> Query(double left, double top, double right, double bottom)
    {
        if (!new[] { left, top, right, bottom }.All(double.IsFinite) || right < left || bottom < top) yield break;
        // Reject a broken transform instead of iterating an unbounded grid.
        if (right - left > 20000 || bottom - top > 20000) yield break;
        for (var y = (int)Math.Floor(top / CellSize); y <= Math.Floor(bottom / CellSize); y++)
        for (var x = (int)Math.Floor(left / CellSize); x <= Math.Floor(right / CellSize); x++)
            if (cells.TryGetValue((x, y), out var list))
                foreach (var marker in list)
                    if (marker.ReferenceX >= left && marker.ReferenceX <= right && marker.ReferenceY >= top && marker.ReferenceY <= bottom)
                        yield return marker;
    }
}
