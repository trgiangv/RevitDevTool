import os
import sys
import importlib

root = os.path.abspath(__root__) if __root__ else ""
script_file = os.path.abspath(__file__) if __file__ else ""
script_dir = os.path.dirname(script_file)
targets = [p for p in (root, script_dir) if p]

if targets:
    normalized_targets = [os.path.normcase(p) for p in targets]
    to_remove = set()

    for name, mod in sys.modules.items():
        path = getattr(mod, "__file__", None)
        if not path:
            continue

        try:
            mod_path = os.path.normcase(os.path.abspath(path))
        except Exception:
            continue

        for target in normalized_targets:
            try:
                if os.path.commonpath([mod_path, target]) == target:
                    to_remove.add(name)
                    break
            except Exception:
                continue
    for name in to_remove:
        sys.modules.pop(name, None)

importlib.invalidate_caches()