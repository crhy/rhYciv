#!/usr/bin/env python3
"""Assert the build version and the newest AppStream release entry agree.

The version is declared in two places that cannot see each other:
Directory.Build.props, which stamps the assemblies, and the Flatpak AppStream
metainfo, which is what a software centre shows. A release built with those two
disagreeing is a silent packaging bug -- the bundle installs and runs, but
reports a version nobody can match to a commit -- so the quality gate fails
rather than letting a tag go out that way.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[1]
BUILD_PROPS = REPOSITORY / "Directory.Build.props"
METAINFO = REPOSITORY / "packaging" / "flatpak" / "io.github.crhy.rhYciv.metainfo.xml"
SITE = REPOSITORY / "website" / "index.html"

# What the website's source is allowed to say about the version: the placeholder
# website/build.sh fills in, and nothing else.
SITE_PLACEHOLDER = "__RHYCIV_VERSION__"


def build_version() -> str:
    props = BUILD_PROPS.read_text(encoding="utf-8")
    prefix = re.search(r"<VersionPrefix>([^<]+)</VersionPrefix>", props)
    if prefix is None:
        raise SystemExit(f"Error: no <VersionPrefix> in {BUILD_PROPS.name}")
    suffix = re.search(r"<VersionSuffix>([^<]*)</VersionSuffix>", props)
    tail = suffix.group(1).strip() if suffix else ""
    return f"{prefix.group(1).strip()}-{tail}" if tail else prefix.group(1).strip()


def newest_release() -> str:
    metainfo = METAINFO.read_text(encoding="utf-8")
    release = re.search(r'<release\s+version="([^"]+)"', metainfo)
    if release is None:
        raise SystemExit(f"Error: no <release version=...> in {METAINFO.name}")
    return release.group(1)


def site_version_is_typed_in() -> list[str]:
    """Versions written into the website's source rather than substituted.

    The front page carried "Download 0.1.2" as literal text and stayed there
    while five releases went out, so the button on the front page offered a
    version months behind the release it linked to. The page is built from a
    placeholder now, and any hand-typed version is a return of that bug.
    """
    if not SITE.exists():
        return []

    page = SITE.read_text(encoding="utf-8")
    if SITE_PLACEHOLDER not in page:
        return ["the placeholder is missing from the page entirely"]

    return sorted(set(re.findall(r"\b\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?\b", page)))


def main() -> int:
    build, newest = build_version(), newest_release()
    if build != newest:
        print(
            f"ERROR: Directory.Build.props declares {build} but the newest AppStream "
            f"release is {newest}. Add the release entry to "
            f"packaging/flatpak/io.github.crhy.rhYciv.metainfo.xml before tagging.",
            file=sys.stderr,
        )
        return 1

    typed_in = site_version_is_typed_in()
    if typed_in:
        print(
            f"ERROR: {SITE.relative_to(REPOSITORY)} states a version of its own "
            f"({', '.join(typed_in)}). The page must carry {SITE_PLACEHOLDER}, which "
            f"website/build.sh fills in, so the site cannot fall behind a release.",
            file=sys.stderr,
        )
        return 1

    print(f"Version {build} agrees with the AppStream metainfo and the website.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
