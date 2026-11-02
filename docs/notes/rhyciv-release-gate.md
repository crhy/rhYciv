---
name: rhyciv-release-gate
description: rhYciv releases are tested by rhY as a locally built flatpak before anything is pushed.
metadata: 
  node_type: memory
  pinned: false
  originSessionId: e6e39c34-dbc8-4a30-a556-a6cf9f7c6cd1
  modified: 2026-09-09T08:02:40.230Z
---

rhY validates rhYciv releases by playing a locally built flatpak, not by reading
a diff. When asking for the 0.1.5 work he put it as: "let me know when there's a
local 0.1.5 flat pak I can try out... make sure to push that as well once the
local flat pack testing goes well."

So the end of a release-shaped piece of work on rhYciv is: bump
`Directory.Build.props` and add the matching `<release>` entry to
`packaging/flatpak/io.github.crhy.rhYciv.metainfo.xml` (the quality gate asserts
the two agree), run `scripts/quality_gate.sh`, build and install the bundle, and
then stop and tell rhY it is ready. Do not push until he has played it and said
it went well — the push is his call, gated on his own testing, not on the tests
passing.

The build is the two commands in `packaging/flatpak/README.md`:
`flatpak-builder --user --install-deps-from=flathub --force-clean
--repo=.flatpak-repo .flatpak-build packaging/flatpak/io.github.crhy.rhYciv.yml`,
then `flatpak build-bundle .flatpak-repo <name>.flatpak io.github.crhy.rhYciv
master --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo`. It takes
a few minutes. The installed bundle can be smoke-tested headlessly the same way
as a local build, by passing the `RHYCIV_*` harness variables through with
`flatpak run --env=...` against an Xvfb display.
