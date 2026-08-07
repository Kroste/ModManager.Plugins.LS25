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
/// VM für den modhoster.de-Katalog-Tab. modhoster hat einen offiziellen
/// JSON-Endpunkt (/mods.json?game_id=1) mit Paginierung — anders als bei
/// Hof Hirschfeld iterieren wir Seiten, nicht Kategorien.
/// <para>Kein In-App-Download: modhoster verlangt Login-Session, robots.txt
/// sperrt Download-Endpunkte. Detail-Klick öffnet die Browser-Seite.</para>
/// </summary>
public sealed partial class ModhosterViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ModhosterCatalogService _service;
    private readonly IHostServices _host;

    private readonly List<ModHubEntry> _allEntries = new();
    private CancellationTokenSource? _loadCts;

    public ModhosterViewModel(ModhosterCatalogService service, IHostServices host)
    {
        _service = service;
        _host = host;
        _ = RefreshCatalogAsync();
    }

    public ObservableCollection<CatalogRow> Rows { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

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

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsBusy = true;
        _allEntries.Clear();
        Rows.Clear();
        int page = 1;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Status = $"Modhoster-Load: Seite {page} …";
                var pageEntries = await _service.FetchCatalogPageAsync(page, ct);
                if (pageEntries.Count == 0) break;
                AddEntries(pageEntries);
                page++;
                if (pageEntries.Count < 20) break;
                await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            }
            Status = $"{Rows.Count} Mods geladen.";
        }
        catch (OperationCanceledException) { /* silent */ }
        catch (Exception ex)
        {
            Log.Warn(ex, "Modhoster-Katalog-Load fehlgeschlagen");
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
            || row.Source.Author.Contains(q, StringComparison.OrdinalIgnoreCase)
            || row.Source.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenDetailInBrowser()
    {
        if (Selected is null) return;
        _host.Shell.OpenExternalUrl(Selected.Source.DetailUrl);
    }
}
