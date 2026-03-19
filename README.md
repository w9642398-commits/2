# RailCAD MVP

RailCAD is a local desktop engineering MVP for railway alignment design. The repository contains:

- domain entities for horizontal, vertical and cant geometry,
- a geometry engine for deterministic track reconstruction,
- a configurable validation engine inspired by ST-T1-A6 rule groups,
- CSV / TXT / XLSX / LandXML importers,
- CSV / XLSX / DXF / LandXML / HTML exporters,
- a PySide6 desktop UI,
- tests and a sample project.

## Quick start

```bash
python -m venv .venv
source .venv/bin/activate
pip install -e .[dev,full]
pytest
railcad-ui
```

If optional UI dependencies are not installed, the computational core and CLI remain available.

## CLI examples

```bash
railcad-cli sample --output sample_project.json
railcad-cli validate --project sample_project.json
railcad-cli export --project sample_project.json --format html --output report.html
```
