from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(slots=True)
class VerticalSegment:
    id: str
    length: float
    gradient_start_permille: float
    gradient_end_permille: float
    chainage_start: float = 0.0
    chainage_end: float = 0.0
    elevation_start: float = 0.0
    elevation_end: float = 0.0
    segment_type: str = "VerticalSegment"

    def as_dict(self) -> dict[str, Any]:
        return {
            "type": self.segment_type,
            "id": self.id,
            "length": self.length,
            "gradient_start_permille": self.gradient_start_permille,
            "gradient_end_permille": self.gradient_end_permille,
            "chainage_start": self.chainage_start,
            "chainage_end": self.chainage_end,
            "elevation_start": self.elevation_start,
            "elevation_end": self.elevation_end,
        }


@dataclass(slots=True)
class VerticalGrade(VerticalSegment):
    segment_type: str = "VerticalGrade"


@dataclass(slots=True)
class VerticalCurve(VerticalSegment):
    segment_type: str = "VerticalCurve"


@dataclass(slots=True)
class VerticalAlignment:
    name: str
    segments: list[VerticalSegment] = field(default_factory=list)
    start_chainage: float = 0.0
    start_elevation: float = 0.0

    def as_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "segments": [segment.as_dict() for segment in self.segments],
            "start_chainage": self.start_chainage,
            "start_elevation": self.start_elevation,
        }
