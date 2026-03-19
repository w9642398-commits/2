from __future__ import annotations

from dataclasses import dataclass, field
from math import degrees
from typing import Any, Literal

from .common import Point2D

Rotation = Literal["cw", "ccw"]


@dataclass(slots=True)
class HorizontalElement:
    id: str
    length: float
    chainage_start: float = 0.0
    chainage_end: float = 0.0
    start_point: Point2D | None = None
    end_point: Point2D | None = None
    start_azimuth_rad: float = 0.0
    end_azimuth_rad: float = 0.0
    element_type: str = "HorizontalElement"

    def heading_change(self) -> float:
        return self.end_azimuth_rad - self.start_azimuth_rad

    def as_dict(self) -> dict[str, Any]:
        return {
            "type": self.element_type,
            "id": self.id,
            "length": self.length,
            "chainage_start": self.chainage_start,
            "chainage_end": self.chainage_end,
            "start_point": None if self.start_point is None else self.start_point.as_dict(),
            "end_point": None if self.end_point is None else self.end_point.as_dict(),
            "start_azimuth_deg": degrees(self.start_azimuth_rad),
            "end_azimuth_deg": degrees(self.end_azimuth_rad),
        }


@dataclass(slots=True)
class Straight(HorizontalElement):
    element_type: str = "Straight"


@dataclass(slots=True)
class CircularArc(HorizontalElement):
    radius: float = 0.0
    rotation: Rotation = "ccw"
    element_type: str = "CircularArc"

    @property
    def signed_radius(self) -> float:
        return self.radius if self.rotation == "ccw" else -self.radius

    def as_dict(self) -> dict[str, Any]:
        data = HorizontalElement.as_dict(self)
        data.update({"radius": self.radius, "rotation": self.rotation})
        return data


@dataclass(slots=True)
class TransitionCurve(HorizontalElement):
    radius: float = 0.0
    rotation: Rotation = "ccw"
    curve_start: bool = False
    element_type: str = "TransitionCurve"

    @property
    def signed_radius(self) -> float:
        return self.radius if self.rotation == "ccw" else -self.radius

    def as_dict(self) -> dict[str, Any]:
        data = HorizontalElement.as_dict(self)
        data.update({"radius": self.radius, "rotation": self.rotation, "curve_start": self.curve_start})
        return data


@dataclass(slots=True)
class Alignment:
    name: str
    start_point: Point2D
    start_azimuth_deg: float
    elements: list[HorizontalElement] = field(default_factory=list)
    sampled_points: list[Point2D] = field(default_factory=list)

    def as_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "start_point": self.start_point.as_dict(),
            "start_azimuth_deg": self.start_azimuth_deg,
            "elements": [element.as_dict() for element in self.elements],
        }
