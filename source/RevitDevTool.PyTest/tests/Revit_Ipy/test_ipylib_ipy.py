# coding: utf-8

import unittest

from ipylib import midpoint, mm_to_feet
from ipylib.geom import MM_PER_FOOT


class TestGeom(unittest.TestCase):
    def test_mm_to_feet(self):
        self.assertAlmostEqual(mm_to_feet(MM_PER_FOOT), 1.0, places=6)

    def test_midpoint(self):
        self.assertEqual(midpoint(0, 10), 5.0)


class TestGeomErrors(unittest.TestCase):
    def test_mm_to_feet_rejects_none(self):
        self.assertRaises(TypeError, mm_to_feet, None)
