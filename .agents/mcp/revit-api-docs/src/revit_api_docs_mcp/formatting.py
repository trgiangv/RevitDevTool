from __future__ import annotations

from typing import Any

from .store import normalize_href


def compact_entry(entry: dict[str, Any], version: int) -> dict[str, Any]:
    href = entry.get("href", "")
    compact = {
        "title": entry.get("title"),
        "short_title": entry.get("short_title"),
        "kind": entry.get("kind") or entry.get("tag"),
        "namespace": entry.get("namespace"),
        "member_of": entry.get("member_of"),
        "description": entry.get("description"),
        "href": href,
        "url": doc_url(version, href),
    }
    return drop_empty(compact)


def compact_release_entry(entry: dict[str, Any]) -> dict[str, Any]:
    return drop_empty(
        {
            "title": entry.get("title"),
            "kind": entry.get("kind") or entry.get("tag"),
            "href": entry.get("href"),
        }
    )


def filter_results(
    results: list[dict[str, Any]],
    kind: str | None,
    namespace: str | None,
) -> list[dict[str, Any]]:
    filtered = results
    if kind:
        filtered = [entry for entry in filtered if str(entry.get("kind", "")).casefold() == kind.casefold()]
    if namespace:
        filtered = [
            entry
            for entry in filtered
            if namespace.casefold() in str(entry.get("namespace", "")).casefold()
        ]
    return filtered


def doc_url(version: int, href: str) -> str:
    return f"{doc_url_prefix(version)}{normalize_href(href)}"


def doc_url_prefix(version: int) -> str:
    host = "revapidocs.com" if version >= 2027 else "www.revitapidocs.com"
    return f"https://{host}/{version}/"


def changed_sections(left: dict[str, str], right: dict[str, str]) -> list[str]:
    keys = sorted(set(left) | set(right))
    return [key for key in keys if left.get(key, "").strip() != right.get(key, "").strip()]


def drop_empty(data: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in data.items() if value not in (None, "")}

