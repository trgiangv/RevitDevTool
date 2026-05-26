from __future__ import annotations

import json
from typing import Any

from .client import RevitApiDocsClient, parse_symbol_page, select_sections
from .config import validate_version
from .formatting import changed_sections, compact_entry, filter_results
from .release_diff import build_release_diff
from .store import RevitDocsStore, normalize_href


class RevitDocsService:
    def __init__(self, store: RevitDocsStore, client: RevitApiDocsClient) -> None:
        self.store = store
        self.client = client

    def close(self) -> None:
        self.client.close()
        self.store.close()

    def search(
        self,
        query: str,
        version: int,
        kind: str | None,
        namespace: str | None,
        limit: int,
    ) -> dict[str, Any]:
        version = validate_version(version)
        safe_limit = max(1, min(limit, 20))
        results = self.store.search(version, query, safe_limit)
        source = "local"

        if not results:
            online = self.client.search_online(version, query)
            self.store.upsert_entries(version, online, "online_search")
            results = self.store.search(version, query, safe_limit)
            source = "online_fallback"

        filtered = filter_results(results, kind, namespace)[:safe_limit]
        return {
            "version": version,
            "query": query,
            "source": source,
            "count": len(filtered),
            "results": [compact_entry(entry, version) for entry in filtered],
        }

    def get(
        self,
        symbol_or_href: str,
        version: int,
        sections: list[str] | None,
        max_chars: int,
    ) -> dict[str, Any]:
        version = validate_version(version)
        requested = sections or ["summary", "syntax", "parameters", "remarks", "exceptions"]
        safe_chars = max(1000, min(max_chars, 16000))
        entry = self.resolve_entry(version, symbol_or_href)
        href = entry["href"] if entry else normalize_href(symbol_or_href)
        if not href:
            raise ValueError(f"Could not resolve Revit API symbol or href: {symbol_or_href}")

        parsed, url, source = self._load_page(version, href)
        selected = select_sections(parsed, requested, safe_chars)
        return {
            "version": version,
            "symbol": entry["title"] if entry else parsed["title"],
            "href": href,
            "url": url,
            "source": source,
            "sections": selected,
            "api_card": parsed.get("api_card", {}),
            "members": parsed.get("members", [])[:80],
            "related": parsed.get("related", [])[:40],
        }

    def compare(
        self,
        from_version: int,
        to_version: int,
        symbol_or_href: str | None,
        max_chars: int,
        examples_per_kind: int,
        include_removed: bool,
    ) -> dict[str, Any]:
        if not symbol_or_href:
            return build_release_diff(
                self.client,
                from_version,
                to_version,
                examples_per_kind,
                include_removed,
            )

        left = self.get(
            symbol_or_href,
            version=from_version,
            sections=["summary", "syntax", "remarks", "exceptions"],
            max_chars=max_chars,
        )
        right = self.get(
            symbol_or_href,
            version=to_version,
            sections=["summary", "syntax", "remarks", "exceptions"],
            max_chars=max_chars,
        )
        return {
            "symbol": symbol_or_href,
            "from_version": from_version,
            "to_version": to_version,
            "same_href": left["href"] == right["href"],
            "changed_sections": changed_sections(left["sections"], right["sections"]),
            "from": left,
            "to": right,
        }

    def resolve_entry(self, version: int, symbol_or_href: str) -> dict[str, Any] | None:
        entry = self.store.find_entry(version, symbol_or_href)
        if entry:
            return entry
        result = self.search(symbol_or_href, version=version, kind=None, namespace=None, limit=5)
        if not result["results"]:
            return None
        href = result["results"][0]["href"]
        return self.store.find_entry(version, href) or {"href": href, "title": result["results"][0]["title"]}

    def _load_page(self, version: int, href: str) -> tuple[dict[str, Any], str, str]:
        page = self.store.get_page(version, href)
        if page:
            metadata = json.loads(page.get("metadata_json") or "{}")
            parsed = {
                "title": page["title"],
                "text": page["text"],
                "sections": json.loads(page["sections_json"] or "{}"),
                "members": metadata.get("members", []),
                "related": metadata.get("related", []),
                "api_card": metadata.get("api_card", {}),
            }
            return parsed, page["url"], "cache"

        url, html = self.client.fetch_symbol_page(version, href)
        parsed = parse_symbol_page(html)
        self.store.save_page(
            version,
            href,
            url,
            parsed["title"],
            parsed["text"],
            parsed["sections"],
            {
                "members": parsed.get("members", []),
                "related": parsed.get("related", []),
                "api_card": parsed.get("api_card", {}),
            },
        )
        source = "local_file" if url.startswith("file:") else "remote"
        return parsed, url, source
