"""Verify the versioned world-definition source with the server's actual generator."""

import json
from pathlib import Path
import sys
import tempfile

from tool_process import execute


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: world_definitions.py <studio-tool.dll> <server-tool.dll>")
    studio, server = (Path(argument).resolve() for argument in sys.argv[1:])
    if not studio.is_file() or not server.is_file():
        raise FileNotFoundError("Both built tool assemblies must exist")

    with tempfile.TemporaryDirectory(prefix="world-definitions-") as temporary:
        root = Path(temporary)
        project = root / "project"
        project.mkdir()
        execute(studio, "create", project, "World definitions")
        terrain = execute(studio, "new-terrain", project, "Ground", 1010, 2, 2, 1, 0, 0)
        second = execute(studio, "new-world", project, "Second", 2003, terrain, "0xFFFFFFFFFFFFFFFF")
        execute(studio, "new-world", project, "First", 2002, terrain, "274877906960")
        execute(studio, "edit-world", project, second, "Second display name", 2003, terrain,
                "18446744073709551615")

        source = root / "source.json"
        alternate = root / "alternate.json"
        compiled = root / "map-definitions.json"
        compiled_again = root / "map-definitions-again.json"
        execute(studio, "export-map-definitions", project, source)
        execute(studio, "export-map-definitions", project, alternate)
        if source.read_bytes() != alternate.read_bytes():
            raise AssertionError("Source revision export is not reproducible")

        records = [
            {"mapId": 2002, "mapDataId": 1010, "flags": 274877906960},
            {"mapId": 2003, "mapDataId": 1010, "flags": 18446744073709551615},
        ]
        if json.loads(source.read_bytes()) != {"formatVersion": 1, "maps": records}:
            raise AssertionError("Studio source did not preserve world identity, terrain aliasing, and 64-bit flags")

        execute(server, "generate-map-definitions", source, compiled)
        execute(server, "generate-map-definitions", alternate, compiled_again)
        if compiled.read_bytes() != compiled_again.read_bytes():
            raise AssertionError("Server-generated map-definition catalog is not deterministic")
        if json.loads(compiled.read_bytes()) != {"formatVersion": 1, "maps": records}:
            raise AssertionError("Server generator did not accept the authored identities and complete flags")

        execute(server, "generate-map-definitions", source, compiled, expected=0)
        malformed = root / "duplicate.json"
        malformed.write_text(json.dumps({"formatVersion": 1, "maps": records + [records[0]]}))
        rejected = root / "rejected.json"
        execute(server, "generate-map-definitions", malformed, rejected, expected=1)
        if rejected.exists():
            raise AssertionError("Invalid world definitions published an output")

    print("World-definition source and server-generated catalog integration passed")


if __name__ == "__main__":
    main()
