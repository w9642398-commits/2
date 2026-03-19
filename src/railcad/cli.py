from __future__ import annotations

import argparse
from pathlib import Path

from railcad.application import ProjectService, load_sample_project


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="railcad-cli")
    sub = parser.add_subparsers(dest="command", required=True)

    sample = sub.add_parser("sample")
    sample.add_argument("--output", required=True)

    validate = sub.add_parser("validate")
    validate.add_argument("--project", required=True)

    export = sub.add_parser("export")
    export.add_argument("--project", required=True)
    export.add_argument("--format", required=True, choices=["csv", "xlsx", "dxf", "landxml", "html"])
    export.add_argument("--output", required=True)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    service = ProjectService()
    if args.command == "sample":
        project = load_sample_project()
        service.save_project(project, args.output)
        print(f"Sample project written to {args.output}")
        return 0
    if args.command == "validate":
        project = service.load_project(args.project)
        issues = service.validate(project)
        for issue in issues:
            print(f"{issue.severity.upper()} {issue.code}: {issue.title} [{issue.affected_object}] {issue.message}")
        print(f"Issues: {len(issues)}")
        return 0
    if args.command == "export":
        project = service.load_project(args.project)
        service.export(project, args.format, args.output)
        print(f"Exported {args.format} to {args.output}")
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
