"""
Parser.py — PEP 723 dependency resolver for RevitDevTool.

Usage:
    python Parser.py <script.py>

stdin:
    Installed-package state in one of two formats (auto-detected):
      - JSON array from ``pip list --format=json``  (Pip backend)
      - TOML content of pixi.toml                   (Pixi backend)
      - Empty string when no state is available.

Output (stdout, JSON):
    {
        "requires_python": ">=3.11",
        "to_install": ["numpy==2.4.2", "black"]
    }

to_install contains only packages not yet installed / declared.

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


def _managed_packages(stdin_content: str) -> dict[str, SpecifierSet]:
    """Return {canonical_name: SpecifierSet} for packages already managed.

    Accepts two formats (auto-detected):
      - JSON array from ``pip list --format=json``
      - TOML from pixi.toml

    Returns empty dict when content is empty or unparseable.
    """
    text = stdin_content.strip()
    if not text:
        return {}

    if text.startswith("[{"):
        return _parse_pip_json(text)

    return _parse_pixi_toml(text)


def _parse_pip_json(text: str) -> dict[str, SpecifierSet]:
    """Parse ``pip list --format=json`` into {canonical_name: SpecifierSet}.

    pip list returns ``[{"name": "foo", "version": "1.2.3"}, ...]``.
    We build an exact ``==version`` specifier so _needs_install can do
    proper version comparison.
    """
    try:
        entries = json.loads(text)
    except json.JSONDecodeError:
        return {}

    managed: dict[str, SpecifierSet] = {}
    for entry in entries:
        name = entry.get("name", "")
        version = entry.get("version", "")
        if not name:
            continue
        spec = SpecifierSet(f"=={version}") if version else SpecifierSet()
        managed[canonicalize_name(name)] = spec
    return managed


def _parse_pixi_toml(text: str) -> dict[str, SpecifierSet]:
    """Parse pixi.toml [dependencies] and [pypi-dependencies]."""
    try:
        data = tomllib.loads(text)
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
    Return True when the package needs to be installed.

    Rules:
    1. Name not in managed set  → must install.
    2. Name present, script has no specifier  → skip (already managed).
    3. Name present, script has specifier:
       - Managed set has no constraint ("*") → trust existing, skip.
       - Otherwise check whether the version the script requires
         is also permitted by the managed constraint.
         If not → reinstall to satisfy the script's constraint.
    """
    canonical = canonicalize_name(req.name)

    if canonical not in managed:
        return True

    if not req.specifier:
        return False  # any version is fine

    managed_spec = managed[canonical]
    if not managed_spec:
        return False

    probe = _probe_version(req.specifier)
    if probe is None:
        return False

    return probe not in managed_spec


def _probe_version(specifier: SpecifierSet) -> Version:
    """Extract a representative Version to test from a SpecifierSet."""
    for spec in specifier:
        try:
            return Version(spec.version)
        except InvalidVersion:
            continue
    return None


def main(script_path: Path, stdin_content: str) -> None:
    requires_python, reqs = _parse_script(script_path)

    if not reqs:
        print(json.dumps({"requires_python": requires_python, "to_install": []}))
        return

    managed = _managed_packages(stdin_content)

    to_install = [str(req) for req in reqs if _needs_install(req, managed)]

    print(json.dumps({"requires_python": requires_python, "to_install": to_install}))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit("Usage: python Parser.py <script.py>  (installed state on stdin)")

    path = Path(sys.argv[1])
    if not path.exists():
        sys.exit(f"Script not found: {path}")

    stdin_content = sys.stdin.read()

    try:
        main(path, stdin_content)
    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)
