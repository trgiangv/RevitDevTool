# coding: utf-8
"""Nested test_*_ipy.py imports ipylib via ancestors of the test file on sys.path."""

import unittest

from ..ipylib.geom import midpoint


class TestNestedImport(unittest.TestCase):
    def test_midpoint_from_nested_module(self):
        self.assertEqual(midpoint(-2, 2), 0.0)
