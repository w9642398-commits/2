from pathlib import Path
from xml.etree import ElementTree as ET

from railcad.application import ProjectService, load_sample_project
from railcad.importers import LandXMLImporter


def test_landxml_roundtrip(tmp_path: Path) -> None:
    service = ProjectService()
    project = service.rebuild(load_sample_project())
    output = tmp_path / "alignment.xml"
    service.export(project, "landxml", output)
    root = ET.parse(output).getroot()
    assert root.tag.endswith("LandXML")
    imported = LandXMLImporter().import_alignment(output)
    assert imported.name == project.alignment.name
    assert len(imported.elements) == len(project.alignment.elements)


def test_csv_and_html_export(tmp_path: Path) -> None:
    service = ProjectService()
    project = service.rebuild(load_sample_project())
    csv_path = tmp_path / "elements.csv"
    html_path = tmp_path / "report.html"
    service.export(project, "csv", csv_path)
    service.export(project, "html", html_path)
    assert csv_path.read_text(encoding="utf-8").startswith("id,type")
    assert "Validation Report" in html_path.read_text(encoding="utf-8")
