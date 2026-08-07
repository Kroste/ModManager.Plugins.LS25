# ModManager.Plugins.LS25

## Grundlagen

- **Was:** LS25-Mod-Manager als Plugin für Kroste ModManager. Extraktion des
  LS-ModManager-Kerns; Zielspiel: FS25 (Steam App-ID 2300320).
- **Stack:** .NET 10, `Kroste.ModManager.PluginContracts` als PackageReference
  aus GitHub Packages, Avalonia (Compile-Zeit, kommt vom Host mit).
- **Repo:** `github.com/Kroste/ModManager.Plugins.LS25`
- **Deploy-Ziel:** `~/.config/ModManager/plugins/ls25/` (Linux) bzw.
  `%APPDATA%\ModManager\plugins\ls25\` (Windows).

## Aktueller Stand

**v0.1.0 (M3.1 — Skelett + Installiert-Tab):**
- `Ls25Plugin`: Entry-Point, ein Target (FS25/2300320).
- `Ls25PathResolver`: Findet Mods-Ordner via Host-liefertem `DetectedGame.UserDataDir`
  (Proton-Docs unter Linux) bzw. Windows Documents/My Games/FarmingSimulator2025.
- `ModDescReader`: liest modDesc.xml aus einer ZIP (Metadaten, DE/EN-Localized
  Title und Description, Author, Version, Multiplayer-Flag). Übernommen aus LS-ModManager.
  Preview-Extraction (DDS → PNG) fehlt noch (v0.6).
- `ModInstallService`: List (aktive + `.zip.disabled` inaktive), Install (mit
  modDesc.xml-Validierung), Uninstall, SetEnabled (.zip.disabled-Toggle).
- `InstalledModsView` + `InstalledModsViewModel`: Code-only Avalonia-View
  (kein XAML im Plugin — vermeidet Compiled-Bindings-Duplizierung). Toolbar:
  Install-ZIP, Refresh, Mods-Ordner öffnen, Toggle Enable/Disable, Uninstall
  (mit Confirm-Dialog). Zeigt Titel/Author/Version/Größe/State pro Mod.

## Roadmap

- **v0.2** — Katalog-Support: ModHub-Service (HTML-Scraping, Kategorien,
  Suche, Download); eigenes ModHub-Tab. Übernahme aus LS-ModManager
  `Services/ModHubService.cs`.
- **v0.3** — Hof Hirschfeld-Katalog.
- **v0.4** — modhoster-Katalog.
- **v0.5** — Backup/Restore, KI-Zusammenfassungen (via IHostServices AI-Provider
  wenn im Host verfügbar).
- **v0.6** — Preview-Bilder aus ZIPs (DDS → PNG via Pfim + SkiaSharp) + Cover-Column
  in der Installiert-Liste.

## Referenz

- **Kein XAML im Plugin**: Views sind Code-Behind-only (Avalonia-XAML im Plugin
  würde einen doppelten Compiled-Bindings-Loader im Prozess bedeuten und
  Style-Resource-Casts brechen; sehr fragil in isolierten AssemblyLoadContexts —
  wir haben zwar keinen LoadContext, aber Code-only ist trotzdem stabiler).
- **NuGet-Feed:** `nuget.config` bindet `kroste-github` (GitHub Packages) ein.
  Lokal braucht der Entwickler-PAT mit `read:packages`-Scope. CI nutzt `GITHUB_TOKEN`.
- **Plugin-Bundle:** Release-Workflow packt `ModManager.Plugins.LS25.dll` +
  `plugin.json` als `ModManager.Plugins.LS25-X.Y.Z.zip`.
- **LS-ModManager-Standalone**: bleibt existieren, wird nicht angetastet. Ab v0.1
  landen neue LS25-Features hier im Plugin, nicht mehr im standalone LS-ModManager.
