from __future__ import annotations

from math import degrees
from pathlib import Path
from xml.etree import ElementTree as ET

from railcad.domain import CircularArc, Project, Straight, TransitionCurve


class LandXMLExporter:
    NS = "http://www.landxml.org/schema/LandXML-1.2"

    def export(self, project: Project, path: str | Path) -> None:
        ET.register_namespace("", self.NS)
        root = ET.Element(f"{{{self.NS}}}LandXML", attrib={"version": "1.2"})
        alignments = ET.SubElement(root, f"{{{self.NS}}}Alignments")
        alignment = ET.SubElement(
            alignments,
            f"{{{self.NS}}}Alignment",
            attrib={"name": project.alignment.name, "length": f"{project.alignment.elements[-1].chainage_end if project.alignment.elements else 0.0}"},
        )
        coord_geom = ET.SubElement(alignment, f"{{{self.NS}}}CoordGeom")
        for element in project.alignment.elements:
            if isinstance(element, Straight):
                node = ET.SubElement(coord_geom, f"{{{self.NS}}}Line")
            elif isinstance(element, CircularArc):
                node = ET.SubElement(
                    coord_geom,
                    f"{{{self.NS}}}Curve",
                    attrib={
                        "length": f"{element.length}",
                        "radius": f"{element.radius}",
                        "rot": element.rotation,
                        "delta": f"{degrees(element.heading_change())}",
                    },
                )
            elif isinstance(element, TransitionCurve):
                node = ET.SubElement(
                    coord_geom,
                    f"{{{self.NS}}}Spiral",
                    attrib={
                        "length": f"{element.length}",
                        "radiusEnd": f"{element.radius}",
                        "rot": element.rotation,
                        "spiType": "clothoidOut" if element.curve_start else "clothoidIn",
                    },
                )
            else:
                continue
            start = ET.SubElement(node, f"{{{self.NS}}}Start")
            start.text = f"{element.start_point.y if element.start_point else 0.0} {element.start_point.x if element.start_point else 0.0}"
            end = ET.SubElement(node, f"{{{self.NS}}}End")
            end.text = f"{element.end_point.y if element.end_point else 0.0} {element.end_point.x if element.end_point else 0.0}"
        tree = ET.ElementTree(root)
        tree.write(path, encoding="utf-8", xml_declaration=True)
