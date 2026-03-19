from __future__ import annotations

from dataclasses import dataclass
from math import cos, degrees, radians, sin

from railcad.domain import Alignment, CircularArc, HorizontalElement, Point2D, Straight, TransitionCurve


@dataclass(slots=True)
class ReconstructionResult:
    alignment: Alignment
    sampled_points: list[Point2D]


class HorizontalGeometryEngine:
    def __init__(self, sample_step: float = 10.0) -> None:
        self.sample_step = sample_step

    def rebuild(self, alignment: Alignment) -> ReconstructionResult:
        current_point = alignment.start_point
        current_azimuth = radians(alignment.start_azimuth_deg)
        current_chainage = 0.0
        sampled: list[Point2D] = [current_point]
        for element in alignment.elements:
            element.start_point = current_point
            element.chainage_start = current_chainage
            element.start_azimuth_rad = current_azimuth
            if isinstance(element, Straight):
                end_point, end_azimuth, element_samples = self._solve_straight(element, current_point, current_azimuth)
            elif isinstance(element, CircularArc):
                end_point, end_azimuth, element_samples = self._solve_arc(element, current_point, current_azimuth)
            elif isinstance(element, TransitionCurve):
                end_point, end_azimuth, element_samples = self._solve_transition(element, current_point, current_azimuth)
            else:
                raise TypeError(f"Unsupported element type: {type(element)!r}")
            current_chainage += element.length
            element.chainage_end = current_chainage
            element.end_point = end_point
            element.end_azimuth_rad = end_azimuth
            current_point = end_point
            current_azimuth = end_azimuth
            sampled.extend(element_samples[1:])
        alignment.sampled_points = sampled
        return ReconstructionResult(alignment=alignment, sampled_points=sampled)

    def point_at_chainage(self, alignment: Alignment, chainage: float) -> Point2D:
        if not alignment.elements:
            return alignment.start_point
        for element in alignment.elements:
            if element.chainage_start <= chainage <= element.chainage_end:
                return self._interpolate_element(element, chainage)
        return alignment.elements[-1].end_point or alignment.start_point

    def _interpolate_element(self, element: HorizontalElement, chainage: float) -> Point2D:
        local = chainage - element.chainage_start
        if isinstance(element, Straight):
            x = element.start_point.x + local * cos(element.start_azimuth_rad)
            y = element.start_point.y + local * sin(element.start_azimuth_rad)
            return Point2D(x, y)
        if isinstance(element, CircularArc):
            samples = self._sample_arc_points(element, element.start_point, element.start_azimuth_rad)
            return self._nearest_by_chainage(samples, local, element.length)
        if isinstance(element, TransitionCurve):
            samples = self._sample_transition_points(element, element.start_point, element.start_azimuth_rad)
            return self._nearest_by_chainage(samples, local, element.length)
        raise TypeError(type(element))

    @staticmethod
    def _nearest_by_chainage(samples: list[Point2D], local: float, total: float) -> Point2D:
        if len(samples) == 1 or total <= 0:
            return samples[-1]
        idx = int(round((local / total) * (len(samples) - 1)))
        idx = max(0, min(len(samples) - 1, idx))
        return samples[idx]

    def _solve_straight(self, element: Straight, point: Point2D, azimuth: float) -> tuple[Point2D, float, list[Point2D]]:
        x = point.x + element.length * cos(azimuth)
        y = point.y + element.length * sin(azimuth)
        samples = [
            Point2D(point.x + s * cos(azimuth), point.y + s * sin(azimuth))
            for s in self._stations(element.length)
        ]
        return Point2D(x, y), azimuth, samples

    def _solve_arc(self, element: CircularArc, point: Point2D, azimuth: float) -> tuple[Point2D, float, list[Point2D]]:
        sign = 1.0 if element.rotation == "ccw" else -1.0
        delta = sign * element.length / element.radius
        end_azimuth = azimuth + delta
        x = point.x + (element.radius / sign) * (sin(end_azimuth) - sin(azimuth))
        y = point.y - (element.radius / sign) * (cos(end_azimuth) - cos(azimuth))
        samples = self._sample_arc_points(element, point, azimuth)
        return Point2D(x, y), end_azimuth, samples

    def _sample_arc_points(self, element: CircularArc, point: Point2D, azimuth: float) -> list[Point2D]:
        sign = 1.0 if element.rotation == "ccw" else -1.0
        pts = []
        for s in self._stations(element.length):
            theta = azimuth + sign * s / element.radius
            x = point.x + (element.radius / sign) * (sin(theta) - sin(azimuth))
            y = point.y - (element.radius / sign) * (cos(theta) - cos(azimuth))
            pts.append(Point2D(x, y))
        return pts

    def _solve_transition(self, element: TransitionCurve, point: Point2D, azimuth: float) -> tuple[Point2D, float, list[Point2D]]:
        samples = self._sample_transition_points(element, point, azimuth)
        delta = self._transition_heading_change(element)
        end_azimuth = azimuth + delta
        return samples[-1], end_azimuth, samples

    def _sample_transition_points(self, element: TransitionCurve, point: Point2D, azimuth: float) -> list[Point2D]:
        sign = 1.0 if element.rotation == "ccw" else -1.0
        stations = self._stations(element.length)
        if len(stations) == 1:
            return [point]
        x = point.x
        y = point.y
        heading = azimuth
        pts = [point]
        for s_prev, s_next in zip(stations[:-1], stations[1:]):
            step = s_next - s_prev
            curvature = self._transition_curvature(element, s_prev + step / 2.0)
            heading += sign * curvature * step
            x += step * cos(heading)
            y += step * sin(heading)
            pts.append(Point2D(float(x), float(y)))
        return pts

    @staticmethod
    def _transition_heading_change(element: TransitionCurve) -> float:
        sign = 1.0 if element.rotation == "ccw" else -1.0
        delta = element.length / (2.0 * element.radius)
        return sign * (delta if not element.curve_start else delta)

    @staticmethod
    def _transition_curvature(element: TransitionCurve, station: float) -> float:
        ratio = station / element.length if element.length else 0.0
        if element.curve_start:
            ratio = 1.0 - ratio
        return ratio / element.radius

    def _stations(self, length: float) -> list[float]:
        if length <= 0:
            return [0.0]
        count = max(2, int(length / self.sample_step) + 1)
        step = length / (count - 1)
        return [i * step for i in range(count)]

    def element_summary(self, alignment: Alignment) -> list[dict[str, float | str]]:
        return [
            {
                "id": element.id,
                "type": element.element_type,
                "length": element.length,
                "chainage_start": element.chainage_start,
                "chainage_end": element.chainage_end,
                "start_azimuth_deg": degrees(element.start_azimuth_rad),
                "end_azimuth_deg": degrees(element.end_azimuth_rad),
            }
            for element in alignment.elements
        ]
