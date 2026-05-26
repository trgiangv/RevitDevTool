from __future__ import annotations

import html
import logging
import re
from pathlib import Path
from typing import Any

import httpx
from bs4 import BeautifulSoup

from .api_card import build_api_card
from .config import docs_source_root
from .store import normalize_href


REQUEST_TIMEOUT = 30.0
logging.getLogger("httpx").setLevel(logging.WARNING)


class RevitApiDocsClient:
    def __init__(self) -> None:
        self.docs_root = docs_source_root()
        self.client = httpx.Client(
            timeout=REQUEST_TIMEOUT,
            follow_redirects=True,
            headers={"User-Agent": "RevitDevTool Revit API Docs MCP/0.1"},
        )

    def close(self) -> None:
        self.client.close()

    def has_local_docs(self) -> bool:
        return self.docs_root is not None

    def search_online(self, version: int, query: str) -> list[dict[str, Any]]:
        if self.has_local_docs() or version >= 2027:
            return self.search_static_index(version, query)

        url = f"https://www.revitapidocs.com/{version}/search"
        response = self.client.get(url, params={"query": query})
        response.raise_for_status()
        payload = response.json()
        results = payload.get("results", payload if isinstance(payload, list) else [])
        return [entry for entry in results if isinstance(entry, dict) and entry.get("href")]

    def search_static_index(self, version: int, query: str) -> list[dict[str, Any]]:
        entries = self.fetch_static_index(version)
        tokens = [token.casefold() for token in re.split(r"\W+", query) if token]
        scored: list[tuple[int, dict[str, Any]]] = []
        for entry in entries:
            title = str(entry.get("title", ""))
            haystack = " ".join(
                str(entry.get(key, "")) for key in ("title", "namespace", "member_of", "description")
            ).casefold()
            score = sum(3 if token in title.casefold() else 1 for token in tokens if token in haystack)
            if score:
                scored.append((score, entry))
        scored.sort(key=lambda item: (-item[0], item[1].get("title", "")))
        return [entry for _, entry in scored[:80]]

    def fetch_static_index(self, version: int) -> list[dict[str, Any]]:
        source = self._local_index_path(version)
        if source:
            text = source.read_text(encoding="utf-8", errors="replace")
        else:
            url = f"https://revapidocs.com/{version}.htm"
            response = self.client.get(url)
            response.raise_for_status()
            text = response.text
        entries: list[dict[str, Any]] = []
        seen: set[str] = set()
        pattern = re.compile(
            r'"title"\s*:\s*"(?P<title>(?:\\"|[^"])*)"\s*,\s*"href"\s*:\s*"(?P<href>'
            + str(version)
            + r'/[^"]+\.htm)"',
            re.IGNORECASE,
        )
        for match in pattern.finditer(text):
            href = normalize_href(match.group("href"))
            if not href or href in seen:
                continue
            title = html.unescape(match.group("title").replace('\\"', '"'))
            seen.add(href)
            entries.append(
                {
                    "title": title,
                    "short_title": strip_known_suffix(title),
                    "href": href,
                    "tag": infer_kind(title),
                    "description": None,
                }
            )
        return entries

    def fetch_symbol_page(self, version: int, href: str) -> tuple[str, str]:
        normalized = normalize_href(href)
        source = self._local_page_path(version, normalized)
        if source:
            return source.as_uri(), source.read_text(encoding="utf-8", errors="replace")

        urls = symbol_urls(version, normalized)
        last_error: Exception | None = None
        for url in urls:
            try:
                response = self.client.get(url)
                response.raise_for_status()
                return url, response.text
            except Exception as error:  # noqa: BLE001 - preserve fallback behavior across mirrors.
                last_error = error
        raise RuntimeError(f"Could not fetch {normalized} for Revit {version}: {last_error}")

    def _local_index_path(self, version: int) -> Path | None:
        if not self.docs_root:
            return None
        path = self.docs_root / f"{version}.htm"
        return path if path.exists() else None

    def _local_page_path(self, version: int, href: str) -> Path | None:
        if not self.docs_root:
            return None
        path = self.docs_root / str(version) / normalize_href(href)
        return path if path.exists() else None


def parse_symbol_page(html_text: str) -> dict[str, Any]:
    soup = BeautifulSoup(html_text, "html.parser")
    for element in soup(["script", "style", "nav", "footer", "header", "noscript"]):
        element.decompose()

    title = clean_text(soup.title.get_text(" ", strip=True) if soup.title else "")
    h1 = soup.find("h1")
    if h1:
        title = clean_text(h1.get_text(" ", strip=True)) or title

    lines = clean_lines(soup.get_text("\n"))
    sections = split_sections(lines)
    compact_text = "\n".join(lines)
    parsed = {
        "title": title,
        "text": compact_text,
        "sections": sections,
        "members": extract_member_rows(soup),
        "related": extract_related_links(soup),
    }
    parsed["api_card"] = build_api_card(parsed)
    return parsed


def select_sections(parsed: dict[str, Any], requested: list[str], max_chars: int) -> dict[str, str]:
    available = parsed["sections"]
    selected: dict[str, str] = {}
    aliases = {
        "summary": ("summary", "remarks"),
        "syntax": ("syntax",),
        "parameters": ("parameters",),
        "return": ("return value", "return"),
        "exceptions": ("exceptions",),
        "remarks": ("remarks",),
        "examples": ("examples", "example"),
        "see_also": ("see also",),
    }
    remaining = max_chars
    for name in requested:
        keys = aliases.get(name, (name,))
        value = ""
        for key in keys:
            value = available.get(key, "")
            if value:
                break
        if not value and name == "summary":
            value = "\n".join(parsed["text"].splitlines()[:12])
        if value:
            selected[name] = truncate(value, remaining)
            remaining -= len(selected[name])
        if remaining <= 0:
            break
    return selected


def split_sections(lines: list[str]) -> dict[str, str]:
    headings = {
        "syntax",
        "parameters",
        "return value",
        "remarks",
        "examples",
        "example",
        "exceptions",
        "see also",
        "properties",
        "methods",
        "constructors",
        "events",
    }
    sections: dict[str, list[str]] = {"summary": []}
    current = "summary"
    for line in lines:
        key = line.strip().casefold()
        if key in headings:
            current = key
            sections.setdefault(current, [])
            continue
        if line not in {"Revit API", "Revit API Docs"}:
            sections.setdefault(current, []).append(line)
    return {key: "\n".join(value).strip() for key, value in sections.items() if value}


def extract_member_rows(soup: BeautifulSoup) -> list[dict[str, str]]:
    members: list[dict[str, str]] = []
    seen: set[str] = set()
    for row in member_rows(soup):
        member = parse_member_row(row)
        if not member or member["href"] in seen:
            continue
        seen.add(member["href"])
        members.append(member)
    return members[:120]


def member_rows(soup: BeautifulSoup) -> list[Any]:
    rows: list[Any] = []
    for table in soup.find_all("table"):
        table_rows = table.find_all("tr")
        if table_rows and is_member_table(table_rows[0]):
            rows.extend(table_rows[1:])
    return rows


def parse_member_row(row: Any) -> dict[str, str] | None:
    cells = [clean_text(cell.get_text(" ", strip=True)) for cell in row.find_all(["td", "th"])]
    if len(cells) < 2:
        return None

    link = row.find("a", href=True)
    name = member_name(link, cells)
    href = normalize_href(str(link.get("href", ""))) if link else ""
    if not name or not href:
        return None

    description = cells[-1]
    return drop_empty(
        {
            "name": name,
            "kind": infer_kind(name),
            "description": description if description != name else "",
            "href": href,
        }
    )


def member_name(link: Any | None, cells: list[str]) -> str:
    return clean_text(link.get_text(" ", strip=True)) if link else cells[0]


def is_member_table(header_row: Any) -> bool:
    headers = [
        clean_text(cell.get_text(" ", strip=True)).casefold()
        for cell in header_row.find_all(["td", "th"])
    ]
    return "name" in headers and "description" in headers


def extract_related_links(soup: BeautifulSoup) -> list[dict[str, str]]:
    related: list[dict[str, str]] = []
    seen: set[str] = set()
    ignored = {"top", "send comments on this topic to autodesk"}
    for link in soup.find_all("a", href=True):
        title = clean_text(link.get_text(" ", strip=True))
        href = normalize_href(str(link.get("href", "")))
        if not title or not href or title.casefold() in ignored or href in seen:
            continue
        seen.add(href)
        related.append(drop_empty({"title": title, "kind": infer_kind(title), "href": href}))
    return related[:80]


def symbol_urls(version: int, href: str) -> list[str]:
    if version >= 2027:
        return [f"https://revapidocs.com/{version}/{href}"]
    return [f"https://www.revitapidocs.com/{version}/{href}", f"https://revapidocs.com/{version}/{href}"]


def infer_kind(title: str) -> str | None:
    for suffix, kind in (
        (" Class", "Class"),
        (" Method", "Method"),
        (" Property", "Property"),
        (" Constructor", "Constructor"),
        (" Enumeration", "Enumeration"),
        (" Interface", "Interface"),
        (" Event", "Event"),
        (" Structure", "Struct"),
    ):
        if title.endswith(suffix):
            return kind
    return None


def strip_known_suffix(title: str) -> str:
    kind = infer_kind(title)
    return title[: -(len(kind) + 1)] if kind and title.endswith(f" {kind}") else title


def drop_empty(data: dict[str, str | None]) -> dict[str, str]:
    return {key: value for key, value in data.items() if value}


def clean_lines(text: str) -> list[str]:
    return [line for line in (clean_text(value) for value in text.splitlines()) if line]


def clean_text(value: str) -> str:
    return re.sub(r"\s+", " ", value.replace("\ufeff", "")).strip()


def truncate(value: str, max_chars: int) -> str:
    if max_chars <= 0:
        return ""
    if len(value) <= max_chars:
        return value
    return value[: max_chars - 20].rstrip() + "\n...[truncated]"
