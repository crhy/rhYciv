<p align="center">
  <img src="RaylibUI/FOSSart/rhyciv-app-icon.png" alt="rhYciv" width="240">
</p>

<h1 align="center">rhYciv</h1>


rhYciv is a standalone, high-resolution turn-based empire strategy game. It
preserves the map, city, research, production, and tactical-unit rhythm of 1990s
4X games while deliberately focusing the endgame on conquest.

No commercial game installation, CD, DLL, font, rules file, artwork, or save
file is required. The repository is designed to be a redistribution-ready base
for downstream forks. rhYciv is an independent project and is not affiliated
with or endorsed by Take-Two Interactive, 2K, Firaxis, MicroProse, or any other
publisher of the Civilization series.

![The map screen: Celtic territory at turn 101, with coastline, resources and wildlife](website/screenshot-map.jpg)

*The map screen. Terrain, resources and wildlife are all replacement art; no commercial game files are used.*

## Gameplay scope

- Explore a procedurally generated world, found cities, improve terrain, trade,
  research technologies, build wonders, and wage war.
- Diplomacy and multiplayer are not implemented yet.
- New games default to conquest-only victory and permanent elimination.
- Spaceship victory, the throne room, animated advisors/high council, and wonder
  movies are outside the streamlined product scope.
- The renderer uses a 1920x1080 logical layout, native high-DPI drawing,
  128x64 working terrain tiles, 300x300 unit/citizen sources, and map zoom levels
  from -7 through 16.
- Land and air units have a rear-left silhouette shadow. Naval units have wakes
  and waterline splashes instead of land shadows.

See [the gameplay guide](docs/GAMEPLAY.md) for systems, differences, and controls.

## Download

**Version 0.2.3 runs on Linux, Windows and macOS.** Get it from the
[latest release](https://github.com/crhy/rhYciv/releases/latest):

| Platform | Download | Run |
|---|---|---|
| Windows x64 | `rhYciv-0.2.3-win-x64.zip` | unzip, run `RaylibUI.exe` |
| macOS (Apple silicon) | `rhYciv-0.2.3-osx-arm64.zip` | unzip, drag `rhYciv.app` to Applications |
| macOS (Intel) | `rhYciv-0.2.3-osx-x64.zip` | as above |
| Linux x64 | `rhYciv-0.2.3-linux-x64.tar.gz` | extract, run `./RaylibUI` |
| Linux Flatpak | `rhYciv-0.2.3-x86_64.flatpak` | see below |

Nothing else is required. Each download carries its own .NET runtime and the
complete art set.

```sh
flatpak install --user ./rhYciv-0.2.3-x86_64.flatpak
flatpak run io.github.crhy.rhYciv
```

### The builds are unsigned

Signing certificates cost money this project does not have yet, so macOS and
Windows will both try to stop an unsigned download.

On **macOS**, the app will be reported as damaged or from an unidentified
developer. It is neither; that is the quarantine flag. Clear it with:

```sh
xattr -dr com.apple.quarantine /Applications/rhYciv.app
```

On **Windows**, choose *More info* then *Run anyway* at the SmartScreen prompt.

Saves, logs and settings live in `rhYciv/` under the platform data directory:
`~/.local/share` on Linux, `%LOCALAPPDATA%` on Windows, and
`~/Library/Application Support` on macOS. Builds before 0.1.0 wrote to `AxxCiv/`;
that directory is migrated on first launch and left in place.

## Build and run

The desktop client requires the .NET 9 SDK:

```sh
dotnet run --project RaylibUI/RaylibUI.csproj
```

It launches directly with the bundled ruleset. An external compatible rules
directory is optional for mod and scenario testing.

Run the complete validation gate before distributing a build:

```sh
./scripts/quality_gate.sh
```

The gate verifies every attributed asset and the Civilopedia text, asserts that
the build version matches the AppStream metainfo, then restores, builds and
tests. The same script runs in CI on Linux, Windows and macOS, so what it says
locally is what CI will say. Flatpak instructions are in
[packaging/flatpak/README.md](packaging/flatpak/README.md).

## Essential controls

- `F11`: toggle borderless desktop resolution
- `Ctrl` + mouse wheel: zoom the map
- `Ctrl` + middle click: reset to 1:1 zoom
- Middle click/drag: center or pan the map
- Hold `Ctrl`: inspect the tile under the pointer
- Hold `Shift`: preview unit, road, or trade-route paths
- `Shift` + right click: move eligible units of the active type
- `Ctrl` + `Alt` + arrows: brightness/saturation; Page Up/Down: gamma; Home: reset

![Fusion Power advance artwork](RaylibUI/FOSSart/Advances/fusionpower.jpg)

*Advance artwork. Every shipped asset has a row in [ASSET-MANIFEST.tsv](ASSET-MANIFEST.tsv) recording its author and licence.*

## Forking and licensing

Code and project-original art are GPL-3.0-only unless an asset’s manifest row
says otherwise. Freeciv-derived standalone data remains GPL-2.0-or-later, and
Liberation fonts remain OFL-1.1. Every shipped media/data file has a pinned row
in [ASSET-MANIFEST.tsv](ASSET-MANIFEST.tsv).

Start with [the documentation index](docs/README.md),
[architecture guide](docs/ARCHITECTURE.md), [clean-room status](docs/CLEAN-ROOM-STATUS.md),
and [contribution rules](CONTRIBUTING.md). Legal and attribution notices are in
[NOTICE.md](NOTICE.md).
