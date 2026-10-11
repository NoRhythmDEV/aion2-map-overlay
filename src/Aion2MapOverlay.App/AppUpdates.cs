using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aion2MapOverlay.App;

internal sealed record AvailableUpdate(Version Version, Uri Archive, Uri Checksums);
internal sealed record UpdateFile(string Path, long Size, string Sha256);
internal sealed record UpdateManifest(string Version, UpdateFile[] Files);

internal static class AppUpdates
{
    public const string ArchiveName = "aion2-map-overlay-win-x64-portable.zip";
    private static readonly HttpClient client = CreateClient();
    private static readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };
    public static Version CurrentVersion { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version!);
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    private static HttpClient CreateClient()
    {
        var result = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        result.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2MapOverlay/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0"));
        return result;
    }

    public static async Task<AvailableUpdate?> CheckAsync(CancellationToken token)
    {
        using var response = await client.GetAsync("https://api.github.com/repos/NoRhythmDEV/aion2-map-overlay/releases/latest", token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$") || !Version.TryParse(tag[1..], out var version) || version <= CurrentVersion ||
            root.GetProperty("prerelease").GetBoolean() || root.GetProperty("draft").GetBoolean()) return null;
        Uri? archive = null, checksums = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name is not (ArchiveName or "SHA256SUMS")) continue;
            var uri = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/NoRhythmDEV/aion2-map-overlay/releases/download/", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected update download location.");
            if (name == ArchiveName) archive = uri; else checksums = uri;
        }
        return archive is null || checksums is null ? null : new(version, archive, checksums);
    }

    public static async Task<string> PrepareAsync(AvailableUpdate update, CancellationToken token)
    {
        var stage = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Aion2MapOverlay-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var checksums = await client.GetStringAsync(update.Checksums, token);
        var match = Regex.Match(checksums, @"(?m)^([a-fA-F0-9]{64})\s+\*?" + Regex.Escape(ArchiveName) + @"\s*$");
        if (!match.Success) throw new InvalidDataException("Release checksum missing.");
        var archivePath = System.IO.Path.Combine(stage, "package.zip");
        using (var response = await client.GetAsync(update.Archive, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var destination = File.Create(archivePath);
            var buffer = new byte[65536]; long length = 0; int read;
            while ((read = await source.ReadAsync(buffer, token)) != 0)
            {
                length += read; if (length > 512L * 1024 * 1024) throw new InvalidDataException("Update package is too large.");
                await destination.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }
        await using (var archive = File.OpenRead(archivePath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(archive, token));
            if (!hash.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch.");
        }
        var payload = System.IO.Path.Combine(stage, "payload");
        await Task.Run(() => ExtractVerified(archivePath, payload, update.Version), token);
        File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "ApplyUpdate.ps1"), System.IO.Path.Combine(stage, "ApplyUpdate.ps1"));
        return stage;
    }

    internal static string SafePath(string root, string relative)
    {
        var parts = relative.Replace('\\', '/').Split('/');
        if (parts.Length == 0 || parts.Any(p => string.IsNullOrWhiteSpace(p) || p is "." or ".." || p.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || p.EndsWith('.') || p.EndsWith(' ')))
            throw new InvalidDataException("Invalid package path.");
        if (parts[0].Equals("data", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("captures", StringComparison.OrdinalIgnoreCase) ||
            parts[0].StartsWith('.') || parts[0].StartsWith("settings.local", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update attempted to overwrite local data.");
        var canonicalRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(canonicalRoot, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        if (!path.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package path escaped its folder.");
        return path;
    }

    internal static void ExtractVerified(string archivePath, string payload, Version expectedVersion)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 2048 || archive.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024) throw new InvalidDataException("Invalid package size.");
        var entry = archive.GetEntry("update-manifest.json") ?? throw new InvalidDataException("Update manifest missing.");
        if (entry.Length > 1024 * 1024) throw new InvalidDataException("Manifest too large.");
        UpdateManifest manifest;
        using (var reader = new StreamReader(entry.Open())) manifest = JsonSerializer.Deserialize<UpdateManifest>(reader.ReadToEnd(), json) ?? throw new InvalidDataException("Invalid manifest.");
        if (manifest.Version != expectedVersion.ToString(3) || manifest.Files is null || manifest.Files.Length == 0) throw new InvalidDataException("Unexpected package version.");
        var files = manifest.Files.ToDictionary(f => f.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        if (!files.ContainsKey("aion2-map-overlay.exe") || !files.ContainsKey("ApplyUpdate.ps1")) throw new InvalidDataException("Application files missing.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in archive.Entries)
        {
            if (file.FullName.EndsWith('/')) continue;
            var name = file.FullName.Replace('\\', '/');
            var destination = SafePath(payload, name);
            if (!seen.Add(name) || ((file.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Invalid duplicate or linked file.");
            if (name != "update-manifest.json" && (!files.TryGetValue(name, out var details) || details.Size != file.Length)) throw new InvalidDataException("Unexpected package file.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            file.ExtractToFile(destination);
            if (name == "update-manifest.json") continue;
            using var data = File.OpenRead(destination);
            var hash = Convert.ToHexString(SHA256.HashData(data));
            if (!hash.Equals(files[name].Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package file checksum mismatch.");
        }
        if (files.Keys.Any(name => !seen.Contains(name))) throw new InvalidDataException("Incomplete update package.");
    }

    public static void InstallAndRestart(string stage)
    {
        var start = new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var value in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", System.IO.Path.Combine(stage, "ApplyUpdate.ps1"),
            "-InstallPath", AppContext.BaseDirectory, "-PayloadPath", System.IO.Path.Combine(stage, "payload"), "-ParentProcessId", Environment.ProcessId.ToString() })
            start.ArgumentList.Add(value);
        if (Process.Start(start) is null) throw new IOException("Could not start update helper.");
    }
}
