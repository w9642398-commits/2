from __future__ import annotations

from typing import Iterable

from railcad.application import ProjectService, load_sample_project
from railcad.domain import Project, ValidationIssue

try:
    from PySide6.QtCore import Qt
    from PySide6.QtGui import QAction
    from PySide6.QtWidgets import (
        QFileDialog,
        QHBoxLayout,
        QLabel,
        QMainWindow,
        QMessageBox,
        QPlainTextEdit,
        QSplitter,
        QTableWidget,
        QTableWidgetItem,
        QTabWidget,
        QTreeWidget,
        QTreeWidgetItem,
        QVBoxLayout,
        QWidget,
    )
except ImportError as exc:  # pragma: no cover
    raise RuntimeError("PySide6 is required for the desktop UI.") from exc


class MainWindow(QMainWindow):
    def __init__(self) -> None:
        super().__init__()
        self.service = ProjectService()
        self.project: Project = self.service.rebuild(load_sample_project())
        self.issues: list[ValidationIssue] = self.service.validate(self.project)
        self.setWindowTitle("RailCAD MVP")
        self.resize(1400, 900)
        self._build_menu()
        self._build_ui()
        self.refresh_all()

    def _build_menu(self) -> None:
        file_menu = self.menuBar().addMenu("Project")
        open_action = QAction("Open JSON", self)
        open_action.triggered.connect(self.open_project)
        save_action = QAction("Save JSON", self)
        save_action.triggered.connect(self.save_project)
        validate_action = QAction("Validate", self)
        validate_action.triggered.connect(self.run_validation)
        export_action = QAction("Export HTML report", self)
        export_action.triggered.connect(self.export_report)
        for action in [open_action, save_action, validate_action, export_action]:
            file_menu.addAction(action)

    def _build_ui(self) -> None:
        root = QWidget()
        layout = QHBoxLayout(root)
        splitter = QSplitter(Qt.Horizontal)
        layout.addWidget(splitter)
        self.setCentralWidget(root)

        self.tree = QTreeWidget()
        self.tree.setHeaderLabels(["Project Explorer"])
        self.tree.itemSelectionChanged.connect(self.highlight_selected)
        splitter.addWidget(self.tree)

        center_splitter = QSplitter(Qt.Vertical)
        splitter.addWidget(center_splitter)

        tabs = QTabWidget()
        self.plan_text = QPlainTextEdit()
        self.plan_text.setReadOnly(True)
        self.profile_text = QPlainTextEdit()
        self.profile_text.setReadOnly(True)
        tabs.addTab(self.plan_text, "Plan")
        tabs.addTab(self.profile_text, "Profile")
        center_splitter.addWidget(tabs)

        self.validation_table = QTableWidget(0, 5)
        self.validation_table.setHorizontalHeaderLabels(["Severity", "Code", "Title", "Object", "Message"])
        center_splitter.addWidget(self.validation_table)

        right_panel = QWidget()
        right_layout = QVBoxLayout(right_panel)
        right_layout.addWidget(QLabel("Properties / Criteria"))
        self.properties = QPlainTextEdit()
        self.properties.setReadOnly(True)
        right_layout.addWidget(self.properties)
        right_layout.addWidget(QLabel("Geometry Elements"))
        self.element_table = QTableWidget(0, 4)
        self.element_table.setHorizontalHeaderLabels(["ID", "Type", "Start", "End"])
        right_layout.addWidget(self.element_table)
        splitter.addWidget(right_panel)
        splitter.setSizes([220, 760, 420])

    def refresh_all(self) -> None:
        self.populate_tree()
        self.populate_plan_view()
        self.populate_profile_view()
        self.populate_properties()
        self.populate_element_table()
        self.populate_validation_table()

    def populate_tree(self) -> None:
        self.tree.clear()
        root = QTreeWidgetItem([self.project.name])
        root.addChild(QTreeWidgetItem([f"Alignment: {self.project.alignment.name}"]))
        root.addChild(QTreeWidgetItem([f"Vertical: {self.project.vertical_alignment.name}"]))
        root.addChild(QTreeWidgetItem([f"Cant: {self.project.cant_alignment.name}"]))
        turnouts = QTreeWidgetItem(["Turnouts"])
        for turnout in self.project.turnouts:
            turnouts.addChild(QTreeWidgetItem([f"{turnout.id} @ {turnout.chainage:.2f}"]))
        root.addChild(turnouts)
        self.tree.addTopLevelItem(root)
        self.tree.expandAll()

    def populate_plan_view(self) -> None:
        lines = ["CHAINAGE | X | Y | ELEMENT"]
        for element in self.project.alignment.elements:
            lines.append(
                f"{element.chainage_start:8.2f} - {element.chainage_end:8.2f} | "
                f"({element.start_point.x:10.3f}, {element.start_point.y:10.3f}) -> "
                f"({element.end_point.x:10.3f}, {element.end_point.y:10.3f}) | {element.element_type} {element.id}"
            )
        self.plan_text.setPlainText("\n".join(lines))

    def populate_profile_view(self) -> None:
        lines = ["CHAINAGE | ELEVATION | GRADE"]
        for segment in self.project.vertical_alignment.segments:
            lines.append(
                f"{segment.chainage_start:8.2f} - {segment.chainage_end:8.2f} | "
                f"{segment.elevation_start:8.3f} -> {segment.elevation_end:8.3f} | "
                f"{segment.gradient_start_permille:.2f}‰ -> {segment.gradient_end_permille:.2f}‰ ({segment.segment_type})"
            )
        self.profile_text.setPlainText("\n".join(lines))

    def populate_properties(self) -> None:
        c = self.project.design_criteria
        text = (
            f"Criteria: {c.name}\n"
            f"Max speed: {c.max_speed_kmh} km/h\n"
            f"Min radius: {c.min_radius_m} m\n"
            f"Min transition: {c.min_transition_length_m} m\n"
            f"Min tangent: {c.min_tangent_length_m} m\n"
            f"Max cant: {c.max_cant_mm} mm\n"
            f"Max gradient: {c.max_gradient_permille} ‰\n"
            f"Min vertical curve: {c.min_vertical_curve_length_m} m"
        )
        self.properties.setPlainText(text)

    def populate_element_table(self) -> None:
        self.element_table.setRowCount(len(self.project.alignment.elements))
        for row, element in enumerate(self.project.alignment.elements):
            values = [element.id, element.element_type, f"{element.chainage_start:.2f}", f"{element.chainage_end:.2f}"]
            for col, value in enumerate(values):
                self.element_table.setItem(row, col, QTableWidgetItem(value))

    def populate_validation_table(self) -> None:
        self.validation_table.setRowCount(len(self.issues))
        for row, issue in enumerate(self.issues):
            values = [issue.severity, issue.code, issue.title, issue.affected_object, issue.message]
            for col, value in enumerate(values):
                self.validation_table.setItem(row, col, QTableWidgetItem(str(value)))

    def highlight_selected(self) -> None:
        items = self.tree.selectedItems()
        if not items:
            return
        text = items[0].text(0)
        for row in range(self.element_table.rowCount()):
            match = text.startswith(self.element_table.item(row, 0).text())
            self.element_table.selectRow(row) if match else None

    def open_project(self) -> None:
        path, _ = QFileDialog.getOpenFileName(self, "Open project", filter="JSON Files (*.json)")
        if not path:
            return
        self.project = self.service.load_project(path)
        self.project = self.service.rebuild(self.project)
        self.issues = self.service.validate(self.project)
        self.refresh_all()

    def save_project(self) -> None:
        path, _ = QFileDialog.getSaveFileName(self, "Save project", filter="JSON Files (*.json)")
        if not path:
            return
        self.service.save_project(self.project, path)
        QMessageBox.information(self, "RailCAD", f"Project saved to {path}")

    def run_validation(self) -> None:
        self.issues = self.service.validate(self.project)
        self.populate_validation_table()

    def export_report(self) -> None:
        path, _ = QFileDialog.getSaveFileName(self, "Export HTML", filter="HTML Files (*.html)")
        if not path:
            return
        self.service.export(self.project, "html", path)
        QMessageBox.information(self, "RailCAD", f"Report exported to {path}")
