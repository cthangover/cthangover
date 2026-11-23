"""Check model reimport in the live Ogneshchanin location on a disposable project copy."""

import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

from dev_control_smoke import MOD, command, request, write_probe


def run(project, output, godot):
    fixture = project / "mods" / MOD
    (fixture / "src").mkdir(parents=True, exist_ok=True)
    (fixture / "manifest.json").write_text(json.dumps({"id": MOD, "version": "1.0.0", "sources": ["src/*.cs"]}), encoding="utf-8")
    write_probe(fixture / "src" / "Probe.cs", "model", "model-test")
    recipe = project / "mods_private/temperate_plants_pack/models3d/temperate_plants/fern.plant"
    original = recipe.read_bytes()
    with tempfile.TemporaryDirectory(prefix="cthangover-dev-profile-") as home, (output / "process.log").open("wb") as log:
        env = dict(os.environ, APPDATA=home, LOCALAPPDATA=home, XDG_DATA_HOME=home)
        process = subprocess.Popen(
            [godot, "--path", str(project), "--log-file", str(output / "godot.log"),
             "--", "--game=ognishchanin", "--new", "--no-hot-reload"],
            cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT,
        )
        try:
            status = command(process, "status")
            deadline = time.monotonic() + 150
            replace = {"command": "replace-model", "provider": "scene3d_base", "object": "ogn_forage_000"}
            while True:
                reply = request(process.pid, replace)
                if reply["ok"]:
                    break
                if reply.get("error") != "A ready 3D location is required." or time.monotonic() > deadline:
                    raise RuntimeError(reply)
                time.sleep(0.5)
            old_model = reply["result"]
            before = command(process, "status")
            assert before["scene"] == "proto_land_yard" and before["pid"] == status["pid"], before

            plant = json.loads(original)
            plant["Height"] = 0.35
            recipe.write_text(json.dumps(plant), encoding="utf-8")
            command(process, "assets.refresh", mod="temperate_plants_pack", path="models3d/temperate_plants/fern.plant")
            new_model = command(process, "replace-model", provider="scene3d_base", object="ogn_forage_000")
            after = command(process, "status")
            assert old_model["placement"] == new_model["placement"] and old_model["model"] != new_model["model"]
            assert old_model["import_key"] and new_model["import_key"] and old_model["import_key"] != new_model["import_key"]
            assert before["scene_instance"] == after["scene_instance"] and before["pid"] == after["pid"]

            command(process, "mod.attach", mod=MOD)
            command(process, "finish", provider=MOD)
            assert process.wait(timeout=20) == 0
            evidence = dict(before=before, after=after, old_model=old_model, new_model=new_model)
            (output / "result.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
            print("PASS live Ogneshchanin model replacement", flush=True)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=10)
            recipe.write_bytes(original)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--godot", default=os.environ.get("GODOT_BIN", "godot"))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    run(args.project.resolve(), args.output.resolve(), args.godot)
