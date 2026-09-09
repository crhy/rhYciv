**The interface, and a government you can change.**

Everything here came out of the 0.1.5 report in issue #115, or was found while
working through it. Two of them turned out to be features that had shipped and
never once run.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.1.6-win-x64.zip` — unzip, run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.1.6-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.1.6-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.1.6-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.1.6-x86_64.flatpak` |

Nothing else is needed. No commercial Civilization II installation, no runtime to install — each download carries its own .NET runtime and the complete art set.

### The builds are unsigned — please read this before reporting a launch failure

They are not code-signed, because signing certificates cost money this project does not have yet. Both desktop platforms will try to stop you:

**macOS** will say the app "is damaged and can't be opened" or is from an unidentified developer. It is not damaged; that is the quarantine flag on anything downloaded unsigned. Clear it:

```
xattr -dr com.apple.quarantine /Applications/rhYciv.app
```

**Windows** will show a SmartScreen warning. Choose *More info* → *Run anyway*.

**Linux Flatpak**:

```
flatpak install --user ./rhYciv-0.1.6-x86_64.flatpak
flatpak run io.github.crhy.rhYciv
```

## You can change how you are governed

REVOLUTION has been in the Kingdom menu since the menus were written with **nothing behind it** — drawn, and doing nothing when clicked. A civilisation was handed Despotism when the game began and stayed under it for the rest of the game however far it researched: Monarchy, The Republic, Communism and Democracy could all be discovered and none of them could be adopted, which takes the whole middle of Civ II off the board.

A revolution now costs two turns of Anarchy, and when those run out your people ask what they are to become. Researching an advance that opens a government offers the revolution there and then rather than leaving you to notice. Adopting one brings every city's rates, unit support, corruption and waste into line with it, and trims a rate the new government does not allow rather than letting an illegal one quietly stay in force. Computer civilisations revolt too — one left under Despotism all game is not playing the same game as you.

## Diplomats and Spies do something at last

The Diplomat work that shipped in 0.1.4 **had never once run**. Every dialog it asks for by name was missing from the game's text, so the popup was looked up, not found, and nothing was shown: walking a Diplomat onto a lone enemy unit or an enemy city put up no offer whatever, and the unit stood there having achieved exactly what it achieved before the feature was written.

With that fixed, bribing a unit and inciting a city work. Two more are new:

**Investigate City** brings back a full account of somebody else's city — what it is building, what it has built, and, the reason anybody does this before an attack, what is standing in it. The city opens in a view-only window: Buy, Change and Rename are not there at all rather than present and inert, its citizens cannot be moved about, and its garrison cannot be given orders.

**Steal Technology** carries out one of its owner's secrets — an advance they know and you do not. **A city can only be robbed once**, however many agents follow; otherwise a rival capital is an endless supply of technology to anybody willing to keep building Diplomats.

Throughout, the Civ II cost: **a Diplomat does not come home from any of it, and a Spy does**, having spent a move. That difference is the whole argument for researching the Spy.

## Dialogs

**Every message is narrower and taller.** They were set to 62% of the window, about a hundred and forty characters to a line at 1080p, which is hard to read and left each message as one long band across the middle of the screen. They are set to roughly the measure a book is set to now and grow downwards into a paragraph. A dialog is also never narrower than a line it is not allowed to break, so a long web address no longer runs out through the side of the frame.

**Messages wait their turn.** They were released the moment the one before closed, without looking at what that message had opened — so answering "zoom to city" on a disorder report put the city window up and the next report straight on top of it, covering the city you had just asked to look at. A queued message is now released only when nothing is in the way: no window open, nothing still being played out on the map. A dialog you asked for yourself is not held back.

Links to the site, Discord and Telegram are in the About dialog.

## Lists

**The Go To dialog can be left.** It offers Ok and Cancel, and anything that was not Ok was taken as a request to swap between your own cities and everybody's — so Cancel reopened the dialog with the other list instead of closing it, and the only way out was to send the unit somewhere. The toggle has a button of its own now.

**Find City finds the city.** Its Ok was an empty branch with the one line that would have done the work commented out, so the command did nothing at all.

**Typing a letter jumps to the next entry under it**, at any list in the game — press R again for the next city under R. Row text is larger, and the research chooser's buttons read Goal, Info, Ok.

## The city

**Every square on the resource map can be clicked.** The click worked out which square you meant from scratch — its own division of the position, its own corrections for the diagonal edges of a diamond, and an adjustment its author left a comment saying he did not understand — while drawing the same map used none of that. Squares the two disagreed about could not be selected at all.

**Taking a city names it and opens it**, and losing one says so; both were empty methods. **The size beside a city's name keeps up with what it builds** — building a settler takes a citizen and nothing told the map. **An expensive item's shields fill the production box** in rows, closing up and overlapping when there are too many to fit, rather than collapsing to a single row.

## Rules

**A starving city disbands a settler it supports before it loses a citizen**, as Civ II does. The rest of the food box was checked against the original and matches.

**A sleeping unit wakes when something hostile steps alongside it.** It used to sleep through anything, so a stack left to hold a pass could be walked round, or attacked, without ever being offered to you. Fortified units are deliberately left alone.

**Combat draws from the game's seeded generator**, so a battle can be replayed from a saved game. It used its own random source, which made it the one part of a game that could not be reproduced from a report.

## Also

City names and population numbers on the map are readable — 28px and 23px rather than 22 and 18. Left and right step between cities alphabetically. A list row scrolled out of view no longer paints its selection band out over the map.

## Known limitations

Diplomacy, multiplayer and several advisor screens are not implemented yet. Some interface art is still placeholder. Scripted Lua dialogs do not work at all — see #110.

Each download is about 190 MB because it is fully self-contained. It cannot be trimmed: the game discovers its interface implementations by reflection at startup, and trimming removes exactly those assemblies.

## Reporting problems

Please open an issue at https://github.com/crhy/rhYciv/issues. Say which platform and which download, and attach the log from:

- **Linux** `~/.local/share/rhYciv/Logs` (Flatpak: `~/.var/app/io.github.crhy.rhYciv/data/rhYciv/Logs`)
- **Windows** `%LOCALAPPDATA%\rhYciv\Logs`
- **macOS** `~/Library/Application Support/rhYciv/Logs`
