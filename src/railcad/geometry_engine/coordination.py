from __future__ import annotations

from dataclasses import dataclass

from railcad.domain import Project
from .cant import CantEngine
from .horizontal import HorizontalGeometryEngine
from .vertical import VerticalGeometryEngine


@dataclass(slots=True)
class ProjectGeometrySnapshot:
    horizontal_points: list
    vertical_points: list
    cant_points: list


class ProjectGeometryCoordinator:
    def __init__(self) -> None:
        self.horizontal_engine = HorizontalGeometryEngine()
        self.vertical_engine = VerticalGeometryEngine()
        self.cant_engine = CantEngine()

    def rebuild(self, project: Project) -> ProjectGeometrySnapshot:
        horizontal_result = self.horizontal_engine.rebuild(project.alignment)
        vertical_points = self.vertical_engine.rebuild(project.vertical_alignment)
        cant_points = self.cant_engine.sample(project.cant_alignment)
        return ProjectGeometrySnapshot(
            horizontal_points=horizontal_result.sampled_points,
            vertical_points=vertical_points,
            cant_points=cant_points,
        )
