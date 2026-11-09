"""Verify painted terrain revisions through the server's actual compiled pair readers."""

import json
from pathlib import Path
import sys
import tempfile

from tool_process import execute


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: terrain_regions.py <studio-tool.dll> <server-tool.dll>")
    studio, server = (Path(argument).resolve() for argument in sys.argv[1:])
    if not studio.is_file() or not server.is_file():
        raise FileNotFoundError("Both built tool assemblies must exist")

    with tempfile.TemporaryDirectory(prefix="terrain-regions-") as temporary:
        root = Path(temporary)
        project = root / "project"
        project.mkdir()
        execute(studio, "create", project, "Terrain regions")
        terrain = execute(studio, "new-terrain", project, "Ground", 9311, 4, 3, 8, 0, 5)
        execute(studio, "terrain-region", project, terrain, 1, 1, 3, 2, 65535, 29, -17)
        execute(studio, "terrain-exit", project, terrain, "new", -2, 40, 9, 0)

        source = root / "terrain.json"
        execute(studio, "export-base-terrain", project, terrain, source)
        authored = json.loads(source.read_bytes())
        expected = [
            {"surfaceId": 65535, "passabilityFlag": 29, "elevation": -17}
            if x >= 1 and y >= 1 else {"surfaceId": 8, "passabilityFlag": 0, "elevation": 5}
            for y in range(3) for x in range(4)
        ]
        if authored != {
            "formatVersion": 1,
            "mapDataId": 9311,
            "width": 4,
            "height": 3,
            "cells": expected,
            "exits": [{"x": -2, "y": 40, "passwayIndex": 9}],
        }:
            raise AssertionError("Rectangle paint did not preserve exact cells and ordered exits in server source")

        output = root / "compiled"
        again = root / "compiled-again"
        execute(studio, "build-terrain", project, terrain, server, output)
        execute(studio, "build-terrain", project, terrain, server, again)
        for relative in ("terrains/9311.ocbt", "collisions/9311.ocmc"):
            first, second = output / relative, again / relative
            if not first.is_file() or not second.is_file() or first.stat().st_size == 0:
                raise AssertionError(f"Required server artifact missing: {relative}")
            if first.read_bytes() != second.read_bytes():
                raise AssertionError(f"Server artifact was not deterministic: {relative}")

        invalid = root / "invalid.json"
        invalid.write_text(json.dumps({**authored, "width": 0}), encoding="utf-8")
        rejected = root / "rejected"
        execute(server, "build-authored-terrain", invalid, rejected, expected=1)
        if rejected.exists():
            raise AssertionError("Invalid terrain published a server artifact directory")

    print("Studio terrain region, source export and validated server pair integration passed")


if __name__ == "__main__":
    main()
