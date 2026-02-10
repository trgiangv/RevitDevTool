import os
import sys
import gc
import importlib

root = os.path.abspath(__root__) if __root__ else ""
script_file = os.path.abspath(__file__) if __file__ else ""
script_dir = os.path.dirname(script_file)
targets = [p for p in (root, script_dir) if p]

if targets:
    normalized_targets = [os.path.normcase(p) for p in targets]
    to_remove = []

    for name, mod in sys.modules.items():
        path = getattr(mod, "__file__", None)
        if not path:
            continue

        try:
            mod_path = os.path.normcase(os.path.abspath(path))
        except Exception:
            continue

        for target in normalized_targets:
            if mod_path.startswith(target):
                to_remove.append(name)
                break

    before_count = len(to_remove)
    for name in to_remove:
        sys.modules.pop(name, None)

    # Also clear path importer cache for affected roots
    cache_keys_to_remove = []
    for key in sys.path_importer_cache.keys():
        try:
            cache_path = os.path.normcase(os.path.abspath(key))
        except Exception:
            continue
        for target in normalized_targets:
            if cache_path.startswith(target):
                cache_keys_to_remove.append(key)
                break

    for key in cache_keys_to_remove:
        sys.path_importer_cache.pop(key, None)

importlib.invalidate_caches()
gc.collect()