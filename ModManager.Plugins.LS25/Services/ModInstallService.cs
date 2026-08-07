using System;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace ModManager.Plugins.LS25.Services;

/// <summary>
/// Kern-Operationen für Mods im lokalen Mods-Ordner: List, Install, Uninstall,
/// SetEnabled (.zip.disabled-Toggle). Übernommen aus LS-ModManager, aber
/// entkoppelt vom Host-globalen AppPaths — Download-Verwaltung und Preview-Cache
/// gehören ab jetzt zum Plugin (siehe M3.2 in CLAUDE.md).
/// </summary>
public sealed class ModInstallService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly string _modsDir;
    private readonly ModDescReader _reader;

    public ModInstallService(string modsDir, ModDescReader reader)
    {
        _modsDir = modsDir;
        _reader = reader;
    }

    public string ModsDir => _modsDir;

    /// <summary>Liest alle Mods (.zip und .zip.disabled) aus dem Mod-Ordner.</summary>
    public IReadOnlyList<InstalledMod> ListInstalled()
    {
        if (!Directory.Exists(_modsDir))
        {
            Log.Info("Mods-Ordner existiert nicht: {Path}", _modsDir);
            return Array.Empty<InstalledMod>();
        }

        var result = new List<InstalledMod>();
        foreach (var file in Directory.EnumerateFiles(_modsDir))
        {
            var isZip = file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            var isDisabled = file.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase);
            if (!isZip && !isDisabled) continue;

            var info = new FileInfo(file);
            var readResult = _reader.Read(file);
            result.Add(new InstalledMod(
                FilePath: file,
                FileName: Path.GetFileName(file),
                FileSizeBytes: info.Length,
                InstalledUtc: info.LastWriteTimeUtc,
                IsEnabled: isZip,
                Metadata: readResult.Metadata,
                ReadError: readResult.Error));
        }
        return result;
    }

    /// <summary>Kopiert eine ZIP als Mod in den Mods-Ordner.</summary>
    public InstalledMod Install(string sourceZipPath, bool overwrite = false)
    {
        if (!File.Exists(sourceZipPath))
            throw new FileNotFoundException("Mod-ZIP existiert nicht", sourceZipPath);
        if (!ModDescReader.IsModZip(sourceZipPath))
            throw new InvalidDataException("Datei enthält keine modDesc.xml — keine gültige LS/FS-Mod");

        Directory.CreateDirectory(_modsDir);

        var fileName = Path.GetFileName(sourceZipPath);
        var destination = Path.Combine(_modsDir, fileName);
        if (File.Exists(destination) && !overwrite)
            throw new IOException($"Mod ist bereits installiert: {fileName}");

        File.Copy(sourceZipPath, destination, overwrite: true);
        Log.Info("Mod installiert: {Name} → {Path}", fileName, destination);

        var read = _reader.Read(destination);
        var info = new FileInfo(destination);
        return new InstalledMod(destination, fileName, info.Length, info.LastWriteTimeUtc,
            IsEnabled: true, Metadata: read.Metadata, ReadError: read.Error);
    }

    public void Uninstall(InstalledMod mod)
    {
        if (!File.Exists(mod.FilePath))
        {
            Log.Warn("Deinstallation: Datei bereits weg: {Path}", mod.FilePath);
            return;
        }
        File.Delete(mod.FilePath);
        Log.Info("Mod deinstalliert: {Path}", mod.FilePath);
    }

    /// <summary>Aktiviert/Deaktiviert via .zip.disabled-Rename. LS25 ignoriert Dateien,
    /// die nicht auf .zip enden — Mod bleibt im Ordner, wird aber nicht geladen.</summary>
    public InstalledMod SetEnabled(InstalledMod mod, bool enabled)
    {
        if (mod.IsEnabled == enabled) return mod;

        var current = mod.FilePath;
        var target = enabled
            ? current[..^".disabled".Length]
            : current + ".disabled";

        if (File.Exists(target))
            throw new IOException($"Zieldatei existiert bereits: {target}");

        File.Move(current, target);
        Log.Info("Mod {State}: {Path} → {Target}", enabled ? "aktiviert" : "deaktiviert", current, target);

        return mod with { FilePath = target, FileName = Path.GetFileName(target), IsEnabled = enabled };
    }
}
