# AzureDreams_3D_models capture exports

The 44 supplied .3dm files are 3 MB snapshots compatible with virtual_monsbaia:
2 MB PS1 RAM followed by 1 MB VRAM. Unlike the synthetic diagnostic catalogs,
these snapshots already contain scene grids, relocated geometry pointers and
scene-specific texture/palette memory.

Export procedure uses the native ADScene reader from virtual_monsbaia through
its compiled batch adapter. Each source gets its own folder with:
- catalog.obj / catalog.mtl: geometry and local material references.
- textures/*.png: per-scene TPAGE+CLUT images, with RGB555 and transparency.
- textures/*_stp.png: original per-texel semitransparency-bit masks.
- faces.json: source cell positions and face texture/blending metadata.
- scene_grid.json: original grid dimensions, cell values and RAM pointers.
- vram.bin: raw VRAM from the supplied capture.

Source filenames identify house upgrades, shops, public buildings, monster hut
sizes, pool quest variants, a racetrack and town_min/town_max. These labels are
retained as supplied. Capture conditions and equivalence to a particular disc
or save progression have not been independently established.

OBJ indices and PNG structures are validated by the export script. Manifest
records source and OBJ SHA256 hashes and per-scene geometry/texture counts.
The native visualizer's sequential face traversal is retained; alternate faces
can coexist. Standard OBJ materials do not reproduce PS1 blend modes, lighting,
animated texture uploads or gameplay. No NPC rigs, movement animations, audio
or event scripts are recovered by this geometry exporter.

Reproduce from the Azure workspace:
python "Azure Dreams Reimagined/Tools/extract_scene_captures.py"

Outputs remain separate from Unity Assets pending scene integration.
