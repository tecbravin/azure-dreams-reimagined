using System;
using System.Collections;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Tower fusion presentation, following the retail state machine
    /// advance_paired_object_sequence (DUNGEON.BIN 0x182C3C0, one state step per 30 Hz tick):
    ///  3/4  Koh turns to the camera; the two familiars walk to the tiles beside him while the
    ///       scene tint drops from 128 to 32 over 32 ticks; MERGE.STR starts on arrival.
    ///  5    16 ticks, then six linked lights rise around Koh (create_and_link_six_offset_objects).
    ///  6    32 ticks, then six light trails fly from Koh to each familiar
    ///       (create_six_offset_effects) and the halo ellipse is drawn.
    ///  7    60 ticks with both familiars drawn untinted (process_adjacent_objects_with_gray_nodes).
    ///  8    8 ticks, then "A was fused with B.", spiralling lights on both familiars
    ///       (create_linked_objects) and a fading light in front of Koh (create_positioned_graphic_object).
    ///  9    38 ticks halving both familiars' colour, then "W absorbed L." plus the talent list,
    ///       merge_creature_abilities_and_state, and the survivor reappears in front of Koh with the
    ///       angular burst (create_four_groups_of_angular_effects).
    ///  10   64 + 32 ticks.
    ///  11   32 ticks restoring the tint while the survivor walks back to its own tile.
    /// The status windows show both familiars during the ritual and the survivor afterwards.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const float FusionTick = 1f / 30f;
        private const int FusionGlowLayer = 29;
        private const float FusionDarkness = 0.75f; // retail tint 32 of 128
        private static readonly Color FusionLightColor = new(0.55f, 0.85f, 1f, 1f);

        private sealed class FusionStatusWindow
        {
            public string Name;
            public ActorRecord Actor;
        }

        private readonly List<FusionStatusWindow> _fusionWindows = new();
        private string _fusionTalents;
        private Texture2D _fusionGlowTexture;
        private Sprite _fusionGlowSprite;
        private Material _fusionGlowMaterial;
        private WaitForSeconds _fusionTickWait;

        private IEnumerator PresentFamiliarFusion(FusionPreview preview)
        {
            var redView = _familiar;
            var blueView = _blueCollarAlly?.View;
            if (!redView || !blueView || !_player || preview == null) yield break;

            string redName = FamiliarDisplayName;
            string blueName = _blueCollarFamiliar != null ? _blueCollarFamiliar.Name : blueView.name;
            string winnerName = preview.BlueSurvives ? blueName : redName;
            string loserName = preview.BlueSurvives ? redName : blueName;
            var winnerView = preview.BlueSurvives ? blueView : redView;
            var winnerRecord = preview.BlueSurvives ? preview.Blue : preview.Red;

            Vector3 redStart = redView.transform.position, blueStart = blueView.transform.position;
            Vector2Int redCell = _familiarCell, blueCell = _blueCollarAlly.Cell;
            Vector3 winnerStart = preview.BlueSurvives ? blueStart : redStart;
            Vector2Int winnerCell = preview.BlueSurvives ? blueCell : redCell;

            var camera = _gameCamera ? _gameCamera : Camera.main;
            Vector3 right = camera ? FlatDirection(camera.transform.right, Vector3.right) : Vector3.right;
            Vector3 toCamera = camera ? FlatDirection(-camera.transform.forward, Vector3.back) : Vector3.back;
            Vector2Int kohCell = _playerCell;
            Vector3 kohFeet = _player.transform.position;
            Vector3 redTarget = FusionTile(kohCell, -right, redStart, redCell);
            Vector3 blueTarget = FusionTile(kohCell, right, blueStart, blueCell);
            Vector3 front = FusionTile(kohCell, toCamera, winnerStart, winnerCell);

            var redSprites = redView.GetComponentsInChildren<SpriteRenderer>(true);
            var blueSprites = blueView.GetComponentsInChildren<SpriteRenderer>(true);
            var redColors = Array.ConvertAll(redSprites, sprite => sprite.color);
            var blueColors = Array.ConvertAll(blueSprites, sprite => sprite.color);
            var layers = new List<(GameObject Visual, int Layer)>();
            var fx = new List<GameObject>();
            string musicCue = _playingMusicCue;
            int cameraMask = camera ? camera.cullingMask : 0;
            GameObject darkness = null;
            Camera glowCamera = null;
            bool applied = false, musicPaused = false;

            _fusionTickWait ??= new WaitForSeconds(FusionTick);
            _fusionPlaying = true;
            _fusionTalents = null;
            _fusionWindows.Clear();
            _fusionWindows.Add(new FusionStatusWindow { Name = redName, Actor = preview.Red });
            _fusionWindows.Add(new FusionStatusWindow { Name = blueName, Actor = preview.Blue });

            bool Alive() => applied
                ? winnerView && _familiar == winnerView
                : redView && blueView && _familiar == redView && _blueCollarAlly?.View == blueView;

            try
            {
                EnsureFusionGlowAssets();
                darkness = CreateFusionDarkness(camera);
                glowCamera = CreateFusionGlowCamera(camera);
                if (camera && glowCamera) camera.cullingMask = cameraMask & ~(1 << FusionGlowLayer);

                // States 1-4: Koh faces the camera; the familiars take the tiles beside him.
                if (_kohSprite) _kohSprite.SetFacing(new Vector2(toCamera.x, toCamera.z));
                for (int tick = 1; tick <= 32; tick++)
                {
                    if (!Alive()) yield break;
                    float t = tick / 32f;
                    SetFusionDarkness(darkness, FusionDarkness * t);
                    redView.transform.position = Vector3.Lerp(redStart, redTarget, t);
                    blueView.transform.position = Vector3.Lerp(blueStart, blueTarget, t);
                    PresentFusionFacing(redView, redTarget - redStart, tick < 32);
                    PresentFusionFacing(blueView, blueTarget - blueStart, tick < 32);
                    yield return _fusionTickWait;
                }
                PresentFusionFacing(redView, toCamera, false);
                PresentFusionFacing(blueView, toCamera, false);

                // MERGE.STR, file 1/channel 0, sectors 0 through 0x52f.
                var cue = AssetMode.Load<AudioClip>("Sfx/Retail/Fusion");
                SetAreaMusicPaused(true);
                musicPaused = true;
                if (cue) PlayRetailClip(cue, 0.75f);
                else PlayRetailSound(RetailSfx.Restore);

                // State 5: six lights gather around Koh.
                for (int tick = 0; tick < 16; tick++) { if (!Alive()) yield break; yield return _fusionTickWait; }
                bool ringActive = true;
                StartCoroutine(FusionKohRing(kohFeet, fx, () => ringActive && _fusionPlaying));
                for (int tick = 0; tick < 32; tick++) { if (!Alive()) yield break; yield return _fusionTickWait; }

                // State 6: the lights stream from Koh into both familiars; the halo ellipse.
                ringActive = false;
                Vector3 chest = kohFeet + Vector3.up * 0.75f;
                StartCoroutine(FusionLightTrails(chest, redTarget + Vector3.up * 0.45f, fx, 0f));
                StartCoroutine(FusionLightTrails(chest, blueTarget + Vector3.up * 0.45f, fx, 180f));
                StartCoroutine(FusionHalo(kohFeet + Vector3.up * 0.35f, fx));
                for (int tick = 0; tick < 60; tick++) { if (!Alive()) yield break; yield return _fusionTickWait; }

                // State 7: the familiars shine untinted against the darkened floor.
                MoveToFusionGlowLayer(redView, layers);
                MoveToFusionGlowLayer(blueView, layers);
                for (int tick = 0; tick < 8; tick++) { if (!Alive()) yield break; yield return _fusionTickWait; }

                // State 8.
                _message = T($"{redName} was fused with {blueName}.", $"{redName} foi fundido com {blueName}.");
                StartCoroutine(FusionSpiral(redTarget, fx));
                StartCoroutine(FusionSpiral(blueTarget, fx));
                var frontLight = SpawnFusionGlow(front + Vector3.up * 0.45f, 1.1f, FusionLightColor, fx);

                // State 9: both familiars fade out (each colour channel halves every tick).
                float shade = 1f;
                for (int tick = 0; tick < 38; tick++)
                {
                    if (!Alive()) yield break;
                    shade -= shade * 0.5f;
                    if (shade < 5f / 255f) shade = 0f;
                    TintFusionSprites(redSprites, redColors, shade, 1f);
                    TintFusionSprites(blueSprites, blueColors, shade, 1f);
                    if (frontLight)
                    {
                        float half = Mathf.Lerp(1f, 0.5f, tick / 37f);
                        frontLight.color = new Color(FusionLightColor.r, FusionLightColor.g, FusionLightColor.b, half);
                    }
                    yield return _fusionTickWait;
                }

                ApplyFamiliarFusion(preview, redView, blueView);
                applied = true;
                if (frontLight) Destroy(frontLight.gameObject);
                var winnerSprites = winnerView.GetComponentsInChildren<SpriteRenderer>(true);
                var winnerColors = preview.BlueSurvives ? blueColors : redColors;
                if (winnerSprites.Length == winnerColors.Length) TintFusionSprites(winnerSprites, winnerColors, 1f, 1f);
                else foreach (var sprite in winnerSprites) if (sprite) sprite.color = Color.white;
                winnerView.transform.position = front;
                PresentFusionFacing(winnerView, toCamera, false);
                string talents = DescribeTalents(winnerRecord);
                bool hasTalents = winnerRecord.StatusFlags != 0;
                _message = T($"{winnerName} absorbed {loserName}.", $"{winnerName} absorveu {loserName}.") +
                    (hasTalents ? "\n" + talents : string.Empty);
                _fusionWindows.Clear();
                _fusionWindows.Add(new FusionStatusWindow { Name = winnerName, Actor = winnerRecord });
                _fusionTalents = hasTalents ? talents : null;
                StartCoroutine(FusionBurst(front + Vector3.up * 0.1f, fx));

                // State 10.
                for (int tick = 0; tick < 96; tick++) { if (!Alive()) yield break; yield return _fusionTickWait; }

                // State 11: the tint returns while the survivor walks back to its own tile.
                for (int tick = 1; tick <= 32; tick++)
                {
                    if (!Alive()) yield break;
                    float t = tick / 32f;
                    SetFusionDarkness(darkness, FusionDarkness * (1f - t));
                    winnerView.transform.position = Vector3.Lerp(front, winnerStart, t);
                    PresentFusionFacing(winnerView, winnerStart - front, tick < 32);
                    yield return _fusionTickWait;
                }
            }
            finally
            {
                TintFusionSprites(redSprites, redColors, 1f, 1f);
                TintFusionSprites(blueSprites, blueColors, 1f, 1f);
                if (redView) redView.transform.position = redStart;
                if (blueView) blueView.transform.position = blueStart;
                foreach (var (visual, layer) in layers)
                    if (visual) visual.layer = layer;
                foreach (var effect in fx)
                    if (effect) Destroy(effect);
                if (camera) camera.cullingMask = cameraMask;
                if (glowCamera) Destroy(glowCamera.gameObject);
                if (darkness)
                {
                    var material = darkness.GetComponent<MeshRenderer>()?.sharedMaterial;
                    if (material) Destroy(material);
                    Destroy(darkness);
                }
                if (musicPaused && musicCue == _playingMusicCue && !_levelUpMusicActive) SetAreaMusicPaused(false);
                _fusionWindows.Clear();
                _fusionTalents = null;
                _fusionPlaying = false;
            }
            TryStartFloorCollapse();
        }

        private void DrawFusionStatusWindows(float width)
        {
            if (!_fusionPlaying || _fusionWindows.Count == 0 || _area != Area.Tower) return;
            const float windowWidth = 288f, windowHeight = 212f;
            if (_fusionWindows.Count == 1)
            {
                var window = _fusionWindows[0];
                float height = windowHeight + (_fusionTalents != null ? 44f : 0f);
                var panel = new Rect((width - windowWidth) * 0.5f, 16f, windowWidth, height);
                DrawFusionStatusWindow(panel, window);
                if (_fusionTalents != null)
                    DrawHudText(new Rect(panel.x + 10f, panel.y + windowHeight - 4f, panel.width - 20f, 44f),
                        T("Talents: ", "Talentos: ") + _fusionTalents, _messageStyle);
                return;
            }
            DrawFusionStatusWindow(new Rect(16f, 16f, windowWidth, windowHeight), _fusionWindows[0]);
            DrawFusionStatusWindow(new Rect(width - windowWidth - 16f, 16f, windowWidth, windowHeight), _fusionWindows[1]);
        }

        private void DrawFusionStatusWindow(Rect panel, FusionStatusWindow window)
        {
            if (window?.Actor == null) return;
            DrawDialogueFrame(panel);
            var actor = window.Actor;
            DrawFamiliarStatus(panel, window.Name, actor, actor.Hp, actor.MaxHp, actor.CurrentAttack, actor.CurrentDefense);
        }

        private static Vector3 FlatDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback;
        }

        // The retail sequence picks tiles with dir_step_x/dir_step_y, so offsets snap to the eight directions.
        private static Vector2Int StepTowards(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.z, direction.x);
            int octant = Mathf.RoundToInt(angle / (Mathf.PI * 0.25f));
            float snapped = octant * Mathf.PI * 0.25f;
            return new Vector2Int(Mathf.RoundToInt(Mathf.Cos(snapped)), Mathf.RoundToInt(Mathf.Sin(snapped)));
        }

        private Vector3 FusionTile(Vector2Int kohCell, Vector3 direction, Vector3 start, Vector2Int startCell)
        {
            var cell = kohCell + StepTowards(direction);
            var position = CellPosition(cell);
            position.y = start.y + (position.y - CellPosition(startCell).y);
            return position;
        }

        private static void PresentFusionFacing(GameObject view, Vector3 direction, bool moving)
        {
            if (!view || !view.TryGetComponent<ActorLocomotionView>(out var locomotion)) return;
            direction.y = 0f;
            locomotion.Present(direction, moving ? Time.time * 2f % 1f : 0f, moving);
        }

        private static void TintFusionSprites(SpriteRenderer[] sprites, Color[] colors, float brightness, float alpha)
        {
            for (int i = 0; i < sprites.Length && i < colors.Length; i++)
                if (sprites[i]) sprites[i].color = colors[i] * new Color(brightness, brightness, brightness, alpha);
        }

        private static void MoveToFusionGlowLayer(GameObject view, List<(GameObject Visual, int Layer)> layers)
        {
            if (!view) return;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                var visual = renderer.gameObject;
                if (visual.layer == FusionGlowLayer) continue;
                layers.Add((visual, visual.layer));
                visual.layer = FusionGlowLayer;
            }
        }

        private static GameObject CreateFusionDarkness(Camera camera)
        {
            if (!camera) return null;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) return null;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Fusion darkness";
            if (quad.TryGetComponent<Collider>(out var collider)) Destroy(collider);
            quad.transform.SetParent(camera.transform, false);
            float distance = camera.nearClipPlane + 0.02f;
            float height = camera.orthographic
                ? camera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            quad.transform.localPosition = new Vector3(0f, 0f, distance);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(height * camera.aspect * 1.25f, height * 1.25f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Drawn after everything the main camera renders, like the retail global tint.
            renderer.sharedMaterial = new Material(shader) { color = new Color(0f, 0f, 0f, 0f), renderQueue = 4000 };
            return quad;
        }

        private static void SetFusionDarkness(GameObject darkness, float alpha)
        {
            if (!darkness) return;
            var material = darkness.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (material) material.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }

        // Draws the familiars and the lights over the darkened scene: the retail sequence renders
        // them untinted while the rest of the floor keeps the global tint of 32.
        private static Camera CreateFusionGlowCamera(Camera camera)
        {
            if (!camera) return null;
            var holder = new GameObject("Fusion glow camera");
            holder.transform.SetParent(camera.transform, false);
            var glow = holder.AddComponent<Camera>();
            glow.CopyFrom(camera);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            glow.depth = camera.depth + 1f;
            glow.clearFlags = CameraClearFlags.Depth;
            glow.cullingMask = 1 << FusionGlowLayer;
            return glow;
        }

        private void EnsureFusionGlowAssets()
        {
            if (!_fusionGlowTexture)
            {
                const int size = 32;
                _fusionGlowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Fusion glow",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    byte alpha = (byte)Mathf.RoundToInt(255f * falloff * falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                _fusionGlowTexture.SetPixels32(pixels);
                _fusionGlowTexture.Apply(false, true);
                _fusionGlowSprite = Sprite.Create(_fusionGlowTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            if (!_fusionGlowMaterial)
            {
                var shader = AssetMode.Load<Shader>("Shaders/FusionGlow") ?? Shader.Find("AzureDreams/FusionGlow") ??
                    Shader.Find("Sprites/Default");
                if (shader) _fusionGlowMaterial = new Material(shader) { name = "Fusion glow" };
            }
        }

        private SpriteRenderer SpawnFusionGlow(Vector3 position, float size, Color color, List<GameObject> fx)
        {
            EnsureFusionGlowAssets();
            var glow = new GameObject("Fusion light") { layer = FusionGlowLayer };
            glow.transform.SetParent(transform, true);
            glow.transform.position = position;
            glow.transform.localScale = Vector3.one * size;
            var sprite = glow.AddComponent<SpriteRenderer>();
            sprite.sprite = _fusionGlowSprite;
            if (_fusionGlowMaterial) sprite.sharedMaterial = _fusionGlowMaterial;
            sprite.color = color;
            glow.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            fx.Add(glow);
            return sprite;
        }

        private static Color FusionLight(float alpha) =>
            new(FusionLightColor.r, FusionLightColor.g, FusionLightColor.b, Mathf.Clamp01(alpha));

        // create_and_link_six_offset_objects: six lights circling Koh and rising.
        private IEnumerator FusionKohRing(Vector3 feet, List<GameObject> fx, Func<bool> active)
        {
            var orbs = new SpriteRenderer[6];
            for (int i = 0; i < orbs.Length; i++) orbs[i] = SpawnFusionGlow(feet, 0.45f, FusionLight(0f), fx);
            for (int tick = 0; active(); tick++)
            {
                float rise = Mathf.Min(1f, tick / 24f);
                for (int i = 0; i < orbs.Length; i++)
                {
                    if (!orbs[i]) continue;
                    float angle = (i * 60f + tick * 12f) * Mathf.Deg2Rad;
                    float radius = Mathf.Lerp(0.2f, 0.6f, rise);
                    orbs[i].transform.position = feet + new Vector3(Mathf.Cos(angle) * radius, 0.1f + rise * 0.75f, Mathf.Sin(angle) * radius);
                    orbs[i].color = FusionLight(rise * (0.7f + 0.3f * Mathf.Sin(tick * 0.5f + i)));
                }
                yield return _fusionTickWait;
            }
            foreach (var orb in orbs)
                if (orb) Destroy(orb.gameObject);
        }

        // create_six_offset_effects: six lights with eight-point position histories flying to a familiar.
        private IEnumerator FusionLightTrails(Vector3 from, Vector3 to, List<GameObject> fx, float phase)
        {
            const int trailLength = 8, flight = 24, stagger = 3;
            var heads = new SpriteRenderer[6][];
            var histories = new Vector3[6][];
            for (int i = 0; i < heads.Length; i++)
            {
                heads[i] = new SpriteRenderer[trailLength];
                histories[i] = new Vector3[trailLength];
                for (int j = 0; j < trailLength; j++)
                {
                    heads[i][j] = SpawnFusionGlow(from, Mathf.Lerp(0.42f, 0.12f, j / (float)(trailLength - 1)), FusionLight(0f), fx);
                    histories[i][j] = from;
                }
            }
            Vector3 across = Vector3.Cross(Vector3.up, (to - from).normalized);
            int total = flight + stagger * 5 + trailLength + 8;
            for (int tick = 0; tick < total; tick++)
            {
                for (int i = 0; i < heads.Length; i++)
                {
                    float s = Mathf.Clamp01((tick - i * stagger) / (float)flight);
                    float swirl = (phase + i * 60f) * Mathf.Deg2Rad + s * Mathf.PI * 1.5f;
                    float spread = Mathf.Sin(s * Mathf.PI) * 0.7f;
                    var head = Vector3.Lerp(from, to, s) + (across * Mathf.Cos(swirl) + Vector3.up * Mathf.Sin(swirl)) * spread;
                    for (int j = trailLength - 1; j > 0; j--) histories[i][j] = histories[i][j - 1];
                    histories[i][0] = head;
                    float fade = s >= 1f ? Mathf.Clamp01(1f - (tick - i * stagger - flight) / 8f) : (tick >= i * stagger ? 1f : 0f);
                    for (int j = 0; j < trailLength; j++)
                    {
                        if (!heads[i][j]) continue;
                        heads[i][j].transform.position = histories[i][j];
                        heads[i][j].color = FusionLight(fade * (1f - j / (float)trailLength));
                    }
                }
                yield return _fusionTickWait;
            }
            foreach (var trail in heads)
            foreach (var sprite in trail)
                if (sprite) Destroy(sprite.gameObject);
        }

        // draw_textured_ellipse: the halo that opens around Koh while the lights travel.
        private IEnumerator FusionHalo(Vector3 center, List<GameObject> fx)
        {
            var halo = SpawnFusionGlow(center, 0.1f, FusionLight(0f), fx);
            for (int tick = 0; tick < 60 && halo; tick++)
            {
                float t = tick / 59f;
                halo.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 3.2f, Mathf.Sqrt(t));
                halo.color = FusionLight(Mathf.Sin(t * Mathf.PI) * 0.65f);
                yield return _fusionTickWait;
            }
            if (halo) Destroy(halo.gameObject);
        }

        // create_linked_objects: six lights spiralling around a familiar as it dissolves.
        private IEnumerator FusionSpiral(Vector3 feet, List<GameObject> fx)
        {
            var orbs = new SpriteRenderer[6];
            for (int i = 0; i < orbs.Length; i++) orbs[i] = SpawnFusionGlow(feet, 0.32f, FusionLight(0f), fx);
            for (int tick = 0; tick < 46; tick++)
            {
                float t = tick / 45f;
                for (int i = 0; i < orbs.Length; i++)
                {
                    if (!orbs[i]) continue;
                    float angle = (i * 60f + tick * 18f) * Mathf.Deg2Rad;
                    float radius = Mathf.Lerp(0.55f, 0.1f, t);
                    orbs[i].transform.position = feet + new Vector3(Mathf.Cos(angle) * radius, 0.15f + t * 1.1f, Mathf.Sin(angle) * radius);
                    orbs[i].color = FusionLight((1f - t) * 0.9f + 0.1f);
                }
                yield return _fusionTickWait;
            }
            foreach (var orb in orbs)
                if (orb) Destroy(orb.gameObject);
        }

        // create_four_groups_of_angular_effects: four bursts of sparks around the survivor.
        private IEnumerator FusionBurst(Vector3 center, List<GameObject> fx)
        {
            const int groups = 4, perGroup = 6, life = 40;
            var sparks = new SpriteRenderer[groups * perGroup];
            var velocities = new Vector3[sparks.Length];
            for (int g = 0; g < groups; g++)
            for (int k = 0; k < perGroup; k++)
            {
                int index = g * perGroup + k;
                float angle = (g * 90f + 45f + (k - (perGroup - 1) * 0.5f) * 12f) * Mathf.Deg2Rad;
                velocities[index] = new Vector3(Mathf.Cos(angle), 0.35f + 0.08f * k, Mathf.Sin(angle)) * (0.05f + 0.012f * k);
                sparks[index] = SpawnFusionGlow(center, 0.28f, FusionLight(1f), fx);
            }
            var flash = SpawnFusionGlow(center + Vector3.up * 0.4f, 2.2f, FusionLight(0.9f), fx);
            for (int tick = 0; tick < life; tick++)
            {
                float t = tick / (float)(life - 1);
                for (int i = 0; i < sparks.Length; i++)
                {
                    if (!sparks[i]) continue;
                    sparks[i].transform.position += velocities[i];
                    velocities[i].y -= 0.004f;
                    sparks[i].color = FusionLight(1f - t);
                }
                if (flash) flash.color = FusionLight(Mathf.Clamp01(0.9f - t * 3f));
                yield return _fusionTickWait;
            }
            foreach (var spark in sparks)
                if (spark) Destroy(spark.gameObject);
            if (flash) Destroy(flash.gameObject);
        }

        // Monster Hut fusion: only the MERGE.STR cue and a result message.
        private void PlayHutFusionCue()
        {
            var cue = AssetMode.Load<AudioClip>("Sfx/Retail/Fusion");
            if (!cue)
            {
                PlayRetailSound(RetailSfx.Restore);
                return;
            }
            StartCoroutine(PlayHutFusionCue(cue));
        }

        private IEnumerator PlayHutFusionCue(AudioClip cue)
        {
            string musicCue = _playingMusicCue;
            SetAreaMusicPaused(true);
            PlayRetailClip(cue, 0.75f);
            yield return new WaitForSeconds(cue.length);
            if (musicCue == _playingMusicCue && !_levelUpMusicActive) SetAreaMusicPaused(false);
        }
    }
}
