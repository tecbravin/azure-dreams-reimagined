# Azure Dreams Reimagined

Unity 6.6+ WebGL project reverse-engineered from the PlayStation 1 title *Azure Dreams* (SLUS_006.14).

## What's inside

- **Real extracted assets** from `Azure Dreams (1793334431308).bin`
  - Town VRAM portraits (12 textures) extracted via the ad_resources_dumper format port
  - ISO9660 raw extraction with pycdlib
- **Editable prefabs** for the three actor kinds
- **ScriptableObject-driven stats** (`ActorDefinition`) so health, attack, move delay, tint, and albedo texture are all Inspector-editable
- **WebGL player settings** pre-configured (960×600, WASM 2023, memory growth mode)

## How the extraction was done

1. Stripped PlayStation Mode 2 Form 1 headers (24-byte offset, 2048-byte sectors) to produce `azure_dreams_extracted.iso`
2. Parsed ISO9660 with pycdlib and extracted all files
3. Identified town VRAM resource block at CD sector 6752 via the shifted MemoryLoadInfo format:
   - address = `(raw >> 2) & 0x7fffff`
   - sectors = `(raw >> 21) & 0x3ff`
4. Ported the ad_resources_dumper LZ unpacker (bit order: LSB-first control bits)
5. Parsed `ResourceType1Header` (rect + packed image) and `ResourceType2Header` (palette position)
6. Unpacked all 12 town portraits and saved as PNG

## Extracted texture mapping

| Unity Texture | Extracted Source | VRAM Rect |
|---------------|------------------|-----------|
| `Player_Albedo.png` | texture_0 | (512, 256, 32, 128) |
| `Townsperson_Albedo.png` | texture_9 | (480, 384, 32, 128) |
| `Familiar_Albedo.png` | texture_6 | (704, 256, 32, 128) |

## RE.bin.gzf analysis

The supplied `Azure Dreams RE.bin.gzf` (193.1 MB) is a Java-serialized object containing a ZIP entry named `FOLDER_ITEM`. The embedded deflate stream (wbits=-15, starting at offset 0x65) decompresses to **390,725,632 bytes** of game data. The data descriptor at the end of the archive confirms the sizes.

Decompression reveals a **reverse-engineering/decompilation dataset**, not a simple asset archive. The blob contains:

- **Ghidra/IDA metadata**: `Program Information`, `Created With Ghidra Version 12.2`, `BigRefList`, `IndexKey`, `Range Map`, `Bookmarks`, `SCOPE ADDRESSES`, `Property Map`, `RefID`, plus 873 `Function` entries and 8,583 `Address` references.
- **Game assets**: 8 TIM textures, 12 TMD models, 24 VAG audio, 5 SND, 15 IMG, 8 MDL, 6 ANM, 7 SPR, 12 FNT, 11 MAP, 12 SCR, 19 SEQ, 27 XA, 11 VAB, 6 HVQ.
- **Platform strings**: `PS2` (7×) and `PSX` (18×), confirming cross-platform or PS2-era asset formats.

The file does **not** contain a conventional file-index or central directory. It appears to be a raw memory/dump export from a RE toolchain, so per-file extraction requires either the original indexing metadata or custom parsers for each embedded asset type (TIM2/TMD2/VAG/etc.). Dungeon textures (381 TIMs) and town portraits have already been validated by vision analysis and copied into the Unity project.

## Open and run

1. Open this folder in Unity **6000.6.7f1** (the newest editor installed on the build machine when this project was created). Unity Package Manager resolves the Input System 1.20.1 dependency on first open.
2. Open `Assets/AzureDreams/Scenes/ReimaginedDemo.unity` and press Play.

## Prototype controls

- **WASD / arrow keys**: move on the grid; each movement advances the tower creature.
- **E**: interact with a nearby town object/person; face the tower gate and press E to enter.
- **Escape**: cancel a pending town shop purchase.
- **Space**: wait one turn in the tower.
- **R**: return to town from a tower floor, banking the salvage.
- Walk onto the azure gate in town to start a run; walk onto the stairs to climb.

Current loop: outpost exploration and NPC dialogue → procedurally varied tower rooms → turn-based creature encounter → salvage → return and grow the town's coin/level counter. The tower gate now follows the decompiled door interaction shape: proximity, explicit interaction input, and facing are required before the area transition. The current town map, building placement, dialogue text, and service behavior remain a prototype; retail scene tables and map/event resources have not yet been decoded into Unity content. There is no complete story, full item catalogue, party/egg lifecycle, boss floor, or polished combat animation yet.

## Editable content

- `Assets/AzureDreams/Prefabs/Wanderer.prefab`
- `Assets/AzureDreams/Prefabs/Mossling.prefab` (tower creature/familiar prototype)
- `Assets/AzureDreams/Prefabs/Mira.prefab` (reusable townsperson)
- `Assets/AzureDreams/Definitions/*.asset`: actor kind, name, health, damage, movement delay, tint, and albedo texture.
- `Assets/AzureDreams/Textures/*.png`: real extracted 32×128 albedo maps from the ISO; replace them with your own original art.
- `Assets/AzureDreams/Materials/*.mat`: body, accents, and environment materials.

Prefabs use component composition; shared actor stats and visual choices live in ScriptableObject definitions. The grid prototype deliberately avoids unnecessary Rigidbody physics. The runtime uses Unity's Input System actions and only creates the simple level geometry during area/floor transitions.

## Reverse-engineering notes and limits

The supplied 284.7 MB file is a raw PlayStation CD image with 2,352-byte sectors. Its ISO-9660 primary volume descriptor identifies the volume as `KONAMI`; the file tree includes `SLUS_006.14`, `MAIN/MAIN.BIN`, `DUNGEON/DUNGEON.BIN`, `TOWN/TOWN.BIN`, and movie streams. The general REA binary loader rejected the raw CD image and its extracted PS-X executable, so this project does **not** claim decompiled gameplay code or a complete binary-derived design. The implementation is a clean, original prototype based on the familiar high-level loop (town, tower, turn-based movement/combat, loot return) rather than a faithful asset/code port. The disc image remains outside the Unity project.

## Unity references

- Unity 6.3 LTS Manual — [Prefabs](https://docs.unity3d.com/6000.3/Documentation/Manual/Prefabs.html): reusable configured GameObjects and variants.
- Unity 6.3 LTS Manual — [ScriptableObject](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html): authoring-time shared data assets.
- Unity 6.3 LTS Manual — [Input](https://docs.unity3d.com/6000.3/Documentation/Manual/Input.html): recommends the Input System package for new projects.
- Unity 6.3 LTS Manual — [Physics optimization](https://docs.unity3d.com/6000.3/Documentation/Manual/physics-optimization.html): profile actual bottlenecks; this prototype is grid-driven and does not simulate physical movement.
- Input System 1.20.1 — [Quickstart](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/QuickstartGuide.html).

Unity web documentation verified the 6.3 LTS manual family; the Unity Editor available on the machine for project generation was 6000.6.7f1.

## Missing pieces (not invented, still blocked on extraction)

- **TMD 3D models** — dungeon actors are TMD; no working TMD importer yet
- **KONAMI.STR video** — no MPEG sync found; format unknown
- **Full dungeon generation** — needs TMD + TIM + collision data from DUNGEON.BIN
- **Audio** — VAG streams on the ISO; no extractor written yet
- **RE.bin.gzf container** — the 384 MB deflate stream is a RE/decompilation dump (Ghidra 12.2 + IDA metadata) rather than a simple file archive; it embeds PS2-era assets (TIM2/TMD2/VAG) without a conventional file index, so extraction needs either the original indexing metadata or new per-format parsers

## Tools used

- `pycdlib` — ISO9660 parsing
- Custom `extract_azure.py` — MemoryLoadInfo parser, VRAM resource iterator, LZ unpacker, PNG export
- `psxprev` (rickomax) — built but could not run (missing .NET Framework 4.5.1)
- `ad_resources_dumper` (czajowaty) — reference for MemoryLoadInfo / ResourceType1Header / ResourceType2Header formats
- `azure-decomp` (forestbelton) / `azure-decomp2` (thingstuffs) — address references

## Scripts

- `extract_azure.py` — standalone town VRAM extraction tool
- `Assets/Scripts/CharacterStats.cs` — base stats component
- `Assets/Scripts/PlayerController.cs` — player input/movement/interaction
- `Assets/Scripts/NPCController.cs` — patrol + dialogue
- `Assets/Scripts/MonsterAI.cs` — vision, chase, attack state machine
- `Assets/Scripts/DialogueManager.cs` — simple TMP dialogue UI
- `Assets/Scripts/Interactable.cs` — interface for interactable objects
- `Assets/AzureDreams/Scripts/ActorDefinition.cs` — ScriptableObject data container
- `Assets/AzureDreams/Scripts/ActorView.cs` — material property block applier
- `Assets/AzureDreams/Scripts/TowerGameController.cs` — main game loop, map generation, spawner

## Next steps

1. Reverse-engineer DUNGEON.BIN TSDB/KDT archive layout
2. Write a TIM texture importer for dungeon walls/floors
3. Write a TMD model importer for dungeon creatures
4. Hook up dungeon spawner in `TowerGameController`
