from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Any

from .alignment import Alignment
from .cant import CantAlignment
from .design import ClearanceConstraint, DesignCriteria, SpeedProfile, SpeedSegment
from .survey import ExistingTrackGeometry, SurveyPointSet
from .turnout import Turnout
from .vertical import VerticalAlignment


@dataclass(slots=True)
class Project:
    name: str
    alignment: Alignment
    vertical_alignment: VerticalAlignment
    cant_alignment: CantAlignment
    design_criteria: DesignCriteria
    speed_profile: SpeedProfile = field(default_factory=lambda: SpeedProfile([]))
    turnouts: list[Turnout] = field(default_factory=list)
    survey_point_sets: list[SurveyPointSet] = field(default_factory=list)
    existing_track_geometries: list[ExistingTrackGeometry] = field(default_factory=list)
    clearance_constraints: list[ClearanceConstraint] = field(default_factory=list)

    def as_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "alignment": self.alignment.as_dict(),
            "vertical_alignment": self.vertical_alignment.as_dict(),
            "cant_alignment": self.cant_alignment.as_dict(),
            "design_criteria": asdict(self.design_criteria),
            "speed_profile": [asdict(segment) for segment in self.speed_profile.segments],
            "turnouts": [turnout.as_dict() for turnout in self.turnouts],
            "survey_point_sets": [
                {
                    "name": point_set.name,
                    "source": point_set.source,
                    "points": [point.as_dict() for point in point_set.points],
                }
                for point_set in self.survey_point_sets
            ],
            "existing_track_geometries": [
                {
                    "name": geometry.name,
                    "source": geometry.source,
                    "centerline": [point.as_dict() for point in geometry.centerline],
                }
                for geometry in self.existing_track_geometries
            ],
            "clearance_constraints": [asdict(constraint) for constraint in self.clearance_constraints],
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "Project":
        from .alignment import Alignment, CircularArc, Straight, TransitionCurve
        from .cant import CantAlignment, CantSegment
        from .common import Point2D, Point3D
        from .design import ClearanceConstraint, DesignCriteria, SpeedProfile, SpeedSegment
        from .survey import ExistingTrackGeometry, SurveyPointSet
        from .vertical import VerticalAlignment, VerticalCurve, VerticalGrade

        alignment_data = data["alignment"]
        elements = []
        for raw in alignment_data["elements"]:
            if raw["type"] == "Straight":
                elements.append(Straight(id=raw["id"], length=raw["length"]))
            elif raw["type"] == "CircularArc":
                elements.append(
                    CircularArc(
                        id=raw["id"],
                        length=raw["length"],
                        radius=raw["radius"],
                        rotation=raw["rotation"],
                    )
                )
            elif raw["type"] == "TransitionCurve":
                elements.append(
                    TransitionCurve(
                        id=raw["id"],
                        length=raw["length"],
                        radius=raw["radius"],
                        rotation=raw["rotation"],
                        curve_start=raw.get("curve_start", False),
                    )
                )
        alignment = Alignment(
            name=alignment_data["name"],
            start_point=Point2D(**alignment_data["start_point"]),
            start_azimuth_deg=alignment_data["start_azimuth_deg"],
            elements=elements,
        )

        vertical_data = data["vertical_alignment"]
        segments = []
        for raw in vertical_data["segments"]:
            cls_ = VerticalGrade if raw["type"] == "VerticalGrade" else VerticalCurve
            segments.append(
                cls_(
                    id=raw["id"],
                    length=raw["length"],
                    gradient_start_permille=raw["gradient_start_permille"],
                    gradient_end_permille=raw["gradient_end_permille"],
                )
            )
        vertical = VerticalAlignment(
            name=vertical_data["name"],
            segments=segments,
            start_chainage=vertical_data["start_chainage"],
            start_elevation=vertical_data["start_elevation"],
        )

        cant_data = data["cant_alignment"]
        cant = CantAlignment(
            name=cant_data["name"],
            segments=[CantSegment(**raw) for raw in cant_data["segments"]],
        )
        criteria = DesignCriteria(**data["design_criteria"])
        speed_profile = SpeedProfile([SpeedSegment(**raw) for raw in data.get("speed_profile", [])])
        turnouts = [Turnout(**raw) for raw in data.get("turnouts", [])]
        survey_sets = [
            SurveyPointSet(
                name=raw["name"],
                source=raw.get("source", ""),
                points=[Point3D(**point) for point in raw.get("points", [])],
            )
            for raw in data.get("survey_point_sets", [])
        ]
        existing_geometries = [
            ExistingTrackGeometry(
                name=raw["name"],
                source=raw.get("source", ""),
                centerline=[Point3D(**point) for point in raw.get("centerline", [])],
            )
            for raw in data.get("existing_track_geometries", [])
        ]
        clearance_constraints = [
            ClearanceConstraint(**raw) for raw in data.get("clearance_constraints", [])
        ]
        return cls(
            name=data["name"],
            alignment=alignment,
            vertical_alignment=vertical,
            cant_alignment=cant,
            design_criteria=criteria,
            speed_profile=speed_profile,
            turnouts=turnouts,
            survey_point_sets=survey_sets,
            existing_track_geometries=existing_geometries,
            clearance_constraints=clearance_constraints,
        )
