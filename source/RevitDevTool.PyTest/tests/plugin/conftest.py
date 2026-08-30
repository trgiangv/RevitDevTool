"""Local plugin unit tests — do not dispatch to a host."""

import os

import pytest

os.environ["REVITDEVTOOL_PYTEST_DISABLE"] = "1"

pytest_plugins = ["pytester"]
