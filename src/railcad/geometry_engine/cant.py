from __future__ import annotations

from dataclasses import dataclass

from railcad.domain import CantAlignment


@dataclass(slots=True)
class CantPoint:
    chainage: float
    cant_mm: float


class CantEngine:
    def sample(self, cant_alignment: CantAlignment, step: float = 10.0) -> list[CantPoint]:
        if not cant_alignment.segments:
            return []
        start = min(segment.chainage_start for segment in cant_alignment.segments)
        end = max(segment.chainage_end for segment in cant_alignment.segments)
        samples: list[CantPoint] = []
        station = start
        while station <= end + 1e-9:
            samples.append(CantPoint(chainage=station, cant_mm=cant_alignment.cant_at(station)))
            station += step
        return samples

    def max_gradient(self, cant_alignment: CantAlignment) -> float:
        max_gradient = 0.0
        for segment in cant_alignment.segments:
            if segment.length > 0:
                gradient = abs(segment.cant_end_mm - segment.cant_start_mm) / segment.length
                max_gradient = max(max_gradient, gradient)
        return max_gradient
