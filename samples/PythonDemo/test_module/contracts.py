from dataclasses import dataclass
from typing import Protocol


@dataclass(frozen=True)
class DataPacket:
    name: str
    values: tuple[float, ...]


class Transformer(Protocol):
    name: str

    def transform(self, packet: DataPacket) -> DataPacket:
        ...
