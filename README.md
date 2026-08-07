# ModManager.Plugins.LS25

[![CI](https://github.com/Kroste/ModManager.Plugins.LS25/actions/workflows/ci.yml/badge.svg)](https://github.com/Kroste/ModManager.Plugins.LS25/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Kroste/ModManager.Plugins.LS25)](https://github.com/Kroste/ModManager.Plugins.LS25/releases)

LS25-Mod-Manager als Plugin für den [Kroste ModManager](https://github.com/Kroste/Mod-Manager).
Extraktion des LS-ModManager-Kerns (List/Install/Enable/Disable/Uninstall) —
Katalog-Anbindung an ModHub/Hof Hirschfeld/modhoster, KI-Zusammenfassungen und
Backup/Restore folgen in späteren Versionen.

## Ziel-Spiel

- **Farming Simulator 25** (Steam App-ID 2300320)
  - Windows: `Documents\My Games\FarmingSimulator2025\mods\`
  - Linux (Proton): `<Proton-Präfix>/drive_c/users/steamuser/My Documents/My Games/FarmingSimulator2025/mods/`

## Aktueller Umfang (v0.1.0)

- Tab **Installiert**: alle Mods im Mods-Ordner listen, Aktiv/Inaktiv toggeln
  (`.zip.disabled`-Rename), lokale ZIP installieren (mit modDesc.xml-Validierung),
  deinstallieren, Mods-Ordner öffnen.

## Roadmap

- v0.2 — Katalog **ModHub** (giants-software.com)
- v0.3 — Katalog **Hof Hirschfeld**
- v0.4 — Katalog **modhoster.de**
- v0.5 — Backup/Restore, KI-Zusammenfassungen (via IHostServices AI-Provider)
- v0.6 — Preview-Bilder (DDS → PNG dekodieren)

## Installation

Aus dem [Release](https://github.com/Kroste/ModManager.Plugins.LS25/releases) das
ZIP entpacken nach:

- **Windows:** `%APPDATA%\ModManager\plugins\ls25\`
- **Linux:**   `~/.config/ModManager/plugins/ls25/`

Beim nächsten App-Start erkennt der Host das Plugin, ab v0.4 des ModManagers
läuft die Installation live über die Sidebar-Karte („Plugin verfügbar → ⬇ Installieren").

## Entwicklung

```bash
dotnet build
```

Braucht Zugriff auf das Kroste-GitHub-Packages-Feed:

- Im CI: `secrets.GITHUB_TOKEN` reicht (siehe `.github/workflows/ci.yml`).
- Lokal: `gh auth refresh -s read:packages` und danach
  `dotnet nuget update source kroste-github --username kroste --password $(gh auth token) --store-password-in-clear-text`.

## Lizenz

MIT — siehe [LICENSE](LICENSE).
