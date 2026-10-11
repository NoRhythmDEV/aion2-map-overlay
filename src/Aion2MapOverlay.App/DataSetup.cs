using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aion2MapOverlay.Data;
using OpenCvSharp;

namespace Aion2MapOverlay.App;

internal static class DataSetup
{
    private const string MarkersUrl = "https://questlog.gg/aion-2/api/trpc/map.getMarkers?input=%7B%22language%22%3A%22en%22%7D";
    private const string ReferenceUrl = "https://cdn.questlog.gg/aion-2/map/v3/World_L_A.webp";
    public static bool HasData => File.Exists(Paths.Markers) && File.Exists(Paths.Reference) && File.Exists(Path.Combine(Paths.Icons, "manifest.json"));
    public static async Task DownloadAsync(IProgress<string> progress, CancellationToken token, string? destinationRoot = null)
    {
        var destination = destinationRoot ?? Paths.DataRoot;
        var stage = destination + ".setup-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(stage, "reference")); Directory.CreateDirectory(Path.Combine(stage, "icons"));
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2MapOverlay/" + AppUpdates.CurrentVersion);
        progress.Report("Downloading marker locations from Questlog…");
        var raw = await ReadLimitedAsync(client, new Uri(MarkersUrl), token);
        using var document = JsonDocument.Parse(raw);
        var records = document.RootElement.GetProperty("result").GetProperty("data").EnumerateArray()
            .Where(m => m.GetProperty("mapWorldId").GetString() == "1010").ToArray();
        if (records.Length == 0) throw new InvalidDataException("Questlog returned no Verteron markers.");
        var markerPath = Path.Combine(stage, "map-1010.json");
        await File.WriteAllTextAsync(markerPath, JsonSerializer.Serialize(records), token);
        var index = QuestlogData.LoadVerteron(markerPath, 2048);
        if (index.Markers.Count == 0) throw new InvalidDataException("No usable resource locations were returned.");
        progress.Report("Downloading the Verteron map reference…");
        var map = await ReadLimitedAsync(client, new Uri(ReferenceUrl), token);
        var referencePath = Path.Combine(stage, "reference", "World_L_A.webp");
        await File.WriteAllBytesAsync(referencePath, map, token);
        using (var decoded = Cv2.ImRead(referencePath, ImreadModes.Grayscale))
            if (decoded.Empty() || decoded.Width != 8192 || decoded.Height != 8192) throw new InvalidDataException("Unexpected reference map format.");
        var icons = new Dictionary<string, string>();
        var groups = index.Markers.GroupBy(m => m.GroupKey).ToArray();
        var done = 0;
        foreach (var group in groups)
        {
            var marker = group.First();
            var icon = marker.Name == "Empyrean Trace" ? "/map/icon/monolith.webp" : marker.Icon;
            progress.Report($"Downloading resource icons… {++done}/{groups.Length}");
            if (string.IsNullOrEmpty(icon)) continue;
            var path = Path.ChangeExtension(icon, ".webp").Replace('\\', '/');
            if ((!path.StartsWith("/map/", StringComparison.Ordinal) && !path.StartsWith("/assets/", StringComparison.Ordinal)) || path.Contains("..") || path.Contains('?') || path.Contains('#')) throw new InvalidDataException("Unexpected icon location.");
            var filename = Regex.Replace(marker.Name.ToLowerInvariant(), "[^a-z0-9]+", "-") + ".webp";
            var bytes = await ReadLimitedAsync(client, new Uri("https://cdn.questlog.gg/aion-2" + path), token);
            var iconPath = Path.Combine(stage, "icons", filename);
            await File.WriteAllBytesAsync(iconPath, bytes, token);
            using (var decoded = Cv2.ImRead(iconPath))
                if (decoded.Empty()) throw new InvalidDataException("An icon could not be decoded.");
            icons[group.Key] = filename;
        }
        await File.WriteAllTextAsync(Path.Combine(stage, "icons", "manifest.json"), JsonSerializer.Serialize(icons), token);
        await File.WriteAllTextAsync(Path.Combine(stage, "provenance.json"), JsonSerializer.Serialize(new {
            markerSource = MarkersUrl, referenceSource = ReferenceUrl, downloadedAtUtc = DateTime.UtcNow,
            markerResponseSha256 = Convert.ToHexString(SHA256.HashData(raw)), referenceSha256 = Convert.ToHexString(SHA256.HashData(map)),
            attribution = "Questlog.gg / NCSOFT and respective owners", redistributionPermission = "Not established; downloaded directly for local use",
            verteronRecords = records.Length, usableLocations = index.Markers.Count
        }, new JsonSerializerOptions { WriteIndented = true }), token);
        token.ThrowIfCancellationRequested();
        if (!Directory.Exists(destination)) Directory.Move(stage, destination);
        else
        {
            foreach (var source in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stage, source);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target + ".download", true); File.Move(target + ".download", target, true);
            }
        }
        progress.Report($"Ready: {index.Markers.Count:N0} resource and collectible locations downloaded.");
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpClient client, Uri uri, CancellationToken token)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var destination = new MemoryStream(); var buffer = new byte[65536]; int read;
        while ((read = await source.ReadAsync(buffer, token)) != 0)
        {
            if (destination.Length + read > 100L * 1024 * 1024) throw new InvalidDataException("Website response is too large.");
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }
}
