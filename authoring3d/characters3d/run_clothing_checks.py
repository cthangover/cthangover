"""Run only explicit wardrobe components through the existing isolated 3D host driver."""

from pathlib import Path
import sys


sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "actor3d"))
import run_movement_checks


def main():
    run_movement_checks.CASES["clothing_data"] = [
        "--with-mods=characters3d_base", "--run-tests", "--mod=characters3d_base",
        "--test-filter=characters3d/wardrobe_*",
    ]
    run_movement_checks.EXPECTED["clothing_data"] = 4
    run_movement_checks.CASES["clothing_mesh"] = [
        "--with-mods=characters3d_base", "--itest=characters3d_clothing", "--run-tests",
        "--mod=characters3d_base__clothing", "--test-filter=characters3d/clothing_mesh_*",
    ]
    run_movement_checks.EXPECTED["clothing_mesh"] = 4
    run_movement_checks.CASES["clothing_target"] = [
        "--with-mods=characters3d_base", "--itest=anim3d_base_contracts", "--run-tests",
        "--mod=anim3d_base__contracts", "--test-filter=anim3d/target_transaction_*",
    ]
    run_movement_checks.EXPECTED["clothing_target"] = 3
    run_movement_checks.CASES["clothing_boundary"] = [
        "--with-mods=characters3d_base", "--itest=characters3d_clothing", "--run-tests",
        "--mod=characters3d_base__clothing", "--test-filter=characters3d/clothing_boundary_*",
    ]
    run_movement_checks.EXPECTED["clothing_boundary"] = 3
    return run_movement_checks.main()


if __name__ == "__main__":
    main()
