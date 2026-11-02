---
name: rhyciv-defork-identity
description: "rhYciv must carry no upstream Civ2-clone/axx0 identity — names, namespaces, folders, and data paths should all read as rhYciv."
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 88945ea0-4c80-4f07-843f-d4ed7bccda16
  modified: 2026-09-06T00:26:59.143Z
---

# rhYciv: remove all upstream identity

`~/rhYciv` (GitHub `crhy/rhYciv`) began as a fork of `axx0/Civ2-clone` and has
since diverged into a standalone, clean-room re-implementation. The user's
standing position, stated when scoping the cleanup tracked in issue #67
("Defork and massive codebase cleanup"), is that the codebase should retain
**no connection to axx0or to the original Civilization 2** in anything a
developer or user can see.

Concretely, the user asked to "clean EVERYTHING up, get rid of unnecessary
folders, move stuff into folders that make sense and have no connection with
Axxo or the original Civ 2." When offered a cautious option that would have
renamed only the solution file and left the `Civ2` / `Civ2Gold` / `Civ2TOT`
project names in place, the user explicitly chose the deeper option that also
renames the projects and rewrites their namespaces across the whole tree.

So when working in this repository, prefer the thorough option over the
conservative one for anything identity-related: project and assembly names,
namespaces, directory layout, on-disk data paths (the save directory was
`AxxCiv/Saves`), solution file names, and documentation wording. Do not assume
an upstream-derived name is worth keeping merely because renaming it produces a
large diff — the user has already accepted that cost. Attribution that is
legally required (`LICENSE`, `NOTICE.md`, and the asset provenance manifest)
is the exception and must stay accurate.
