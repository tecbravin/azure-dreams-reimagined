# Imported C++ tool exports

- Sprites/Portraits/AdResourcesDumper/Frames: 306 composed portrait frames.
- Sprites/Portraits/AdResourcesDumper/Components: 1,363 component sprites.
- Models/Extracted/VirtualMonsbaia/type_*: 41 diagnostic OBJ/MTL catalogs.
- Textures/Extracted/VirtualMonsbaia: 633 textures plus 633 coverage masks.
- Textures/Extracted/AdResourcesDumper/TownVRAM: raw VRAM preview.
- Data/Extracted: source manifests, animation durations and face metadata.
- Assets/StreamingAssets/Extracted/VirtualMonsbaia: .3dm diagnostic snapshots
  and raw VRAM buffers, preserved as source data.

The import manifest lists all 3,145 source exports and destination hashes.
MTL texture paths were adapted to their Unity destination. Portraits import as
single sprites, point filtered, without mipmaps or compression. Coverage masks
import in linear color space. The dedicated Editor importer only targets these
new extracted folders.

VirtualMonsbaia models retain the diagnostic catalog arrangement; they do not
represent the retail city layout. Texture coverage is partial and OBJ materials
do not reproduce PS1 blending. Importing these assets does not automatically
replace existing scene buildings or assign NPC identities.

Reproduce the copy with Tools/import_cpp_exports.py from the Azure workspace.
Unity validation: AzureDreamsReimagined.Editor.CppExtractedAssetImporter.Verify.
See Logs/CppExtractedAssetImport.json for the native Editor result.
