from __future__ import annotations

from math import isclose

from railcad.domain import CircularArc, Project, Straight, TransitionCurve, ValidationIssue
from railcad.geometry_engine import CantEngine, HorizontalGeometryEngine, VerticalGeometryEngine
from .rulebook import RuleBook


class ProjectValidator:
    def __init__(self, rulebook: RuleBook | None = None) -> None:
        self.rulebook = rulebook or RuleBook.load_default()
        self.horizontal_engine = HorizontalGeometryEngine()
        self.vertical_engine = VerticalGeometryEngine()
        self.cant_engine = CantEngine()

    def validate(self, project: Project) -> list[ValidationIssue]:
        self.horizontal_engine.rebuild(project.alignment)
        self.vertical_engine.rebuild(project.vertical_alignment)
        issues: list[ValidationIssue] = []
        issues.extend(self._validate_chainage(project))
        issues.extend(self._validate_element_sequence(project))
        issues.extend(self._validate_horizontal(project))
        issues.extend(self._validate_vertical(project))
        issues.extend(self._validate_cant(project))
        issues.extend(self._validate_cross_consistency(project))
        return issues

    def _validate_chainage(self, project: Project) -> list[ValidationIssue]:
        issues: list[ValidationIssue] = []
        previous_end = 0.0
        for element in project.alignment.elements:
            if not isclose(element.chainage_start, previous_end, abs_tol=1e-6):
                issues.append(
                    ValidationIssue(
                        severity="error",
                        code="CH001",
                        title="Chainage discontinuity",
                        message="Horizontal chainage is discontinuous.",
                        affected_object=element.id,
                        chainage_start=element.chainage_start,
                        expected_value=previous_end,
                        actual_value=element.chainage_start,
                        suggestion="Rebuild the alignment or correct preceding element lengths.",
                    )
                )
            previous_end = element.chainage_end
        return issues

    def _validate_element_sequence(self, project: Project) -> list[ValidationIssue]:
        issues: list[ValidationIssue] = []
        elements = project.alignment.elements
        for previous, current in zip(elements, elements[1:]):
            if isinstance(previous, CircularArc) and isinstance(current, CircularArc) and previous.rotation != current.rotation:
                issues.append(
                    ValidationIssue(
                        severity="warning",
                        code="SEQ001",
                        title="Back-to-back reverse arcs",
                        message="Opposite arcs should usually be separated by a transition or tangent.",
                        affected_object=f"{previous.id}->{current.id}",
                        chainage_start=previous.chainage_end,
                        suggestion="Insert a tangent or paired transition curves.",
                    )
                )
        return issues

    def _validate_horizontal(self, project: Project) -> list[ValidationIssue]:
        issues: list[ValidationIssue] = []
        rules = self.rulebook.data
        min_tangent = rules["tangent_insertion_limits"]["min_length_m"]
        min_transition = rules["transition_length_limits"]["min_length_m"]
        for element in project.alignment.elements:
            if isinstance(element, CircularArc):
                speed = project.speed_profile.speed_at(element.chainage_start, project.design_criteria.max_speed_kmh)
                radius_limit = self._radius_limit_for_speed(speed)
                if element.radius < radius_limit:
                    issues.append(
                        ValidationIssue(
                            severity="error",
                            code="HZ001",
                            title="Radius below minimum",
                            message="Arc radius is below configured limit for the active speed profile.",
                            affected_object=element.id,
                            chainage_start=element.chainage_start,
                            chainage_end=element.chainage_end,
                            expected_value=radius_limit,
                            actual_value=element.radius,
                            suggestion="Increase radius or reduce allowed speed on this section.",
                        )
                    )
            if isinstance(element, TransitionCurve) and element.length < min_transition:
                issues.append(
                    ValidationIssue(
                        severity="error",
                        code="HZ002",
                        title="Transition too short",
                        message="Transition curve length is below configured minimum.",
                        affected_object=element.id,
                        chainage_start=element.chainage_start,
                        chainage_end=element.chainage_end,
                        expected_value=min_transition,
                        actual_value=element.length,
                        suggestion="Increase clothoid length or relax criteria in the rulebook.",
                    )
                )
            if isinstance(element, Straight) and 0 < element.length < min_tangent:
                issues.append(
                    ValidationIssue(
                        severity="warning",
                        code="HZ003",
                        title="Tangent insertion too short",
                        message="Straight insert is shorter than configured minimum.",
                        affected_object=element.id,
                        chainage_start=element.chainage_start,
                        chainage_end=element.chainage_end,
                        expected_value=min_tangent,
                        actual_value=element.length,
                        suggestion="Merge homogeneous geometry or increase tangent length.",
                    )
                )
        return issues

    def _validate_vertical(self, project: Project) -> list[ValidationIssue]:
        issues: list[ValidationIssue] = []
        limits = self.rulebook.data["vertical_geometry_limits"]
        for segment in project.vertical_alignment.segments:
            if max(abs(segment.gradient_start_permille), abs(segment.gradient_end_permille)) > limits["max_gradient_permille"]:
                issues.append(
                    ValidationIssue(
                        severity="error",
                        code="VT001",
                        title="Vertical gradient too large",
                        message="Vertical gradient exceeds configured maximum.",
                        affected_object=segment.id,
                        chainage_start=segment.chainage_start,
                        chainage_end=segment.chainage_end,
                        expected_value=limits["max_gradient_permille"],
                        actual_value=max(abs(segment.gradient_start_permille), abs(segment.gradient_end_permille)),
                        suggestion="Reduce gradient or modify design criteria.",
                    )
                )
            if segment.segment_type == "VerticalCurve" and segment.length < limits["min_vertical_curve_length_m"]:
                issues.append(
                    ValidationIssue(
                        severity="warning",
                        code="VT002",
                        title="Vertical curve too short",
                        message="Vertical curve length is below configured minimum.",
                        affected_object=segment.id,
                        chainage_start=segment.chainage_start,
                        chainage_end=segment.chainage_end,
                        expected_value=limits["min_vertical_curve_length_m"],
                        actual_value=segment.length,
                        suggestion="Lengthen the crest/sag curve.",
                    )
                )
        return issues

    def _validate_cant(self, project: Project) -> list[ValidationIssue]:
        issues: list[ValidationIssue] = []
        limits = self.rulebook.data["cant_limits"]
        gradient = self.cant_engine.max_gradient(project.cant_alignment)
        if gradient > limits["max_cant_gradient_mm_per_m"]:
            issues.append(
                ValidationIssue(
                    severity="error",
                    code="CT001",
                    title="Cant ramp too steep",
                    message="Cant gradient exceeds configured maximum.",
                    affected_object=project.cant_alignment.name,
                    expected_value=limits["max_cant_gradient_mm_per_m"],
                    actual_value=gradient,
                    suggestion="Extend cant ramp length.",
                )
            )
        for segment in project.cant_alignment.segments:
            peak = max(abs(segment.cant_start_mm), abs(segment.cant_end_mm))
            if peak > limits["max_cant_mm"]:
                issues.append(
                    ValidationIssue(
                        severity="error",
                        code="CT002",
                        title="Cant exceeds limit",
                        message="Cant exceeds configured maximum value.",
                        affected_object=segment.id,
                        chainage_start=segment.chainage_start,
                        chainage_end=segment.chainage_end,
                        expected_value=limits["max_cant_mm"],
                        actual_value=peak,
                        suggestion="Reduce cant or adjust curve radius/speed.",
                    )
                )
        return issues

    def _validate_cross_consistency(self, project: Project) -> list[ValidationIssue]:
        issues: list[ValidationIssue] = []
        for element in project.alignment.elements:
            if isinstance(element, CircularArc):
                mid_chainage = (element.chainage_start + element.chainage_end) / 2.0
                cant = project.cant_alignment.cant_at(mid_chainage)
                speed = project.speed_profile.speed_at(mid_chainage, project.design_criteria.max_speed_kmh)
                desired_cant = min(project.design_criteria.max_cant_mm, (11.8 * speed * speed) / max(element.radius, 1.0))
                if abs(cant - desired_cant) > 60.0:
                    issues.append(
                        ValidationIssue(
                            severity="warning",
                            code="CO001",
                            title="Cant / radius mismatch",
                            message="Cant is not well synchronized with curvature and speed.",
                            affected_object=element.id,
                            chainage_start=element.chainage_start,
                            chainage_end=element.chainage_end,
                            expected_value=round(desired_cant, 1),
                            actual_value=round(cant, 1),
                            suggestion="Rebalance cant or speed profile on the arc.",
                        )
                    )
        if project.vertical_alignment.segments:
            end_h = project.alignment.elements[-1].chainage_end if project.alignment.elements else 0.0
            end_v = project.vertical_alignment.segments[-1].chainage_end
            if abs(end_h - end_v) > 1e-6:
                issues.append(
                    ValidationIssue(
                        severity="warning",
                        code="CO002",
                        title="Plan/profile extent mismatch",
                        message="Horizontal alignment and vertical profile have different total lengths.",
                        affected_object=project.name,
                        expected_value=end_h,
                        actual_value=end_v,
                        suggestion="Extend or trim the profile so both disciplines share the same corridor extent.",
                    )
                )
        return issues

    def _radius_limit_for_speed(self, speed: float) -> float:
        for band in self.rulebook.data["radius_limits"]["speed_bands"]:
            if speed <= band["max_speed_kmh"]:
                return band["min_radius_m"]
        return self.rulebook.data["radius_limits"]["default_min_radius_m"]
