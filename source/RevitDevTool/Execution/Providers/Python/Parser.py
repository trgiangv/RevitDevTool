"""
Parser.py — PEP 723 dependency resolver for RevitDevTool.

Usage:
    pixi run python Parser.py <script.py>

stdin:
    Content of pixi.toml (or empty string when not yet created).

Output (stdout, JSON):
    {
        "requires_python": ">=3.11",
        "to_install": ["numpy==2.4.2", "black"]
    }

to_install contains only packages not yet explicitly declared in pixi.toml.
Transitive dependencies resolved by pixi are intentionally ignored.

Exit codes:
    0 — success
    1 — fatal error (bad TOML, invalid requirement, etc.)
"""

import json
import re
import sys
from pathlib import Path
import tomllib

from packaging.requirements import Requirement, InvalidRequirement
from packaging.utils import canonicalize_name
from packaging.specifiers import SpecifierSet
from packaging.version import Version, InvalidVersion

_BLOCK_RE = re.compile(
    r"^#\s*///\s*script\s*\n"
    r"((?:#[^\n]*\n)*?)"
    r"#\s*///\s*$",
    re.MULTILINE,
)
_STRIP_COMMENT_RE = re.compile(r"^#\s?", re.MULTILINE)


def _parse_script(script_path: Path) -> tuple[str, list[Requirement]]:
    """Extract (requires_python, [Requirement]) from a PEP 723 inline script."""
    try:
        source = script_path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError as e:
        raise RuntimeError(
            f"{script_path} must be UTF-8 encoded (PEP 723 requirement)"
        ) from e
    m = _BLOCK_RE.search(source)
    if not m:
        return "", []

    raw_toml = _STRIP_COMMENT_RE.sub("", m.group(1))
    metadata = tomllib.loads(raw_toml)

    reqs: list[Requirement] = []
    for raw in metadata.get("dependencies", []):
        try:
            reqs.append(Requirement(raw.strip()))
        except InvalidRequirement as e:
            print(json.dumps({"error": f"Invalid dependency '{raw}': {e}"}), file=sys.stderr)
            sys.exit(1)

    return metadata.get("requires-python", ""), reqs


def _parse_specifier(val: object) -> SpecifierSet:
    """Convert a raw pixi.toml dependency value to a SpecifierSet.

    Supported formats:
      ">=2.4.2,<3"                          → SpecifierSet(">=2.4.2,<3")
      "*"                                   → SpecifierSet()  (any version)
      {"version": "==3.1.0", "extras": []}  → SpecifierSet("==3.1.0")
    """
    if isinstance(val, str):
        spec_str = "" if val.strip() == "*" else val.strip()
    elif isinstance(val, dict):
        raw = str(val.get("version", ""))
        spec_str = "" if raw == "*" else raw
    else:
        spec_str = ""

    try:
        return SpecifierSet(spec_str)
    except Exception:
        return SpecifierSet()


def _managed_packages(pixi_toml_content: str) -> dict[str, SpecifierSet]:
    """Return {canonical_name: SpecifierSet} for packages explicitly declared
    in pixi.toml [dependencies] and [pypi-dependencies].
    Transitive dependencies and 'python' itself are excluded.
    Returns empty dict when content is empty or unparseable.
    """
    if not pixi_toml_content.strip():
        return {}

    try:
        data = tomllib.loads(pixi_toml_content)
    except tomllib.TOMLDecodeError:
        return {}

    managed: dict[str, SpecifierSet] = {
        canonicalize_name(name): _parse_specifier(val)
        for section in ("dependencies", "pypi-dependencies")
        for name, val in data.get(section, {}).items()
    }

    if "python" in managed:
        del managed["python"]
    return managed


def _needs_install(req: Requirement, managed: dict[str, SpecifierSet]) -> bool:
    """
    Return True when the package must be added via 'pixi add'.

    Rules:
    1. Name not in pixi.toml  → must install.
    2. Name present, script has no specifier  → skip (pixi manages it).
    3. Name present, script has specifier:
       - pixi.toml has no constraint ("*") → trust pixi, skip.
       - Otherwise check whether *every* version the script requires
         is also permitted by pixi.toml's constraint.
         If not → reinstall so pixi can update its constraint.
    """
    canonical = canonicalize_name(req.name)

    if canonical not in managed:
        return True

    if not req.specifier:
        return False  # any version is fine

    pixi_spec = managed[canonical]
    if not pixi_spec:  # pixi.toml says "*"
        return False

    # Probe: pick a representative version from the script's specifier
    # and check whether it is also allowed by pixi.toml's constraint.
    # We extract the version number embedded in the first operator clause.
    probe = _probe_version(req.specifier)
    if probe is None:
        return False  # can't determine → trust pixi

    return probe not in pixi_spec


def _probe_version(specifier: SpecifierSet) -> "Version | None":
    """Extract a representative Version to test from a SpecifierSet."""
    for spec in specifier:
        try:
            return Version(spec.version)
        except InvalidVersion:
            continue
    return None


def main(script_path: Path, pixi_toml_content: str) -> None:
    requires_python, reqs = _parse_script(script_path)

    if not reqs:
        print(json.dumps({"requires_python": requires_python, "to_install": []}))
        return

    managed = _managed_packages(pixi_toml_content)

    to_install = [str(req) for req in reqs if _needs_install(req, managed)]

    print(json.dumps({"requires_python": requires_python, "to_install": to_install}))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit("Usage: pixi run python Parser.py <script.py>  (pixi.toml content on stdin)")

    path = Path(sys.argv[1])
    if not path.exists():
        sys.exit(f"Script not found: {path}")

    pixi_toml = sys.stdin.read()

    try:
        main(path, pixi_toml)
    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)