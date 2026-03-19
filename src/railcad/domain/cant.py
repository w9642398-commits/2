from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(slots=True)
class CantSegment:
    id: str
    chainage_start: float
    chainage_end: float
    cant_start_mm: float
    cant_end_mm: float

    @property
    def length(self) -> float:
        return self.chainage_end - self.chainage_start

    def cant_at(self, chainage: float) -> float:
        if self.length <= 0:
            return self.cant_end_mm
        ratio = (chainage - self.chainage_start) / self.length
        ratio = min(1.0, max(0.0, ratio))
        return self.cant_start_mm + ratio * (self.cant_end_mm - self.cant_start_mm)

    def as_dict(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "chainage_start": self.chainage_start,
            "chainage_end": self.chainage_end,
            "cant_start_mm": self.cant_start_mm,
            "cant_end_mm": self.cant_end_mm,
        }


@dataclass(slots=True)
class CantAlignment:
    name: str
    segments: list[CantSegment] = field(default_factory=list)

    def cant_at(self, chainage: float) -> float:
        for segment in self.segments:
            if segment.chainage_start <= chainage <= segment.chainage_end:
                return segment.cant_at(chainage)
        return 0.0

    def as_dict(self) -> dict[str, Any]:
        return {"name": self.name, "segments": [segment.as_dict() for segment in self.segments]}
