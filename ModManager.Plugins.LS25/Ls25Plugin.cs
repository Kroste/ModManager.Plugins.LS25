using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;
using ModManager.Plugins.LS25.Views;

namespace ModManager.Plugins.LS25;

public sealed class Ls25Plugin : IGameModPlugin
{
    public PluginMetadata Metadata { get; } = new(
        Id: "kroste.ls25",
        DisplayName: "Landwirtschafts-Simulator 25",
        Version: "0.4.0",
        Author: "Kroste",
        Description: "Mod-Manager für den Landwirtschafts-Simulator 25 (FS25) mit ModHub-, Hof-Hirschfeld- und Modhoster-Katalog.");

    public IReadOnlyList<GameTarget> Targets { get; } = new[]
    {
        new GameTarget(
            "farming-simulator-25", "Farming Simulator 25",
            SteamAppId: 2300320,
            AlternativeExecutableNames: new[] { "FarmingSimulator2025.exe" },
            Platforms: Platforms.Both),
    };

    private IHostServices? _host;
    private Ls25Paths? _paths;
    private ModHubService? _hub;
    private HofHirschfeldCatalogService? _hofHirschfeld;
    private ModhosterCatalogService? _modhoster;
    private CatalogCache? _cache;
    private readonly Dictionary<string, ModInstallService> _installers = new();
    private readonly ModDescReader _reader = new();
    private readonly Ls25PathResolver _pathResolver = new();

    public Task InitializeAsync(IHostServices host, IReadOnlyList<DetectedGame> activatedGames, CancellationToken ct)
    {
        _host = host;
        _paths = new Ls25Paths(host);
        _cache = new CatalogCache(_paths);
        _hub = new ModHubService(_paths, host.CreateHttpClient("modhub"));
        _hofHirschfeld = new HofHirschfeldCatalogService(host.CreateHttpClient("hofhirschfeld"));
        _modhoster = new ModhosterCatalogService(host.CreateHttpClient("modhoster"));

        foreach (var game in activatedGames)
        {
            var modsDir = _pathResolver.GetModsDir(game);
            if (modsDir is null)
            {
                host.Logger.Warn("LS25: konnte keinen Mods-Pfad für {Game} ableiten", game.Target.DisplayName);
                continue;
            }
            _installers[game.Target.GameId] = new ModInstallService(modsDir, _reader, _paths);
            host.Logger.Info("LS25 initialisiert: Mods-Ordner = {Path}", modsDir);
        }
        return Task.CompletedTask;
    }

    public IEnumerable<IGameTabContribution> GetTabContributions(DetectedGame game)
    {
        if (!_installers.TryGetValue(game.Target.GameId, out var installer) || _host is null
            || _hub is null || _cache is null || _hofHirschfeld is null || _modhoster is null)
            yield break;

        yield return new InstalledTab(installer, _host);
        yield return new ModHubTab(_hub, _cache, installer, _host);
        yield return new HofHirschfeldTab(_hofHirschfeld, _host);
        yield return new ModhosterTab(_modhoster, _host);
        yield return new DownloadsTab(installer, _host);
    }

    public Task ShutdownAsync()
    {
        _hub?.Dispose();
        _hofHirschfeld?.Dispose();
        _modhoster?.Dispose();
        _host?.Logger.Info("LS25 shutdown");
        return Task.CompletedTask;
    }

    private sealed class InstalledTab : IGameTabContribution
    {
        private readonly ModInstallService _installer;
        private readonly IHostServices _host;
        public InstalledTab(ModInstallService installer, IHostServices host)
        { _installer = installer; _host = host; }
        public string Id => "installed";
        public string Label => "Installiert";
        public string Icon => "\U0001F69C";
        public int Order => 0;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new InstalledModsView { DataContext = new InstalledModsViewModel(_installer, _host) };
    }

    private sealed class ModHubTab : IGameTabContribution
    {
        private readonly ModHubService _hub;
        private readonly CatalogCache _cache;
        private readonly ModInstallService _installer;
        private readonly IHostServices _host;
        public ModHubTab(ModHubService hub, CatalogCache cache, ModInstallService installer, IHostServices host)
        { _hub = hub; _cache = cache; _installer = installer; _host = host; }
        public string Id => "modhub";
        public string Label => "ModHub";
        public string Icon => "\U0001F3EA"; // 🏪
        public int Order => 10;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new ModHubView { DataContext = new ModHubViewModel(_hub, _cache, _installer, _host) };
    }

    private sealed class HofHirschfeldTab : IGameTabContribution
    {
        private readonly HofHirschfeldCatalogService _service;
        private readonly IHostServices _host;
        public HofHirschfeldTab(HofHirschfeldCatalogService service, IHostServices host)
        { _service = service; _host = host; }
        public string Id => "hofhirschfeld";
        public string Label => "Hof Hirschfeld";
        public string Icon => "\U0001F3E1"; // 🏡
        public int Order => 15;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new HofHirschfeldView { DataContext = new HofHirschfeldViewModel(_service, _host) };
    }

    private sealed class ModhosterTab : IGameTabContribution
    {
        private readonly ModhosterCatalogService _service;
        private readonly IHostServices _host;
        public ModhosterTab(ModhosterCatalogService service, IHostServices host)
        { _service = service; _host = host; }
        public string Id => "modhoster";
        public string Label => "modhoster";
        public string Icon => "\U0001F310"; // 🌐
        public int Order => 17;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new ModhosterView { DataContext = new ModhosterViewModel(_service, _host) };
    }

    private sealed class DownloadsTab : IGameTabContribution
    {
        private readonly ModInstallService _installer;
        private readonly IHostServices _host;
        public DownloadsTab(ModInstallService installer, IHostServices host)
        { _installer = installer; _host = host; }
        public string Id => "downloads";
        public string Label => "Downloads";
        public string Icon => "\U0001F4E5"; // 📥
        public int Order => 20;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new DownloadsView { DataContext = new DownloadsViewModel(_installer, _host) };
    }
}
