from __future__ import annotations

from pathlib import Path
from xml.etree import ElementTree as ET

from railcad.domain import Alignment, CircularArc, Point2D, Straight, TransitionCurve


class LandXMLImporter:
    NS = {"lx": "http://www.landxml.org/schema/LandXML-1.2"}

    def import_alignment(self, path: str | Path) -> Alignment:
        root = ET.parse(path).getroot()
        alignment_node = root.find(".//lx:Alignment", self.NS)
        if alignment_node is None:
            raise ValueError("No Alignment element found in LandXML file.")
        coord_geom = alignment_node.find("lx:CoordGeom", self.NS)
        if coord_geom is None:
            raise ValueError("No CoordGeom found in Alignment.")
        first_geom = next(iter(coord_geom), None)
        if first_geom is None:
            raise ValueError("CoordGeom is empty.")
        start = self._parse_xy(first_geom.find("lx:Start", self.NS))
        elements = []
        for child in coord_geom:
            tag = child.tag.rsplit("}", 1)[-1]
            if tag == "Line":
                start_pt = self._parse_xy(child.find("lx:Start", self.NS))
                end_pt = self._parse_xy(child.find("lx:End", self.NS))
                length = start_pt.distance_to(end_pt)
                if not elements:
                    start = start_pt
                elements.append(Straight(id=f"L{len(elements)+1}", length=length))
            elif tag == "Curve":
                length = float(child.attrib["length"])
                radius = abs(float(child.attrib["radius"]))
                rot = "ccw" if child.attrib.get("rot", "ccw").lower() == "ccw" else "cw"
                elements.append(CircularArc(id=f"A{len(elements)+1}", length=length, radius=radius, rotation=rot))
            elif tag == "Spiral":
                length = float(child.attrib["length"])
                radius = abs(float(child.attrib.get("radiusEnd", child.attrib.get("radiusStart", "1"))))
                rot = "ccw" if child.attrib.get("rot", "ccw").lower() == "ccw" else "cw"
                curve_start = child.attrib.get("spiType", "clothoid").lower() == "clothoidout"
                elements.append(
                    TransitionCurve(
                        id=f"T{len(elements)+1}",
                        length=length,
                        radius=radius,
                        rotation=rot,
                        curve_start=curve_start,
                    )
                )
        return Alignment(
            name=alignment_node.attrib.get("name", Path(path).stem),
            start_point=start,
            start_azimuth_deg=float(alignment_node.attrib.get("staStart", "0.0")) * 0.0,
            elements=elements,
        )

    @staticmethod
    def _parse_xy(node: ET.Element | None) -> Point2D:
        if node is None or node.text is None:
            raise ValueError("Missing coordinate node in LandXML.")
        y, x = [float(token) for token in node.text.split()[:2]]
        return Point2D(x=x, y=y)
