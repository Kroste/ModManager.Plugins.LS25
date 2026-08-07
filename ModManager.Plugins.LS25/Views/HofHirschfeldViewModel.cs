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
/// VM für den Hof-Hirschfeld-Katalog-Tab. Anders als beim GIANTS-ModHub gibt
/// es hier keinen zentralen Index — wir iterieren über die Kategorien-Slugs
/// und je Kategorie über die Seiten. Kein In-App-Download (Consent-Overlay auf
/// der Site, siehe HofHirschfeldCatalogService); Detail-Klick öffnet den
/// Browser.
/// </summary>
public sealed partial class HofHirschfeldViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly HofHirschfeldCatalogService _service;
    private readonly IHostServices _host;

    private readonly List<ModHubEntry> _allEntries = new();
    private CancellationTokenSource? _loadCts;

    public HofHirschfeldViewModel(HofHirschfeldCatalogService service, IHostServices host)
    {
        _service = service;
        _host = host;
        _ = InitializeAsync();
    }

    public ObservableCollection<CatalogRow> Rows { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _status = "Kategorien werden geladen …";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private CatalogRow? _selected;

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(CatalogRow? value) => OnPropertyChanged(nameof(HasSelection));
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task InitializeAsync()
    {
        await RefreshCatalogAsync();
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsBusy = true;
        _allEntries.Clear();
        Rows.Clear();
        Status = "Kategorien werden geladen …";
        try
        {
            var categories = await _service.FetchCategorySlugsAsync(ct);
            int catsProcessed = 0;
            foreach (var slug in categories)
            {
                if (ct.IsCancellationRequested) break;
                Status = $"Kategorie {++catsProcessed}/{categories.Count}: {slug} …";
                var page = 1;
                while (!ct.IsCancellationRequested)
                {
                    var pageEntries = await _service.FetchCategoryPageAsync(slug, page, ct);
                    if (pageEntries.Count == 0) break;
                    AddEntries(pageEntries);
                    page++;
                    if (pageEntries.Count < 12) break;
                    await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
                }
            }
            Status = $"{Rows.Count} Mods aus {categories.Count} Kategorien.";
        }
        catch (OperationCanceledException) { /* silent */ }
        catch (Exception ex)
        {
            Log.Warn(ex, "Hof-Hirschfeld-Katalog-Load fehlgeschlagen");
            Status = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddEntries(IEnumerable<ModHubEntry> entries)
    {
        var seen = new HashSet<string>(_allEntries.Select(e => e.DetailUrl), StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.DetailUrl)) continue;
            _allEntries.Add(entry);
            var row = new CatalogRow(entry);
            if (RowMatchesFilter(row)) Rows.Add(row);
        }
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var e in _allEntries)
        {
            var row = new CatalogRow(e);
            if (RowMatchesFilter(row)) Rows.Add(row);
        }
    }

    private bool RowMatchesFilter(CatalogRow row)
    {
        var q = SearchText?.Trim();
        if (string.IsNullOrEmpty(q)) return true;
        return row.Source.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || row.Source.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenDetailInBrowser()
    {
        if (Selected is null) return;
        _host.Shell.OpenExternalUrl(Selected.Source.DetailUrl);
    }
}
