# coding: utf-8
"""Tiny IronPython-safe helpers (2.7 / 3.4)."""

MM_PER_FOOT = 304.8


def mm_to_feet(mm):
    if mm is None:
        raise TypeError("mm is required")
    return float(mm) / MM_PER_FOOT


def midpoint(a, b):
    return (float(a) + float(b)) / 2.0
