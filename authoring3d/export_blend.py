"""Export an editable Blender source to a self-contained runtime GLB atomically.

Run on the Windows host through the workspace watcher. Sources and provenance
belong outside runtime mod repositories; the output belongs in models3d/.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile


def sha256(path: Path) -> str:
    """Stream a source/output fingerprint without loading an entire model into memory."""
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def validate_glb(path: Path) -> None:
    """Require GLB 2.0 and embedded resources before replacing a working model."""
    with path.open("rb") as stream:
        header = stream.read(20)
        if len(header) != 20:
            raise ValueError("Blender output is truncated")
        magic, version, size, json_size, chunk_type = struct.unpack("<5I", header)
        if (magic, version, chunk_type) != (0x46546C67, 2, 0x4E4F534A) or size != path.stat().st_size:
            raise ValueError("Blender output is not a complete GLB 2.0 file")
        if json_size > size - 20 or json_size % 4:
            raise ValueError("Invalid GLB JSON chunk")
        document = json.loads(stream.read(json_size))
    for group in ("buffers", "images"):
        for item in document.get(group, []):
            uri = item.get("uri")
            if uri is not None and (not isinstance(uri, str) or not uri.startswith("data:")):
                raise ValueError(f"GLB has an external {group} dependency: {uri}")


def atomic_json(path: Path, value: dict) -> None:
    """Publish one complete provenance file beside the external editable source."""
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=".blend-export-", suffix=".json", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def export_blend(source: Path, output: Path, blender: str, provenance: Path | None = None,
                 timeout: float = 300) -> dict:
    """Run Blender without embedded scripts; keep the previous GLB on any export/validation failure."""
    source = source.resolve(strict=True)
    output = output.resolve()
    if source.suffix.lower() != ".blend" or not source.is_file():
        raise ValueError("Source must be an existing .blend file")
    if output.suffix.lower() != ".glb":
        raise ValueError("Runtime output must have the .glb extension")
    if timeout <= 0:
        raise ValueError("Timeout must be positive")
    if provenance is not None and provenance.resolve() in (source, output):
        raise ValueError("Provenance must not overwrite the source or runtime model")
    output.parent.mkdir(parents=True, exist_ok=True)
    source_hash = sha256(source)
    worker = Path(__file__).with_name("blender_export_worker.py")
    with tempfile.TemporaryDirectory(prefix=".blend-export-", dir=output.parent) as temporary:
        stage = Path(temporary)
        generated = stage / "model.glb"
        metadata = stage / "export.json"
        command = [blender, "--background", "--factory-startup", "--disable-autoexec",
                   "--python-exit-code", "1", "--python", str(worker), "--",
                   "--source", str(source), "--output", str(generated), "--metadata", str(metadata)]
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
        if result.stdout:
            print(result.stdout, end="")
        if result.stderr:
            print(result.stderr, end="")
        if result.returncode:
            raise RuntimeError(f"Blender export failed with exit code {result.returncode}")
        validate_glb(generated)
        if sha256(source) != source_hash:
            raise RuntimeError("Blender source changed during export; previous model retained")
        record = json.loads(metadata.read_text(encoding="utf-8"))
        record.update({"source": str(source), "output": str(output), "source_sha256": source_hash,
                       "output_sha256": sha256(generated), "format": "glb", "embedded_scripts": False})
        os.replace(generated, output)
    if provenance is not None:
        atomic_json(provenance.resolve(), record)
    return record


def main() -> None:
    """CLI entry point with explicit paths and a bounded Blender process."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--blender", default="blender.exe")
    parser.add_argument("--provenance", type=Path)
    parser.add_argument("--timeout", type=float, default=300)
    args = parser.parse_args()
    result = export_blend(args.source, args.output, args.blender, args.provenance, args.timeout)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
