"""Build and run the isolated MCP transport probe in a temporary directory."""

from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    fixture = Path(__file__).resolve().parent
    with tempfile.TemporaryDirectory(prefix="cthangover-mcp-sdk-") as directory:
        project = Path(directory)
        shutil.copytree(fixture, project, dirs_exist_ok=True, ignore=shutil.ignore_patterns("bin", "obj"))
        result = subprocess.run(
            ["dotnet", "run", "--project", "SdkProbe.csproj", "--verbosity", "quiet"],
            cwd=project,
            check=False,
        )
        return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
