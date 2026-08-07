using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;
using NLog;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// VM für den ModHub-Katalog-Tab: Katalog beim ersten Öffnen aus Cache laden,
/// im Hintergrund alle Seiten sammeln, Live-Suchfilter, Sort- und Kategorie-
/// Auswahl, Klick auf Karte → Download in Plugin-Downloads-Ordner.
/// </summary>
public sealed partial class ModHubViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string Language = "de";

    private readonly ModHubService _hub;
    private readonly CatalogCache _cache;
    private readonly ModInstallService _installer;
    private readonly IHostServices _host;

    private readonly List<ModHubEntry> _allEntries = new();
    private HashSet<string>? _seenSnapshot;
    private CancellationTokenSource? _fullLoadCts;

    public ModHubViewModel(ModHubService hub, CatalogCache cache,
        ModInstallService installer, IHostServices host)
    {
        _hub = hub;
        _cache = cache;
        _installer = installer;
        _host = host;

        Categories = new ObservableCollection<ModHubCategory>
        {
            new("", "Alle Kategorien"),
        };
        SelectedCategory = Categories[0];

        // Ersten Fresh-Load im Hintergrund starten. Cache zeigt sofort was;
        // Full-Load appended nur wirklich neue Einträge (kein Flicker).
        _ = InitializeAsync();
    }

    public ObservableCollection<CatalogRow> Rows { get; } = new();
    public ObservableCollection<ModHubCategory> Categories { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ModHubCategory? _selectedCategory;

    [ObservableProperty]
    private string _status = "Katalog wird geladen …";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private CatalogRow? _selected;

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(CatalogRow? value) => OnPropertyChanged(nameof(HasSelection));
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(ModHubCategory? value) => ApplyFilter();

    private async Task InitializeAsync()
    {
        // Erst Cache-Snapshot anzeigen (offline sofort).
        var snapshot = _cache.Load(Language);
        _seenSnapshot = _cache.LoadSeenSnapshot(Language);
        if (snapshot is not null)
        {
            AddEntries(snapshot.Entries);
            Status = $"{Rows.Count} Mods aus Cache (Alter: {(int)(DateTime.UtcNow - snapshot.SavedUtc).TotalHours} h).";
        }

        // Kategorien parallel holen (billig).
        _ = LoadCategoriesAsync();

        // Jetzt Full-Load — appended nur neue Einträge, kein Clear.
        await RefreshCatalogAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var html = await _hub.FetchCatalogPageHtmlAsync(1, Language);
            if (html is null) return;
            var cats = ModHubService.ParseCategories(html);
            foreach (var cat in cats.Where(c => Categories.All(x => x.Filter != c.Filter)))
                Categories.Add(cat);
        }
        catch (Exception ex) { Log.Debug(ex, "Kategorien-Load fehlgeschlagen"); }
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        _fullLoadCts?.Cancel();
        _fullLoadCts = new CancellationTokenSource();
        var ct = _fullLoadCts.Token;

        IsBusy = true;
        Status = "Vollständiger Katalog-Load …";
        int page = 1;
        int totalNew = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var pageEntries = await _hub.FetchCatalogPageAsync(page, Language, ct);
                if (pageEntries.Count == 0) break;

                int addedThisPage = AddEntries(pageEntries);
                totalNew += addedThisPage;
                Status = $"Katalog-Load: Seite {page} · {Rows.Count} Mods sichtbar";
                page++;
                if (pageEntries.Count < 20) break; // GIANTS Page-Size
                await Task.Delay(TimeSpan.FromMilliseconds(300), ct); // sanfter Rate-Limit
            }

            // Snapshot der aktuellen URLs für "neue Mods seit letztem Start"-Erkennung.
            _cache.Save(_allEntries, Language);
            _cache.SaveSeenSnapshot(_allEntries.Select(e => e.DetailUrl), Language);
            Status = $"{Rows.Count} Mods im Katalog · {totalNew} neu geladen";
        }
        catch (OperationCanceledException) { /* silent */ }
        catch (Exception ex)
        {
            Log.Warn(ex, "Katalog-Load abgebrochen");
            Status = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private int AddEntries(IEnumerable<ModHubEntry> entries)
    {
        int added = 0;
        var seen = new HashSet<string>(_allEntries.Select(e => e.DetailUrl), StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.DetailUrl)) continue;
            _allEntries.Add(entry);
            var row = new CatalogRow(entry) { IsNew = _seenSnapshot is not null && !_seenSnapshot.Contains(entry.DetailUrl) };
            if (RowMatchesFilter(row)) Rows.Add(row);
            added++;
        }
        return added;
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var e in _allEntries)
        {
            var row = new CatalogRow(e) { IsNew = _seenSnapshot is not null && !_seenSnapshot.Contains(e.DetailUrl) };
            if (RowMatchesFilter(row)) Rows.Add(row);
        }
    }

    private bool RowMatchesFilter(CatalogRow row)
    {
        var q = SearchText?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            if (!(row.Source.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || row.Source.Author.Contains(q, StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        if (SelectedCategory is not null && !string.IsNullOrEmpty(SelectedCategory.Filter))
        {
            if (!row.Source.Category.Contains(SelectedCategory.Label, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DownloadSelectedAsync()
    {
        if (Selected is null) return;
        var modIdMatch = System.Text.RegularExpressions.Regex.Match(
            Selected.Source.DetailUrl, @"mod_id=(\d+)");
        if (!modIdMatch.Success)
        {
            _host.Notifications.Notify("Keine Mod-ID in URL erkennbar.", NotificationLevel.Warning);
            return;
        }
        int modId = int.Parse(modIdMatch.Groups[1].Value);

        using var scope = _host.BeginProgress($"Download: {Selected.Source.Title}");
        var progress = new Progress<ModDownloadProgress>(p =>
        {
            var frac = p.Fraction ?? 0;
            scope.Report(frac, p.FormatShort());
        });
        try
        {
            var result = await _hub.DownloadModAsync(modId, Language, progress,
                default, Selected.Source.PreviewUrl);
            if (result is null)
            {
                _host.Notifications.Notify("Download fehlgeschlagen (siehe Log).", NotificationLevel.Error);
                return;
            }
            _host.Notifications.Notify($"Heruntergeladen: {result.FileName}", NotificationLevel.Success);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "ModHub-Download fehlgeschlagen für {Title}", Selected.Source.Title);
            _host.Notifications.Notify($"Fehler: {ex.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenDetailInBrowser()
    {
        if (Selected is null) return;
        _host.Shell.OpenExternalUrl(Selected.Source.DetailUrl);
    }
}

public sealed partial class CatalogRow : ObservableObject
{
    public CatalogRow(ModHubEntry source) => Source = source;
    public ModHubEntry Source { get; }
    public string Title => Source.Title;
    public string Author => Source.Author;
    public string Category => Source.Category;
    public string? Version => Source.Version;
    public string? SizeText => Source.SizeText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private bool _isNew;

    public string BadgeText => IsNew ? "NEU" : "";
}
