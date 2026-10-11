using System.IO;

namespace Aion2MapOverlay.App;

internal static class Paths
{
    public static string DataRoot { get; internal set; } = FindDataRoot();
    public static string Reference => Path.Combine(DataRoot, "reference", "World_L_A.webp");
    public static string Title => Path.Combine(DataRoot, "reference", "verteron-title.png");
    public static string Markers => Path.Combine(DataRoot, "map-1010.json");
    public static string Icons => Path.Combine(DataRoot, "icons");
    public static string Settings => Path.Combine(AppContext.BaseDirectory, "settings.local.json");
    private static string FindDataRoot()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "data");
        if (File.Exists(Path.Combine(portable, "map-1010.json"))) return portable;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "data", "processed", "questlog");
            if (File.Exists(Path.Combine(path, "map-1010.json"))) return path;
        }
        return portable;
    }
}
