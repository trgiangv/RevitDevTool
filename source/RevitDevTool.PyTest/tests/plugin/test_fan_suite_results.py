from unittest.mock import MagicMock

import pytest

from revitdevtool_pytest.constants import SUITE_ITEM_NAME
from revitdevtool_pytest.models import CaseResult
from revitdevtool_pytest.reporting import fan_suite_results

_SUITE_ONLY_SOURCE = """import unittest

class GeneratedTests(unittest.TestCase):
    def helper(self):
        pass
"""


def _collect_suite_item(pytester: pytest.Pytester):
    pytester.makeini("[pytest]\n")
    target = pytester.path / "test_suite_only_ipy.py"
    target.write_text(_SUITE_ONLY_SOURCE, encoding="utf-8")
    items = pytester.getitems(target)
    return next(item for item in items if item.name == SUITE_ITEM_NAME)


def test_fan_suite_results_maps_child_nodeids_to_suite_item(pytester: pytest.Pytester):
    suite = _collect_suite_item(pytester)
    child_nodeid = f"{suite.nodeid.removesuffix(SUITE_ITEM_NAME)}T::test_a"
    child_result = CaseResult(
        nodeid=child_nodeid,
        outcome="passed",
        phase="call",
    )
    results = {child_nodeid: [child_result]}
    remapped = fan_suite_results([suite], results)
    assert remapped[suite.nodeid] == [child_result]
    assert child_nodeid not in remapped


def test_fan_suite_results_leaves_normal_item_nodeids():
    item = MagicMock()
    item.nodeid = "tests/x_ipy.py::T::test_a"
    item.name = "test_a"
    child_result = CaseResult(
        nodeid=item.nodeid,
        outcome="passed",
        phase="call",
    )
    results = {item.nodeid: [child_result]}
    remapped = fan_suite_results([item], results)
    assert remapped == results
