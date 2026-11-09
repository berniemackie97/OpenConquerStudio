"""Run the built Studio and server tools in independent processes."""

from pathlib import Path
import subprocess


def execute(tool, *arguments, expected=0):
    result = subprocess.run(
        ["dotnet", str(tool), *map(str, arguments)],
        capture_output=True,
        text=True,
        timeout=90,
        check=False,
    )
    if result.returncode != expected:
        raise AssertionError(
            f"{Path(tool).name} returned {result.returncode}, expected {expected}\n"
            f"{result.stdout}\n{result.stderr}"
        )
    return result.stdout.strip()
