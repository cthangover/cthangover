"""Exercise live development commands against one disposable copy of the game."""

import argparse
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import time
import zlib

CLIENT = Path(__file__).resolve().parents[1] / "DebugClient" / "dev_control.py"
sys.path.insert(0, str(CLIENT.parent))
from dev_control import PipeUnavailable, request

MOD = "test_development_control"
SOURCE = """using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.HotReload;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Godot;

public sealed class ProbeCommand : IModDevelopmentCommand, IModSaveable
{
    public IReadOnlyCollection<string> Commands { get; } =
        new[] { "probe", "texture", "finish", "save-player-slot", "fail-save", "read-state", "build-scene", EXTRA };
    private static int state;
    private static bool failSave;
    private Texture2D texture;
    public void OnSave(ModSlot slot)
    {
        if (failSave)
            throw new InvalidOperationException("checkpoint probe save failure");
        var data = new DataBlob();
        data.WriteJson(state);
        slot.Data["state"] = data;
    }

    public void OnLoadVoid(ModSlot slot) => state = slot.Data["state"].ReadJson<int>();

    public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
    {
        if (command == "save-player-slot")
        {
            state = 1;
            SaveService.Save("manual");
            state = 42;
            return Task.FromResult<JsonNode>(null);
        }
        if (command == "fail-save")
        {
            failSave = context.Request["enabled"].GetValue<bool>();
            return Task.FromResult<JsonNode>(null);
        }
        if (command == "read-state")
            return Task.FromResult<JsonNode>(JsonValue.Create(state));
        if (command == "build-scene")
        {
            var node = TscnScenes.LoadAndBuild("scenes/dev_control_checkpoint.tscn");
            try
            {
                return Task.FromResult<JsonNode>(JsonValue.Create(node?.GetType().Assembly == typeof(ProbeCommand).Assembly));
            }
            finally
            {
                node?.Free();
            }
        }
        if (command == "finish")
        {
            context.Tree.CreateTimer(0.5).Timeout += Stop;
            return Task.FromResult<JsonNode>(null);
        }
        if (command == "texture")
        {
            texture ??= Textures.Resolve("cthangover_dev_probe");
            using var image = texture.GetImage();
            return Task.FromResult<JsonNode>(new JsonObject
            {
                ["id"] = texture.GetInstanceId(),
                ["width"] = texture.GetWidth(),
                ["red"] = image.GetPixel(0, 0).R
            });
        }
        return Task.FromResult<JsonNode>(JsonValue.Create(VALUE));
    }

    private void Stop() => ApplicationExit.Quit((SceneTree)Engine.GetMainLoop());
}
"""


def write_probe(path, value, extra):
    path.write_text(SOURCE.replace("VALUE", json.dumps(value)).replace("EXTRA", json.dumps(extra)), encoding="utf-8")


def install_checkpoint_scene(fixture):
    (fixture / "scenes").mkdir()
    (fixture / "src/CheckpointScene.cs").write_text(
        "namespace DevelopmentProbe { public partial class CheckpointScene : Godot.Node { } }\n", encoding="utf-8"
    )
    (fixture / "scenes/dev_control_checkpoint.tscn").write_text(
        f'[gd_scene format=3]\n\n[ext_resource type="Script" path="res://mods/{MOD}/src/CheckpointScene.cs" id="1"]\n'
        '\n[node name="CheckpointScene" type="Node"]\nscript = ExtResource("1")\n', encoding="utf-8"
    )


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def write_texture(path, width, red):
    pixels = bytes([red, 0, 100, 255]) * width
    contents = b"\x89PNG\r\n\x1a\n"
    contents += chunk(b"IHDR", struct.pack(">IIBBBBB", width, 1, 8, 6, 0, 0, 0))
    contents += chunk(b"IDAT", zlib.compress(b"\0" + pixels)) + chunk(b"IEND", b"")
    path.write_bytes(contents)


def command(process, name, accepted=True, **params):
    deadline = time.monotonic() + 120
    while True:
        try:
            answer = request(process.pid, {"command": name, **params})
            break
        except PipeUnavailable as error:
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError(f"Cannot reach game for {name}: {error}") from error
            time.sleep(0.25)
    assert answer["ok"] == accepted, (name, answer)
    return answer.get("result")


def cli(process, *args, stdin=None, legacy=False, exit_code=0):
    target = str(process.pid) if legacy else f"--pid={process.pid}"
    run = subprocess.run(
        [sys.executable, "-X", "utf8", str(CLIENT), target, *args],
        input=stdin, encoding="utf-8", capture_output=True, timeout=30,
    )
    assert run.returncode == exit_code, (args, run.returncode, run.stdout, run.stderr)
    return json.loads(run.stdout).get("result") if run.stdout else None


def run(project, output, godot, checkpoint_only=False):
    manifest = project / "config" / "mod_config.json"
    config = json.loads(manifest.read_text(encoding="utf-8"))
    assert config.get("development_control") is True, "Use an isolated copy with development_control enabled."
    fixture = project / "mods" / MOD
    assert not fixture.exists(), "The fixture needs a fresh project copy."
    with tempfile.TemporaryDirectory(prefix="cthangover-dev-profile-") as home, (output / "process.log").open("wb") as log:
        env = dict(os.environ, APPDATA=home, LOCALAPPDATA=home, XDG_DATA_HOME=home)
        process = subprocess.Popen(
            [godot, "--path", str(project), "--log-file", str(output / "godot.log"),
             "--", "--game=novel_template", "--new", "--no-hot-reload"],
            cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT,
        )
        try:
            before = command(process, "status")
            deadline = time.monotonic() + 30
            while before["scene"] != "my_novel_town" and time.monotonic() < deadline:
                time.sleep(0.25)
                before = command(process, "status")
            assert before["scene"] == "my_novel_town" and MOD not in before["mods"], before
            assert cli(process, "status")["pid"] == process.pid
            assert cli(process, "-", legacy=True, stdin='{"command":"status"}')["pid"] == process.pid
            assert cli(process, "raw", stdin='{"command":"status"}')["pid"] == process.pid

            (fixture / "src").mkdir(parents=True)
            (fixture / "ui").mkdir()
            (fixture / "manifest.json").write_text(json.dumps({"id": MOD, "name": "Development probe", "version": "1.0.0",
                                                             "sources": ["src/*.cs"]}), encoding="utf-8")
            code = fixture / "src" / "Probe.cs"
            texture = fixture / "ui" / "cthangover_dev_probe.png"
            write_probe(code, "first", "old-routine")
            write_texture(texture, 1, 255)
            if checkpoint_only:
                install_checkpoint_scene(fixture)

            attached = cli(process, "mod.attach", f"--mod={MOD}", "--checkpoint=false")
            assert attached["loaded"] == [MOD] and attached["unloaded"] == [] and attached["kept"], attached
            assert command(process, "old-routine", provider=MOD) == "first"

            if checkpoint_only:
                verify_checkpoint(process, before, code, Path(home))
                assert process.wait(timeout=15) == 0
                print("PASS development checkpoint", flush=True)
                return

            assert "old-routine" in cli(process, "commands", f"--mod={MOD}")
            node = cli(process, "node.inspect", "--path=/root/SceneManager", "--field=currentSceneName",
                       "--field=isInitialized", "--property=name")
            assert node["fields"]["currentSceneName"] == "my_novel_town" and node["properties"]["name"] == "SceneManager", node
            assert node["fields"]["isInitialized"] is True, node
            runtime = cli(process, "runtime.inspect", "--mod=__core__")
            assert runtime["state"] and runtime["pid"] == process.pid, runtime

            old_image = command(process, "texture", provider=MOD)
            write_texture(texture, 2, 0)
            asset = cli(process, "assets.refresh", f"--mod={MOD}", "--path=ui/cthangover_dev_probe.png")
            new_image = command(process, "texture", provider=MOD)
            assert asset["live_textures"] == 1 and old_image["id"] == new_image["id"], (asset, old_image, new_image)
            assert old_image["width"] == 1 and new_image["width"] == 2 and old_image["red"] == 1 and new_image["red"] == 0

            write_probe(code, "second", "new-routine")
            cli(process, "mod.reload", f"--mod={MOD}", "--checkpoint=maybe", exit_code=2)
            updated = cli(process, "mod.reload", f"--mod={MOD}", "--checkpoint=false")
            assert updated["loaded"] == [MOD] and updated["unloaded"] == [MOD] and updated["leaked"] == [], updated
            assert cli(process, "invoke", f"--provider={MOD}", "--command=new-routine",
                       "--arg=label=probe", "--json-arg=repeat=2") == "second"
            command(process, "old-routine", accepted=False, provider=MOD)
            code.write_text("this is not a valid program", encoding="utf-8")
            rejected = cli(process, "mod.reload", f"--mod={MOD}", exit_code=1)
            assert not rejected["applied"] and rejected["errors"], rejected
            assert command(process, "new-routine", provider=MOD) == "second"
            after = command(process, "status")
            assert after["scene_instance"] == before["scene_instance"] and before["pid"] == after["pid"]

            cli(process, "scene.switch", "--scene=my_novel_home")
            deadline = time.monotonic() + 15
            while time.monotonic() < deadline:
                switched = command(process, "status")
                if switched["scene"] == "my_novel_home":
                    break
                time.sleep(0.1)
            assert switched["scene"] == "my_novel_home" and before["pid"] == switched["pid"], switched
            command(process, "finish", provider=MOD)
            assert process.wait(timeout=15) == 0
            evidence = dict(before=before, attached=attached, updated=updated, rejected=rejected, inspected=node,
                            before_texture=old_image, after_texture=new_image, after=after, switched=switched)
            (output / "result.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
            print("PASS development control", flush=True)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=10)


def verify_checkpoint(process, before, code, home):
    assert command(process, "build-scene", provider=MOD) is True
    command(process, "save-player-slot", provider=MOD)
    session = next(home.rglob("last_session.json"))
    continue_entry = session.read_bytes()
    saves = next(home.rglob("manual.json")).parent
    assert json.loads(continue_entry)["FileName"] == "manual"

    write_probe(code, "checkpoint", "checkpoint-routine")
    command(process, "fail-save", provider=MOD, enabled=True)
    rejected = request(process.pid, {"command": "mod.reload", "mod": MOD, "checkpoint": True})
    assert not rejected["ok"] and "checkpoint probe save failure" in json.dumps(rejected), rejected
    assert command(process, "old-routine", provider=MOD) == "first"
    assert command(process, "status")["scene_instance"] == before["scene_instance"]
    assert command(process, "read-state", provider=MOD) == 42
    assert not list(saves.glob("__hot_reload*"))

    command(process, "fail-save", provider=MOD, enabled=False)
    parked_saves = saves.with_name("parked-saves")
    saves.rename(parked_saves)
    try:
        saves.touch()
        rejected = request(process.pid, {"command": "mod.reload", "mod": MOD, "checkpoint": True})
        assert not rejected["ok"] and "Could not create hot reload checkpoint" in json.dumps(rejected), rejected
        assert command(process, "status")["scene_instance"] == before["scene_instance"]
    finally:
        saves.unlink(missing_ok=True)
        parked_saves.rename(saves)

    saved = cli(process, "mod.reload", f"--mod={MOD}", "--checkpoint")
    assert saved["applied"] and saved["leaked"] == [] and saved["loaded"] == [MOD], saved
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        status = command(process, "status")
        if status["scene_instance"] != before["scene_instance"] and status["scene"] == before["scene"]:
            break
        time.sleep(0.1)
    else:
        raise RuntimeError("The reloaded gameplay scene did not become active.")
    assert status["pid"] == before["pid"]
    assert command(process, "checkpoint-routine", provider=MOD) == "checkpoint"
    assert command(process, "build-scene", provider=MOD) is True
    assert command(process, "read-state", provider=MOD) == 42
    assert session.read_bytes() == continue_entry
    assert not list(saves.glob("__hot_reload*"))
    command(process, "finish", provider=MOD)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--godot", default=os.environ.get("GODOT_BIN", "godot"))
    parser.add_argument("--checkpoint-only", action="store_true")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    run(args.project.resolve(), args.output.resolve(), args.godot, args.checkpoint_only)
