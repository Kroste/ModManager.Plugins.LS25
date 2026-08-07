using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;
using ModManager.Plugins.LS25.Services.Ai;
using NLog;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// VM für den ModHub-Katalog-Tab. Aggregiert die drei Quellen (GIANTS ModHub,
/// Hof Hirschfeld, modhoster) in einer Liste — wie im standalone LS-ModManager.
/// GIANTS lädt seitenweise mit Direct-Download, die anderen zwei nur Detail-
/// im-Browser wegen Consent-Overlay / Login-Pflicht.
/// </summary>
public sealed partial class ModHubViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string Language = "de";

    private readonly ModHubService _hub;
    private readonly HofHirschfeldCatalogService _hof;
    private readonly ModhosterCatalogService _modhoster;
    private readonly CatalogCache _cache;
    private readonly ModInstallService _installer;
    private readonly ModPreviewService _previews;
    private readonly Func<IAiProvider?> _aiFactory;
    private readonly IHostServices _host;

    private readonly List<ModHubEntry> _allEntries = new();
    private HashSet<string>? _seenSnapshot;
    private CancellationTokenSource? _fullLoadCts;
    private CancellationTokenSource? _coverCts;

    public ModHubViewModel(ModHubService hub, HofHirschfeldCatalogService hof,
        ModhosterCatalogService modhoster, CatalogCache cache,
        ModInstallService installer, ModPreviewService previews,
        Func<IAiProvider?> aiFactory, IHostServices host)
    {
        _hub = hub;
        _hof = hof;
        _modhoster = modhoster;
        _cache = cache;
        _installer = installer;
        _previews = previews;
        _aiFactory = aiFactory;
        _host = host;

        Categories = new ObservableCollection<ModHubCategory>
        {
            new("", "Alle Kategorien"),
        };
        SelectedCategory = Categories[0];

        Sources = new ObservableCollection<SourceFilterOption>
        {
            new(null, "Alle Quellen"),
            new(ModHubEntry.GiantsSource, "GIANTS ModHub"),
            new(ModHubEntry.HofHirschfeldSource, "Hof Hirschfeld"),
            new(ModHubEntry.ModhosterSource, "modhoster"),
        };
        SelectedSource = Sources[0];

        _ = InitializeAsync();
    }

    public ObservableCollection<CatalogRow> Rows { get; } = new();
    public ObservableCollection<ModHubCategory> Categories { get; }
    public ObservableCollection<SourceFilterOption> Sources { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ModHubCategory? _selectedCategory;

    [ObservableProperty]
    private SourceFilterOption? _selectedSource;

    [ObservableProperty]
    private string _status = "Katalog wird geladen …";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanDownloadSelected))]
    [NotifyPropertyChangedFor(nameof(SelectionNeedsBrowser))]
    [NotifyPropertyChangedFor(nameof(CanSummarizeSelected))]
    private CatalogRow? _selected;

    public bool HasSelection => Selected is not null;
    public bool CanDownloadSelected => Selected?.Source.CanInAppDownload == true;
    public bool SelectionNeedsBrowser => Selected is not null && !Selected.Source.CanInAppDownload;

    /// <summary>KI-Zusammenfassung braucht die Detail-Beschreibung. Aktuell nur
    /// bei GIANTS-Rows verfügbar — modhoster/Hof Hirschfeld haben keinen HTTP-
    /// abgreifbaren Description-Text (Login-Pflicht bzw. Consent-Overlay).</summary>
    public bool CanSummarizeSelected =>
        Selected?.Source.Source == ModHubEntry.GiantsSource;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private bool _summaryVisible;

    [ObservableProperty]
    private bool _summaryBusy;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(ModHubCategory? value) => ApplyFilter();
    partial void OnSelectedSourceChanged(SourceFilterOption? value) => ApplyFilter();

    private async Task InitializeAsync()
    {
        var snapshot = _cache.Load(Language);
        _seenSnapshot = _cache.LoadSeenSnapshot(Language);
        if (snapshot is not null)
        {
            AddEntries(snapshot.Entries);
            Status = $"{Rows.Count} Mods aus Cache (Alter: {(int)(DateTime.UtcNow - snapshot.SavedUtc).TotalHours} h).";
        }

        _ = LoadCategoriesAsync();
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
        Status = "Katalog-Load …";
        try
        {
            var giantsTask = LoadGiantsAsync(ct);
            var hofTask = LoadHofHirschfeldAsync(ct);
            var modhosterTask = LoadModhosterAsync(ct);
            await Task.WhenAll(giantsTask, hofTask, modhosterTask);

            _cache.Save(_allEntries, Language);
            _cache.SaveSeenSnapshot(_allEntries.Select(e => e.DetailUrl), Language);
            Status = $"{_allEntries.Count} Mods im Katalog · {Rows.Count} sichtbar";
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

    private async Task LoadGiantsAsync(CancellationToken ct)
    {
        int page = 1;
        while (!ct.IsCancellationRequested)
        {
            var pageEntries = await _hub.FetchCatalogPageAsync(page, Language, ct);
            if (pageEntries.Count == 0) break;
            AddEntries(pageEntries);
            Status = $"GIANTS Seite {page} · {Rows.Count} sichtbar";
            page++;
            if (pageEntries.Count < 20) break;
            await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        }
    }

    private async Task LoadHofHirschfeldAsync(CancellationToken ct)
    {
        try
        {
            var slugs = await _hof.FetchCategorySlugsAsync(ct);
            foreach (var slug in slugs)
            {
                if (ct.IsCancellationRequested) return;
                var entries = await _hof.FetchCategoryPageAsync(slug, 1, ct);
                AddEntries(entries);
            }
        }
        catch (Exception ex) { Log.Debug(ex, "Hof-Hirschfeld-Load fehlgeschlagen"); }
    }

    private async Task LoadModhosterAsync(CancellationToken ct)
    {
        try
        {
            int page = 1;
            while (!ct.IsCancellationRequested)
            {
                var entries = await _modhoster.FetchCatalogPageAsync(page, ct);
                if (entries.Count == 0) break;
                AddEntries(entries);
                page++;
                if (entries.Count < 20) break;
                if (page > 20) break; // safety
                await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
            }
        }
        catch (Exception ex) { Log.Debug(ex, "Modhoster-Load fehlgeschlagen"); }
    }

    private int AddEntries(IEnumerable<ModHubEntry> entries)
    {
        int added = 0;
        var seen = new HashSet<string>(_allEntries.Select(e => e.DetailUrl), StringComparer.Ordinal);
        var newRows = new List<CatalogRow>();
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.DetailUrl)) continue;
            _allEntries.Add(entry);
            var row = new CatalogRow(entry) { IsNew = _seenSnapshot is not null && !_seenSnapshot.Contains(entry.DetailUrl) };
            if (RowMatchesFilter(row))
            {
                Rows.Add(row);
                newRows.Add(row);
            }
            added++;
        }
        if (newRows.Count > 0) _ = LoadCoversForAsync(newRows);
        return added;
    }

    private async Task LoadCoversForAsync(List<CatalogRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.Cover is not null) continue;
            if (string.IsNullOrWhiteSpace(row.Source.PreviewUrl)) continue;
            try
            {
                var path = await _previews.GetOrDownloadCoverAsync(row.Source.PreviewUrl);
                if (path is null || !File.Exists(path)) continue;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        using var s = File.OpenRead(path);
                        row.Cover = new Bitmap(s);
                    }
                    catch (Exception ex) { Log.Debug(ex, "Cover-Bitmap-Load {p}", path); }
                });
            }
            catch (Exception ex) { Log.Debug(ex, "Cover-Load fehlgeschlagen: {u}", row.Source.PreviewUrl); }
        }
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var e in _allEntries)
        {
            var row = new CatalogRow(e) { IsNew = _seenSnapshot is not null && !_seenSnapshot.Contains(e.DetailUrl) };
            if (RowMatchesFilter(row)) Rows.Add(row);
        }
        _ = LoadCoversAsync();
    }

    private async Task LoadCoversAsync()
    {
        _coverCts?.Cancel();
        _coverCts = new CancellationTokenSource();
        var ct = _coverCts.Token;
        // Snapshot der Rows um Race gegen ApplyFilter zu vermeiden.
        var snapshot = Rows.ToArray();
        foreach (var row in snapshot)
        {
            if (ct.IsCancellationRequested) return;
            if (row.Cover is not null) continue;
            if (string.IsNullOrWhiteSpace(row.Source.PreviewUrl)) continue;
            try
            {
                var path = await _previews.GetOrDownloadCoverAsync(row.Source.PreviewUrl, ct);
                if (path is null || !File.Exists(path)) continue;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        using var s = File.OpenRead(path);
                        row.Cover = new Bitmap(s);
                    }
                    catch (Exception ex) { Log.Debug(ex, "Cover-Bitmap-Load {p}", path); }
                });
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { Log.Debug(ex, "Cover-Load fehlgeschlagen: {u}", row.Source.PreviewUrl); }
        }
    }

    private bool RowMatchesFilter(CatalogRow row)
    {
        if (SelectedSource?.SourceKey is string src && !string.Equals(row.Source.Source, src, StringComparison.Ordinal))
            return false;

        var q = SearchText?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            if (!(row.Source.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || row.Source.Author.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || row.Source.Category.Contains(q, StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        if (SelectedCategory is not null && !string.IsNullOrEmpty(SelectedCategory.Filter))
        {
            if (!row.Source.Category.Contains(SelectedCategory.Label, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanDownloadSelected))]
    private async Task DownloadSelectedAsync()
    {
        if (Selected is null || !Selected.Source.CanInAppDownload) return;
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

    [RelayCommand(CanExecute = nameof(CanSummarizeSelected))]
    private async Task SummarizeSelectedAsync()
    {
        if (Selected is null) return;
        var ai = _aiFactory();
        if (ai is null)
        {
            _host.Notifications.Notify(
                "Ollama nicht konfiguriert — bitte im Einstellungen-Tab Endpoint/Modell setzen.",
                NotificationLevel.Warning);
            return;
        }

        var modIdMatch = System.Text.RegularExpressions.Regex.Match(
            Selected.Source.DetailUrl, @"mod_id=(\d+)");
        if (!modIdMatch.Success) return;
        int modId = int.Parse(modIdMatch.Groups[1].Value);

        SummaryVisible = true;
        SummaryBusy = true;
        SummaryText = $"Lade Detail-Beschreibung für \"{Selected.Source.Title}\" …";
        try
        {
            var detail = await _hub.FetchModDetailAsync(modId, Language);
            if (detail is null || string.IsNullOrWhiteSpace(detail.DescriptionText))
            {
                SummaryText = "Keine Beschreibung im Detail-Endpoint gefunden.";
                return;
            }

            SummaryText = $"KI-Zusammenfassung wird erstellt via {ai.Name} …";
            var systemPrompt = "Du bist ein deutschsprachiger LS25-Mod-Reviewer. " +
                "Fasse die Mod-Beschreibung in 3–5 Sätzen zusammen: " +
                "Was macht der Mod? Welche Fahrzeuge/Objekte/Features? Zielgruppe? " +
                "Kein Werbe-Sprech, sachlich.";
            var userPrompt = $"Titel: {detail.Title}\nAutor: {detail.Author}\n\nBeschreibung:\n{detail.DescriptionText}";
            var answer = await ai.CompleteAsync(systemPrompt, userPrompt);
            SummaryText = string.IsNullOrWhiteSpace(answer)
                ? "KI hat keine Antwort geliefert."
                : answer;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Summarize fehlgeschlagen für Mod {Id}", modId);
            SummaryText = $"Fehler: {ex.Message}";
        }
        finally
        {
            SummaryBusy = false;
        }
    }

    [RelayCommand]
    private void CloseSummary()
    {
        SummaryVisible = false;
        SummaryText = string.Empty;
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

    public string SourceLabel => Source.Source switch
    {
        ModHubEntry.GiantsSource => "GIANTS",
        ModHubEntry.HofHirschfeldSource => "Hof Hirschfeld",
        ModHubEntry.ModhosterSource => "modhoster",
        _ => Source.Source,
    };

    public bool CanInAppDownload => Source.CanInAppDownload;
    public bool NeedsBrowser => !Source.CanInAppDownload;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private bool _isNew;

    [ObservableProperty]
    private Bitmap? _cover;

    public string BadgeText => IsNew ? "NEU" : "";
}

public sealed record SourceFilterOption(string? SourceKey, string Label);
