"""Run selected movement contracts on the host in one disposable 3D project copy."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

from capture_window import save_client


CASES = {
    "editor_character_fields": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_editor",
        "--itest=anim3d_base_contracts", "--itest=characters3d_editor_contracts",
        "--run-tests", "--mod=characters3d_editor__contracts",
        "--test-filter=characters3d_editor/authoring_*",
    ],
    "editor_character_layers": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_editor",
        "--itest=anim3d_base_contracts", "--itest=characters3d_editor_contracts",
        "--run-tests", "--mod=characters3d_editor__contracts", "--test-filter=characters3d_editor/layers_*",
    ],
    "editor_actor_layers": [
        "--with-mods=proto_land_pack,actor3d_base,scene3d_editor,characters3d_editor",
        "--itest=actor3d_movement", "--itest=actor3d_movement_editor", "--run-tests",
        "--mod=actor3d_base__movement_editor", "--test-filter=actor3d/layers_editor_*",
    ],
    "editor_traversal": [
        "--with-mods=proto_land_pack,actor3d_base,scene3d_editor,characters3d_editor",
        "--itest=actor3d_movement", "--itest=actor3d_movement_editor", "--run-tests",
        "--mod=actor3d_base__movement_editor", "--test-filter=actor3d/traversal_editor_*",
    ],
    "editor_dialog_units": [
        "--with-mods=dialog3d_base,proto_land_pack,scene3d_editor,characters3d_editor",
        "--run-tests", "--mod=dialog3d_base", "--test-filter=dialog3d/actor_binding_complete_json",
    ],
    "editor_dialog_actor": [
        "--with-mods=dialog3d_base,proto_land_pack,scene3d_editor,characters3d_editor",
        "--itest=actor3d_movement", "--itest=dialog3d_actor_editor", "--run-tests",
        "--mod=dialog3d_base__actor_editor", "--test-filter=dialog3d/editor_*",
    ],
    "editor_placement": [
        "--with-mods=proto_land_pack,actor3d_base,scene3d_editor,characters3d_editor,combat3d_editor",
        "--itest=actor3d_movement", "--itest=actor3d_placement_editor", "--run-tests",
        "--mod=actor3d_base__placement_editor", "--test-filter=actor3d/placement_editor_*",
    ],
    "handoff": [
        "--with-mods=proto_land_pack,navigation3d_base,world_stream3d_base,npc_routines_base,effects3d_base",
        "--itest=interactive3d_contracts", "--itest=npc_routines_base_contracts",
        "--itest=navigation3d_contracts", "--itest=navigation3d_streaming",
        "--run-tests", "--mod=navigation3d_base__streaming",
        "--test-filter=navigation3d/native_composite_item_action_region_save_roundtrip",
    ],
    "clearance": [
        "--with-mods=proto_land_pack,navigation3d_base,npc_routines_base",
        "--itest=interactive3d_contracts", "--itest=npc_routines_base_contracts",
        "--itest=navigation3d_contracts", "--run-tests", "--mod=navigation3d_base__contracts",
        "--test-filter=navigation3d/clearance_*",
    ],
    "pack_calibration": [
        "--with-mods=proto_land_pack", "--itest=proto_land_contracts",
        "--run-tests", "--mod=proto_land_pack__contracts",
        "--test-filter=proto_land/contact_calibration_native_geometry",
    ],
    "pack_all": [
        "--with-mods=proto_land_pack", "--itest=proto_land_contracts",
        "--run-tests", "--mod=proto_land_pack__contracts",
    ],
    "anim_units": [
        "--with-mods=proto_land_pack,anim3d_base", "--run-tests", "--mod=anim3d_base",
    ],
    "anim_override_units": [
        "--with-mods=proto_land_pack,anim3d_base", "--run-tests", "--mod=anim3d_base",
        "--test-filter=anim3d/override_*",
    ],
    "anim_layers": [
        "--with-mods=proto_land_pack,characters3d_base", "--itest=anim3d_base_contracts",
        "--run-tests", "--mod=anim3d_base__contracts", "--test-filter=anim3d/layer_*",
    ],
    "character_layers": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_base",
        "--itest=anim3d_base_contracts", "--itest=characters3d_base_contracts",
        "--run-tests", "--mod=characters3d_base__contracts", "--test-filter=characters3d/layer_*",
    ],
    "actor_layers": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/layers_*",
    ],
    "editor_anim_units": [
        "--with-mods=proto_land_pack,anim3d_editor", "--run-tests", "--mod=anim3d_editor",
    ],
    "editor_anim_contracts": [
        "--with-mods=proto_land_pack,anim3d_editor,characters3d_base",
        "--itest=anim3d_base_contracts", "--itest=anim3d_editor_contracts",
        "--run-tests", "--mod=anim3d_editor__contracts",
    ],
    "editor_anim_fields": [
        "--with-mods=proto_land_pack,anim3d_editor,characters3d_base",
        "--itest=anim3d_base_contracts", "--itest=anim3d_editor_contracts",
        "--run-tests", "--mod=anim3d_editor__contracts", "--test-filter=anim3d_editor/*_ru_en",
    ],
    "anim_contracts": [
        "--with-mods=proto_land_pack,characters3d_base", "--itest=anim3d_base_contracts",
        "--run-tests", "--mod=anim3d_base__contracts",
    ],
    "data": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,combat3d_base",
        "--run-tests", "--mod=actor3d_base", "--test-filter=actor3d/movement_*",
    ],
    "poses": [
        "--with-mods=proto_land_pack,actor3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_pose_*",
    ],
    "water": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_swim*,actor3d/movement_dive*",
    ],
    "climb": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_climb*,actor3d/movement_fall*,actor3d/movement_ladder*",
    ],
    "navigation": [
        "--with-mods=proto_land_pack,navigation3d_base", "--run-tests", "--mod=navigation3d_base",
        "--test-filter=navigation3d/capability_*",
    ],
    "reload": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_reload_*",
    ],
    "editor": [
        "--with-mods=proto_land_pack,actor3d_base,scene3d_editor,characters3d_editor",
        "--itest=actor3d_movement", "--itest=actor3d_movement_editor", "--run-tests",
        "--mod=actor3d_base__movement_editor", "--test-filter=actor3d/movement_editor_*",
    ],
    "editor_marker": [
        "--with-mods=proto_land_pack,actor3d_base,scene3d_editor,characters3d_editor",
        "--itest=actor3d_movement", "--itest=actor3d_movement_editor", "--run-tests",
        "--mod=actor3d_base__movement_editor",
        "--test-filter=actor3d/movement_editor_location_addon_and_marker_fields",
    ],
    "restore_descend": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_reload_ladder_reservation_and_progress,actor3d/movement_ladder_down_native_landing",
    ],
    "contacts": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_contacts_*",
    ],
    "npc": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_npc_*",
    ],
    "ships": [
        "--with-mods=proto_land_pack,proto_sea_pack,actor3d_base,water3d_base,stats_base,ships3d_base",
        "--itest=actor3d_movement", "--itest=actor3d_movement_ships", "--run-tests",
        "--mod=actor3d_base__movement_ships", "--test-filter=actor3d/movement_ship_*",
    ],
    "arrival": [
        "--with-mods=proto_land_pack,interactive3d_base", "--run-tests", "--mod=interactive3d_base",
        "--test-filter=interactive3d/physical_arrival_*",
    ],
    "combat": [
        "--with-mods=proto_land_pack,actor3d_base,combat3d_base",
        "--itest=actor3d_movement", "--itest=combat3d_base_contracts", "--itest=actor3d_movement_combat",
        "--run-tests", "--mod=actor3d_base__movement_combat", "--test-filter=actor3d/movement_combat_*",
    ],
    "actor_units": [
        "--with-mods=proto_land_pack,actor3d_base", "--run-tests", "--mod=actor3d_base",
    ],
    "actor_contracts": [
        "--with-mods=proto_land_pack,actor3d_base,proto_sea_pack", "--itest=actor3d_contracts",
        "--run-tests", "--mod=actor3d_base__contracts",
    ],
    "interactive_units": [
        "--with-mods=proto_land_pack,interactive3d_base,combat3d_base", "--run-tests", "--mod=interactive3d_base",
    ],
    "interactive_contracts": [
        "--with-mods=proto_land_pack,interactive3d_base,combat3d_base,effects3d_base",
        "--itest=interactive3d_contracts", "--itest=combat3d_base_contracts",
        "--itest=actor3d_movement", "--itest=actor3d_movement_combat",
        "--run-tests", "--mod=interactive3d_base__contracts",
    ],
    "editor_interactive_units": [
        "--with-mods=proto_land_pack,interactive3d_editor", "--run-tests", "--mod=interactive3d_editor",
    ],
    "editor_interactive_contracts": [
        "--with-mods=proto_land_pack,interactive3d_editor,combat3d_base,effects3d_base",
        "--itest=interactive3d_contracts", "--itest=interactive3d_editor_contracts",
        "--itest=combat3d_base_contracts", "--itest=actor3d_movement", "--itest=actor3d_movement_combat",
        "--run-tests", "--mod=interactive3d_editor__contracts",
    ],
    "characters_units": [
        "--with-mods=proto_land_pack,characters3d_base", "--run-tests", "--mod=characters3d_base",
    ],
    "characters_contracts": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_base",
        "--itest=anim3d_base_contracts", "--itest=characters3d_base_contracts",
        "--run-tests", "--mod=characters3d_base__contracts",
    ],
    "characters_shutdown": [
        "--with-mods=proto_land_pack,characters3d_base", "--itest=characters3d_shutdown",
        "--itest=anim3d_base_contracts", "--itest=characters3d_base_contracts",
        "--run-tests", "--mod=characters3d_base__shutdown",
    ],
    "characters_lod_fitting": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_base",
        "--itest=anim3d_base_contracts", "--itest=characters3d_base_contracts",
        "--run-tests", "--mod=characters3d_base__contracts",
        "--test-filter=characters3d/lod_jitter_has_no_tree_allocations_and_replacement_reapplies_band",
    ],
    "editor_characters": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_editor",
        "--itest=anim3d_base_contracts", "--itest=characters3d_editor_contracts",
        "--run-tests", "--mod=characters3d_editor__contracts",
    ],
    "editor_character_locale": [
        "--with-mods=proto_land_pack,proto_sea_pack,characters3d_editor",
        "--itest=anim3d_base_contracts", "--itest=characters3d_editor_contracts",
        "--run-tests", "--mod=characters3d_editor__contracts", "--test-filter=editors3d/*",
    ],
    "pack": [
        "--with-mods=proto_land_pack", "--itest=proto_land_contracts", "--run-tests",
        "--mod=proto_land_pack__contracts", "--test-filter=proto_land/load_assets_*,proto_land/all_clips_*",
    ],
    "stats_policy": [
        "--with-mods=proto_land_pack,stats_base", "--run-tests", "--mod=stats_base",
        "--test-filter=service/participation_is_opt_in",
    ],
    "template_policy": [
        "--with-mods=proto_land_pack,actor3d_base,stats_base", "--run-tests", "--mod=actor3d_base__stats",
        "--test-filter=actor3d/movement_optional_character_template",
    ],
    "drop": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_fall_drop_*",
    ],
    "ledge_drop": [
        "--with-mods=proto_land_pack,actor3d_base,water3d_base,stats_base,navigation3d_base",
        "--itest=actor3d_movement", "--run-tests", "--mod=actor3d_base__movement",
        "--test-filter=actor3d/movement_climb_*,actor3d/movement_fall_*,actor3d/movement_ladder_moving*",
    ],
}

EXPECTED = {
    "editor_character_fields": 2,
    "editor_character_layers": 1, "editor_actor_layers": 1,
    "editor_traversal": 2,
    "editor_dialog_units": 1, "editor_dialog_actor": 2,
    "editor_placement": 3,
    "handoff": 1,
    "clearance": 4,
    "pack_calibration": 1,
    "pack_all": 10,
    "anim_units": 12, "anim_contracts": 19, "anim_override_units": 2, "anim_layers": 4,
    "editor_anim_units": 1, "editor_anim_contracts": 7, "editor_anim_fields": 2,
    "data": 3, "poses": 1, "water": 3, "climb": 6, "navigation": 1,
    "reload": 2, "editor": 4, "contacts": 1, "npc": 2, "ships": 1, "arrival": 1, "combat": 1,
    "actor_units": 8, "actor_contracts": 15, "interactive_units": 24, "interactive_contracts": 13, "pack": 2,
    "stats_policy": 1,
    "template_policy": 1,
    "drop": 1,
    "ledge_drop": 5,
    "editor_marker": 1, "restore_descend": 2,
    "editor_interactive_units": 3, "editor_interactive_contracts": 3,
    "characters_units": 10, "characters_contracts": 19, "characters_shutdown": 1,
    "character_layers": 2, "actor_layers": 2,
    "characters_lod_fitting": 1,
    "editor_characters": 12, "editor_character_locale": 1,
}


def write_json(path, value):
    """Keep partial outcomes reviewable even if a later selected launch fails."""
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def snapshot(root, target, editors, cases):
    """Copy only selected 3D mods, their actual dependencies and the shared host infrastructure."""
    target.mkdir()
    ignore = shutil.ignore_patterns(".git", ".git-template", "bin", "obj", "docs", "__pycache__")
    for folder in ["Core", "scenes", "config"]:
        shutil.copytree(root / folder, target / folder, ignore=ignore)
    shutil.copytree(root / "collections/frontend", target / "collections/frontend")
    for name in ["Cthangover.csproj", "global.json", "Directory.Build.props", "Directory.Build.targets", "project.godot"]:
        shutil.copy2(root / name, target / name)
    shutil.copytree(root / ".godot/mono/temp/bin/Debug", target / ".godot/mono/temp/bin/Debug")
    pending = [
        "actor3d_base", "proto_land_pack", "water3d_base", "interactive3d_base", "navigation3d_base",
        "combat3d_base", "ships3d_base", "proto_sea_pack", "menu_base", "mod_manager", "dialog_base", "schema_tools",
        "items_base",
    ]
    if editors:
        pending.extend(["scene3d_editor", "characters3d_editor"])
    for case in cases:
        for argument in CASES[case]:
            if argument.startswith("--with-mods="):
                pending.extend(argument.split("=", 1)[1].split(","))
    copied = set()
    while pending:
        name = pending.pop()
        if name in copied:
            continue
        source = root / "mods" / name
        if not source.exists():
            source = root / "mods_private" / name
        shutil.copytree(source, target / source.parent.name / name, ignore=ignore)
        copied.add(name)
        manifest = json.loads((source / "manifest.json").read_text(encoding="utf-8-sig"))
        for dependency in manifest.get("depends", []):
            pending.append(re.split(r"[><=]", dependency)[0])
    return sorted(copied)


def main():
    """Build once, then require a nonempty successful terminal and a clean native shutdown per selected case."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--godot", default=r"C:\godot\godot.exe")
    parser.add_argument("--case", choices=CASES, action="append", required=True)
    parser.add_argument("--physics", choices=["godot", "jolt"], default="godot")
    args = parser.parse_args()
    root = Path.cwd()
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    checks = []
    with tempfile.TemporaryDirectory(prefix="cthangover-movement-") as temporary:
        project = Path(temporary) / "source"
        mods = snapshot(root, project, any(case.startswith("editor") for case in args.case), args.case)
        engine = "Jolt Physics" if args.physics == "jolt" else "Godot Physics"
        (project / "override.cfg").write_text('[physics]\n3d/physics_engine="' + engine + '"\n', encoding="utf-8")
        hashes = {}
        for group in ["Core", "mods", "mods_private"]:
            for path in (project / group).rglob("*"):
                if path.is_file() and (path.suffix in [".cs", ".json", ".tscn", ".glb", ".properties", ".r16", ".gdshader", ".scenario"]
                                       or path.name.endswith(".cs.txt")):
                    hashes[path.relative_to(project).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        write_json(output / "inputs.json", {"mods": mods, "hashes": hashes, "physics": engine})
        with (output / "build.log").open("wb") as log:
            subprocess.run(
                ["dotnet", "build", "Cthangover.csproj", "--nologo", "-v", "minimal"],
                cwd=project, stdout=log, stderr=subprocess.STDOUT, timeout=180, check=True,
            )
        for case in args.case:
            folder = output / case
            folder.mkdir()
            environment = os.environ.copy()
            for variable, name in [("APPDATA", "roaming"), ("LOCALAPPDATA", "local"), ("XDG_DATA_HOME", "data")]:
                home = Path(temporary) / case / name
                home.mkdir(parents=True)
                environment[variable] = str(home)
            environment["ACTOR3D_MOVEMENT_EVIDENCE"] = str(folder)
            if case in ["pack", "pack_all", "pack_calibration"]:
                environment["LAND_PACK_EVIDENCE"] = str(folder)
                environment["INTERACTIVE3D_SHOTS"] = str(folder / "captures")
            if case == "editor_interactive_contracts":
                environment["INTERACTIVE3D_SHOTS"] = str(folder / "captures")
            if case == "characters_contracts":
                environment["INTERACTIVE3D_SHOTS"] = str(folder / "captures")
            if case == "editor_characters":
                environment["ANIM3D_SHOTS"] = str(folder / "captures")
                environment["EDITORS3D_LOCALE_SHOTS"] = str(folder / "locales")
            if case == "editor_anim_contracts":
                environment["ANIM3D_SHOTS"] = str(folder / "captures")
            command = [args.godot, "--path", str(project), "--verbose", "--", "--frontend", *CASES[case]]
            with (folder / "stdout.log").open("wb") as log:
                process = subprocess.Popen(command, cwd=temporary, env=environment, stdout=log, stderr=subprocess.STDOUT)
                write_json(folder / "process.json", {"pid": process.pid, "command": command})
                try:
                    timeout = 1800 if case == "pack_all" else 720 if case == "pack" else 300
                    deadline = time.monotonic() + timeout
                    compared_frames = set()
                    while process.poll() is None:
                        if case == "editor_characters" and not compared_frames:
                            for capture in (folder / "locales").rglob("*.png"):
                                comparison = folder / "display" / capture.relative_to(folder / "locales")
                                comparison.parent.mkdir(parents=True, exist_ok=True)
                                save_client(process.pid, comparison)
                                compared_frames.add(capture)
                                write_json(folder / "display-comparison.json", {
                                    "source": str(capture.relative_to(folder)),
                                    "display": str(comparison.relative_to(folder)),
                                })
                                break
                        display_request = folder / "captures/display-check.ready"
                        display_done = folder / "captures/display-check.done"
                        if display_request.exists() and not display_done.exists():
                            save_client(process.pid, folder / "captures/display-client.png")
                            display_done.write_text("captured\n", encoding="utf-8")
                        request = folder / "capture.request.json"
                        if request.exists():
                            name = json.loads(request.read_text(encoding="utf-8-sig"))
                            save_client(process.pid, folder / (name + "-display.png"))
                            request.unlink()
                            (folder / (name + ".captured")).write_text("captured\n", encoding="utf-8")
                        if time.monotonic() >= deadline:
                            raise subprocess.TimeoutExpired(command, timeout)
                        time.sleep(0.01 if case == "editor_characters" else 0.05)
                    code = process.returncode
                except BaseException:
                    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True)
                    process.wait(timeout=20)
                    raise
            for source_name, destination in [("logs", "game-logs"), ("mod_tests", "mod-tests")]:
                source = Path(environment["APPDATA"]) / "cthangover" / source_name
                if source.exists():
                    shutil.copytree(source, folder / destination)
            text = (folder / "stdout.log").read_text(encoding="utf-8", errors="replace")
            terminals = re.findall(r"\[TEST_RESULT\] name=mod_units passed=(\d+) failed=(\d+)", text)
            all_text = text
            for path in (folder / "game-logs").glob("*.log*"):
                all_text += "\n" + path.read_text(encoding="utf-8", errors="replace")
            errors = []
            for path in (folder / "game-logs").glob("cthangover_errors.log*"):
                for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
                    if line.strip():
                        errors.append(path.name + ": " + line)
            lines = all_text.splitlines()
            for index, line in enumerate(lines):
                lowered = line.lower()
                if any(marker in lowered for marker in [
                    "error cs", "lack lifecycle binding", "leaked at exit", "still in use at exit",
                    "objectdb instances leaked", "was leaked", "were leaked", "rid allocations", "leaked instance",
                ]):
                    errors.append(line)
                if line.startswith("ERROR:"):
                    known_overlay = "Loader Message" in line and index + 1 < len(lines)
                    known_overlay = known_overlay and "EOSOverlayVkLayer-Win64.json" in lines[index + 1]
                    if not known_overlay:
                        errors.append(line)
            if len(terminals) != 1 or terminals[0] != (str(EXPECTED[case]), "0"):
                errors.append("Expected " + str(EXPECTED[case]) + " passing cases: " + str(terminals))
            row = {"case": case, "exit_code": code, "terminal": terminals, "errors": sorted(set(errors))}
            checks.append(row)
            write_json(output / "checks.json", checks)
            print(json.dumps(row, ensure_ascii=False), flush=True)
    if any(row["exit_code"] or row["errors"] for row in checks):
        raise RuntimeError("Selected movement contracts failed; inspect " + str(output / "checks.json"))


if __name__ == "__main__":
    main()
