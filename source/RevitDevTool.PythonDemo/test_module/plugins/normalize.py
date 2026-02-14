from __future__ import annotations

from dataclasses import dataclass

from test_module.contracts import DataPacket


@dataclass(frozen=True)
class NormalizePlugin:
    name: str = "normalize:max"

    def transform(self, packet: DataPacket) -> DataPacket:
        max_value = max(packet.values) if packet.values else 1.0
        if max_value == 0:
            return packet
        return DataPacket(
            name=f"{packet.name}|{self.name}",
            values=tuple(round(v / max_value, 6) for v in packet.values),
        )


def build_plugin() -> NormalizePlugin:
    return NormalizePlugin()
