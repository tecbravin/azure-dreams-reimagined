# Confirmed scene visual replacement

ReimaginedDemo now references Prefabs/Koh.prefab instead of the authored
Wanderer prefab. The original Wanderer asset is retained. Koh inherits the
same ActorView/gameplay configuration; its placeholder mesh renderers are
hidden and KohSpriteView presents the supplied Koh walk sheet.

The sheet was visually inspected: eight 96x96 cells in a 768x96 RGB image.
The material removes its solid background by comparing with a corner sample;
source pixels and importer metadata are preserved. Runtime Sprite.Create
provides frame rectangles; no importer slicing or guessed directional rows.
The single supplied direction remains the same when moving in other directions.
Playback at 8 FPS and a 48-pixel unit scale are presentation settings, not
claimed recovered retail timing. The sprite faces the active main camera.

No house/NPC identity was inferred from provisional demo names: Inn, Market,
Town Residence, Mira and Rowan have no proven retail counterparts. Their
visuals are retained. Captured building models remain available for subsequent
placement once the scene's intended retail identities/layout are established.

Native Unity graphics-enabled validation passed 48/48 cases in
Logs/ConfirmedVisualPlayMode.xml, including Town events, geometry/VRAM tests,
and real keyboard movement/rendering of ReimaginedDemo. The smoke test verifies
KohSpriteView, a valid sprite and hidden placeholder mesh renderers. Screenshot:
Logs/Movement/demo-town-smoke.png. The rendered scene was inspected.

Apply/reproduce with AzureDreamsReimagined.Editor.ConfirmedSceneVisuals.Apply.
