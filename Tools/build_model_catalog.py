"""Run PSXPrev scans and catalog their exports alongside captured scene models.

From the Unity project: python Tools/build_model_catalog.py [--scan]
"""

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import subprocess


PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
EXTRACTED = WORKSPACE / "extracted_assets" / "reimagined_pipeline" / "PSXPrev"
CAPTURES = WORKSPACE / "extracted_assets" / "azure_dreams_3d_models"
PSXPREV = WORKSPACE / "psxprev" / "bin" / "Release" / "PSXPrev.exe"
SOURCES = (
    ("azure_dreams_all_formats", WORKSPACE / "azure_dreams_extracted.iso"),
    ("azure_dreams_re_extracted", WORKSPACE / "azure_dreams_re_extracted.iso"),
)


def scan(source_name: str, iso: Path) -> None:
    if not PSXPREV.is_file() or not iso.is_file():
        raise FileNotFoundError(f"PSXPrev or source ISO missing: {PSXPREV}, {iso}")
    destination = EXTRACTED / source_name
    destination.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment["PSXPREV_EXPORT_DIR"] = str(destination)
    result = subprocess.run(
        [str(PSXPREV), str(iso), "-scaniso"],
        cwd=WORKSPACE, env=environment, timeout=900, check=False,
    )
    if result.returncode or not (destination / "models.tsv").is_file():
        raise RuntimeError(f"PSXPrev scan failed for {iso}: exit {result.returncode}")
    if (destination / "export-error.txt").exists():
        raise RuntimeError((destination / "export-error.txt").read_text())


def obj_metrics(path: Path) -> dict:
    vertices = 0
    faces = 0
    minimum = [float("inf")] * 3
    maximum = [float("-inf")] * 3
    with path.open(encoding="utf-8", errors="replace") as source:
        for line in source:
            if line.startswith("v "):
                coordinates = [float(value) for value in line.split()[1:4]]
                if len(coordinates) != 3:
                    continue
                vertices += 1
                for axis, value in enumerate(coordinates):
                    minimum[axis] = min(minimum[axis], value)
                    maximum[axis] = max(maximum[axis], value)
            elif line.startswith("f "):
                faces += 1
    return {
        "vertices": vertices,
        "faces": faces,
        "extent": [round(maximum[i] - minimum[i], 4) for i in range(3)] if vertices else [],
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def catalog() -> list[dict]:
    entries = []
    for folder in sorted(CAPTURES.glob("*/catalog.obj")):
        entries.append({
            "kind": "captured_scene",
            "label": folder.parent.name,
            "source": str(WORKSPACE / "AzureDreams_3D_models" / (folder.parent.name + ".3dm")),
            "model": str(folder),
            "textures": len(list((folder.parent / "textures").glob("*.png"))),
            **obj_metrics(folder),
        })
    for source_name, iso in SOURCES:
        directory = EXTRACTED / source_name
        manifest = directory / "models.tsv"
        if not manifest.exists():
            continue
        with manifest.open(encoding="utf-8", errors="replace") as source:
            for row in csv.reader(source, delimiter="\t"):
                if len(row) < 4:
                    continue
                model = directory / f"model_{row[0]}.obj"
                if not model.is_file():
                    continue
                entries.append({
                    "kind": "psxprev_candidate",
                    "label": row[1],
                    "source": str(iso),
                    "source_format": row[2],
                    "source_offset": row[3],
                    "model": str(model),
                    "textures": len(list(directory.glob("*.png"))),
                    **obj_metrics(model),
                })
    return entries


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", action="store_true", help="rerun both PSXPrev ISO scans")
    args = parser.parse_args()
    if args.scan:
        for source_name, iso in SOURCES:
            scan(source_name, iso)
    entries = catalog()
    destination = WORKSPACE / "extracted_assets" / "reimagined_pipeline" / "ModelCatalog"
    destination.mkdir(parents=True, exist_ok=True)
    summary = {
        "captured_scenes": sum(item["kind"] == "captured_scene" for item in entries),
        "psxprev_candidates": sum(item["kind"] == "psxprev_candidate" for item in entries),
        "identified_characters": 0,
        "note": "Scanner candidates need visual/source verification. Captured OBJs are town scenes, not character meshes.",
        "models": entries,
    }
    output = destination / "catalog.json"
    output.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"{output}: {summary['captured_scenes']} scenes, {summary['psxprev_candidates']} scanner candidates, 0 identified characters")


if __name__ == "__main__":
    main()
