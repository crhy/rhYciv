**Saved games that can be loaded again, and cities that grow.**

Two faults in this release had been quietly ruining whole games. Neither of them
looked like a fault in any one place: a saved game reported success and was
written unreadable, and cities stopped growing because of a rule about what a
civilisation can see.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.1.8-win-x64.zip` — unzip, run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.1.8-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.1.8-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.1.8-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.1.8-x86_64.flatpak` |

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
flatpak install --user ./rhYciv-0.1.8-x86_64.flatpak
flatpak run io.github.crhy.rhYciv
```

## Your saved games open again

Saving worked. Loading did not, and the reason was three steps removed from
anything about saving.

The save writer asks .NET what type each field is and writes it accordingly. A
field that holds *a value, or nothing at all* — a research goal that may not have
been chosen yet — is a kind of type that answers "object" to that question, so
the writer took the branch that writes an object and produced `{}` where a number
belonged. Nothing complained. The failure came on the way back in, where the
reader wants a number, finds an object, and gives up on the entire file.

A research goal is set the first time you answer the research prompt. So in
practice **every real saved game was written unreadable**: the game said the save
had succeeded, and would not open it again. The turns of anarchy after a
revolution and a city's stolen technology went the same way.

The writer produces a number now, and the reader accepts the damaged form as
"not set" — so **the saves already on your disk open** rather than being lost.

Telling you the save could not be read used to crash on its own account, which is
the worst possible moment for a second fault. That is fixed too.

## Cities grow

A citizen is only put to work on a square its civilisation can see, and founding
a city revealed nothing at all: the engine marked no squares, and the interface
marked only the one the settler was standing on. A new city therefore worked its
own centre square and nothing else — two food produced, two food eaten, no
surplus, and no growth ever — and sat at size one until a unit happened to wander
across its fields.

Nothing about that looks wrong in any one place. The food box is right, the
growth rule is right, and the worker assignment is right. They simply could not
agree.

What it looked like from the outside was a game where nothing ever got bigger: a
saved game from **AD 1220, turn 162**, held thirty-four cities across the whole
world and the largest was **size four**. A city now goes from size one to size
three inside thirty turns.

## Clicks that register

Clicking away to another window and clicking back stopped units being selectable.
Whether a click could begin on a control was decided once, when the pointer
entered it — and entering with a button already down, which is exactly what the
click that raises the window looks like, left it refusing clicks with nothing to
re-arm it. The map is one control filling most of the window, so there was
nowhere to leave and re-enter: the only way out was to sweep the pointer over the
menu bar and back.

## Winning and losing end the game

Both used to be a message and nothing more. A player whose last city fell was
told their civilisation had passed into memory, and was then left sitting in a
game they no longer had a civilisation in: no turn ever came round to them again,
and the only way out was to quit the program. Winning was the same — the world
was yours, and then you went on playing it. Either screen now returns you to the
main menu.

## Zoom follows the pointer

Ctrl and the wheel changed the zoom and left the view centred on the active unit,
so the square you were aiming at slid away from the cursor — worse the further it
was from the unit. Zooming in now anchors on the square under the pointer, so
what you are pointing at stays where it is. Zooming back out past normal lets go,
and the view returns to following the unit whose turn it is.

The horizontal shift that decides which column of a round world the drawing
starts from was also being changed while the whole map was on screen, where it is
not used — so selecting a unit while zoomed out quietly rotated the world
sideways, and the rotation only appeared on the next step of zoom.

## A city says everything it has to say at once

A city that came out of disorder and finished a unit in the same turn asked
twice, and answering "zoom to city" on the first opened the city window with the
second message still queued behind it — so the news arrived after you had already
looked at the city it was about. Everything one city has to report now arrives as
one message with one Zoom to City to answer.

## Smaller things

- **City names on the map** are sized to be read rather than growing with the
  zoom until the name is wider than the city. "Carthago" was being drawn three
  times the width of Carthage.
- **The fortification marker in the city window** is fitted to the unit standing
  in it, rather than to a map tile there is none of in a list row.
- **The website states the version it is actually offering.** The front page
  carried "Download 0.1.2" as typed-in text and stayed there while five releases
  went out. Everything the page says about the build is now read from the build.
- **A crash that kills the process before any handler can run leaves something
  behind.** The packaged launcher keeps what the game and the runtime printed, and
  the next launch folds it into the crash report — which is the difference between
  "it crashed on turn 49" and knowing why.
- **A repeating scheduled job runs more than once.** The scheduler replaced an
  entry of the same name in place, and the loop removed the entry after running
  it, so an action that asked to be run again wrote its next run into the very
  slot about to be removed.
