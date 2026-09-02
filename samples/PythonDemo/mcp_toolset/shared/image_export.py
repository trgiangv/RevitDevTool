"""Shared helpers for Revit image export options."""

from Autodesk.Revit import DB


def map_dpi(resolution: int) -> DB.ImageResolution:
    """Map a requested DPI value to the nearest Revit ImageResolution enum."""
    if resolution <= 72:
        return DB.ImageResolution.DPI_72
    if resolution <= 150:
        return DB.ImageResolution.DPI_150
    if resolution <= 300:
        return DB.ImageResolution.DPI_300
    return DB.ImageResolution.DPI_600
