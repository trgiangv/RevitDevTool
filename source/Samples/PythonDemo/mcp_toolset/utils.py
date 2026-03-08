"""Compatibility facade for ToolSet shared helpers."""

from shared.context import get_doc, get_uidoc, get_uiapp
from shared.element_helpers import (
    element_id_value,
    find_family_symbol_safely,
    normalize_string,
)


async def try_log(ctx, level, message):
    if ctx is None:
        return
    try:
        log_method = getattr(ctx, level, None)
        if callable(log_method):
            await log_method(message)
    except Exception:
        pass
