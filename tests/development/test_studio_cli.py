"""Exercise the compiled command-line host in independent processes."""

import json
from pathlib import Path
import subprocess
import tempfile
import time
import unittest


ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools/OpenConquerStudio.Tool/bin/Release/net10.0/OpenConquerStudio.Tool.dll"


class StudioCliTests(unittest.TestCase):
    def run_tool(self, *args, expected=0):
        result = subprocess.run(
            ["dotnet", str(TOOL), *map(str, args)],
            capture_output=True, text=True, timeout=30, check=False,
        )
        self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
        return result

    def test_authoring_build_and_verification_across_processes(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Independent game")
            header = bytearray(18)
            header[2], header[12], header[14], header[16], header[17] = 10, 2, 1, 32, 8
            source = root / "two-colors.tga"
            payload = header + bytes([1, 0, 0, 255, 255, 0, 255, 0, 255])
            source.write_bytes(payload)
            asset = self.run_tool("import", project, source, "Sprite").stdout.strip()
            self.run_tool("edit", project, asset, "Edited sprite", "true", "1")
            self.run_tool("new-texture", project, "New color", 16, 8, 20, 40, 80)
            self.run_tool("build", project, root / "first")
            self.run_tool("build", project, root / "second")
            self.run_tool("verify", root / "first")
            first = {p.name: p.read_bytes() for p in (root / "first").iterdir()}
            second = {p.name: p.read_bytes() for p in (root / "second").iterdir()}
            self.assertEqual(first, second)
            manifest = json.loads(first["content.json"])
            entry = next(t for t in manifest["textures"] if t["name"] == "Edited sprite")
            self.assertEqual((1, 2), (entry["width"], entry["height"]))
            texture = root / "first" / (asset.replace("-", "") + ".octex")
            self.assertEqual(bytes([0, 255, 0, 255, 255, 0, 0, 255]), texture.read_bytes()[68:])
            self.assertEqual(payload, source.read_bytes())
            self.run_tool("build", project, root / "first", expected=1)
            texture.write_bytes(texture.read_bytes()[:-1])
            self.run_tool("verify", root / "first", expected=1)

    def test_existing_client_artwork_can_be_authored_and_compiled(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Game artwork")
            catalog = self.run_tool("list-artwork").stdout
            key = "conquer/login/account/row"
            self.assertIn(key, catalog)
            asset = self.run_tool("import-artwork", project, key).stdout.strip()
            self.run_tool("edit", project, asset, "New row", "true", "1")
            self.run_tool("build", project, root / "build")
            self.run_tool("verify", root / "build")
            header = bytearray(18)
            header[2], header[12], header[14], header[16], header[17] = 10, 2, 1, 32, 8
            replacement = root / "replacement.tga"
            replacement.write_bytes(header + bytes([1, 0, 0, 255, 255, 0, 255, 0, 255]))
            self.run_tool("replace", project, asset, replacement)
            self.run_tool("build", project, root / "replacement-build")
            replaced = json.loads((root / "replacement-build/content.json").read_text())
            self.assertEqual(asset, replaced["textures"][0]["id"])
            self.assertEqual((1, 2), (replaced["textures"][0]["width"], replaced["textures"][0]["height"]))
            authored = json.loads((project / "project.ocstudio").read_text())
            revision = authored["resources"][0]
            document = json.loads((project / "Assets" / asset.replace("-", "") / (revision["sha256"] + ".ocasset.json")).read_text())
            self.assertEqual(key, document["asset"]["sourceKey"])
            manifest = json.loads((root / "build/content.json").read_text())
            self.assertEqual((25, 166), (manifest["textures"][0]["width"], manifest["textures"][0]["height"]))

    def test_bad_import_leaves_saved_project_unchanged(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Game")
            before = (project / "project.ocstudio").read_bytes()
            source = root / "broken.tga"
            source.write_bytes(b"broken")
            self.run_tool("import", project, source, "Bad", expected=1)
            self.assertEqual(before, (project / "project.ocstudio").read_bytes())
            self.run_tool("edit", project, "invalid-id", "Bad", "true", "0", expected=1)
            self.run_tool("unknown", expected=2)

    def test_sprite_authoring_references_and_compilation_across_processes(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Animated game")
            texture = self.run_tool("new-texture", project, "Sheet", 2, 1, 40, 80, 120).stdout.strip()
            atlas = self.run_tool("new-atlas", project, texture, "Sprites").stdout.strip()
            listing = self.run_tool("list", project).stdout.splitlines()
            first_sprite = next(line.split()[0] for line in listing if line.split()[1] == "sprite")
            self.run_tool("sprite", project, atlas, first_sprite, "Left", 0, 0, 1, 1, 0, 0)
            second_sprite = self.run_tool("sprite", project, atlas, "new", "Right", 1, 0, 1, 1, 0, 0).stdout.strip()
            animation = self.run_tool("new-animation", project, atlas, "Clip", 1000, 100).stdout.strip()
            self.run_tool("clip", project, animation, "Once", 1000, "false")
            frames = [line.split()[0] for line in self.run_tool("list", project).stdout.splitlines() if line.split()[1] == "frame"]
            self.run_tool("frame", project, animation, frames[1], atlas, second_sprite, 250, 0)
            self.run_tool("delete", project, texture, expected=1)
            self.run_tool("build", project, root / "first")
            self.run_tool("build", project, root / "second")
            self.assertEqual(
                {p.name: p.read_bytes() for p in (root / "first").iterdir()},
                {p.name: p.read_bytes() for p in (root / "second").iterdir()},
            )
            self.run_tool("verify", root / "first")
            manifest = json.loads((root / "first/content.json").read_text())
            self.assertEqual(2, manifest["schemaVersion"])
            self.assertEqual({"atlas", "animation"}, {r["kind"] for r in manifest["resources"]})
            clip = root / "first" / (animation.replace("-", "") + ".ocanim")
            clip.write_bytes(clip.read_bytes()[:-1])
            self.run_tool("verify", root / "first", expected=1)

    def test_terrain_export_matches_server_source_contract_across_processes(self):
        with tempfile.TemporaryDirectory(prefix="studio-terrain-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "World")
            terrain = self.run_tool("new-terrain", project, "Field", 9100, 3, 2, 12, 0, -30).stdout.strip()
            self.run_tool("terrain-cell", project, terrain, 2, 1, 65535, 5, -32768)
            first = root / "first.json"
            second = root / "second.json"
            self.run_tool("export-base-terrain", project, terrain, first)
            self.run_tool("export-base-terrain", project, terrain, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())

            payload = json.loads(first.read_bytes())
            self.assertEqual(
                ["formatVersion", "mapDataId", "width", "height", "cells", "exits"],
                list(payload),
            )
            self.assertEqual((1, 9100, 3, 2),
                             (payload["formatVersion"], payload["mapDataId"], payload["width"], payload["height"]))
            self.assertEqual(6, len(payload["cells"]))
            self.assertEqual(
                {"surfaceId": 12, "passabilityFlag": 0, "elevation": -30},
                payload["cells"][0],
            )
            self.assertEqual(
                {"surfaceId": 65535, "passabilityFlag": 5, "elevation": -32768},
                payload["cells"][-1],
            )
            self.assertEqual([], payload["exits"])
            self.run_tool("export-base-terrain", project, terrain, first, expected=1)
            self.run_tool("build", project, root / "incomplete", expected=1)
            self.assertFalse((root / "incomplete").exists())

    def test_ordered_terrain_exit_authoring_persists_across_independent_processes(self):
        with tempfile.TemporaryDirectory(prefix="studio-exits-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "World")
            terrain = self.run_tool("new-terrain", project, "Ground", 9700, 2, 1, 0, 0, 0).stdout.strip()
            first = self.run_tool("terrain-exit", project, terrain, "new", -50, 1000, 4294967295, 0).stdout.strip()
            second = self.run_tool("terrain-exit", project, terrain, "new", 6, -9, 3, 1).stdout.strip()
            self.run_tool("terrain-exit", project, terrain, first, -40, 1200, 4294967295, 1)

            output = root / "source.json"
            self.run_tool("export-base-terrain", project, terrain, output)
            authored = json.loads(output.read_bytes())
            self.assertEqual(
                [{"x": 6, "y": -9, "passwayIndex": 3},
                 {"x": -40, "y": 1200, "passwayIndex": 4294967295}],
                authored["exits"],
            )
            self.run_tool("terrain-exit", project, terrain, "missing", 1, 2, 3, 0, expected=1)
            self.run_tool("remove-terrain-exit", project, terrain, second)
            after = root / "after.json"
            self.run_tool("export-base-terrain", project, terrain, after)
            self.assertEqual(
                [{"x": -40, "y": 1200, "passwayIndex": 4294967295}],
                json.loads(after.read_bytes())["exits"],
            )
            self.run_tool("remove-terrain-exit", project, terrain, second, expected=1)

    def test_world_definitions_preserve_shared_terrain_bindings_and_full_flags(self):
        with tempfile.TemporaryDirectory(prefix="studio-worlds-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Worlds")
            terrain = self.run_tool("new-terrain", project, "Ground", 1010, 2, 1, 1, 0, 0).stdout.strip()
            later = self.run_tool("new-world", project, "Later", 2003, terrain, "0xFFFFFFFFFFFFFFFF").stdout.strip()
            earlier = self.run_tool("new-world", project, "Earlier", 2002, terrain, 0).stdout.strip()
            self.run_tool("edit-world", project, later, "Later renamed", 2003, terrain, "18446744073709551615")
            first = root / "first.json"
            second = root / "second.json"
            self.run_tool("export-map-definitions", project, first)
            self.run_tool("export-map-definitions", project, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())

            payload = json.loads(first.read_bytes())
            self.assertEqual(["formatVersion", "maps"], list(payload))
            self.assertEqual(1, payload["formatVersion"])
            self.assertEqual([
                {"mapId": 2002, "mapDataId": 1010, "flags": 0},
                {"mapId": 2003, "mapDataId": 1010, "flags": 18446744073709551615},
            ], payload["maps"])
            self.assertNotIn(earlier, first.read_text())
            self.assertNotIn(later, first.read_text())
            self.run_tool("delete", project, terrain, expected=1)
            self.run_tool("new-world", project, "Duplicate", 2002, terrain, 1, expected=1)
            self.run_tool("edit-world", project, earlier, "Earlier", 2002, terrain, "-1", expected=1)
            self.run_tool("export-map-definitions", project, first, expected=1)
            self.assertEqual(payload, json.loads(first.read_bytes()))

    def test_process_termination_during_save_preserves_a_complete_committed_project(self):
        with tempfile.TemporaryDirectory(prefix="studio-crash-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Recovery")
            self.run_tool("new-texture", project, "Committed", 1, 1, 1, 2, 3)
            previous = (project / "project.ocstudio").read_bytes()
            before = set((project / "Inputs").iterdir())
            process = subprocess.Popen(
                ["dotnet", str(TOOL), "new-texture", str(project), "Interrupted", "4096", "1024", "40", "80", "120"],
                stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            )
            terminated = False
            try:
                deadline = time.monotonic() + 15
                while process.poll() is None and time.monotonic() < deadline:
                    if set((project / "Inputs").iterdir()) - before:
                        terminated = True
                        process.kill()
                        break
                    time.sleep(0.001)
                stdout, stderr = process.communicate(timeout=15)
                if not terminated:
                    self.assertEqual(0, process.returncode,
                                     stdout.decode(errors="replace") + stderr.decode(errors="replace"))
            finally:
                if process.poll() is None:
                    process.kill()
                    process.communicate()
            # Scheduling can place termination on either side of the single index commit.
            # Both outcomes must load, compile, and validate without repair or missing dependencies.
            index = json.loads((project / "project.ocstudio").read_bytes())
            self.assertIn(len(index["resources"]), (1, 2))
            if len(index["resources"]) == 1:
                self.assertEqual(previous, (project / "project.ocstudio").read_bytes())
            self.run_tool("build", project, root / "after-crash")
            self.run_tool("verify", root / "after-crash")


if __name__ == "__main__":
    unittest.main()
