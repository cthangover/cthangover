"""Regenerate small cross-platform provider fixtures with an external 7zz/7z.

Usage: python generate.py /workspace/tools/7zip/linux-x64/7zz
The password is public fixture data, not a credential. No tool is needed to run tests.
"""
from pathlib import Path
import subprocess
import sys
import tempfile

PASSWORD = "fixture-пароль"
target = Path(__file__).resolve().parent
with tempfile.TemporaryDirectory() as temporary:
    root = Path(temporary)
    (root / "manifest.json").write_text('{"id":"test_sevenzip","version":"1.2.3","category":"library"}', encoding="utf-8")
    (root / "Data").mkdir()
    (root / "Data" / "привет.txt").write_text("Привет из 7z!\n", encoding="utf-8")
    (root / "Data" / "payload.bin").write_bytes(bytes(range(256)) * 1024)
    (root / "empty").mkdir()
    (root / "zero.txt").write_bytes(b"")
    (root / ".hidden").mkdir()
    (root / ".hidden" / "secret.txt").write_text("hidden", encoding="utf-8")
    for name, options in [
        ("plain", ["-ms=off"]),
        ("solid", ["-ms=on"]),
        ("encrypted", ["-ms=off", "-p" + PASSWORD]),
        ("encrypted_solid", ["-ms=on", "-p" + PASSWORD]),
        ("encrypted_headers", ["-ms=off", "-mhe=on", "-p" + PASSWORD]),
        ("encrypted_headers_solid", ["-ms=on", "-mhe=on", "-p" + PASSWORD]),
    ]:
        archive = target / (name + ".7z")
        archive.unlink(missing_ok=True)
        subprocess.run([sys.argv[1], "a", "-t7z", "-m0=LZMA2", "-mx=1", "-md=1m", *options, str(archive), "."], cwd=root, check=True, stdout=subprocess.DEVNULL)
        print(archive.name, archive.stat().st_size)

    for name, encryption in [("encrypted_aes", "AES256"), ("encrypted_pkware", "ZipCrypto")]:
        archive = target / (name + ".zip")
        archive.unlink(missing_ok=True)
        subprocess.run([sys.argv[1], "a", "-tzip", "-mem=" + encryption, "-pfixture-password", str(archive), "."], cwd=root, check=True, stdout=subprocess.DEVNULL)
        print(archive.name, archive.stat().st_size)
