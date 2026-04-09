"""Serialize a collected pytest item into a TestRequest for remote execution."""

from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING

from .models import TestRequest

if TYPE_CHECKING:
    import pytest


def serialize_test(item: pytest.Item) -> TestRequest:
    """Read the full module source and extract test/class names."""
    return TestRequest(
        module_source=Path(str(item.fspath)).read_text(encoding="utf-8"),
        test_name=item.name,
        file_path=str(item.fspath),
        class_name=item.cls.__name__ if item.cls is not None else None,
    )
