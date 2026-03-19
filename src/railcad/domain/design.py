from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(slots=True)
class DesignCriteria:
    name: str
    max_speed_kmh: float
    min_radius_m: float
    min_transition_length_m: float
    min_tangent_length_m: float
    max_cant_mm: float
    max_gradient_permille: float
    min_vertical_curve_length_m: float
    extra: dict[str, Any] = field(default_factory=dict)


@dataclass(slots=True)
class SpeedSegment:
    chainage_start: float
    chainage_end: float
    speed_kmh: float

    def contains(self, chainage: float) -> bool:
        return self.chainage_start <= chainage <= self.chainage_end


@dataclass(slots=True)
class SpeedProfile:
    segments: list[SpeedSegment]

    def speed_at(self, chainage: float, default: float) -> float:
        for segment in self.segments:
            if segment.contains(chainage):
                return segment.speed_kmh
        return default


@dataclass(slots=True)
class ClearanceConstraint:
    name: str
    minimum_clearance_m: float
    chainage_start: float
    chainage_end: float
    description: str = ""
