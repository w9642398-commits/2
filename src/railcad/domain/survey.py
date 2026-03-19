from __future__ import annotations

from dataclasses import dataclass, field

from .common import Point3D


@dataclass(slots=True)
class SurveyPointSet:
    name: str
    points: list[Point3D] = field(default_factory=list)
    source: str = ""


@dataclass(slots=True)
class ExistingTrackGeometry:
    name: str
    centerline: list[Point3D] = field(default_factory=list)
    source: str = ""
