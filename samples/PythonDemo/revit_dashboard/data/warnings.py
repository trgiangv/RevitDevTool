"""Collect and classify Revit document warnings by severity."""

from typing import TYPE_CHECKING

import Autodesk.Revit.DB as DB

if TYPE_CHECKING:
    from revit_dashboard.contracts.payload import WarningItem

# ---------------------------------------------------------------------------
# Severity classification based on warning description patterns
# ---------------------------------------------------------------------------

_CRITICAL_PATTERNS = (
    "slightly off axis",
    "not enclosed",
    "duplicate",
    "identical instances",
    "completely inside another",
    "overlap",
    "document corruption",
)

_MODERATE_PATTERNS = (
    "not attached",
    "duplicate mark",
    "miss the target",
    "not clean",
    "not placed",
    "multiple rooms",
    "join",
)


def _classify_severity(description: str, revit_severity: int) -> str:
    """Classify a warning into critical / moderate / info.

    Uses Revit's ``FailureSeverity`` enum value **plus** pattern matching
    on the description text for finer-grained categorisation.

    Args:
        description: The warning message text.
        revit_severity: Integer value of ``FailureSeverity`` enum
            (0 = None, 1 = Warning, 2 = Error, 3 = DocumentCorruption).
    """
    # DocumentCorruption or Error → always critical
    if revit_severity >= 2:
        return "critical"

    desc_lower = description.lower()

    for pattern in _CRITICAL_PATTERNS:
        if pattern in desc_lower:
            return "critical"

    for pattern in _MODERATE_PATTERNS:
        if pattern in desc_lower:
            return "moderate"

    return "info"


def _categorise_warning(description: str) -> str:
    """Assign a human-readable category label based on the description."""
    desc_lower = description.lower()

    if any(kw in desc_lower for kw in ("room", "area", "enclosed")):
        return "Rooms"
    if any(kw in desc_lower for kw in ("join", "wall", "attach")):
        return "Joins"
    if any(kw in desc_lower for kw in ("tag", "annotation", "text")):
        return "Annotations"
    if any(kw in desc_lower for kw in ("duplicate", "identical", "mark")):
        return "Duplicates"
    if any(kw in desc_lower for kw in ("axis", "offset", "slope", "geometry", "inside")):
        return "Geometry"
    if any(kw in desc_lower for kw in ("analytical", "structural", "beam", "column")):
        return "Structure"
    if any(kw in desc_lower for kw in ("parameter", "value", "formula")):
        return "Parameters"
    return "General"


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------


def collect_warnings(doc: DB.Document) -> list[WarningItem]:
    """Collect all warnings from the Revit document.

    Returns a list of ``WarningItem`` dicts ready for JSON serialisation.
    """
    warnings: list[WarningItem] = []

    try:
        failure_messages = doc.GetWarnings()
    except Exception as exc:
        print(f"[Warnings] Failed to get warnings: {exc}")
        return warnings

    if failure_messages is None:
        return warnings

    for idx, fm in enumerate(failure_messages):
        try:
            description = fm.GetDescriptionText() or ""
            severity_enum = fm.GetSeverity()
            severity_int = int(severity_enum) if severity_enum is not None else 1

            failing_ids: list[int] = []
            try:
                for eid in fm.GetFailingElements():
                    failing_ids.append(eid.IntegerValue)
            except Exception:
                pass

            # Also include additional elements
            try:
                for eid in fm.GetAdditionalElements():
                    if eid.IntegerValue not in failing_ids:
                        failing_ids.append(eid.IntegerValue)
            except Exception:
                pass

            warnings.append({
                "id": idx,
                "description": description,
                "severity": _classify_severity(description, severity_int),
                "element_ids": failing_ids,
                "category": _categorise_warning(description),
            })
        except Exception as exc:
            print(f"[Warnings] Skip warning {idx}: {exc}")

    return warnings
