"""Run the managed MCP dependency probe against the current Cthangover core."""

import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    source = Path(__file__).resolve().parent
    project = source.parents[2]
    env = os.environ.copy()
    if os.name != "nt":
        env.setdefault("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1")
    with tempfile.TemporaryDirectory(prefix="cthangover-mcp-build-") as directory:
        scratch = Path(directory)
        for name in ("ManagedMcpProbe.csproj", "ManagedMcpProbe.cs.txt", "BridgeCommand.cs.txt"):
            shutil.copyfile(source / name, scratch / name)
        dependencies = scratch / "dependencies"
        dependencies.mkdir()
        shutil.copyfile(source / "dependencies" / "McpDependencies.csproj", dependencies / "McpDependencies.csproj")
        subprocess.run(
            ["dotnet", "build", str(dependencies / "McpDependencies.csproj"), "--nologo", "--verbosity", "quiet"],
            cwd=project, env=env, check=True,
        )
        subprocess.run(
            ["dotnet", "run", "--project", str(scratch / "ManagedMcpProbe.csproj"), "--verbosity", "quiet",
             f"-p:CoreProject={project / 'Cthangover.csproj'}", "--", str(dependencies / "bin" / "Debug" / "net10.0")],
            cwd=project, env=env, check=True,
        )


if __name__ == "__main__":
    main()
