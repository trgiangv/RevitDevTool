from __future__ import annotations

from typing import Any

from .client import RevitApiDocsClient
from .config import validate_version
from .formatting import compact_release_entry, doc_url_prefix
from .store import normalize_href


def build_release_diff(
    client: RevitApiDocsClient,
    from_version: int,
    to_version: int,
    examples_per_kind: int = 3,
    include_removed: bool = False,
) -> dict[str, Any]:
    from_version = validate_version(from_version)
    to_version = validate_version(to_version)
    safe_examples = max(1, min(examples_per_kind, 20))

    old_entries = entries_by_href(client.fetch_static_index(from_version))
    new_entries = entries_by_href(client.fetch_static_index(to_version))

    old_hrefs = set(old_entries)
    new_hrefs = set(new_entries)
    added = sorted_entries(new_hrefs - old_hrefs, new_entries)
    removed = sorted_entries(old_hrefs - new_hrefs, old_entries)

    response: dict[str, Any] = {
        "from_version": from_version,
        "to_version": to_version,
        "basis": "Navigation index href GUID comparison. Identifies added/removed documentation topics without fetching detail pages.",
        "url_prefix": doc_url_prefix(to_version),
        "counts": {
            "from_total": len(old_entries),
            "to_total": len(new_entries),
            "added": len(added),
            "removed": len(removed),
        },
        "added_by_kind": count_by_kind(added),
        "added_examples": examples_by_kind(added, safe_examples),
    }
    if include_removed:
        response["removed_by_kind"] = count_by_kind(removed)
        response["removed_examples"] = examples_by_kind(removed, safe_examples)
    return response


def entries_by_href(entries: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    return {
        normalize_href(str(entry.get("href", ""))): entry
        for entry in entries
        if normalize_href(str(entry.get("href", "")))
    }


def sorted_entries(hrefs: set[str], entries: dict[str, dict[str, Any]]) -> list[dict[str, Any]]:
    return [entries[href] for href in sorted(hrefs, key=lambda item: sort_title(entries[item]))]


def count_by_kind(entries: list[dict[str, Any]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for entry in entries:
        kind = entry_kind(entry)
        counts[kind] = counts.get(kind, 0) + 1
    return dict(sorted(counts.items(), key=lambda item: (-item[1], item[0])))


def examples_by_kind(entries: list[dict[str, Any]], limit: int) -> dict[str, list[dict[str, Any]]]:
    grouped: dict[str, list[dict[str, Any]]] = {}
    for entry in entries:
        kind = entry_kind(entry)
        grouped.setdefault(kind, [])
        if len(grouped[kind]) < limit:
            grouped[kind].append(compact_release_entry(entry))
    return dict(sorted(grouped.items(), key=lambda item: (-len(item[1]), item[0])))


def entry_kind(entry: dict[str, Any]) -> str:
    return str(entry.get("tag") or entry.get("kind") or "Other")


def sort_title(entry: dict[str, Any]) -> str:
    return str(entry.get("title") or entry.get("short_title") or "")

