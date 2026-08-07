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
        Version: "0.1.0",
        Author: "Kroste",
        Description: "Mod-Manager für den Landwirtschafts-Simulator 25 (FS25).");

    public IReadOnlyList<GameTarget> Targets { get; } = new[]
    {
        new GameTarget(
            "farming-simulator-25", "Farming Simulator 25",
            SteamAppId: 2300320,
            AlternativeExecutableNames: new[] { "FarmingSimulator2025.exe" },
            Platforms: Platforms.Both),
    };

    private IHostServices? _host;
    private readonly Dictionary<string, ModInstallService> _installers = new();
    private readonly Ls25PathResolver _paths = new();
    private readonly ModDescReader _reader = new();

    public Task InitializeAsync(IHostServices host, IReadOnlyList<DetectedGame> activatedGames, CancellationToken ct)
    {
        _host = host;
        foreach (var game in activatedGames)
        {
            var modsDir = _paths.GetModsDir(game);
            if (modsDir is null)
            {
                host.Logger.Warn("LS25: konnte keinen Mods-Pfad für {Game} ableiten", game.Target.DisplayName);
                continue;
            }
            _installers[game.Target.GameId] = new ModInstallService(modsDir, _reader);
            host.Logger.Info("LS25 initialisiert: Mods-Ordner = {Path}", modsDir);
        }
        return Task.CompletedTask;
    }

    public IEnumerable<IGameTabContribution> GetTabContributions(DetectedGame game)
    {
        if (!_installers.TryGetValue(game.Target.GameId, out var installer) || _host is null)
            yield break;

        yield return new InstalledTab(installer, _host);
    }

    public Task ShutdownAsync()
    {
        _host?.Logger.Info("LS25 shutdown");
        return Task.CompletedTask;
    }

    private sealed class InstalledTab : IGameTabContribution
    {
        private readonly ModInstallService _installer;
        private readonly IHostServices _host;

        public InstalledTab(ModInstallService installer, IHostServices host)
        {
            _installer = installer;
            _host = host;
        }

        public string Id => "installed";
        public string Label => "Installiert";
        public string Icon => "\U0001F69C"; // 🚜
        public int Order => 0;
        public bool IsVisible(DetectedGame game) => true;

        public Control CreateView(DetectedGame game, IHostServices host) =>
            new InstalledModsView { DataContext = new InstalledModsViewModel(_installer, _host) };
    }
}
