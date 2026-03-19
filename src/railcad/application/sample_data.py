from __future__ import annotations

import json
from pathlib import Path

from railcad.domain import Project


def load_sample_project() -> Project:
    path = Path(__file__).resolve().parent.parent / "samples" / "sample_project.json"
    return Project.from_dict(json.loads(path.read_text(encoding="utf-8")))
