from __future__ import annotations

import csv
from pathlib import Path
from typing import Iterable

from railcad.domain import Point3D, SurveyPointSet


class SurveyImporter:
    def from_csv(self, path: str | Path, mapping: dict[str, str] | None = None, delimiter: str = ",") -> SurveyPointSet:
        mapping = mapping or {"x": "x", "y": "y", "z": "z"}
        points: list[Point3D] = []
        with Path(path).open(newline="", encoding="utf-8") as handle:
            reader = csv.DictReader(handle, delimiter=delimiter)
            for row in reader:
                points.append(
                    Point3D(
                        x=float(row[mapping["x"]]),
                        y=float(row[mapping["y"]]),
                        z=float(row.get(mapping.get("z", "z"), 0.0)),
                    )
                )
        return SurveyPointSet(name=Path(path).stem, source=str(path), points=points)

    def from_xlsx(self, path: str | Path, sheet_name: str | None = None) -> SurveyPointSet:
        try:
            import openpyxl
        except ImportError as exc:
            raise RuntimeError("XLSX import requires openpyxl.") from exc
        workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
        sheet = workbook[sheet_name] if sheet_name else workbook.active
        rows = list(sheet.iter_rows(values_only=True))
        headers = [str(value).strip().lower() for value in rows[0]]
        index = {name: headers.index(name) for name in ["x", "y", "z"] if name in headers}
        points = [
            Point3D(x=float(row[index["x"]]), y=float(row[index["y"]]), z=float(row[index.get("z", 0)] or 0.0))
            for row in rows[1:]
            if row[index["x"]] is not None and row[index["y"]] is not None
        ]
        return SurveyPointSet(name=Path(path).stem, source=str(path), points=points)
