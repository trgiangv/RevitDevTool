from __future__ import annotations

from functools import lru_cache
from typing import Iterable

from test_module.contracts import DataPacket
from test_module.plugins import load_plugins


@lru_cache(maxsize=32)
def _cached_seed_values(seed: int) -> tuple[float, ...]:
    # Cache stays valid inside one script run, but should be recreated on next run.
    base = float(seed)
    return (base + 1.0, base + 3.0, base + 5.0, base + 8.0)


def run_pipeline(seed: int, extra_values: Iterable[float]) -> DataPacket:
    packet = DataPacket(name=f"seed:{seed}", values=_cached_seed_values(seed) + tuple(extra_values))
    for plugin in load_plugins():
        packet = plugin.transform(packet)
    return packet


def cache_info() -> str:
    info = _cached_seed_values.cache_info()
    return f"hits={info.hits}, misses={info.misses}, currsize={info.currsize}"
