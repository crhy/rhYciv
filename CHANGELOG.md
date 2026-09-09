# Changelog

Notable changes to rhYciv. Entries reference the issue they close.

The AppStream release notes in `packaging/flatpak/io.github.crhy.rhYciv.metainfo.xml`
carry a shorter, user-facing summary of each release; this file is the full record.

## [0.1.6] — 2026-09-09

The interface, from a full pass over the 0.1.5 report (#115).

### Dialogs

- **Every message is narrower and taller.** They were set to 62% of the window,
  about a hundred and forty characters to a line at 1080p, which is hard to read
  and left each message as one long band across the middle of the screen. They are
  now set to roughly the measure a book is set to and grow downwards into a
  paragraph. A dialog is also never narrower than a line it is not allowed to
  break, so a long web address no longer runs out through the side of the frame.
- **Messages wait their turn.** They were released the moment the one before
  closed, without looking at what that message had opened: answering "zoom to
  city" on a disorder report put the city window up and the next report straight on
  top of it. A queued message is now released only when nothing is in the way — no
  window open, nothing still being played out on the map. A dialog the player asked
  for themselves is not held back.
- **Links to the site, Discord and Telegram are in the About dialog** (#107).

### Lists

- **The Go To dialog can be left.** It offers Ok and Cancel, and anything that was
  not Ok was taken as a request to swap between your own cities and everybody's, so
  Cancel reopened the dialog with the other list instead of closing it. The toggle
  has a button of its own now.
- **Find City finds the city.** Its Ok was an empty branch with the one line that
  would have done the work commented out, so the command did nothing at all.
- **Typing a letter jumps to the next entry under it**, at any list in the game.
  Press it again for the next one. Row text is larger (#97).
- **The research chooser reads Goal, Info, Ok.**

### The city

- **Every square on the resource map can be clicked.** The click worked out which
  square was meant from scratch — its own division, its own corrections for the
  diagonal edges of a diamond, and an adjustment its author recorded as not
  understanding — while drawing the same map used none of it. Squares the two
  disagreed about could not be selected at all.
- **Taking a city names it and opens it**, and losing one says so. Both were empty
  methods that did nothing.
- **The size beside a city's name keeps up with what it builds.** Building a settler
  takes a citizen, and nothing told the map, so the old number stayed until
  something unrelated redrew the square.
- **An expensive item's shields fill the production box** in rows, closing up and
  overlapping when there are too many to fit, rather than collapsing to a single
  row.

### Rules

- **A starving city disbands a settler it supports before it loses a citizen**, as
  Civ II does. The rest of the food box was checked against the original and
  matches; it is pinned with tests now.
- **A sleeping unit wakes when something hostile steps alongside it.** It used to
  sleep through anything. Fortified units are deliberately left alone.
- **Combat draws from the game's seeded generator**, so a battle can be replayed
  from a save. It used its own `System.Random`, which made it the one part of a
  game that could not be reproduced from a report.

### Drawing

- **A list row scrolled out of view no longer paints its selection band over the
  map.** Rows painted it before the base draw, and the base draw is where the
  visibility check lives.

## [0.1.5] — 2026-09-09

Responsiveness: the turn comes back to the player, and the map keeps up.

### The turn

- **One press of End Turn plays one turn, not one civilisation.** Starting a
  civilisation's turn returned to the caller and waited, which is right for the
  player and wrong for everybody else: a computer civilisation has nothing that
  will come back later and ask it to carry on, so it moved its first unit and
  stopped, holding the turn. With eight rivals on the board the player had to
  press Enter eight times, into a game that appeared to be ignoring them, before
  they could move again. Each computer civilisation now plays its whole turn and
  hands the world on.
- **Computer civilisations move their whole army, and their orders resolve.**
  Following on from the same fault, only the first unit of each rival ever moved,
  and none of their end-of-turn orders ran — a computer unit told to fortify never
  became fortified, and their settlers never finished a road or an irrigation
  ditch they had started.
- **A request for the next unit made from inside the last one is served, not
  dropped.** Choosing a unit tells the interface, the interface moves it, and
  finishing its move asks for the next one — from inside the call that is still
  choosing. That request was thrown away to stop the recursion; it is now
  remembered and served as soon as the call in progress finishes.

### Keyboard and mouse

- **A keypress is never lost, however long the frame took.** Keys were read by
  asking about every key on the keyboard once a frame. A key pressed and let go
  between two frames is already back up by the time it is asked about, so on a
  long frame the press simply never happened — the other half of having to press
  Enter several times. Keys now come from the window's queue of what was actually
  pressed.
- **The side panel keeps answering the mouse.** Almost everything in it is rebuilt
  whenever the active unit changes, and the screen went on delivering the mouse to
  the controls that had been replaced, so clicks in the panel did nothing until
  the pointer was moved away and back.

### The map

- **Redrawing the map is about ten times faster.** A screenful of map was composed
  by handing every tile to raylib's general-purpose image drawing, which resampled
  the tile to the size it is drawn at and threw the result away, once per tile per
  redraw. A full redraw took between seventeen and forty milliseconds and now
  takes two or three. Resampled tiles are kept beside the tiles they came from,
  and the blend is a straight run over rows.
- **A redraw happens when it is asked for.** The static view of the map ticks once
  every two seconds, and a redraw waited for that tick — so ground coming into
  view, a city founded, a unit lost or the grid being switched on could sit unseen
  for two seconds. A move being played out is still left to finish.
- **A rival's turn cannot queue up minutes of watching.** Every move the player can
  see is composed into a short animation the moment it happens; now that rivals
  play their whole turn at once, a turn spent in sight of a dozen units would have
  built and then played a dozen of them. Past a limit the move is drawn rather
  than animated.

### Research

- **A technology goal, and a Goal button that shows the way to it.** Name an
  advance to work towards and the research chooser will list only the advances
  that lead there — its outstanding prerequisites, and the goal itself once
  everything it needs is known. When nothing that can be started now brings the
  goal any nearer it says so rather than showing an empty list. The goal is kept
  in saved games and retires itself when it is reached.
- **The chooser no longer asks the same question twice.** The guard against a
  second chooser stacking behind the first was cancelled every turn by the
  backstop meant to protect it: the engine asks for research from the start of
  the turn's bookkeeping, which runs before the player is told the turn has
  begun, so the flag was cleared on the same turn it was set. It now lasts until
  the question is actually answered, and is only set when a chooser really went
  up.

### Diagnostics

- `RHYCIV_FRAME_LOG=<seconds>` reports how many frames the game is drawing and how
  long the worst one took. A game that stops answering the keyboard is nearly
  always a game whose frames have grown long enough to swallow a keypress between
  polls, which is hard to judge by eye.

## [0.1.4] — 2026-09-07

Saving, and the fixes from the 0.1.3 beta test (#113).

### Saving and loading

- **Save Game and Load Game work from the menu.** Both entries carried no command
  id, so they were drawn and did nothing when clicked. The commands behind them
  existed and were bound to Ctrl+S and Ctrl+L, so saving worked — but only if you
  already knew the shortcut.
- **Autosave each turn actually saves.** It has been a checkbox in Game Options for
  as long as the dialog has existed and nothing ever read it. It writes at the
  start of the player's turn, before anything has moved, so the newest autosave is
  always a position that can be picked up cleanly. Three slots rotate, written so
  that a failure part way through cannot destroy the one it is replacing.
- **Opening the Save dialog no longer crashes on a short leader name.** The
  suggested file name took the leader's first two characters with `Substring`,
  which throws on a one-letter leader or a blank name — before the dialog drew, so
  nothing on screen said why.

### The turn

- **Enter ends the turn on the first press.** Ending a turn walks every unit giving
  it its end-of-turn processing, and it returned the moment it met one that needed
  a decision, abandoning the rest of the list. So each press got only as far as the
  next such unit. It was not only slow: the units behind it were never processed at
  all, so a unit told to fortify did not become fortified — and did not get its
  defensive bonus — until whatever preceded it had been resolved.

### Diplomats

- **A Diplomat can buy units and cities.** It has no attack strength, so walking
  one into an enemy was refused outright and the unit was good for nothing. A lone
  unit in the open can be bribed; a stack cannot, and nor can a garrison inside a
  city — that is bought by inciting the city, which brings its defenders across. A
  capital cannot be incited at any price. Prices rise with the owner's treasury and
  fall with distance from the seat of their government.

### Rules

- **Huts use the original game's measured odds.** There are five outcomes, equally
  likely — tribes, gold, mercenaries, scrolls, barbarians — and an empty village is
  not one of them; it exists only as a consolation when one of the five cannot be
  delivered. It was a sixth outcome drawn as often as the rest. Tribes and
  barbarians are withheld in favour of mercenaries near a city, or before the
  finder has founded one.
- **Switching production says what it will cost.** The penalty for changing between
  a unit, a building and a wonder has always been charged; nothing said so, and the
  shields simply disappeared.

### The map

- **The coastline is dark water meeting a thread of beach.** It ran through
  saturated turquoise from 48 pixels out with a wide cream beach behind it, which
  drew a lit outline round every island. The shoreline wanders further off the line
  between its corners, so a coast is lobed rather than a chain of facets, and the
  water held open inside an enclosed square is lopsided instead of a perfect circle.
- **The generation matte is gone from the art.** Citizens in the city window were
  outlined and veiled in magenta and several city sprites had pink specks on the
  roofs. Edge pixels have the matte's share subtracted back out; opaque patches are
  removed where they reach outside the picture and painted in where they are sealed
  within it.
- **The marker for a unit killed in combat appears.** It decided who had died from
  the per-round hitpoint series, which records health at the *start* of a round —
  so the loser's last entry is its health just before the fatal blow, always above
  zero, and the pause and marker never once happened.
- **Production shields are an even block.** Rows all hold the same number and the
  block is as near square as the cost allows, instead of filling to the panel width
  and leaving a single shield stranded on the last row.
- **The flag over a city is three times the size**, and special resources sit high
  in their square so a whale breaches out of water rather than sand.

### Elsewhere

- **Left and right step between cities** from inside the city window.
- The caret in a text box is clamped rather than trusted, so a key arriving with it
  out of range cannot end a session.
- A hung CI runner no longer blocks every later run on master.

## [0.1.3] — 2026-09-06

Everything below was reported in the 0.1.2 beta test (#111).

### Fixed — the map

- **Roads and railways connect.** Each of the nine connection sprites is a
  half-spoke that has to run from the tile centre to the exact point on the
  boundary where the neighbour is reached, so the two halves meet. They were cut
  from the painted sources by measuring where the ink happened to lie, so they
  started somewhere near the middle and stopped somewhere near the edge and
  neither end landed where it had to. The geometry is now constructed and the
  painted material swept along it, so a spoke cannot be misplaced.
- **Rivers run as rivers, and reach the sea.** A river is one picture per tile,
  chosen by which of the four edge-sharing neighbours also carry water — sixteen
  distinct pictures. The bundled art was eight free-hand meanders handed out as
  `index % 8`, so the picture drawn bore no relation to where the river actually
  ran. All sixteen are now composed from half-spokes that meet on the boundary.
  River mouths had never been replaced at all and still came from the
  compatibility sheet, which is the coarse blue arc that appeared where a river
  met the coast; they are painted deltas now.
- **An ocean square is drawn as water.** The coastline set is chosen by how many
  of a square's four corners are land, and with three or four of them the
  shoreline never crossed the square, so the tile came out as solid grass — a
  one-square bay was a meadow, and the whales in it appeared to be breaching out
  of a field. Every one of the sixteen now keeps open water at its centre.
- **Irrigation and farmland are ploughed fields**, drawn at map resolution,
  rather than the compatibility sheet's 64x32 cell scaled up into a coarse blue
  lattice. Irrigation is hand-cut ditches; farmland is the same field
  cross-ploughed, and the channels intersect rather than one crossing over the
  other.
- **The flag over a city is sharp.** The high-resolution flags were in the art
  set but nothing loaded them, so the map drew the classic sprite — a dozen
  pixels across — enlarged to match a tile composed several times larger.
- **The goody hut is the painted art** rather than the generated sheet icon.
- **The map no longer scrolls off into the fog.** Movement was announced to every
  player who had ever *explored* the square it happened on, rather than to those
  who could see it now, so every enemy step through territory you had once walked
  was animated on your map and the view followed it into the dark.

### Fixed — playing a turn

- **A road is worth a third of a movement point to every unit.** Ground movement
  carried a rule that a unit whose whole allowance was one point spent all of it
  on any move costing less than a full point — which is every move along a road.
  So settlers, warriors, phalanxes and musketeers walked their own roads at one
  square a turn.
- **Enter ends the turn**, and the side panel says so, flashing "End of Turn
  (Press ENTER)" once every unit has moved. Nothing distinguished waiting for the
  turn to be ended from choosing to look around mid-turn.
- **A kill can be seen.** Combat ended on the last frame of the explosion and
  handed straight back, so a unit killed during someone else's turn was gone
  before it could be seen to die. The map now holds on the square for about a
  second and marks it with the fallen-soldier icon. Another civilisation's move
  is held at its destination for the same reason.
- **Huts usually hold something.** The six outcomes were drawn evenly, and since
  several of the others degrade into a consolation of their own, a good third of
  huts came up empty. The empty village is now the exception it is meant to be,
  and mercenaries arrive as soldiers instead of as a copy of whatever unit walked
  into the village — which had been handing a free Settlers to any settler that
  found a hut.
- **A new city builds something it can build.** The opening item was the cheapest
  thing in the whole ruleset, taken from tables that carry every slot the format
  defines including disabled ones costing nothing, so a new city routinely opened
  building an item that was not buildable and sat at zero shields.

### Fixed — elsewhere

- **The crash report for a session that died without a handler is produced on
  Windows too.** The record is held open for the length of a session, and Windows
  will not let an open file be renamed or removed unless it was opened to allow
  it, so promoting a leftover record into a crash report threw instead. It only
  bit when a session record was still open in the same process — a real previous
  session is a dead process, which holds no handle — but the file is now opened
  to allow the next launch to take it over regardless.

### Fixed — reading the screen

- **Small text is legible.** The font atlases are rasterised at 96 to 112 pixels
  and most text is drawn between 14 and 20; with only bilinear filtering, shrinking
  a glyph five times sampled a twentieth of the pixels it covered and dropped most
  of the stroke. They are mipmapped now.
- **City names in the Go To dialog** are set at a readable size. The listbox text
  size was 12, fixed when the interface was laid out against a much smaller window.
- **The Civilopedia's technology description** is inset from the edge of its panel
  rather than starting hard against the rule.
- **Production shields are justified** across the width of the box, so a row reads
  as a gauge, instead of being packed to the left and stopping.

## [0.1.2] — 2026-09-06

### Fixed

- **Crash after founding a city.** Asking for the next unit when none was left
  awaiting orders cleared the selection, which put the map into unit-moving mode,
  which asked for the next unit again — recursing until the stack overflowed and
  the process died. A StackOverflowException cannot be caught, so this left no
  crash report at all. Introduced in 0.1.1 by the fix for #74; with no unit to
  move the map now falls back to the view piece, and the engine refuses to be
  asked for a unit while it is already answering.

## [0.1.1] — 2026-09-06

### Fixed

- **A failed save no longer destroys the save it was replacing.** Saving opened the
  destination file with `FileMode.Truncate`, emptying an existing save before
  writing a byte of the new one, so anything that threw part way through
  serialisation left a fragment where a finished game had been. Saves are now built
  beside the target and moved into place only once complete.
- **Loading an unreadable save reports an error instead of crashing.** Nothing
  caught a failure on the load path, so choosing a corrupt or truncated save in the
  load dialog took the whole game down.
- **Crash on saving.** Once the barbarians had founded or taken a city, every save
  threw an IndexOutOfRangeException and the game was lost. The saved per-tribe city
  counter is a fixed-width array indexed by TribeId; the barbarians carry TribeId
  -1, and while the loading code already skipped them, the saving code did not.
  Tribes with no slot in the format are now dropped on write, as they already were
  on read.

- The Alt key no longer moves focus into the menu bar. It opened the menus and
  had no way to close them again, so the key was a one-way trip that had to be
  undone with the mouse. The menus are still reachable by clicking them. (#103)
- Citizens in the city window are justified to the left of their panel. They were
  centred, which meant a citizen moved every time the city grew or a worker became
  a specialist, so the faces never stayed where they were last clicked. (#64)
- The Supplies and Demands lines in the city window are inset from the panel
  border instead of rendering hard against it. (#83)
- Disbanding a unit from the city window clears it from the Units Present and
  Units Supported boxes straight away. Both boxes only rebuilt their contents when
  the window was rescaled, so a disbanded unit stayed listed until something else
  resized the window. If the disbanded unit was the active one, the map now moves
  to the next unit instead of leaving it blinking where it stood. (#94)
- A unit put to sleep to recover now wakes when it is back to full health, instead
  of staying asleep for a player who has stopped thinking about it. A unit that was
  already healthy when told to sleep is unaffected and stays asleep until woken,
  and a fortified unit stays fortified. (#96)
- The Change Production list keeps a stable order: units first, then improvements,
  each in the order the ruleset declares them. The list is appended to as advances
  are discovered, so an item unlocked mid-game used to land at the bottom of sixty
  entries rather than in its usual place — which is how a buildable Temple went
  unnoticed after Ceremonial Burial. (#91)
- **Crash:** researching an advance that enables a terrain improvement — fortresses,
  for one — took the game down with a NullReferenceException. The notification went
  through a player interface that nothing in the solution implements, so the field
  was always null and the message could never be shown.
- Units and improvements in the Change Production list are drawn at a readable
  size. Their icons were scaled by a constant that divided by 1024, the size the
  art used to be; it is now 300 square, so units rendered at about fourteen pixels
  and improvement icons at roughly one. Icons are now fitted to the row, so the
  same thing cannot happen again when art is redrawn at a new size. (#81)
- A unit that has finished its turn stops blinking. When no unit was left awaiting
  orders the game never cleared the selection, and because the active-unit setter
  refuses a unit whose turn has ended without clearing the previous one, whichever
  unit spent the last move point stayed selected and blinked for the rest of the
  turn. Finishing a turn by taking an order that occupies the unit — fortifying, or
  starting a road, mine or irrigation — now also moves on, where before only
  running the movement points to zero did. (#74)

### Changed

- The Civilopedia's city-improvement pages no longer offer a Description button.
  The description is already on the page it navigated away from. (#104)
- The app icon is the new shield logo, with its background removed so it sits
  correctly on a desktop, a dock and a store listing rather than as a black tile.
- The AppStream metainfo lists only 0.1.0 onward. AppStream orders releases by
  version rather than document order, so the pre-0.1.0 betas outranked 0.1.0 and
  software centres reported the wrong version. Those releases remain on GitHub
  Releases and in git history.

### Added

- **Crashes that no handler can catch are now reported.** A fault in a native
  library or the graphics driver kills the process outright, so the existing crash
  handler — which covers managed exceptions — never runs and the player is left
  with nothing to send. The game now keeps a running record of what it is doing,
  flushed as it happens: version, platform, each turn, each command, each dialog,
  and every save or load. A session that ends properly deletes it; one still
  present at the next launch is promoted to a crash report carrying the last thing
  the game managed to do. Managed crash reports carry the same recent activity.
- `RHYCIV_TEST_PRODUCTION` and `RHYCIV_TEST_ADVANCES` in the review harness, which
  open the Change Production list over the city window with a chosen number of
  advances already granted. The dialog is the hardest part of the city screen to
  reach by hand; opening it this way is what exposed the improvement-advance crash.

## [0.1.0] — 2026-09-05

The first release built for Linux, Windows and macOS together, and the first that
is not a beta. See [the release notes](docs/RELEASE-NOTES.md).

### Added

- Self-contained downloads for Windows x64, macOS (Apple silicon and Intel) and
  Linux x64, alongside the Linux Flatpak.
- Painted road and railway connection sprites, cut into the eight half-spokes the
  renderer composites per connected neighbour.
- Continuous integration running the quality gate on Linux, Windows and macOS.

### Changed

- The project is separated from its upstream fork. The solution, the projects, the
  engine namespace, the interface classes and the save directory all carry the
  rhYciv name. Saves written by earlier builds are migrated on first launch. (#67)
- The city window is finished in the same painted stone as the rest of the
  interface rather than flat grey.

### Fixed

- Road and railway connection sprites drew the full four-way rosette into all nine
  slots, so a tile with one neighbour showed roads running off every side.
