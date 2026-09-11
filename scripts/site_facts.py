#!/usr/bin/env python3
"""The facts the website states about the build, read from the build itself.

The front page used to carry the version and the test count as typed-in text,
and both went stale: the download button offered 0.1.2 while 0.1.7 was the
release it linked to, and the page claimed 213 tests when the suite had grown
well past that. Anything the site says about the build is answered here instead,
and website/build.sh substitutes it at build time.

Usage: site_facts.py version|tests
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[1]
BUILD_PROPS = REPOSITORY / "Directory.Build.props"
TESTS = REPOSITORY / "RhyCiv.Tests"


def version() -> str:
    props = BUILD_PROPS.read_text(encoding="utf-8")
    prefix = re.search(r"<VersionPrefix>([^<]+)</VersionPrefix>", props)
    if prefix is None:
        raise SystemExit(f"Error: no <VersionPrefix> in {BUILD_PROPS.name}")
    suffix = re.search(r"<VersionSuffix>([^<]*)</VersionSuffix>", props)
    tail = suffix.group(1).strip() if suffix else ""
    return f"{prefix.group(1).strip()}-{tail}" if tail else prefix.group(1).strip()


def test_count() -> int:
    """Test methods in the suite.

    Counted from the source rather than from a run, so building the site does not
    depend on being able to execute the tests. A [Theory] runs once per case and
    so is undercounted here; the page says "automated tests", which the number of
    test methods answers honestly.
    """
    total = 0
    for source in TESTS.rglob("*.cs"):
        text = source.read_text(encoding="utf-8", errors="replace")
        total += len(re.findall(r"^\s*\[(?:Fact|Theory)\b", text, re.MULTILINE))

    if total == 0:
        raise SystemExit(f"Error: found no tests under {TESTS.name}")
    return total


def main(argv: list[str]) -> int:
    if len(argv) != 2 or argv[1] not in {"version", "tests"}:
        print(__doc__, file=sys.stderr)
        return 2

    print(version() if argv[1] == "version" else test_count())
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
