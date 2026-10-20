# -*- coding: utf-8 -*-
"""Dashboard log.

Lines look like ``[revit_dashboard] message``. ``REVIT_DASHBOARD_LOG`` selects
the level and defaults to INFO.
"""

from __future__ import division, print_function

import logging
import os
import sys

LOGGER_NAME = "revit_dashboard"  # type: str

_LEVELS = {
    "DEBUG": logging.DEBUG,
    "INFO": logging.INFO,
    "WARNING": logging.WARNING,
    "ERROR": logging.ERROR,
    "CRITICAL": logging.CRITICAL,
}  # type: dict[str, int]


class _FlushStreamHandler(logging.StreamHandler):
    """Stream handler that flushes so the Revit journal sees the line."""

    def emit(self, record):
        # type: (logging.LogRecord) -> None
        logging.StreamHandler.emit(self, record)
        self.flush()


def _level_from_env():
    # type: () -> int
    name = os.environ.get("REVIT_DASHBOARD_LOG", "INFO").upper()
    return _LEVELS.get(name, logging.INFO)


def get_logger():
    # type: () -> logging.Logger
    """Return the dashboard logger, configuring it on the first call."""
    logger = logging.getLogger(LOGGER_NAME)
    if logger.handlers:
        return logger
    handler = _FlushStreamHandler(sys.stdout)
    handler.setFormatter(logging.Formatter("[revit_dashboard] %(message)s"))
    logger.addHandler(handler)
    logger.setLevel(_level_from_env())
    logger.propagate = False
    return logger
