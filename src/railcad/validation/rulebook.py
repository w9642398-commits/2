from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(slots=True)
class RuleBook:
    data: dict[str, Any]

    @classmethod
    def load_default(cls) -> "RuleBook":
        path = Path(__file__).resolve().parent.parent / "data" / "st_t1_a6_rules.json"
        return cls(json.loads(path.read_text()))

    @classmethod
    def load(cls, path: str | Path) -> "RuleBook":
        return cls(json.loads(Path(path).read_text()))
