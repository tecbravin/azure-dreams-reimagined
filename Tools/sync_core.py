"""Build/test the pure core, then import its Unity-safe assembly. Run with python Tools/sync_core.py.
UnityPort excludes ONLY desktop filesystem/JSON SaveSystem; Unity uses its own PlayerPrefs adapter.
The movement resolver and ActorRecord are the same source tested by the normal core test project.
"""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import os

project = Path(__file__).resolve().parents[1]
core_home = project.parent / "AzureDreams"
subprocess.run(["dotnet", "test", str(core_home / "AzureDreams.Core.Tests/AzureDreams.Core.Tests.csproj")], check=True)
scratch = Path(os.environ["TMPDIR"])
scratch.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix="unity-core-", dir=scratch) as output:
    subprocess.run(["dotnet", "build", str(core_home / "AzureDreams.Core/AzureDreams.Core.csproj"),
                    "-c", "Release", "-p:UnityPort=true", "-o", output], check=True)
    dll = Path(output) / "AzureDreams.Core.dll"
    destination = project / "Assets/Plugins/AzureDreams.Core.dll"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dll, destination)
    digest = hashlib.sha256(dll.read_bytes()).hexdigest()
    manifest = {"assembly": destination.relative_to(project).as_posix(), "sha256": digest,
                "framework": "netstandard2.1", "build_property": "UnityPort=true",
                "excluded": ["desktop SaveSystem.cs", "System.Text.Json package"],
                "sources": {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                            for p in sorted((core_home / "AzureDreams.Core").glob("*.cs"))
                            if p.name != "SaveSystem.cs"}}
    (project / "Documentation/core-import.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("Imported", destination, "SHA256", digest)
