from __future__ import annotations

import csv
from pathlib import Path

from railcad.domain import Project


class CSVExporter:
    def export_elements(self, project: Project, path: str | Path) -> None:
        with Path(path).open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(
                handle,
                fieldnames=[
                    "id",
                    "type",
                    "length",
                    "chainage_start",
                    "chainage_end",
                    "start_x",
                    "start_y",
                    "end_x",
                    "end_y",
                ],
            )
            writer.writeheader()
            for element in project.alignment.elements:
                writer.writerow(
                    {
                        "id": element.id,
                        "type": element.element_type,
                        "length": element.length,
                        "chainage_start": element.chainage_start,
                        "chainage_end": element.chainage_end,
                        "start_x": element.start_point.x if element.start_point else "",
                        "start_y": element.start_point.y if element.start_point else "",
                        "end_x": element.end_point.x if element.end_point else "",
                        "end_y": element.end_point.y if element.end_point else "",
                    }
                )
