from __future__ import annotations

import json
import sqlite3
import time
from pathlib import Path
from typing import Any


class RevitDocsStore:
    def __init__(self, path: Path) -> None:
        self.path = path
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.connection = sqlite3.connect(self.path)
        self.connection.row_factory = sqlite3.Row
        self._initialize()

    def close(self) -> None:
        self.connection.close()

    def _initialize(self) -> None:
        self.connection.executescript(
            """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS entries (
                version INTEGER NOT NULL,
                href TEXT NOT NULL,
                title TEXT NOT NULL,
                short_title TEXT,
                kind TEXT,
                namespace TEXT,
                member_of TEXT,
                member_of_href TEXT,
                description TEXT,
                source TEXT NOT NULL,
                raw_json TEXT,
                updated_at REAL NOT NULL,
                PRIMARY KEY (version, href)
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts USING fts5(
                version UNINDEXED,
                href UNINDEXED,
                title,
                short_title,
                kind,
                namespace,
                member_of,
                description
            );

            CREATE TABLE IF NOT EXISTS pages (
                version INTEGER NOT NULL,
                href TEXT NOT NULL,
                url TEXT NOT NULL,
                title TEXT,
                text TEXT,
                sections_json TEXT,
                metadata_json TEXT,
                fetched_at REAL NOT NULL,
                PRIMARY KEY (version, href)
            );

            """
        )
        self._add_column_if_missing("pages", "metadata_json", "TEXT")
        self.connection.commit()

    def _add_column_if_missing(self, table: str, column: str, column_type: str) -> None:
        columns = {
            row["name"]
            for row in self.connection.execute(f"PRAGMA table_info({table})").fetchall()
        }
        if column not in columns:
            self.connection.execute(f"ALTER TABLE {table} ADD COLUMN {column} {column_type}")

    def upsert_entries(self, version: int, entries: list[dict[str, Any]], source: str) -> int:
        now = time.time()
        rows = [self._normalize_entry(version, entry, source, now) for entry in entries]
        with self.connection:
            for row in rows:
                self.connection.execute(
                    """
                    INSERT INTO entries (
                        version, href, title, short_title, kind, namespace, member_of,
                        member_of_href, description, source, raw_json, updated_at
                    )
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    ON CONFLICT(version, href) DO UPDATE SET
                        title = excluded.title,
                        short_title = excluded.short_title,
                        kind = excluded.kind,
                        namespace = excluded.namespace,
                        member_of = excluded.member_of,
                        member_of_href = excluded.member_of_href,
                        description = excluded.description,
                        source = excluded.source,
                        raw_json = excluded.raw_json,
                        updated_at = excluded.updated_at
                    """,
                    row,
                )
                self.connection.execute(
                    "DELETE FROM entries_fts WHERE version = ? AND href = ?",
                    (row[0], row[1]),
                )
                self.connection.execute(
                    """
                    INSERT INTO entries_fts (
                        version, href, title, short_title, kind, namespace, member_of, description
                    )
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    (row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[8]),
                )
        return len(rows)

    def search(self, version: int, query: str, limit: int) -> list[dict[str, Any]]:
        phrase = self._fts_query(query)
        rows = self.connection.execute(
            """
            SELECT e.version, e.href, e.title, e.short_title, e.kind, e.namespace,
                   e.member_of, e.member_of_href, e.description, e.source,
                   bm25(entries_fts) AS rank
            FROM entries_fts
            JOIN entries e ON e.version = entries_fts.version AND e.href = entries_fts.href
            WHERE entries_fts.version = ? AND entries_fts MATCH ?
            ORDER BY
                CASE
                    WHEN lower(e.title) = lower(?) THEN 0
                    WHEN lower(e.short_title) = lower(?) THEN 1
                    WHEN lower(e.title) = lower(? || ' Class') THEN 2
                    WHEN lower(e.title) LIKE lower(? || '%') THEN 3
                    ELSE 4
                END,
                rank
            LIMIT ?
            """,
            (version, phrase, query, query, query, query, limit),
        ).fetchall()
        return [dict(row) for row in rows]

    def find_entry(self, version: int, symbol_or_href: str) -> dict[str, Any] | None:
        href = normalize_href(symbol_or_href)
        if href:
            row = self.connection.execute(
                "SELECT * FROM entries WHERE version = ? AND href = ?",
                (version, href),
            ).fetchone()
            if row:
                return dict(row)

        exact = self.connection.execute(
            """
            SELECT * FROM entries
            WHERE version = ?
              AND (title = ? OR short_title = ? OR title = ? || ' Class')
            ORDER BY CASE WHEN title = ? THEN 0 ELSE 1 END
            LIMIT 1
            """,
            (version, symbol_or_href, symbol_or_href, symbol_or_href, symbol_or_href),
        ).fetchone()
        return dict(exact) if exact else None

    def get_page(self, version: int, href: str) -> dict[str, Any] | None:
        row = self.connection.execute(
            "SELECT * FROM pages WHERE version = ? AND href = ?",
            (version, normalize_href(href)),
        ).fetchone()
        return dict(row) if row else None

    def save_page(
        self,
        version: int,
        href: str,
        url: str,
        title: str,
        text: str,
        sections: dict[str, str],
        metadata: dict[str, Any] | None = None,
    ) -> None:
        with self.connection:
            self.connection.execute(
                """
                INSERT INTO pages (version, href, url, title, text, sections_json, metadata_json, fetched_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(version, href) DO UPDATE SET
                    url = excluded.url,
                    title = excluded.title,
                    text = excluded.text,
                    sections_json = excluded.sections_json,
                    metadata_json = excluded.metadata_json,
                    fetched_at = excluded.fetched_at
                """,
                (
                    version,
                    normalize_href(href),
                    url,
                    title,
                    text,
                    json.dumps(sections, ensure_ascii=False),
                    json.dumps(metadata or {}, ensure_ascii=False),
                    time.time(),
                ),
            )

    @staticmethod
    def _normalize_entry(version: int, entry: dict[str, Any], source: str, now: float) -> tuple[Any, ...]:
        href = normalize_href(str(entry.get("href", "")))
        title = str(entry.get("title") or entry.get("short_title") or href)
        short_title = entry.get("short_title") or strip_suffixes(title)
        kind = entry.get("tag") or entry.get("kind")
        return (
            version,
            href,
            title,
            short_title,
            kind,
            entry.get("namespace"),
            entry.get("member_of"),
            normalize_href(str(entry.get("member_of_href", ""))) or entry.get("member_of_href"),
            entry.get("description"),
            source,
            json.dumps(entry, ensure_ascii=False),
            now,
        )

    @staticmethod
    def _fts_query(query: str) -> str:
        tokens = [token.strip('"*') for token in query.replace(".", " ").split() if token.strip('"*')]
        if not tokens:
            return '""'
        return " OR ".join(f'"{token}"*' for token in tokens)


def normalize_href(value: str) -> str:
    value = value.strip()
    if not value:
        return ""
    value = value.split("#", 1)[0].split("?", 1)[0].rstrip("/")
    if value.startswith(("http://", "https://")):
        value = value.rsplit("/", 1)[-1]
    if "/" in value:
        value = value.rsplit("/", 1)[-1]
    return value if value.endswith(".htm") else ""


def strip_suffixes(title: str) -> str:
    suffixes = (
        " Constructor",
        " Class",
        " Enumeration",
        " Interface",
        " Method",
        " Property",
        " Event",
        " Structure",
        " Members",
    )
    for suffix in suffixes:
        if title.endswith(suffix):
            return title[: -len(suffix)]
    return title
