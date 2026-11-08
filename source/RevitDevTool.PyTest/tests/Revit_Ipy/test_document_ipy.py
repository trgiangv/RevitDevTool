# coding: utf-8
import unittest

from ipylib import active_document


class TestActiveDocument(unittest.TestCase):
    def setUp(self):
        self.doc = active_document()
        if self.doc is None:
            self.skipTest("no active document")

    def test_title(self):
        title = self.doc.Title
        print(title)
        self.assertGreater(len(title), 0)

    def test_is_not_family_document(self):
        self.assertFalse(self.doc.IsFamilyDocument)


class TestCollectors(unittest.TestCase):
    def setUp(self):
        self.doc = active_document()
        if self.doc is None:
            self.skipTest("no active document")

    def test_wall_collector_returns_list(self):
        from Autodesk.Revit.DB import FilteredElementCollector, Wall

        walls = list(
            FilteredElementCollector(self.doc)
            .OfClass(Wall)
            .WhereElementIsNotElementType()
        )
        self.assertIsInstance(walls, list)
        print("walls={}".format(len(walls)))

    @unittest.skip("example skip — IronPython unittest.skip")
    def test_skipped_placeholder(self):
        self.fail("should be skipped")
