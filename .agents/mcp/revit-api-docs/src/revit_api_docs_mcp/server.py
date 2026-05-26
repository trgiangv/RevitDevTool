from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from .client import RevitApiDocsClient
from .config import DEFAULT_VERSION, database_path
from .service import RevitDocsService
from .store import RevitDocsStore


mcp = FastMCP("revit-api-docs")
service = RevitDocsService(RevitDocsStore(database_path()), RevitApiDocsClient())


@mcp.tool()
def revit_docs_search(
    query: str,
    version: int = DEFAULT_VERSION,
    kind: str | None = None,
    namespace: str | None = None,
    limit: int = 8,
) -> dict[str, Any]:
    """Search Revit API Docs with local FTS first and online fallback."""
    return service.search(query, version, kind, namespace, limit)


@mcp.tool()
def revit_docs_get(
    symbol_or_href: str,
    version: int = DEFAULT_VERSION,
    sections: list[str] | None = None,
    max_chars: int = 6000,
) -> dict[str, Any]:
    """Retrieve one Revit API doc page, returning only requested sections."""
    return service.get(symbol_or_href, version, sections, max_chars)


@mcp.tool()
def revit_docs_compare(
    from_version: int,
    to_version: int,
    symbol_or_href: str | None = None,
    max_chars: int = 5000,
    examples_per_kind: int = 3,
    include_removed: bool = False,
) -> dict[str, Any]:
    """Compare either one API symbol or two version indexes for release-level changes."""
    return service.compare(
        from_version=from_version,
        to_version=to_version,
        symbol_or_href=symbol_or_href,
        max_chars=max_chars,
        examples_per_kind=examples_per_kind,
        include_removed=include_removed,
    )


def main() -> None:
    try:
        mcp.run()
    finally:
        service.close()


if __name__ == "__main__":
    main()
