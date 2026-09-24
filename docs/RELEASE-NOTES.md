**Barbarians that fight fair, cease-fires that end, and a map that blends.**

0.2.2 was played and reported on. This release corrects the rules those reports questioned, each checked against Civ II's manual, the Civilization wiki and player findings, and ships the terrain and river art that was waiting on a branch.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.2.3-win-x64.zip` — **extract the folder first**, then run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.2.3-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.2.3-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.2.3-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.2.3-x86_64.flatpak` |

**Extract before running.** Windows produces `Could not load file or assembly 'System.Runtime'` when the `.exe` is double-clicked while still inside the zip. Nothing is wrong with the download.

The builds are unsigned. **macOS**: `xattr -dr com.apple.quarantine /Applications/rhYciv.app`. **Windows**: SmartScreen → *More info* → *Run anyway*.

## Rules

- **Barbarians are never veterans by default.** From King upwards they had been arriving as veterans; no Civ II source lists that, and in the original they do not. Difficulty still scales how hard they hit (part of #75, #134).
- **The barbarians never declare war.** They are always at war, so the declaration was noise (#181).
- **A cease-fire ends.** It runs out after five turns and the two civilisations return to no treaty, not war; tribute from either side restarts the count. The manual says "approximately 16", but cease-fires in the original end far sooner in play, and five is the ruling until one is timed (part of #174).
- **A finished building leaves production at once**, instead of sitting there and raising the "cannot build" message, and **selling a building pays one gold per shield** (#185).

## Diplomacy

The audience shows the leader large on the left with what is said centred on the right (#183). A new government offers "Revolt!" or "Keep" (#184), and breaking a treaty asks "Declare War" or "Cower in Fear".

## The map

Land blends from one terrain to the next instead of forming a quilt, the sea has no seams, and whales are drawn whole. Rivers follow the painted river, and shores are drawn from the land they meet.

## Research

`docs/CIV2-RULES-RESEARCH.md` now records, for each rule changed or confirmed, the sources checked, what each says, and where they disagree (#140, #149, #134, #174).
