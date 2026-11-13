"""Contract tests for atomic publication, failure retention and provenance without launching Blender."""

import json
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from authoring3d.export_blend import export_blend, validate_glb


class ExportBlendTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.directory = Path(self.sandbox.name)
        self.source = self.directory / "editable scene.blend"
        self.source.write_bytes(b"BLENDER fixture")
        self.output = self.directory / "runtime model.glb"
        self.provenance = self.directory / "provenance.json"

    @staticmethod
    def glb(document):
        data = json.dumps(document).encode()
        data += b" " * (-len(data) % 4)
        return struct.pack("<5I", 0x46546C67, 2, len(data) + 20, len(data), 0x4E4F534A) + data

    def successful_blender(self, command, **kwargs):
        self.assertIn("--disable-autoexec", command)
        self.assertEqual(300, kwargs["timeout"])
        args = command[command.index("--") + 1:]
        Path(args[args.index("--output") + 1]).write_bytes(self.glb({"asset": {"version": "2.0"}}))
        Path(args[args.index("--metadata") + 1]).write_text(json.dumps({"blender_version": "test", "objects": 1}))
        return subprocess.CompletedProcess(command, 0, "", "")

    def failing_blender(self, command, **kwargs):
        return subprocess.CompletedProcess(command, 7, "", "fixture failure")

    def invalid_blender(self, command, **kwargs):
        result = self.successful_blender(command, **kwargs)
        args = command[command.index("--") + 1:]
        Path(args[args.index("--output") + 1]).write_bytes(b"truncated")
        return result

    def test_success_publishes_glb_and_source_hash(self):
        with patch("authoring3d.export_blend.subprocess.run", side_effect=self.successful_blender):
            record = export_blend(self.source, self.output, "Blender path/blender.exe", self.provenance)
        validate_glb(self.output)
        self.assertEqual(64, len(record["source_sha256"]))
        self.assertEqual(64, len(record["output_sha256"]))
        self.assertEqual("test", json.loads(self.provenance.read_text())["blender_version"])
        self.assertFalse(record["embedded_scripts"])
        self.assertFalse(list(self.directory.glob(".blend-export-*")))

    def test_nonzero_exit_preserves_previous_model(self):
        self.output.write_bytes(b"previous")
        with patch("authoring3d.export_blend.subprocess.run", side_effect=self.failing_blender):
            with self.assertRaisesRegex(RuntimeError, "exit code 7"):
                export_blend(self.source, self.output, "blender")
        self.assertEqual(b"previous", self.output.read_bytes())
        self.assertFalse(list(self.directory.glob(".blend-export-*")))

    def test_invalid_export_preserves_previous_model(self):
        self.output.write_bytes(b"previous")
        with patch("authoring3d.export_blend.subprocess.run", side_effect=self.invalid_blender):
            with self.assertRaises(ValueError):
                export_blend(self.source, self.output, "blender")
        self.assertEqual(b"previous", self.output.read_bytes())

    def test_external_resources_rejected(self):
        self.output.write_bytes(self.glb({"images": [{"uri": "texture.png"}]}))
        with self.assertRaisesRegex(ValueError, "external images"):
            validate_glb(self.output)

    def test_source_and_provenance_cannot_be_overwritten(self):
        with self.assertRaises(ValueError):
            export_blend(self.source, self.output, "blender", self.source)
        with self.assertRaises(ValueError):
            export_blend(self.source, self.output.with_suffix(".blend"), "blender")
        with self.assertRaises(ValueError):
            export_blend(self.source, self.output, "blender", timeout=0)


if __name__ == "__main__":
    unittest.main()
