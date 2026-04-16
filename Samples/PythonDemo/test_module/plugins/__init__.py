from __future__ import annotations

import importlib
from typing import List

from test_module.contracts import Transformer

PLUGIN_MODULES = (
    "test_module.plugins.scale",
    "test_module.plugins.normalize",
)


def load_plugins() -> List[Transformer]:
    plugins: List[Transformer] = []
    for module_name in PLUGIN_MODULES:
        module = importlib.import_module(module_name)
        plugins.append(module.build_plugin())
    return plugins
