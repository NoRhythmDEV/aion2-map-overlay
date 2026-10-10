using System.Text.Json;
using Aion2MapOverlay.Core.Markers;

namespace Aion2MapOverlay.Data;

public static class QuestlogData
{
    public static MarkerIndex LoadVerteron(string path, int referenceSize)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var markers = new List<MapMarker>();
        var ids = new HashSet<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.GetProperty("mapWorldId").GetString() != "1010") continue;
            var type = item.GetProperty("mapType").GetString()!;
            if (type is not ("gatherable" or "collectable")) continue;
            var id = item.GetProperty("id").GetString()!;
            if (!ids.Add(id)) throw new InvalidDataException("Duplicate marker: " + id);
            var position = item.GetProperty("coordinates")[0];
            var x = position[0].GetDouble(); var y = position[1].GetDouble();
            if (!double.IsFinite(x) || !double.IsFinite(y)) throw new InvalidDataException("Non-finite coordinate");
            // World_L_A: 8 sectors of 102,000 units; upper left is (-408000,+408000).
            markers.Add(new(id, item.GetProperty("name").GetString()!, type,
                item.GetProperty("mapCategory").GetString()!,
                (x + 408000) / 816000 * referenceSize,
                (408000 - y) / 816000 * referenceSize,
                item.TryGetProperty("icon", out var icon) ? icon.GetString() : null));
        }
        return new(markers);
    }
}
