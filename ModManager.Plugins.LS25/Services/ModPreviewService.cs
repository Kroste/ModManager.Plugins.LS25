using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace ModManager.Plugins.LS25.Services;

/// <summary>
/// Extrahiert und cached Preview-Bilder — für Installiert-Tab aus dem
/// Mod-ZIP (via <see cref="ModDescReader"/>), für ModHub-Tab per HTTP
/// vom GIANTS-CDN. Cache liegt in <see cref="Ls25Paths.PreviewsCacheDir"/>,
/// pro Mod ein basename+Extension. Existierende Cache-Files werden nicht
/// überschrieben — teure Extraktion läuft nur einmal.
/// </summary>
public sealed class ModPreviewService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly Ls25Paths _paths;
    private readonly ModDescReader _reader;
    private readonly HttpClient _http;

    public ModPreviewService(Ls25Paths paths, ModDescReader reader, HttpClient? http = null)
    {
        _paths = paths;
        _reader = reader;
        _http = http ?? DefaultHttp;
    }

    /// <summary>Liefert Cache-Path zu einer Mod-ZIP. Wenn kein Cache existiert,
    /// wird das Preview aus dem ZIP extrahiert und gespeichert. Läuft im Hintergrund-
    /// Thread — extrahieren + DDS-decode kosten je nach Größe 10–100 ms.</summary>
    public async Task<string?> GetOrExtractInstalledPreviewAsync(string zipPath, CancellationToken ct = default)
    {
        var cached = _paths.FindExistingPreview(zipPath);
        if (cached is not null) return cached;

        return await Task.Run(() =>
        {
            try
            {
                var result = _reader.Read(zipPath);
                if (result.PreviewPngBytes is null || result.PreviewPngBytes.Length == 0)
                    return null;
                var ext = Ls25Paths.GuessImageExtension(result.PreviewPngBytes);
                if (ext == ".bin") ext = ".png"; // Reader liefert PNG-Bytes bei DDS-Decode
                var target = _paths.PreviewCacheBasePathFor(zipPath) + ext;
                File.WriteAllBytes(target, result.PreviewPngBytes);
                return target;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Preview-Extract fehlgeschlagen für {p}", zipPath);
                return (string?)null;
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Cover-Download vom ModHub-CDN. Cache-Key ist ein SHA1-Hash der
    /// URL (Filename), damit lange URLs mit Query-Strings kein Dateisystem-
    /// Problem auslösen. Dateiendung wird aus den Magic-Bytes der Response
    /// bestimmt (JPG vs PNG).</summary>
    public async Task<string?> GetOrDownloadCoverAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var hash = Sha1Hex(url);
        var basePath = Path.Combine(_paths.PreviewsCacheDir, "catalog_" + hash);
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png" })
        {
            var candidate = basePath + ext;
            if (File.Exists(candidate)) return candidate;
        }

        try
        {
            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return null;
            var ext = Ls25Paths.GuessImageExtension(bytes);
            if (ext == ".bin") return null;
            var target = basePath + ext;
            await File.WriteAllBytesAsync(target, bytes, ct);
            return target;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Cover-Download fehlgeschlagen: {url}", url);
            return null;
        }
    }

    private static string Sha1Hex(string s)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
