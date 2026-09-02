"""Filesystem helpers using .NET APIs (IronPython-friendly for IDE stubs)."""

import os

from System.IO import Directory


def list_directory_names(directory: str) -> list[str]:
    """Return entry names in *directory* (same shape as ``os.listdir``)."""
    return [os.path.basename(path) for path in Directory.GetFileSystemEntries(directory)]
