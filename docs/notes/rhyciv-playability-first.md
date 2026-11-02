---
name: rhyciv-playability-first
description: "On rhYciv, playability and responsiveness outrank every other kind of work, and are fixed before new features or polish."
metadata: 
  node_type: memory
  pinned: false
  originSessionId: e6e39c34-dbc8-4a30-a556-a6cf9f7c6cd1
  modified: 2026-09-09T07:27:38.394Z
---

rhY treats rhYciv as a game first and a codebase second. When he reported that
0.1.4 was "still very laggy and buggy, with the player having to hit the enter
key numerous times before he can move a unit or click on a city", he framed the
priority explicitly: "The most important thing: It must be playable... This is a
game. It must be responsive at all times. Go through absolutely every aspect of
the code, and let's solve this issue before we start working on additional
issues."

So when a responsiveness or playability complaint is open, it comes before
features, art, and refactoring, and it is worth auditing broadly rather than
patching the first plausible cause — rhY asked for every aspect of the code to
be gone through, not for a quick fix. Input that is dropped, frames long enough
to swallow a keypress, and anything that makes the player repeat an action are
all treated as defects of the same seriousness as a crash.

A practical note for that kind of audit: the game can be driven headlessly for
measurement without touching rhY's desktop. Run the Raylib front end under
`Xvfb` with `LIBGL_ALWAYS_SOFTWARE=1`, and use the built-in harness environment
variables — `RHYCIV_AUTOSTART`, `RHYCIV_AUTOSTART_SEED`, `RHYCIV_TEST_CITY`,
`RHYCIV_TEST_TURNS`, `RHYCIV_SHOT_DIR`/`RHYCIV_SHOT_INTERVAL`, and
`RHYCIV_FRAME_LOG` — to play turns, capture screenshots and report frame times.
