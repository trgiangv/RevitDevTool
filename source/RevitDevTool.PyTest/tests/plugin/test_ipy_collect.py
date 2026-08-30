from pathlib import Path

import pytest

from revitdevtool_pytest.ipy_collect import is_ipy_test_path, scan_ipy_tests


def test_is_ipy_test_path():
    assert is_ipy_test_path(Path("test_math_ipy.py"))
    assert is_ipy_test_path(Path("tests/Revit_Ipy/test_geom_ipy.py"))
    assert not is_ipy_test_path(Path("test_math.py"))
    assert not is_ipy_test_path(Path("math_ipy_test.py"))
    assert not is_ipy_test_path(Path("math_ipy_script.py"))
    assert not is_ipy_test_path(Path("test_math_ipy_script.py"))
    assert not is_ipy_test_path(Path("tests/Revit_Ipy/ipylib/geom.py"))


def test_scan_ipy_tests_finds_testcase_methods(tmp_path: Path):
    target = tmp_path / "test_math_ipy.py"
    target.write_text(
        "\n".join(
            [
                "# coding: utf-8",
                "import unittest",
                "",
                "class TestMath(unittest.TestCase):",
                "    def test_add(self):",
                "        self.assertEqual(2 + 3, 5)",
                "",
                "    def helper(self):",
                "        pass",
                "",
                "    def test_add_negative(self):",
                "        self.assertEqual(0, 0)",
                "",
                "class NotATest(object):",
                "    def test_ignored(self):",
                "        pass",
            ]
        ),
        encoding="utf-8",
    )
    has_case, tests = scan_ipy_tests(target)
    assert has_case
    assert tests == [("TestMath", "test_add"), ("TestMath", "test_add_negative")]


def test_scan_ipy_tests_without_testcase(tmp_path: Path):
    target = tmp_path / "test_empty_ipy.py"
    target.write_text("def test_not_unittest():\n    pass\n", encoding="utf-8")
    has_case, tests = scan_ipy_tests(target)
    assert not has_case
    assert tests == []


_IPY_SAMPLE = """# coding: utf-8
import unittest

class TestMath(unittest.TestCase):
    def test_add(self):
        self.assertEqual(2 + 3, 5)

    def test_add_negative(self):
        self.assertEqual(0, 0)
"""


def test_vscode_style_nodeid_selects_one_item(pytester: pytest.Pytester):
    """VS Code passes file.py::Class::method; collectors must nest Class then method."""
    pytester.makeini("[pytest]\n")
    target = pytester.path / "test_math_ipy.py"
    target.write_text(_IPY_SAMPLE, encoding="utf-8")
    result = pytester.runpytest(
        f"{target}::TestMath::test_add",
        "--collect-only",
        "-q",
        "-p", "revitdevtool",
    )
    result.stdout.fnmatch_lines(["*test_math_ipy.py::TestMath::test_add*"])
    assert result.ret == 0
    assert "ERROR: not found" not in result.stdout.str()
    collected = [line for line in result.stdout.str().splitlines() if "::" in line and "test_math_ipy" in line]
    assert len(collected) == 1
