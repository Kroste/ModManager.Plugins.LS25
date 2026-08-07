using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;

namespace ModManager.Plugins.LS25.Views;

public sealed partial class DownloadsViewModel : ObservableObject
{
    private readonly ModInstallService _installer;
    private readonly IHostServices _host;

    public DownloadsViewModel(ModInstallService installer, IHostServices host)
    {
        _installer = installer;
        _host = host;
        DownloadsDir = installer.DownloadsDir ?? "(nicht konfiguriert)";
        RefreshCommand.Execute(null);
    }

    public string DownloadsDir { get; }

    public ObservableCollection<ModRow> Rows { get; } = new();

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
        Rows.Clear();
        try
        {
            foreach (var m in _installer.ListDownloaded()
                         .OrderByDescending(m => m.InstalledUtc))
                Rows.Add(new ModRow(m));

            var totalBytes = Rows.Sum(r => r.Source.FileSizeBytes);
            Summary = Rows.Count == 0
                ? "Keine heruntergeladenen Mods."
                : $"{Rows.Count} ZIPs · {totalBytes / 1024.0 / 1024.0:F1} MB gesamt";
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25 Downloads-Liste konnte nicht geladen werden");
            Summary = "Fehler beim Lesen des Downloads-Ordners.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void InstallSelected()
    {
        if (Selected is null) return;
        try
        {
            var installed = _installer.Install(Selected.Source.FilePath, overwrite: false);
            _host.Notifications.Notify($"Installiert: {installed.FileName}", NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "LS25 Install-from-download fehlgeschlagen");
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        bool ok = await _host.Dialogs.ConfirmAsync(
            "Download löschen",
            $"„{Selected.Source.FileName}“ aus dem Downloads-Ordner löschen?",
            okLabel: "Löschen", cancelLabel: "Abbrechen");
        if (!ok) return;
        try
        {
            _installer.DeleteDownload(Selected.Source.FilePath);
            _host.Notifications.Notify($"Gelöscht: {Selected.Source.FileName}", NotificationLevel.Success);
            Refresh();
        }
        catch (Exception ex)
        {
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void OpenDownloadsFolder() => _host.Shell.OpenDirectory(DownloadsDir);
}
