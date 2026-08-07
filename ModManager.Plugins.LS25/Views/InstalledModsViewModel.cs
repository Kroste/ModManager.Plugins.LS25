using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;

namespace ModManager.Plugins.LS25.Views;

public sealed partial class InstalledModsViewModel : ObservableObject
{
    private readonly ModInstallService _installer;
    private readonly IHostServices _host;

    public InstalledModsViewModel(ModInstallService installer, IHostServices host)
    {
        _installer = installer;
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

public sealed class ModRow
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

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes / 1024.0 / 1024.0:F1} MB";
    }
}
