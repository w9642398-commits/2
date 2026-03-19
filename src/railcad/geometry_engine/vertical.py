from __future__ import annotations

from dataclasses import dataclass

from railcad.domain import VerticalAlignment, VerticalCurve, VerticalGrade, VerticalSegment


@dataclass(slots=True)
class VerticalPoint:
    chainage: float
    elevation: float
    gradient_permille: float


class VerticalGeometryEngine:
    def rebuild(self, vertical_alignment: VerticalAlignment) -> list[VerticalPoint]:
        current_chainage = vertical_alignment.start_chainage
        current_elevation = vertical_alignment.start_elevation
        points = [
            VerticalPoint(
                chainage=current_chainage,
                elevation=current_elevation,
                gradient_permille=vertical_alignment.segments[0].gradient_start_permille
                if vertical_alignment.segments
                else 0.0,
            )
        ]
        for segment in vertical_alignment.segments:
            segment.chainage_start = current_chainage
            segment.elevation_start = current_elevation
            current_chainage += segment.length
            segment.chainage_end = current_chainage
            current_elevation = self._elevation_delta(segment) + segment.elevation_start
            segment.elevation_end = current_elevation
            points.append(
                VerticalPoint(
                    chainage=current_chainage,
                    elevation=current_elevation,
                    gradient_permille=segment.gradient_end_permille,
                )
            )
        return points

    def elevation_at(self, vertical_alignment: VerticalAlignment, chainage: float) -> float:
        for segment in vertical_alignment.segments:
            if segment.chainage_start <= chainage <= segment.chainage_end:
                local = chainage - segment.chainage_start
                return segment.elevation_start + self._segment_elevation(segment, local)
        return vertical_alignment.start_elevation

    def _elevation_delta(self, segment: VerticalSegment) -> float:
        return self._segment_elevation(segment, segment.length)

    @staticmethod
    def _segment_elevation(segment: VerticalSegment, local: float) -> float:
        g1 = segment.gradient_start_permille / 1000.0
        g2 = segment.gradient_end_permille / 1000.0
        if isinstance(segment, VerticalGrade):
            return local * g1
        rate = (g2 - g1) / segment.length if segment.length else 0.0
        return g1 * local + 0.5 * rate * local * local

    def sample(self, vertical_alignment: VerticalAlignment, step: float = 10.0) -> list[VerticalPoint]:
        if not vertical_alignment.segments:
            return []
        max_chainage = vertical_alignment.segments[-1].chainage_end
        stations: list[float] = []
        station = vertical_alignment.start_chainage
        while station <= max_chainage + 1e-9:
            stations.append(station)
            station += step
        return [
            VerticalPoint(
                chainage=station,
                elevation=self.elevation_at(vertical_alignment, station),
                gradient_permille=self.gradient_at(vertical_alignment, station),
            )
            for station in stations
        ]

    def gradient_at(self, vertical_alignment: VerticalAlignment, chainage: float) -> float:
        for segment in vertical_alignment.segments:
            if segment.chainage_start <= chainage <= segment.chainage_end:
                if isinstance(segment, VerticalGrade) or segment.length == 0:
                    return segment.gradient_end_permille
                ratio = (chainage - segment.chainage_start) / segment.length
                return segment.gradient_start_permille + ratio * (
                    segment.gradient_end_permille - segment.gradient_start_permille
                )
        return 0.0
