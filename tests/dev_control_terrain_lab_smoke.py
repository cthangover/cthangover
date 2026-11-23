"""Verify the study plot in a single live Ogneshchanin process on Windows."""

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

from dev_control_smoke import MOD, cli, command, write_probe

LAB = "ognishchanin_terrain_lab"


def verify(process, project, output):
    before = command(process, "status")
    deadline = time.monotonic() + 120
    while before["scene"] != "proto_land_yard" and time.monotonic() < deadline:
        time.sleep(.25)
        before = command(process, "status")
    assert before["scene"] == "proto_land_yard" and LAB not in before["mods"], before

    attached = command(process, "mod.attach", mod=LAB, checkpoint=False)
    assert LAB in attached["loaded"] and attached["kept"], attached
    baseline = command(process, "open", provider=LAB)
    assert baseline["plants"] > 0 and baseline["terrain"] == baseline["shown"], baseline
    capture(process, output, "baseline")

    preview = command(process, "preview-stamp", provider=LAB, angle=-35, cut=.48)
    assert preview["preview"] and preview["terrain"] == baseline["terrain"] and preview["shown"] != preview["terrain"], preview
    assert preview["undo"] == 0 and preview["lab_id"] == baseline["lab_id"], preview
    canceled = command(process, "cancel-preview", provider=LAB)
    assert not canceled["preview"] and canceled["shown"] == baseline["shown"], canceled
    preview = command(process, "preview-stamp", provider=LAB, angle=25, cut=.48)
    capture(process, output, "preview")

    applied = command(process, "apply-stamp", provider=LAB)
    assert not applied["preview"] and applied["undo"] == 1 and applied["shown"] == preview["shown"] == applied["terrain"], applied
    assert applied["center_density"] == applied["center_grass_weight"] == 0, applied
    assert abs(applied["center_height"] - baseline["center_height"] + .48) < .0001, applied
    assert applied["plants"] < baseline["plants"], (applied, baseline)

    capture(process, output, "pigment-off")
    tinted = command(process, "pigment", provider=LAB, strength=.85)
    assert abs(tinted["pigment"] - .85) < .0001 and tinted["terrain_id"] == applied["terrain_id"], tinted
    assert tinted["plants"] == applied["plants"] and tinted["terrain"] == applied["terrain"], tinted
    capture(process, output, "pigment-on")
    command(process, "pigment", provider=LAB, strength=0)
    capture(process, output, "pigment-off-repeat")
    command(process, "view", provider=LAB, mode="overview")
    capture(process, output, "overview")

    undone = command(process, "undo", provider=LAB)
    assert undone["terrain"] == undone["shown"] == baseline["terrain"] and undone["redo"] == 1, undone
    redone = command(process, "redo", provider=LAB)
    assert redone["terrain"] == redone["shown"] == applied["terrain"] and redone["undo"] == 1, redone
    command(process, "close", provider=LAB)
    shore = verify_shore(process, output)
    source = project / "mods_private" / LAB / "src" / "TerrainLabCommand.cs"
    text = source.read_text(encoding="utf-8")
    assert text.count('"strength", .65f') == 1
    source.write_text(text.replace('"strength", .65f', '"strength", .55f'), encoding="utf-8")
    reloaded = command(process, "mod.reload", mod=LAB, checkpoint=False)
    assert reloaded["loaded"] == [LAB] and reloaded["unloaded"] == [LAB] and not reloaded["leaked"], reloaded
    reopened = command(process, "open", provider=LAB)
    default_strength = command(process, "pigment", provider=LAB)
    assert abs(default_strength["pigment"] - .55) < .0001, default_strength
    command(process, "close", provider=LAB)
    after = command(process, "status")
    assert after["pid"] == before["pid"] == process.pid and after["scene_instance"] == before["scene_instance"], (before, after)

    evidence = dict(before=before, after=after, attached=attached, baseline=baseline, preview=preview, applied=applied,
                    tinted=tinted, undone=undone, redone=redone, shore=shore, reloaded=reloaded, reopened=reopened)
    (output / "result.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")


def capture(process, output, name):
    location = cli(process, "invoke", f"--provider={LAB}", "--command=capture", f"--arg=name={name}")["path"]
    target = output / (name + ".png")
    shutil.copyfile(location, target)
    return target


def verify_shore(process, output):
    initial = command(process, "open", provider=LAB, plot="shore")
    assert initial["plot"] == "shore" and initial["blend_strength"] == 0, initial
    off = capture(process, output, "shore-linear")
    on = command(process, "blend", provider=LAB, strength=1, width=.32, slope=.4)
    relief = capture(process, output, "shore-relief")
    command(process, "blend", accepted=False, provider=LAB, width=0)
    assert command(process, "status", provider=LAB) == on, on
    wide = command(process, "blend", provider=LAB, width=.8, slope=0)
    wider = capture(process, output, "shore-relief-wide")
    assert relief.read_bytes() != off.read_bytes() and wider.read_bytes() != relief.read_bytes()
    restored = command(process, "blend", provider=LAB, strength=0)
    again = capture(process, output, "shore-linear-repeat")
    assert again.read_bytes() == off.read_bytes()
    identities = ("lab_id", "terrain_id", "terrain", "shown", "view", "undo", "redo", "plants")
    for state in (on, wide, restored):
        assert all(state[key] == initial[key] for key in identities), (initial, state)
    command(process, "view", provider=LAB, mode="overview")
    capture(process, output, "shore-overview-linear")
    command(process, "blend", provider=LAB, strength=1, width=.32, slope=.4)
    capture(process, output, "shore-overview-relief")
    command(process, "close", provider=LAB)
    return dict(initial=initial, on=on, wide=wide, restored=restored)


def run(project, output, godot, profile):
    fixture = project / "mods" / MOD
    (fixture / "src").mkdir(parents=True)
    (fixture / "manifest.json").write_text(json.dumps({"id": MOD, "version": "1.0.0", "sources": ["src/*.cs"]}), encoding="utf-8")
    write_probe(fixture / "src" / "Probe.cs", "terrain lab", "terrain-test")
    env = dict(os.environ, APPDATA=str(profile), LOCALAPPDATA=str(profile), XDG_DATA_HOME=str(profile))
    with (output / "process.log").open("wb") as log:
        process = subprocess.Popen(
            [godot, "--path", str(project), "--resolution", "1280x768", "--log-file", str(output / "godot.log"),
             "--", "--game=ognishchanin", "--new", "--no-hot-reload"],
            cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT,
        )
        try:
            verify(process, project, output)
            command(process, "mod.attach", mod=MOD)
            command(process, "finish", provider=MOD)
            assert process.wait(timeout=25) == 0
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=10)
            logs = profile / "cthangover" / "logs"
            if logs.is_dir():
                shutil.copytree(logs, output / "runtime-logs")
    print("PASS live terrain lab", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--godot", required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    args.profile.mkdir(parents=True, exist_ok=True)
    run(args.project.resolve(), args.output.resolve(), args.godot, args.profile.resolve())
