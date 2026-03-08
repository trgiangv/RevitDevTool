"""Revit context access helpers."""

from __future__ import annotations


def get_uiapp():
    from RevitDevTool.Core import RevitContext

    return RevitContext.UiApplication


def get_uidoc():
    from RevitDevTool.Core import RevitContext

    return RevitContext.ActiveUiDocument


def get_doc():
    from RevitDevTool.Core import RevitContext

    return RevitContext.ActiveDocument

