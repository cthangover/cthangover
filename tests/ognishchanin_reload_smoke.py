"""Verify compatible 3D asset reload and repeat a recorded shore operation in an isolated game profile."""

import argparse
import json
import math
import os
import subprocess
import sys
import tempfile
import time
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
FIXTURE = "test_ognishchanin_reload"
ACTOR = "proto_land_actor"
sys.path.insert(0, str(PROJECT / "DebugClient"))
from dev_control import PipeUnavailable, request


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def patch(fixture, kind, entity, values):
    write_json(fixture / "patches" / kind / "probe.json", {"Items": [{"Id": entity, **values}]})


def send(game, command, **arguments):
    if game.poll() is not None:
        raise RuntimeError(f"Godot exited early with code {game.returncode}")
    reply = request(game.pid, {"command": command, **arguments}, discovery=game.profile / "unused-discovery.json")
    if not reply.get("ok"):
        raise RuntimeError(f"{command}: {reply}")
    return reply["result"]


def probe(game, command, **arguments):
    return send(game, command, provider=FIXTURE, **arguments)


def playable(game, *, patched=False, previous=None):
    deadline = time.monotonic() + 145
    while time.monotonic() < deadline:
        try:
            result = probe(game, "inspect", patched=patched)
            if (result.get("ready") and result["host"] != previous
                    and result["nav_status"] == "ready" and result["nav_points"] > 0):
                return result
        except PipeUnavailable:
            pass
        time.sleep(.5)
    raise TimeoutError(f"Location and navigation did not become playable: {locals().get('result')}")


def same_placement(before, after):
    assert math.dist(before["position"], after["position"]) < .04, (before, after)
    assert before["dock_position"] == after["dock_position"], (before, after)


def verify_shore(result):
    assert "Terrain" in result["ground"], result
    assert result["floor"] - result["before"] > .3, result
    assert abs(result["after"] - result["floor"]) < .08, result
    assert result["on_floor"] and result["standing"] and result["released"], result
    assert result["mode"] == "Ground", result


def exercise(game, fixture, source, started, record):
    initial = playable(game)
    record.update(initial=initial, cold_ms=(time.monotonic() - started) * 1000, pid=game.pid)
    assert initial["dock_collision"] is not None, initial
    probe(game, "checkpoint")
    manual = next(game.profile.rglob("p0_manual.json"))
    session = next(game.profile.rglob("last_session.json"))
    saved_bytes = manual.read_bytes()
    session_bytes = session.read_bytes()

    patch(fixture, "character3d_presentation", "ognishchanin/hero", {
        "Sockets$add": [{"Id": "p0_probe", "Bone": "RightHand", "Transform": {"Position": [.07, 0, 0]}}],
        "Lods": [{"Id": "p0_near", "Distance": 0, "LodBias": .78}],
    })
    send(game, "assets.refresh", mod=FIXTURE, path="patches/character3d_presentation/probe.json")
    started = time.monotonic()
    replacement = send(game, "refresh-visual", provider="actor3d_base", actor=ACTOR)
    record["visual_ms"] = (time.monotonic() - started) * 1000
    record["replacement"] = replacement
    refreshed = playable(game, patched=True)
    record["visual"] = refreshed
    same_placement(initial, refreshed)
    for key in ("host", "body", "collider", "dock_collision", "animation", "action"):
        assert initial[key] == refreshed[key], (key, initial, refreshed)
    assert initial["model"] == replacement["previous_model"] != refreshed["model"] == replacement["model"]
    assert refreshed["lod"] == "p0_near" and abs(refreshed["socket_x"] - .07) < .001, refreshed

    previous = refreshed
    record["reloads"] = []
    for revision, radius in ((2, .31), (3, .32)):
        speed = 1 + revision * .1
        patch(fixture, "actor3d", ACTOR, {"Radius": radius})
        patch(fixture, "model3d_asset", "ognishchanin/start/dock", {"Options": {"MaxTextureSize": 1024 // (revision - 1)}})
        patch(fixture, "anim3d_controller", "ognishchanin/start_combat", {"Roles$add": [{"Id": "idle", "Speed": speed}]})
        (fixture / "src/ReloadProbeCommand.cs").write_text(
            source.replace("Revision = 1;", f"Revision = {revision};"), encoding="utf-8")
        started = time.monotonic()
        reloaded = send(game, "mod.reload", mod=FIXTURE, checkpoint=True)
        assert reloaded["applied"] and not reloaded["leaked"] and FIXTURE in reloaded["loaded"], reloaded
        after = playable(game, patched=True, previous=previous["host"])
        elapsed = (time.monotonic() - started) * 1000
        same_placement(previous, after)
        assert after["revision"] == revision and abs(after["radius"] - radius) < .001, after
        assert after["dock_collision"] and after["dock_collision"] != previous["dock_collision"], after
        assert after["collider"] != previous["collider"], after
        assert after["lod"] == "p0_near" and abs(after["socket_x"] - .07) < .001, after
        after["idle_speed"] = probe(game, "measure-idle")["speed"]
        assert abs(after["idle_speed"] - speed) < .001, after
        assert send(game, "status")["pid"] == game.pid and manual.read_bytes() == saved_bytes
        assert session.read_bytes() == session_bytes
        record["reloads"].append({"ready_ms": elapsed, "reload_ms": reloaded["elapsed_ms"], "snapshot": after,
                                  "loaded": reloaded["loaded"], "leaked": reloaded["leaked"]})
        previous = after

    record["replays"] = []
    for _ in range(2):
        probe(game, "restore")
        restored = playable(game, patched=True, previous=previous["host"])
        same_placement(initial, restored)
        result = probe(game, "shore-cancel")
        record["replays"].append(result)
        verify_shore(result)
        previous = restored
    assert abs(record["replays"][0]["after"] - record["replays"][1]["after"]) < .01, record["replays"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="cthangover-p0-reload-") as temporary:
        profile = Path(temporary)
        fixture = profile / "cthangover/mods" / FIXTURE
        source = (PROJECT / "tests/fixtures/ognishchanin_reload/ReloadProbeCommand.cs.txt").read_text(encoding="utf-8")
        write_json(fixture / "manifest.json", {"id": FIXTURE, "name": "Ognishchanin reload probe", "version": "0.1.0",
                   "depends": ["ognishchanin_game", "actor3d_base", "navigation3d_base"], "sources": ["src/*.cs"]})
        write_json(fixture / "definitions/shore.json", {"Kind": "traversal3d", "Items": [{"Id": "p0/shore_grab",
                   "Location": "proto_land/yard", "Capability": 4, "Start": {"Position": [-4.57659, -.67, -30.648985]},
                   "End": {"Position": [-4.57659, .16, -30.648985]}}]})
        (fixture / "src").mkdir()
        (fixture / "src/ReloadProbeCommand.cs").write_text(source, encoding="utf-8")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        command = [args.godot, "--path", str(PROJECT), "--resolution", "960x540", "--", "--game=ognishchanin", "--new",
                   f"--with-mods={FIXTURE}", "--development-control", "--no-hot-reload"]
        environment = {**os.environ, "APPDATA": temporary, "XDG_DATA_HOME": temporary}
        record = {}
        started = time.monotonic()
        log = args.output.with_suffix(".log")
        with log.open("wb") as output, subprocess.Popen(command, env=environment, stdout=output,
                                                        stderr=subprocess.STDOUT) as game:
            game.profile = profile
            try:
                exercise(game, fixture, source, started, record)
                try:
                    probe(game, "finish")
                except (BrokenPipeError, ValueError):
                    pass
                assert game.wait(timeout=20) == 0
                record["passed"] = True
            finally:
                if game.poll() is None:
                    game.terminate()
                    game.wait(timeout=10)
                write_json(args.output, record)
        print(json.dumps(record, ensure_ascii=False))


if __name__ == "__main__":
    main()
