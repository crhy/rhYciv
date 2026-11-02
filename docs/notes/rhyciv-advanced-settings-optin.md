---
name: rhyciv-advanced-settings-optin
description: "On rhYciv, new rules beyond plain Civ II play and any developer tooling go behind Advanced Settings toggles that default to off."
metadata: 
  node_type: memory
  pinned: false
  originSessionId: e6e39c34-dbc8-4a30-a556-a6cf9f7c6cd1
  modified: 2026-09-09T22:26:00.760Z
---

rhY wants anything in rhYciv that is not part of ordinary Civ II play to be an
opt-in toggle rather than something the player simply gets. When global warming
was implemented he asked, unprompted, to "make global warming a toggle option in
the advanced settings, and have it off by default", and immediately afterwards
said the same of the developer menus: "let's make the cheat and editing menus
toggle settings in advanced settings, and have them off by default".

So when adding a rule that changes how the game plays beyond what Civ II does by
default, or exposing developer tooling such as cheat or map-editor menus, put it
behind a checkbox in the Advanced Settings dialog and leave it off unless the
player turns it on. The reasoning is that a player who has not asked for a rule
should not have it happen to them, and the menu bar should be the one the game is
meant to be played with.

The mechanism in the codebase: `Settings` (Engine/src/Settings.cs) holds the
flags, persists them to appsettings.json so a choice survives between sessions,
and `SetAdvancedSettings` writes them; the dialog is `@ADVANCEDSETTINGS` in
Game.txt driven by `RaylibUI/RunGame/Commands/AdvancedSettings.cs`. Adding a
setting means a property plus its load/save lines, a checkbox line in the dialog,
and an entry in that command's checkbox list.
