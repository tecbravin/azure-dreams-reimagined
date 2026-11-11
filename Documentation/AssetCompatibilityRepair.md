# Asset and Timeline compatibility repair

Observed baseline: Unity 6000.5.6f1, Timeline 1.8.7. AnimatorBindingCache used
GetInstanceID, which this Editor rejects as CS0619. The official Unity package
registry's Timeline 1.8.13 uses GetObjectId with a UNITY_6000_3_OR_NEWER branch
to GetEntityId. The unmodified official package is embedded under
Packages/com.unity.timeline; Package Manager resolved it as embedded. No edits
were made to Library/PackageCache and the existing manifest pin was preserved.

Six quarantined .png files actually contained serialized Sprite YAML. They
were restored from verified_decode/sprites/quarantine; their .meta GUIDs were
preserved. Backups are in Temp/AssetRepairBackup. The split-spritesheet builder
now saves generated Sprite assets with .asset extensions, never over .png.
Quarantined builders use persistent imported Sprite subassets instead of
unsaved in-memory sprites. The targeted postprocessor imports quarantine PNGs
as single point-filtered sprites.

KohColorKey.shader uses consistent LF line endings. The CLUT generator names
new assets clut_words; the Editor repaired 51 existing Texture2D object names.
Logs/AssetRepairValidation.json confirms all 40 quarantine PNGs loaded as sprites
and the name repair completed. The original open Editor recompiled and logged
ASSET_REPAIR_OK. Earlier errors remain in the retained log as historical data.

Full-suite validation uses a copy at Validation/AssetAndTests_20261104 with
Assets, Packages and ProjectSettings, running Unity 6000.5.6f1. This preserves
the user's open Editor and unsaved scene. Reports are written to the principal
project's Logs/CurrentFullEditMode.xml and Logs/CurrentFullPlayMode.xml.
