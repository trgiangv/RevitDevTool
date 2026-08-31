from dataclasses import dataclass

from test_module.contracts import DataPacket


@dataclass(frozen=True)
class ScalePlugin:
    name: str = "scale:x2"

    def transform(self, packet: DataPacket) -> DataPacket:
        return DataPacket(name=f"{packet.name}|{self.name}", values=tuple(v * 2 for v in packet.values))


def build_plugin() -> ScalePlugin:
    return ScalePlugin()
