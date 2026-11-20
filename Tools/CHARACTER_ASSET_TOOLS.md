# Original character asset tools

## What the original game actually stores

The retail town character renderer uses sprite resources, not a separate
polygon mesh for each NPC. In the decompilation,
`src/town/person/object/initialize_object_sprite.c` initializes the person
sprite; `src/town/player/initialize_scene_for_mode.c` assigns the town
player's sprite and calls `load_town_sprite_bundle`; and
`src/engine/graphics/sprite_projection.c` submits the visible pieces as
textured quads. `src/engine/graphics/resource_load_indexed_sprite_bundle.c`
loads the indexed sprite bundles and relocates their metadata. These are
specific evidence for the town/player rendering path, rather than a claim
that the disc contains no polygonal data anywhere.

The strongest route to **original character visuals** is therefore to decode
the town sprite bundles from the authenticated disc, retain their palettes,
part offsets, facing selection and frame timing, and render those parts on
camera-facing quads in Unity. The project already has town character PNGs
(`Assets/AzureDreams/Sprites/Characters/`) and a partial `NicoSpriteView`,
but the PNGs alone do not prove that every retail pose and animation was
recovered. `extract_koh_locomotion.py` and the verified dungeon sprite decoder
show the analogous resource decoding approach for Koh and monsters. The next
extractor should follow the town loader's resource table rather than scan for
TMD headers.

A second way to verify the decoder is a runtime RAM/VRAM capture while a
specific NPC is visible. DuckStation exposes RAM and VRAM dump functions;
comparing those dumps with the decoded town bundle would confirm palettes and
frame pieces. A GPU frame capture would contain the submitted sprite quads,
which helps verify pose and placement but still does not reveal a 3D body.

If the requirement is a **volumetric, freely rotatable 3D character**, it
must be reconstructed from the sprite views and other visual references.
Noesis, PSXPrev or a RAM scene capture cannot export a mesh that the original
character renderer does not supply. Such a reconstruction is new artwork,
not an extracted original model.

## PS1 TIM textures

The local `ps1-tim-tool` checkout is at `../../ps1-tim-tool`, relative to this
project. Use its Python CLI to decode standalone PlayStation TIM textures:

```powershell
$env:PYTHONIOENCODING = 'utf-8'
python ..\ps1-tim-tool\src\py\ps1_tim_tool.py character.tim --extract png --output character.png
```

For indexed TIMs with several palettes, repeat with `--palette-index N` and
give each output a distinct name. Preserve the TIM and palette index as source
provenance when importing the PNG into Unity.

The tool converts textures only; it does not decode 3D models or locate TIM
records embedded in game archives. For character geometry, use the existing
captured OBJ assets under `Assets/AzureDreams/Models/Town/CapturedScenes` and
the extraction scripts in this `Tools` folder. Match each extracted texture to
its model material before creating or repairing a character prefab.

## PSXPrev models and embedded textures

The local PSXPrev source checkout is at `../../psxprev`, relative to this
project. It scans PlayStation game data for TMD, PMD, HMD and other supported
model formats, along with TIM textures and animations. Open the built PSXPrev
application, scan the source disc image with `-scaniso` (or `-scanbin -databin`
for a raw BIN), inspect candidate models in the viewer, and export the selected
model with its matching textures. Record source file, byte offset, format,
palette, and character identification alongside every export.

The built executable is `../../psxprev/bin/Release/PSXPrev.exe`. The source
targets .NET Framework 4.5.1, whose reference assemblies are unavailable on
this machine. Rebuild against the installed .NET Framework 4.8 with:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ..\psxprev\PSXPrev.sln /p:Configuration=Release /p:TargetFrameworkVersion=v4.8
```

PSXPrev only
recognizes supported, uncompressed formats, so an empty scan does not prove
the original disc lacks a character model.

For a repeatable batch export, the local PSXPrev checkout now accepts the
`PSXPREV_EXPORT_DIR` environment variable. When set, it exports every model
recognized by the scan as OBJ, writes `models.tsv` with source offsets, and
exits. For example, from the Azure workspace root:

```powershell
$env:PSXPREV_EXPORT_DIR = 'Azure Dreams Reimagined/Extracted/PSXPrev/azure_dreams_all_formats'
& .\psxprev\bin\Release\PSXPrev.exe .\azure_dreams_extracted.iso -scaniso
```

The full scan of `azure_dreams_extracted.iso` exported nine candidates (one
TMD and eight MOD); the scan of `azure_dreams_re_extracted.iso` exported seven
MOD candidates. The raw BIN scan found no supported model. These are scanner
matches, not verified character models: the OBJ files have only 1–560 faces,
and most have implausible dimensions. `azure_dreams_re_2k.iso` has the same
SHA-256 hash as `azure_dreams_extracted.iso`, so it would duplicate that scan.

Run `python Tools/build_model_catalog.py` from the Unity project to combine the
PSXPrev exports with the 44 captured scene OBJs in
`Extracted/ModelCatalog/catalog.json`. Add `--scan` to rerun both ISO scans
before rebuilding the catalog. The catalog records source paths, offsets,
geometry counts, bounds and hashes; it does not label a scanner match as a
character without identification. At present it contains 44 captured scenes,
16 PSXPrev candidates, and zero identified original character models.

## Noesis

Noesis 4.474 is installed at `../../Tools/noesisv4474`. Its command line
converter reads the OBJ files produced by the existing `virtual_monsbaia`
extractor. A direct import of the 3 MiB `.3dm` RAM/VRAM captures produced no
model. The bundled Noesis Python importer did not load in command line mode,
and `.glb` in this Noesis release is a different, proprietary format, not
glTF. Do not label its `.glb` output as a portable glTF model.

`Tools/import_3dm_captures.py` bridges the existing decoder to a real glTF
2.0 binary exporter. It reads each capture's decoded OBJ, MTL, and VRAM PNGs;
if the decoded cache is missing, it runs `extract_scene_captures.py` first.
From this Unity project folder, generate all supplied captures with:

```powershell
python Tools\import_3dm_captures.py
```

Pass one or more `.3dm` paths to export selected captures. Results are in
`Extracted/CapturedGLB/`, with `import_manifest.json` recording geometry and
texture counts. All 44 supplied captures converted and passed GLB header,
chunk, buffer, and image-reference validation: 197,970 triangles and 1,790
embedded textures. These captures represent town/building scenes. They do not
contain separately identified original Patty or Nico character meshes; the
importer cannot create character geometry absent from its source capture.

## TIMmy

TIMmy 1.0 is at `../../Tools/timmy`. It scans arbitrary files for embedded
standard TIM images and can write TIM/BMP files; it cannot extract 3D models.
The executable fails to start on this Windows installation with a system DLL
relocation error. A strict scan for valid TIM headers and block lengths found
zero standard TIM records in `extracted_assets/files/TOWN/TOWN.BIN` and the
raw `Azure Dreams.bin`. The game uses VRAM/CLUT data handled by the project's
existing extraction scripts, so TIMmy cannot recover additional character
models or textures from these inputs.

## Ninja Ripper

Ninja Ripper is at `../../Tools/NinjaRipper`. It captures meshes and textures
from a running Direct3D application; it does not parse a PlayStation ISO or
BIN. To capture original Azure Dreams characters, run the disc in a compatible
Direct3D emulator, configure that emulator as Ninja Ripper's target, and rip
frames where the desired character is visible. The included Noesis importer
for `.rip` files is `tools/noesis_importer/fmt_ninjaripper_rip.py`.

No compatible PlayStation emulator executable or existing `.rip` capture was
found in this workspace or on the command path during this check. The present
`virtual_monsbaia` viewer uses OpenGL, which this Ninja Ripper version does
not list as a capture backend. No original character mesh was extracted by
Ninja Ripper in this environment.
