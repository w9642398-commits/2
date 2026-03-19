from math import isclose

from railcad.application import load_sample_project
from railcad.geometry_engine import HorizontalGeometryEngine


def test_horizontal_rebuild_chainage_continuity() -> None:
    project = load_sample_project()
    engine = HorizontalGeometryEngine(sample_step=5.0)
    result = engine.rebuild(project.alignment)

    assert len(result.sampled_points) > 10
    previous = 0.0
    for element in project.alignment.elements:
        assert isclose(element.chainage_start, previous, abs_tol=1e-6)
        assert element.chainage_end > element.chainage_start
        assert element.start_point is not None
        assert element.end_point is not None
        previous = element.chainage_end


def test_arc_and_transition_change_direction() -> None:
    project = load_sample_project()
    engine = HorizontalGeometryEngine(sample_step=2.0)
    engine.rebuild(project.alignment)
    assert project.alignment.elements[1].end_azimuth_rad > project.alignment.elements[1].start_azimuth_rad
    assert project.alignment.elements[2].end_azimuth_rad > project.alignment.elements[2].start_azimuth_rad
