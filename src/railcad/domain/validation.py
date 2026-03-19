from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(slots=True)
class ValidationRule:
    code: str
    title: str
    severity: str
    parameters: dict[str, Any]


@dataclass(slots=True)
class ValidationIssue:
    severity: str
    code: str
    title: str
    message: str
    affected_object: str
    chainage_start: float | None = None
    chainage_end: float | None = None
    expected_value: str | float | None = None
    actual_value: str | float | None = None
    suggestion: str = ""

    def as_dict(self) -> dict[str, Any]:
        return {
            "severity": self.severity,
            "code": self.code,
            "title": self.title,
            "message": self.message,
            "affected_object": self.affected_object,
            "chainage_start": self.chainage_start,
            "chainage_end": self.chainage_end,
            "expected_value": self.expected_value,
            "actual_value": self.actual_value,
            "suggestion": self.suggestion,
        }
