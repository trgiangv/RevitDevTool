# coding: utf-8
"""Open the dashboard from the pyRevit IronPython engine.

Load ``samples/PythonDemo``. That folder is on ``sys.path``, so
``revit_dashboard`` next to ``commands`` imports directly.
"""

from revit_dashboard.presentation.host import show_dashboard

if __name__ == "__main__":
    show_dashboard()
