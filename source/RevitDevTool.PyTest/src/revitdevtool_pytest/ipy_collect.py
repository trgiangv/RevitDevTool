"""Collect IronPython unittest files without importing them in CPython.

``test_*_ipy.py`` is a pytest collect convention only — how the plugin
routes files onto ``ipytests/run``. The host does not require that name;
it runs unittest on the paths in the request.
"""

from __future__ import annotations

import re
from collections import OrderedDict
from pathlib import Path

import pytest

from .constants import IPY_TEST_PREFIX, IPY_TEST_SUFFIX, NODEID_SEP, SUITE_ITEM_NAME

_CLASS = re.compile(r"^class\s+([A-Za-z_]\w*)\s*\(([^)]*)\)")
_DEF = re.compile(r"^([ \t]*)def\s+(test[A-Za-z0-9_]*)\s*\(")


def is_ipy_test_path(path: Path | str) -> bool:
    name = Path(path).name.lower()
    return name.startswith(IPY_TEST_PREFIX) and name.endswith(IPY_TEST_SUFFIX)


def scan_ipy_tests(path: Path) -> tuple[bool, list[tuple[str, str]]]:
    """Return ``(has_testcase, [(class, method), ...])`` from line text.

    CPython 3 tokenizer/AST is not used so IronPython 2.7 files still scan.
    """
    text = path.read_text(encoding="utf-8")
    has_case = False
    tests: list[tuple[str, str]] = []
    current_class: str | None = None
    class_indent = 0
    for raw in text.splitlines():
        line = raw.split("#", 1)[0]
        match = _CLASS.match(line.lstrip()) if line.strip() else None
        if match:
            indent = len(raw) - len(raw.lstrip())
            if "TestCase" in match.group(2):
                has_case = True
                current_class = match.group(1)
                class_indent = indent
            else:
                current_class = None
            continue
        match = _DEF.match(line)
        if match and current_class is not None:
            indent = len(match.group(1).expandtabs(4))
            if indent > class_indent:
                tests.append((current_class, match.group(2)))
    return has_case, tests


class IpyTestFile(pytest.File):
    def collect(self):
        has_case, tests = scan_ipy_tests(Path(self.path))
        if not has_case:
            raise pytest.Collector.CollectError(
                f"{self.path} must define unittest.TestCase (IronPython unittest flow)."
            )
        if not tests:
            yield IpyTestItem.from_parent(self, name=SUITE_ITEM_NAME)
            return
        by_class: OrderedDict[str, list[str]] = OrderedDict()
        for class_name, method in tests:
            by_class.setdefault(class_name, []).append(method)
        for class_name, methods in by_class.items():
            collector = IpyTestClass.from_parent(self, name=class_name)
            collector.methods = methods
            yield collector


class IpyTestClass(pytest.Collector):
    methods: list[str]

    def collect(self):
        for method in getattr(self, "methods", []):
            yield IpyTestItem.from_parent(self, name=method)


class IpyTestItem(pytest.Item):
    def runtest(self) -> None:
        pass

    def reportinfo(self):
        parent = self.parent.name if self.parent is not None else ""
        label = f"{parent}{NODEID_SEP}{self.name}" if parent else self.name
        return self.path, None, label
