from __future__ import annotations

from pathlib import Path

from railcad.domain import Project


class DXFExporter:
    def export_alignment(self, project: Project, path: str | Path) -> None:
        points = project.alignment.sampled_points or [project.alignment.start_point]
        lines = [
            "0", "SECTION", "2", "ENTITIES",
            "0", "POLYLINE", "8", "ALIGNMENT", "66", "1", "70", "0",
        ]
        for point in points:
            lines.extend(["0", "VERTEX", "8", "ALIGNMENT", "10", f"{point.x}", "20", f"{point.y}", "30", "0.0"])
        lines.extend(["0", "SEQEND", "0", "ENDSEC", "0", "EOF"])
        Path(path).write_text("\n".join(lines), encoding="ascii")
