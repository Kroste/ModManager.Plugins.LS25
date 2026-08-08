using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;

namespace ModManager.Plugins.LS25.Views;

public sealed partial class InstalledModsViewModel : ObservableObject
{
    private readonly ModInstallService _installer;
    private readonly ModBackupService _backup;
    private readonly ModPreviewService _previews;
    private readonly Ls25Paths _paths;
    private readonly IHostServices _host;

    public InstalledModsViewModel(ModInstallService installer, ModBackupService backup,
        ModPreviewService previews, Ls25Paths paths, IHostServices host)
    {
        _installer = installer;
        _backup = backup;
        _previews = previews;
        _paths = paths;
        _host = host;
        ModsDir = installer.ModsDir;
        RefreshCommand.Execute(null);
    }

    public string ModsDir { get; }

    public ObservableCollection<ModRow> Mods { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ModRow? _selected;

    public bool HasSelection => Selected is not null;

    [ObservableProperty]
    private string _summary = "";

    partial void OnSelectedChanged(ModRow? value) => OnPropertyChanged(nameof(HasSelection));

    [RelayCommand]
    private void Refresh()
    {
        Mods.Clear();
        try
        {
            foreach (var m in _installer.ListInstalled()
                         .OrderByDescending(m => m.IsEnabled)
                         .ThenBy(m => m.Metadata?.Title ?? m.FileName, StringComparer.CurrentCultureIgnoreCase))
                Mods.Add(new ModRow(m));

            var enabled = Mods.Count(r => r.Source.IsEnabled);
            var total = Mods.Count;
            var totalBytes = Mods.Where(r => r.Source.IsEnabled).Sum(r => r.Source.FileSizeBytes);
            Summary = total == 0
                ? "Keine Mods im Mods-Ordner."
                : $"{enabled} aktiv / {total} total · {FormatBytes(totalBytes)}";
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Mod-Liste konnte nicht geladen werden");
            Summary = "Fehler beim Lesen des Mods-Ordners.";
        }

        _ = LoadPreviewsAsync(Mods.ToArray());
    }

    private async Task LoadPreviewsAsync(ModRow[] rows)
    {
        foreach (var row in rows)
        {
            try
            {
                var path = await _previews.GetOrExtractInstalledPreviewAsync(row.Source.FilePath);
                if (path is null || !File.Exists(path)) continue;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        using var s = File.OpenRead(path);
                        row.Preview = new Bitmap(s);
                    }
                    catch (Exception ex)
                    {
                        _host.Logger.Debug(ex, "Preview-Bitmap-Load fehlgeschlagen: {p}", path);
                    }
                });
            }
            catch (Exception ex)
            {
                _host.Logger.Debug(ex, "Preview-Extraction fehlgeschlagen: {p}", row.Source.FilePath);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleEnabled()
    {
        if (Selected is null) return;
        try
        {
            var updated = _installer.SetEnabled(Selected.Source, !Selected.Source.IsEnabled);
            var idx = Mods.IndexOf(Selected);
            Mods[idx] = new ModRow(updated);
            Selected = Mods[idx];
            _host.Notifications.Notify(
                $"Mod {(updated.IsEnabled ? "aktiviert" : "deaktiviert")}: {updated.FileName}",
                NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Toggle fehlgeschlagen");
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task UninstallAsync()
    {
        if (Selected is null) return;
        bool ok = await _host.Dialogs.ConfirmAsync(
            "Mod deinstallieren",
            $"„{Selected.Source.FileName}“ wirklich löschen?",
            okLabel: "Löschen", cancelLabel: "Abbrechen");
        if (!ok) return;
        try
        {
            _installer.Uninstall(Selected.Source);
            _host.Notifications.Notify($"Deinstalliert: {Selected.Source.FileName}", NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Uninstall fehlgeschlagen");
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private async Task InstallFromFileAsync()
    {
        var picked = await _host.Dialogs.PickFileAsync(
            "Mod-ZIP wählen",
            ("LS25-Mod (.zip)", new[] { "*.zip" }));
        if (picked is null) return;
        try
        {
            var installed = _installer.Install(picked, overwrite: false);
            _host.Notifications.Notify($"Installiert: {installed.FileName}", NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Install fehlgeschlagen");
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void OpenModsFolder() => _host.Shell.OpenDirectory(ModsDir);

    /// <summary>Startet Farming Simulator 25 über das Steam-Protokoll
    /// <c>steam://run/2300320</c>. Funktioniert Windows + Linux (dort geht
    /// Steam an das Proton-Prefix). Wenn Steam nicht installiert ist, gibt
    /// das OS eine "no handler"-Meldung — wir fangen die als Notify ab.</summary>
    [RelayCommand]
    private void LaunchGame()
    {
        const int fs25AppId = 2300320;
        try
        {
            Process.Start(new ProcessStartInfo($"steam://run/{fs25AppId}") { UseShellExecute = true });
            _host.Notifications.Notify("Starte Farming Simulator 25 über Steam …", NotificationLevel.Info);
            _host.Logger.Info("LS25: Spielstart-URI aufgerufen (AppId {AppId})", fs25AppId);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Spielstart fehlgeschlagen");
            _host.Notifications.Notify(
                $"Spielstart fehlgeschlagen: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (Mods.Count == 0)
        {
            _host.Notifications.Notify("Keine Mods vorhanden — nichts zu sichern.", NotificationLevel.Warning);
            return;
        }
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var target = Path.Combine(_paths.BackupsDir, $"ls25-backup-{timestamp}.zip");
        using var scope = _host.BeginProgress("Backup erstellen …");
        var progress = new Progress<BackupProgress>(p =>
            scope.Report(p.Fraction, $"{p.Current}/{p.Total} · {p.CurrentFileName}"));
        try
        {
            var result = await _backup.CreateBackupAsync(target, progress);
            _host.Notifications.Notify(
                $"Backup: {result.ModCount} Mods · {FormatBytes(result.FileSizeBytes)} → {Path.GetFileName(result.FilePath)}",
                NotificationLevel.Success);
            _host.Shell.OpenDirectory(_paths.BackupsDir);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Backup fehlgeschlagen");
            _host.Notifications.Notify($"Backup-Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var picked = await _host.Dialogs.PickFileAsync(
            "Backup-ZIP wählen",
            ("LS25-Backup (.zip)", new[] { "*.zip" }));
        if (picked is null) return;

        // Preview: Manifest zeigen bevor der Restore läuft.
        BackupManifest manifest;
        try { manifest = ModBackupService.ReadManifest(picked); }
        catch (Exception ex)
        {
            _host.Notifications.Notify($"Backup ungültig: {ex.Message}", NotificationLevel.Error);
            return;
        }

        var confirm = await _host.Dialogs.ConfirmAsync(
            "Backup wiederherstellen",
            $"Backup vom {manifest.CreatedUtc.ToLocalTime():g} · {manifest.Mods.Count} Mods.\n" +
            "Vorhandene Mod-ZIPs mit gleichem Namen werden überschrieben.\nFortfahren?",
            okLabel: "Wiederherstellen", cancelLabel: "Abbrechen");
        if (!confirm) return;

        using var scope = _host.BeginProgress("Backup wiederherstellen …");
        var progress = new Progress<BackupProgress>(p =>
            scope.Report(p.Fraction, $"{p.Current}/{p.Total} · {p.CurrentFileName}"));
        try
        {
            var result = await _backup.RestoreBackupAsync(picked, progress);
            _host.Notifications.Notify(
                $"Restore: {result.RestoredCount} wiederhergestellt, {result.SkippedCount} übersprungen.",
                NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25: Restore fehlgeschlagen");
            _host.Notifications.Notify($"Restore-Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:F1} MB";
        double gb = mb / 1024.0;
        return $"{gb:F2} GB";
    }
}

public sealed partial class ModRow : ObservableObject
{
    public InstalledMod Source { get; }
    public ModRow(InstalledMod source) => Source = source;

    public string Title => Source.Metadata?.Title ?? Source.FileName;
    public string Author => Source.Metadata?.Author ?? "";
    public string Version => Source.Metadata?.Version ?? "";
    public string Size => FormatBytes(Source.FileSizeBytes);
    public bool IsEnabled => Source.IsEnabled;
    public string StateLabel => Source.IsEnabled ? "aktiv" : "inaktiv";
    public string FileName => Source.FileName;
    public string? ErrorText => Source.ReadError;

    [ObservableProperty]
    private Bitmap? _preview;

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes / 1024.0 / 1024.0:F1} MB";
    }
}
