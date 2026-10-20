# -*- coding: utf-8 -*-
"""Collect Revit document warnings.

Severity is the ``FailureSeverity`` name from ``ToString``.
The page classifies the description text.
"""

from __future__ import division, print_function

from Autodesk.Revit import DB
from System.Collections.Generic import ICollection, IList

from revit_dashboard.log import get_logger

log = get_logger()


def collect_warnings(doc):
    # type: (DB.Document) -> list[dict[str, str | int | list[str]]]
    """Collect warnings from the Revit document.

    Each item is ``id``, ``description``, ``severity``, ``element_ids``.
    """
    failure_messages = _failure_messages(doc)
    if failure_messages is None:
        return []

    warnings = []
    for idx, failure in enumerate(failure_messages):
        item = _warning_item(idx, failure)
        if item is not None:
            warnings.append(item)
    return warnings


def _failure_messages(doc):
    # type: (DB.Document) -> IList[DB.FailureMessage] | None
    try:
        return doc.GetWarnings()
    except Exception as exc:
        log.warning("failed to read warnings: {0}".format(exc))
        return None


def _warning_item(idx, failure):
    # type: (int, DB.FailureMessage) -> dict[str, str | int | list[str]] | None
    try:
        return {
            "id": idx,
            "description": failure.GetDescriptionText() or "",
            "severity": _severity_name(failure),
            "element_ids": _element_ids(failure),
        }
    except Exception as exc:
        log.warning("skip warning {0}: {1}".format(idx, exc))
        return None


def _severity_name(failure):
    # type: (DB.FailureMessage) -> str
    severity = failure.GetSeverity()
    if severity is None:
        return ""
    return severity.ToString()


def _element_ids(failure):
    # type: (DB.FailureMessage) -> list[str]
    element_ids = _read_ids(failure.GetFailingElements())
    for extra in _read_ids(failure.GetAdditionalElements()):
        if extra not in element_ids:
            element_ids.append(extra)
    return element_ids


def _read_ids(elements):
    # type: (ICollection[DB.ElementId]) -> list[str]
    values = []  # type: list[str]
    try:
        for element_id in elements:
            if element_id.Equals(DB.ElementId.InvalidElementId):
                continue
            values.append(element_id.ToString())
    except Exception:
        return values
    return values
