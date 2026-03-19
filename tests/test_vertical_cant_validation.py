from railcad.application import load_sample_project
from railcad.geometry_engine import CantEngine, VerticalGeometryEngine
from railcad.validation import ProjectValidator


def test_vertical_alignment_rebuild_and_query() -> None:
    project = load_sample_project()
    engine = VerticalGeometryEngine()
    points = engine.rebuild(project.vertical_alignment)
    assert len(points) == 4
    assert points[-1].elevation > points[0].elevation
    elevation_mid = engine.elevation_at(project.vertical_alignment, 340.0)
    assert elevation_mid > project.vertical_alignment.start_elevation


def test_cant_sampling_and_gradient() -> None:
    project = load_sample_project()
    engine = CantEngine()
    samples = engine.sample(project.cant_alignment, step=20.0)
    assert len(samples) >= 10
    assert engine.max_gradient(project.cant_alignment) > 0.0


def test_validator_detects_radius_issue_when_project_is_modified() -> None:
    project = load_sample_project()
    project.alignment.elements[2].radius = 200.0
    validator = ProjectValidator()
    issues = validator.validate(project)
    codes = {issue.code for issue in issues}
    assert "HZ001" in codes
