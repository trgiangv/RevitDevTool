"""Revit API action dispatcher using RevitTask.RunAsync.

Routes actions to Revit API context using RevitTask.RunAsync
so that Transaction-based calls have a valid API context.

IMPORTANT: Use RevitTask.RunAsync instead of Dispatcher.Invoke
to ensure proper Revit API context.
"""

from __future__ import annotations

from collections.abc import Callable
from typing import Any

from RevitDevTool.Core import RevitContextExecutor
from System import Action


class RevitActionDispatcher:
    """Dispatches Revit API actions using RevitTask.RunAsync for proper API context."""

    def enqueue(
        self,
        action: Callable[..., Any],
        callback: Callable[[dict], None] | None = None,
    ) -> None:
        """Schedule *action* in Revit API context and fire *callback* with the result.
        
        Uses RevitTask.RunAsync to ensure the action runs within a valid Revit API context.
        """

        def _run():
            try:
                result = action()
                if callback is not None:
                    callback(result)
            except Exception as exc:
                if callback is not None:
                    callback({"ok": False, "error": str(exc)})

        RevitContextExecutor.Raise(Action(_run))
