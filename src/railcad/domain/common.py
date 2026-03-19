from __future__ import annotations

from dataclasses import dataclass, field
from math import hypot
from typing import Any


@dataclass(slots=True)
class Point2D:
    x: float
    y: float

    def distance_to(self, other: "Point2D") -> float:
        return hypot(other.x - self.x, other.y - self.y)

    def as_dict(self) -> dict[str, float]:
        return {"x": self.x, "y": self.y}


@dataclass(slots=True)
class Point3D(Point2D):
    z: float

    def as_dict(self) -> dict[str, float]:
        return {"x": self.x, "y": self.y, "z": self.z}


@dataclass(slots=True)
class ChainageRange:
    start: float
    end: float

    @property
    def length(self) -> float:
        return self.end - self.start

    def contains(self, chainage: float) -> bool:
        return self.start <= chainage <= self.end


@dataclass(slots=True)
class StationPoint:
    chainage: float
    point: Point3D | Point2D
    azimuth_deg: float | None = None
    metadata: dict[str, Any] = field(default_factory=dict)
