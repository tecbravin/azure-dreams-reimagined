# Town terrain recovery

## Imported source data

`Tools/extract_town_layout.py` authenticates TOWN.BIN against SHA256
`907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18`.
It reads the native descriptor at RAM `0x800D1B64` and checks its decoded
destination `0x8011AC98`, LBA `4126`, and extent of 27 sectors. All 27
Mode-2 Form-1 sector payloads from `Azure Dreams.bin` are compared with
TOWN.BIN `0x20C800..0x21A000` before import.

The resulting 55,296-byte bank has SHA256
`0e7c87b87ef97cb7814814255d6b3057258362953ad40e1b5f2e067180f07c7a`.
It is imported as `Assets/AzureDreams/Resources/Town/Retail/town_layout_bank.bytes`.
The adjacent `town_layout_metadata.json` records the provenance, tile pointers,
dimensions, offsets, hashes, plot records and default assignment pairs.

The non-null prefix of the table at RAM `0x800D17D8` contains 33 entries.
Twelve reference nonempty width/height-prefixed tile blocks; the others
reference the empty block. No allocation bound is inferred from that terminator.

## Town grid implementation

`TownRetailTileGrid` represents the Town-specific 128x128 halfword grid.
The source is `town/map/initialize_building_shared_buffer.c`: shifts 7,
coordinate masks 127, spans `0x2000`, with 64 world units per tile.
This two-byte grid must not use the dungeon's six-byte cell decoder.

`copy_variant_building_tiles.c` supplies `ApplyPatch` and
`ApplyLittleEndianPatch`: zero is transparent, six tile-ID families select
variants using global coordinate parity, and other halfwords copy unchanged.
`grid_base.c:blit_nonzero_u16_block_to_grid` supplies `BlitNonzeroPatch` and
`BlitLittleEndianPatch`: zero is transparent and all nonzero IDs retain their
original value without variant substitution.

Queries reproduce the Town reader's wrapping and `0x3FFF` mask. Signed
halfword world positions divide by 64 toward zero, including negative values.
Managed writes validate the complete block/region before modifying the grid.

The separate origin-prefixed image blitters (`blit_origin_block.c` and
`blit_indexed_origin_block.c`) write caller-supplied 128-wide buffers. Their
table at `0x800D1868` is not substituted for the world tile table without
proving the destination/caller context.

## Building layout data

The native initializer at file `0x1FF20..0x1FF30` references the plot table
at RAM `0x800D2EA4` (file `0x55744`). Region initialization reads slots
0..32 at eight-byte stride; slot zero is empty. The exporter preserves all
33 records, including raw kind/default bytes and the four rectangle bytes.
`Documentation/TownRetailPlotLayout.svg` shows these source coordinates with
labels `slot : default record ID`; it does not label residents/building names.

`copy_record_tail_bytes.c` initializes 34 assignment pairs from bytes +6/+7
of these records. The extra pair at save address `0x800133E6` is consumed by
the special-building path of `CheckBuildBuildingLandNo`; it is not claimed
to be a 34th spatial plot. Referenced 32-byte building records at
`0x800D2644` are preserved in the metadata with their source kind, dimensions,
availability flag, and raw bytes.

## Validation and remaining binding

The Unity PlayMode report `Logs/TownRetailTerrainPlayMode2.xml` covers 40
passing Town cases, including all six variant families, direct copies,
negative-coordinate queries, bounds rejection, and loading the imported
disc bank through Resources. Earlier native checks also verified all 12
Town prefabs and real keyboard/camera rendering of the reconstruction.

These data and algorithms are not yet a complete retail Town mesh. Tile IDs
still need their Town-specific face/vertex/normal definitions, texture-page
and CLUT bindings, and the complete composition order. Building identities,
NPC placement/scripts and retail save progression remain separate tasks.
The known exported dungeon tileset is not accepted as a Town tileset.
The playable demo retains its provisional layout until these bindings are proven.

## Original geometry and texture recovery (2026-11-04)

`Tools/extract_town_geometry.py` now reads the ISO9660 directory directly from
`Azure Dreams.bin`. It authenticates the resident executable and TOWN.BIN,
checks the native lookup instructions at 0x80046D70/74, and follows the type-0
record at 0x800D1E8C. Four compressed chunks at LBAs 4463/4479/4495/4511
produce 208,084 bytes, SHA256
`93d9a57b7004b9d9220607e61531d126421408f4db1a15b7c602f9f2c579cd68`.
The exported regions contain 934 face-list pointers, 2,314 vertex records and
space for 785 normal records. Capacity is not claimed as an exact allocation
count. The zero auxiliary offset is preserved.

`TownRetailGeometry` loads these authenticated resources, preserves native
signed coordinates (map X, map Y, height), reads each 24-byte packet's four
indices, UVs, TPAGE, CLUT and control bytes, and implements native packet
advancement. `render_shaded_map_tiles` has different jumps after front-facing
and rejected polygons; it is not a flat array of unconditional quads. The
caller must provide the projection result. Depth rejection can terminate a
list. This reader does not claim to reproduce PS1 projection or lighting.

`Tools/extract_town_vram.py` replays the image and palette writes of the four
common texture banks followed by the selected variant bank, in source order.
Variant zero yields 39 commands and 110,336 known VRAM words. Every source
bank is compared with the authenticated TOWN.BIN file. The exported 1024x512
word snapshot includes a separate byte-per-word coverage mask; untouched
memory is unknown. Image-command palette STP changes are represented.

`TownRetailVram` samples 4-bit indexed, 8-bit indexed and 15-bit direct pages,
uses each face's CLUT, expands RGB555, preserves the STP bit separately, and
distinguishes known transparent zero from an unavailable texture/palette word.
This snapshot does not cover every palette and texture referenced by the
Town geometry. Initial/shared uploads outside this routine, palette animation,
PS1 blend modes and scene-specific order still need recovery. It must not be
presented as a complete retail texture atlas.

Native Unity validation: `Logs/TownGeometryAndVramPlayMode.xml` passes 47/47:
40 existing Town integration cases, 3 geometry cases (including all 934 lists
through both front-facing and rejected-face paths), and 4 VRAM cases. Tests
check signed source coordinates, original packet fields, valid vertex/normal
references, bounded traversal, all three texture depths, missing coverage,
and sampling an actual imported Town face. These are data/runtime tests;
the visible demo scene has not yet been replaced by the original Town mesh.
