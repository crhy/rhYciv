**Whole features that shipped and had never once run.**

Every one of these was already in the game: written, saved, drawn, and unable to
happen. Diplomacy, trade routes, pollution and the Wonders report were all
carried in the save format and read by nothing, forty-five menu entries were
drawn with no command behind them, and the computer civilisations were not
researching anything at all.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.1.7-win-x64.zip` — unzip, run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.1.7-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.1.7-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.1.7-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.1.7-x86_64.flatpak` |

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
flatpak install --user ./rhYciv-0.1.7-x86_64.flatpak
flatpak run io.github.crhy.rhYciv
```

## You can talk to the other civilisations

Contact, cease-fires, peace treaties, alliances, embassies, reputation and
attitude have been carried in every saved game since the beginning and **read by
nothing**. Civilisations met by walking into each other, and from that moment
there was no way to end a war, start one deliberately, or make a gift. They
fought until one of them was gone.

Civilisations now meet when their units or cities come in sight of each other,
and a **Foreign Ministry** (Kingdom menu, or Ctrl+D) opens a parley with anyone
you have met: cease-fire, peace, alliance, a declaration of war, a gift of gold
or of an advance. Computer civilisations answer on their own terms — weighing
what they think of you against how the war is going — and **come asking for a
cease-fire themselves when they are losing**, so a war is no longer something
only you can end. A **Diplomat can establish an embassy**, which is the first
thing Civ II's Diplomat is for and the one thing this one could not do.

Treaties bind them the way Civ II's treaties bind, which is to say **not
reliably**. What holds a computer civilisation back is thinking well of you
rather than the piece of paper: nobody moves while their attitude is above
neutral, and below it they turn on the weak, on anyone whose economy has
eclipsed theirs, and on anyone whose own word is worthless. Breaking a treaty
costs **two black marks**, one fades every twenty-four turns times the
difficulty, and every civilisation that has met you thinks less of you for it.
The parley screen shows their opinion of you and your own reputation, because in
the original both are what you learn to read.

Nuclear Gandhi is reproduced deliberately. India beelines for fission and
rocketry, builds warheads ahead of anything else, and having built them stops
finding treaties interesting. A nuclear strike works as Civ II's does:
everything on the square dies, a city loses half its people, and the ground
around it is left poisoned.

## Caravans trade

Cities were given commodities to supply and demand, the city window had a line
for trade routes that read a literal **"+xx"**, the map drew the routes as golden
threads between cities, the save format carried them — and nothing in the game
could create one. A caravan reaching a foreign city was refused as a failed
attack; a caravan reaching one of its own cities simply stood there.

A caravan arriving now offers what Civ II offers: **open a trade route**, **put
its cargo into a wonder** the city is building, or go on to a better market. What
the route is worth is shown before the caravan is spent on it, since that is the
whole basis for deciding whether to trade here or push on. Routes follow Civ II's
arithmetic — the distance plus ten, times the trade the two cities make, over
twenty-four, halved on the same continent and halved again within one
civilisation — so the profitable routes are the long ones to strangers, and a
city that wants what the caravan carries pays twice over. **Routes then bring
their cities trade every turn**, which is what makes them worth opening.

## Wonders are unique, and the world hears about them

**Nothing stopped two civilisations — or two cities of one civilisation — from
each raising the Pyramids** and each taking the benefit. A finished wonder is now
withdrawn from every build list in the world, and a city that was building it is
told the race is lost and picks something else, keeping the shields it has spent.

Civ II's three notices all arrive: scaffolds going up are news everywhere, a
wonder passing three quarters of its cost is told to the civilisations racing for
the same one, and a completed wonder is announced to everybody. Capturing a city
that holds wonders reports them.

The **Wonders of the World** report (F7) built one blank row per city of your own
civilisation and filled in nothing at all, so the one place in the game that
answers "who has built what" answered nothing. It lists every wonder with the
city that holds it, or who is working on it, or that nobody has started.

## Pollution, and the climate

A city works out how much pollution it produces, the improvement exists with art
to draw it, settlers can be ordered to clear it, and the messages were written —
and **nothing ever put a single square of it on the map**, so Mass Transit, the
Recycling Center, the Solar Plant, Hoover Dam and the Eiffel Tower were all
defending against something that could not occur.

A city now rolls its pollution figure each turn against one square of its own
working radius. The square loses half its yield until it is cleaned, and you are
told and shown which one. Left uncleaned, enough of it shifts the climate: forest
becomes jungle, grassland swamp, plains and tundra desert. **Global warming is
off by default** and turned on in the new **Advanced Settings** dialog, along
with the cheat and editor menus, which no longer appear on the menu bar unless
you ask for them.

Computer civilisations' settlers could not improve terrain **at all** — they
founded cities and then wandered, so their land was never irrigated, mined or
roaded, and none of their pollution would ever have been cleaned. They work their
land now.

## Menus that lead somewhere

Forty-five entries across the Game, View, Orders, World, Cheat and Editor menus
were drawn with **no command behind them**: you clicked and nothing happened.
Most of them had working commands the menu simply never reached.

Build Mines, Build Fortress, Build Airbase and Clear Pollution were all
implemented and unreachable — the Orders menu's template for generating one entry
per terrain improvement was keyed to irrigation, so it generated nothing, and
Build Mines pointed at a command id the command does not have. Move Pieces, View
Pieces, Center View and Activate Unit had no commands at all and now do; V swaps
between moving and viewing as it does in Civ II. What is genuinely not
implemented is left out of the menus rather than advertised, and a test now fails
on any new entry that neither names a command nor says it should be hidden.

All eleven **City Report Options** were read from their dialog, written back,
defaulted to off, and then consulted by nothing: turning a message off left it
arriving anyway. They default on, as in Civ II, and each one now governs its
message.

## The difficulty levels differ

The difficulty was asked for at the start of every game, written into the save,
and read in three places. **Prince and Deity played very nearly the same game.**

Computer civilisations pay **160%** of the listed price for everything they build
on Chieftain, sliding to **80%** on Deity, and at Emperor and Deity their cities
bank extra shields on top — Civ II's production box squeeze. Barbarians attack at
**a quarter strength on Chieftain and half as hard again on Deity**, which is
most of what makes a landing frightening on the high levels. You always pay the
listed price and get no help at any level.

**Computer civilisations research now.** Without a Lua script to choose for them
they researched nothing whatever for the entire game, which is why they were
still in the bronze age when you reached the moon.

## A tutorial

The Tutorial help option has been written into every save and consulted by
nothing, because there was no tutorial to govern. It is on for Chieftain, Warlord
and Prince — the levels people learn on — and there is advice to go with it now:
**how the number pad moves a unit**, what a Settlers unit is for, what the city
window does, and how research is chosen. Each piece is shown once and remembered
between games; Advanced Settings has a checkbox to put it all back.

## Known limitations

Multiplayer, several advisor screens, the space race, and the map and rules
editors are not implemented. Some interface art is still placeholder. Scripted
Lua dialogs do not work at all — see #110. What still needs finishing is tracked
in detail in the 0.1.7 to-do issue.

Each download is about 190 MB because it is fully self-contained. It cannot be
trimmed: the game discovers its interface implementations by reflection at
startup, and trimming removes exactly those assemblies.

## Reporting problems

Please open an issue at https://github.com/crhy/rhYciv/issues. Say which platform and which download, and attach the log from:

- **Linux** `~/.local/share/rhYciv/Logs` (Flatpak: `~/.var/app/io.github.crhy.rhYciv/data/rhYciv/Logs`)
- **Windows** `%LOCALAPPDATA%\rhYciv\Logs`
- **macOS** `~/Library/Application Support/rhYciv/Logs`
