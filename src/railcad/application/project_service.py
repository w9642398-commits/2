from __future__ import annotations

import json
from pathlib import Path

from railcad.domain import Project
from railcad.exporters import CSVExporter, DXFExporter, HTMLReportExporter, LandXMLExporter, XLSXExporter
from railcad.geometry_engine import ProjectGeometryCoordinator
from railcad.validation import ProjectValidator


class ProjectService:
    def __init__(self) -> None:
        self.geometry = ProjectGeometryCoordinator()
        self.validator = ProjectValidator()
        self.csv_exporter = CSVExporter()
        self.xlsx_exporter = XLSXExporter()
        self.dxf_exporter = DXFExporter()
        self.landxml_exporter = LandXMLExporter()
        self.html_exporter = HTMLReportExporter()

    def load_project(self, path: str | Path) -> Project:
        return Project.from_dict(json.loads(Path(path).read_text(encoding="utf-8")))

    def save_project(self, project: Project, path: str | Path) -> None:
        Path(path).write_text(json.dumps(project.as_dict(), indent=2), encoding="utf-8")

    def rebuild(self, project: Project) -> Project:
        self.geometry.rebuild(project)
        return project

    def validate(self, project: Project):
        self.rebuild(project)
        return self.validator.validate(project)

    def export(self, project: Project, output_format: str, path: str | Path) -> None:
        issues = self.validate(project)
        if output_format == "csv":
            self.csv_exporter.export_elements(project, path)
        elif output_format == "xlsx":
            self.xlsx_exporter.export(project, issues, path)
        elif output_format == "dxf":
            self.dxf_exporter.export_alignment(project, path)
        elif output_format == "landxml":
            self.landxml_exporter.export(project, path)
        elif output_format == "html":
            self.html_exporter.export(project, issues, path)
        else:
            raise ValueError(f"Unsupported export format: {output_format}")
