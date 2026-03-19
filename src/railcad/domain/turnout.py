from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(slots=True)
class Turnout:
    id: str
    name: str
    chainage: float
    diverging_radius_m: float
    branch_angle_deg: float

    def as_dict(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "chainage": self.chainage,
            "diverging_radius_m": self.diverging_radius_m,
            "branch_angle_deg": self.branch_angle_deg,
        }
