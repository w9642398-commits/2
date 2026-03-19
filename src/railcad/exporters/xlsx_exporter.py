from __future__ import annotations

from pathlib import Path

from railcad.domain import Project, ValidationIssue


class XLSXExporter:
    def export(self, project: Project, issues: list[ValidationIssue], path: str | Path) -> None:
        try:
            import openpyxl
        except ImportError as exc:
            raise RuntimeError("XLSX export requires openpyxl.") from exc
        workbook = openpyxl.Workbook()
        ws_geo = workbook.active
        ws_geo.title = "Geometry"
        ws_geo.append(["ID", "Type", "Length", "Chainage Start", "Chainage End"])
        for element in project.alignment.elements:
            ws_geo.append([element.id, element.element_type, element.length, element.chainage_start, element.chainage_end])
        ws_issues = workbook.create_sheet("Validation")
        ws_issues.append(["Severity", "Code", "Title", "Message", "Object"])
        for issue in issues:
            ws_issues.append([issue.severity, issue.code, issue.title, issue.message, issue.affected_object])
        workbook.save(path)
