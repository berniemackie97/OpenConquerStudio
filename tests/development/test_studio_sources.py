"""Keep the included Content/Rendering sources tied to their recorded ownership and hashes."""

import hashlib
import json
from pathlib import Path
import unittest


class StudioSourceTests(unittest.TestCase):
    def test_source_snapshot_matches_recorded_files(self):
        root = Path(__file__).resolve().parents[2] / "dependencies/OpenConquerClient"
        manifest = json.loads((root / "source.json").read_text())
        self.assertEqual(2, manifest["schemaVersion"])
        self.assertEqual(40, len(manifest["sourceCommit"]))
        recorded = {}
        for entry in manifest["files"]:
            path = Path(entry["path"])
            self.assertFalse(path.is_absolute())
            self.assertNotIn("..", path.parts)
            self.assertNotIn(entry["path"], recorded)
            data = (root / path).read_bytes()
            self.assertEqual(entry["sha256"], hashlib.sha256(data).hexdigest(), entry["path"])
            recorded[entry["path"]] = entry
        actual = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()
                  and not {"bin", "obj"}.intersection(p.relative_to(root).parts)
                  and p.name != "source.json"}
        self.assertEqual(set(recorded), actual)
        self.assertTrue(manifest["studioOwnedChanges"])
