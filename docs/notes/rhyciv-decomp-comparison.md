---
name: rhyciv-decomp-comparison
description: rhY cancelled the decompilation comparison for rhYciv; rules are to be learned black-box from the real Civ II install and rhY's saves instead
metadata:
  type: project
---

On 2026-10-08 rhY asked for a comparison of rhYciv gameplay with https://github.com/nikitastaf1996/civ2_decomp (a byte-matching decompilation of Civilization II for the PlayStation, no licence, functions named only by address, about 56% in C) and recommendations to make rhYciv follow it more closely.

The work is in `/home/rhy/civ2-compare/` (outside the rhYciv repo on purpose): `civ2_decomp/` clone, `tasks/` (c00 index, c01–c13 by area of `docs/CIV2-PARITY-DEEP-AUDIT.md`, c99 synthesis), `out/` notes and `RECOMMENDATIONS.md`, `status.txt`, runner `cqueue.sh` (waits for the Voxa night queue, then runs on Strata).

**Why the guardrails:** rhYciv is a clean-room, redistributable project (`docs/CLEAN-ROOM-STATUS.md`; see [[rhyciv-defork-identity]]). The notes describe behaviour in plain words only; no code, identifiers or strings from the decompilation go into rhYciv, and nothing in the rhYciv tree is changed by this work.

**How to apply:** treat the notes as leads, not facts (a small local model reading unnamed decompiled C); check certainty labels; rhY decides what is implemented, and new rules beyond plain play go behind advanced settings ([[rhyciv-advanced-settings-optin]]). I flagged the clean-room question to rhY; no answer yet as of writing.

**Update 2026-10-08 05:15:** rhY cancelled this comparison before it ran ("rather than doing the comparison with the decomp, let's figure out how we can make either you or strata play the game 1,000 times and learn the rules the same way I did"). Do not run the `~/civ2-compare` tasks. The replacement is black-box: rhY's Civilization II (Multiplayer Gold with the UI Additions patch) is at `/media/rhy/data/Gamez/civ2 Plus` (1.3 GB, ~300 of rhY's own .sav files from real games) and runs in the Bottles flatpak bottle "Civilization II" (runner soda-11.0-5, launched as `Civ2UIALauncher.exe` through the document portal). Plan proposed: (1) mine the existing saves with rhYciv's LegacySaves reader, (2) run the game headless under Xvfb with scripted keys and read results from saves, never on rhY's real desktop and with sound off, (3) run the same set-ups in rhYciv's engine and compare. The first headless launch was NOT done yet; ask rhY before it. Crash hunt for rhYciv: `~/rhyciv-crash/` (worktree `wt`, branch `crash-hunt-20261008`, runner `kqueue.sh`, logs from issue #203), queued after the Voxa night queue.
