from __future__ import annotations

from html import escape
from pathlib import Path

from railcad.domain import Project, ValidationIssue


class HTMLReportExporter:
    def export(self, project: Project, issues: list[ValidationIssue], path: str | Path) -> None:
        rows = "\n".join(
            f"<tr><td>{escape(issue.severity)}</td><td>{escape(issue.code)}</td><td>{escape(issue.title)}</td><td>{escape(issue.message)}</td><td>{escape(issue.affected_object)}</td></tr>"
            for issue in issues
        )
        html = f"""
<!DOCTYPE html>
<html lang=\"en\">
<head>
  <meta charset=\"utf-8\" />
  <title>RailCAD Validation Report</title>
  <style>
    body {{ font-family: Arial, sans-serif; margin: 2rem; }}
    table {{ border-collapse: collapse; width: 100%; }}
    th, td {{ border: 1px solid #ccc; padding: 0.5rem; text-align: left; }}
    th {{ background: #efefef; }}
  </style>
</head>
<body>
  <h1>{escape(project.name)} — Validation Report</h1>
  <p>Alignment: {escape(project.alignment.name)}</p>
  <table>
    <thead><tr><th>Severity</th><th>Code</th><th>Title</th><th>Message</th><th>Object</th></tr></thead>
    <tbody>{rows}</tbody>
  </table>
</body>
</html>
"""
        Path(path).write_text(html.strip(), encoding="utf-8")
