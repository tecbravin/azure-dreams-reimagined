using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AzureDreamsReimagined
{
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MonsterTurnSpriteView : MonoBehaviour
    {
        internal float PlaybackSpeed { get; set; } = 1f;
        internal bool GroundedIdle { get; set; }
        private float AnimationTime => Time.time * PlaybackSpeed;
        private const int CellSize = 64;
        private const int IndexedCellSize = 96;
        private const int Rows = 5;
        private const int WalkStart = 2;
        private const int AttackStart = 7;
        private const int CastStart = 12;
        private const int SleepStart = 14;
        private const int IndexedSleepStart = 18;
        private const int IndexedHitColumn = 20;
        private const int CommunityFramesPerSecond = 8;
        private ActorLocomotionView _locomotion;
        [SerializeField] private Texture2D _atlas;
        [SerializeField] private int _indexedSpecies;
        [SerializeField, Min(0f)] private float _attackLunge;
        private Texture2D _frameAtlas;
        private bool _usingCommunityAtlas;
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private readonly Dictionary<string, Sprite[][]> _communityFrames = new(StringComparer.Ordinal);
        private bool _communityLoadStarted;
        private int _columns;
        private bool UsesRetailLayout => _atlas &&
            (_atlas.width == IndexedHitColumn * IndexedCellSize || _atlas.width == (IndexedHitColumn + 1) * IndexedCellSize);
        private int FrameSize => UsesRetailLayout ? IndexedCellSize : CellSize;
        private int SleepColumn => UsesRetailLayout ? IndexedSleepStart : SleepStart;
        private Vector2 _direction = Vector2.down;
        private bool _walking;
        private float _walkProgress;
        private bool _casting;
        private float _progress = 1f;
        private Vector3 _previousLunge;
        private bool _sleeping, _exhausted, _tinted, _defeated;
        private Color _awakeColor;
        private float _sleepStartedAt;
        private float _defeatStartedAt;

        [Serializable]
        private sealed class CommunityManifest
        {
            public CommunityMonster[] monsters;
        }

        [Serializable]
        private sealed class CommunityMonster
        {
            public int species;
            public int cell_size;
            public CommunityAction[] actions;
        }

        [Serializable]
        private sealed class CommunityAction
        {
            public string name;
            public string file;
            public int columns;
            public int[] frame_counts;
        }

        private static readonly Dictionary<string, Texture2D> CommunitySourceAtlases = new(StringComparer.Ordinal);

        private void Awake()
        {
            int communitySpecies = _indexedSpecies != 0 ? _indexedSpecies : SpeciesFromName(transform.root.name);
            if (communitySpecies == 22 && AssetMode.Load<Texture2D>("Tower/TrollUnarmedActions") is { } unarmed)
            {
                UseEquipmentAtlas(unarmed);
                return;
            }
            // Cyclone and Noise have captured sheets whose layout and artwork are
            // authoritative. The community dumps for them are incomplete (Cyclone's
            // body only; Noise drops its flute in most walk/attack cels), so never
            // replace those sheets with the generic fallback atlas.
            bool keepRetailCycloneAtlas = KeepsAuthoredAtlas(communitySpecies) && _atlas;
            if (communitySpecies > 0 && !keepRetailCycloneAtlas)
            {
                var communityAtlas = AssetMode.Load<Texture2D>($"Monsters/Community/monster_{communitySpecies:x2}");
                if (communityAtlas)
                {
                    _atlas = communityAtlas;
                    _indexedSpecies = communitySpecies;
                    _usingCommunityAtlas = true;
                }
            }
            if (!_atlas || _atlas.height != Rows * FrameSize || _atlas.width % FrameSize != 0) return;
            _columns = _atlas.width / FrameSize;
            if (!UsesRetailLayout && _columns != CastStart && _columns != SleepStart && _columns != SleepStart + 2) return;
            _renderer = GetComponent<SpriteRenderer>();
            var position = transform.localPosition;
            position.y = -0.55f;
            transform.localPosition = position;
        }

        private const int CycloneSpecies = 29;
        private const int NoiseSpecies = 23;
        private static bool KeepsAuthoredAtlas(int species) => species is CycloneSpecies or NoiseSpecies or 34;

        private static int SpeciesFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int clone = name.IndexOf('(');
            if (clone >= 0) name = name.Substring(0, clone).Trim();
            return name switch
            {
                "Hikewne" => 1, "Kewne" => 2, "Dragon" => 3, "Kid" => 4,
                "Ifrit" => 5, "Flame" => 6, "Grineut" => 7, "Griffon" => 8,
                "Saber" => 9, "Snowman" => 10, "Ashra" => 11, "Arachne" => 12,
                "Battnel" => 13, "Nyuel" => 14, "Death" => 15, "Clown" => 16,
                "Univern" => 17, "Unicorn" => 18, "Metal" => 19, "Block" => 20,
                "Pulunpa" => 21, "Troll" => 22, "Noise" => 23, "U_Boat" => 24,
                "Baloon" => 25, "Dreamin" => 26, "Blume" => 27, "Volcano" => 28,
                "Cyclone" => 29, "Manoeva" => 30, "Barong" => 31, "Picket" => 32,
                "Kraken" => 33, "Weadog" => 34, "Stealth" => 35, "Viper" => 36,
                "Naplass" => 37, "Zu" => 38, "Mandara" => 39, "Killer" => 40,
                "Garuda" => 41, "Glacier" => 42, "Tyrant" => 43, "Golem" => 44,
                "Maximum" => 45, _ => 0
            };
        }

        private void Start()
        {
            // SpawnActor can assign the indexed fallback before Start runs. Do
            // not let that temporary sheet prevent the full source sequences
            // from being loaded.
            if (_usingCommunityAtlas && !_communityLoadStarted)
                StartCommunityActionLoad();
            if (_frames != null) return;
            if (_indexedSpecies != 0)
            {
                SetPalette(_indexedSpecies, MonsterPaletteVariant.Enemy);
            }
            else BuildFrames(_atlas);
        }

        internal void SetActionAtlas(Texture2D atlas)
        {
            if (atlas && atlas.name.StartsWith("Troll", StringComparison.Ordinal))
            { UseEquipmentAtlas(atlas); return; }
            if (!_usingCommunityAtlas && _atlas && atlas.width == _atlas.width && atlas.height == _atlas.height) _atlas = atlas;
        }

        internal void UseEquipmentAtlas(Texture2D atlas)
        {
            if (!atlas || atlas.height != 5 * 64 || atlas.width != 16 * 64) return;
            StopAllCoroutines();
            ClearCommunityFrames();
            _communitySourceActions.Clear();
            _communityLoadStarted = true;
            _usingCommunityAtlas = false;
            _indexedSpecies = 0;
            _atlas = atlas;
            _columns = 16;
            _renderer = GetComponent<SpriteRenderer>();
            BuildFrames(atlas);
        }

        internal void UseRetailAtlas(int species, Texture2D atlas)
        {
            if (!atlas || atlas.width != 20 * IndexedCellSize || atlas.height != Rows * IndexedCellSize) return;
            StopAllCoroutines();
            ClearCommunityFrames();
            _communitySourceActions.Clear();
            _usingCommunityAtlas = false;
            _atlas = atlas;
            _indexedSpecies = species;
            _columns = atlas.width / IndexedCellSize;
            _renderer = GetComponent<SpriteRenderer>();
            SetPalette(species, MonsterPaletteVariant.Enemy);
        }

        internal void UseCommunityAtlas(int species, Texture2D atlas)
        {
            // Do not overwrite Cyclone's extracted retail action atlas. It is
            // deliberately separate from the partial community frame export.
            if (KeepsAuthoredAtlas(species) && _atlas &&
                ((_atlas.width == 16 * CellSize && _atlas.height == Rows * CellSize) ||
                 (species == 34 && _atlas.width == 20 * IndexedCellSize && _atlas.height == Rows * IndexedCellSize)))
            {
                _indexedSpecies = species == 34 ? species : 0;
                _usingCommunityAtlas = false;
                return;
            }
            _atlas = atlas;
            _indexedSpecies = species;
            _usingCommunityAtlas = true;
            _renderer = GetComponent<SpriteRenderer>();
            _columns = atlas.width / IndexedCellSize;
            var position = transform.localPosition;
            position.y = -0.55f;
            transform.localPosition = position;
            BuildFrames(MonsterPalettes.Recolor(atlas, species, MonsterPaletteVariant.Enemy, indexed: true));
            if (isActiveAndEnabled && !_communityLoadStarted) StartCommunityActionLoad();
        }

        internal void SetPalette(int species, MonsterPaletteVariant variant)
        {
            if (_communityFrames.Count > 0)
            {
                BuildCommunityFrames(variant);
                return;
            }
            if (_atlas)
                BuildFrames(MonsterPalettes.Recolor(_atlas, _indexedSpecies == 0 ? species : _indexedSpecies, variant,
                    indexed: _indexedSpecies != 0));
        }

        private void StartCommunityActionLoad()
        {
            if (_communityLoadStarted) return;
            _communityLoadStarted = true;
            StartCoroutine(LoadCommunityActions());
        }

        private IEnumerator LoadCommunityActions()
        {
            const string relativeRoot = "Monsters/Community/";
            using var manifestRequest = UnityWebRequest.Get(StreamingUrl(relativeRoot + "manifest.json"));
            yield return manifestRequest.SendWebRequest();
            if (manifestRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Could not load community monster animation manifest: {manifestRequest.error}", this);
                yield break;
            }

            CommunityManifest manifest;
            try { manifest = JsonUtility.FromJson<CommunityManifest>(manifestRequest.downloadHandler.text); }
            catch (Exception exception)
            {
                Debug.LogWarning($"Community monster animation manifest is invalid: {exception.Message}", this);
                yield break;
            }
            CommunityMonster monster = null;
            if (manifest?.monsters != null)
                foreach (var entry in manifest.monsters)
                    if (entry != null && entry.species == _indexedSpecies) { monster = entry; break; }
            if (monster?.actions == null || monster.cell_size != IndexedCellSize) yield break;

            var loaded = new List<(CommunityAction action, Texture2D texture)>();
            foreach (var action in monster.actions)
            {
                if (action == null || string.IsNullOrEmpty(action.name) || string.IsNullOrEmpty(action.file)) continue;
                string key = _indexedSpecies + ":" + action.file;
                if (!CommunitySourceAtlases.TryGetValue(key, out var texture) || !texture)
                {
                    // Palette conversion reads the indexed red channel, so these
                    // streamed source textures must remain CPU-readable.
                    using var request = UnityWebRequestTexture.GetTexture(StreamingUrl(relativeRoot + action.file), false);
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"Could not load {action.file}: {request.error}", this);
                        continue;
                    }
                    texture = DownloadHandlerTexture.GetContent(request);
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    CommunitySourceAtlases[key] = texture;
                }
                loaded.Add((action, texture));
            }

            if (loaded.Count == 0) yield break;
            _communitySourceActions.Clear();
            foreach (var entry in loaded) _communitySourceActions[entry.action.name] = (entry.action, entry.texture);
            BuildCommunityFrames(MonsterPaletteVariant.Enemy);
        }

        private readonly Dictionary<string, (CommunityAction action, Texture2D texture)> _communitySourceActions = new(StringComparer.Ordinal);

        private static string StreamingUrl(string relativePath)
        {
            string root = Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/');
            if (root.Contains("://", StringComparison.Ordinal)) return root + "/" + relativePath;
            return new Uri(Path.Combine(root, relativePath)).AbsoluteUri;
        }

        private void BuildCommunityFrames(MonsterPaletteVariant variant)
        {
            ClearCommunityFrames();
            ClearFrames();
            foreach (var entry in _communitySourceActions)
            {
                CommunityAction action = entry.Value.action;
                Texture2D colored = MonsterPalettes.Recolor(entry.Value.texture, _indexedSpecies, variant, indexed: true);
                int availableColumns = Mathf.Min(action.columns, colored.width / IndexedCellSize);
                if (availableColumns <= 0) continue;
                var rows = new Sprite[Rows][];
                for (int row = 0; row < Rows; row++)
                {
                    int count = action.frame_counts != null && row < action.frame_counts.Length
                        ? Mathf.Min(action.frame_counts[row], availableColumns)
                        : availableColumns;
                    if (count <= 0) continue;
                    rows[row] = new Sprite[count];
                    for (int column = 0; column < count; column++)
                        rows[row][column] = Sprite.Create(colored,
                            new Rect(column * IndexedCellSize, (Rows - 1 - row) * IndexedCellSize,
                                IndexedCellSize, IndexedCellSize),
                            new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
                }
                _communityFrames[action.name] = rows;
            }
            if (_communityFrames.TryGetValue("idle", out var idle))
            {
                var first = FirstCommunitySprite(idle);
                if (first) _renderer.sprite = first;
            }
        }

        private static Sprite FirstCommunitySprite(Sprite[][] rows)
        {
            foreach (var row in rows)
                if (row is { Length: > 0 }) return row[0];
            return null;
        }

        private void BuildFrames(Texture2D atlas)
        {
            if (!_renderer || _frameAtlas == atlas) return;
            ClearFrames();
            _frameAtlas = atlas;
            int size = FrameSize;
            // The indexed monster frames have opaque feet at the bottom edge.
            // A 12-pixel pivot buried them below the tower tile after actor scaling.
            int pivotPixels = UsesRetailLayout ? 0 : 4;
            if (GroundedIdle && _atlas.isReadable) pivotPixels = GroundedIdlePivot(size, pivotPixels);
            float pivotY = (float)pivotPixels / size;
            _frames = new Sprite[Rows * _columns];
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < _columns; column++)
                    _frames[row * _columns + column] = Sprite.Create(atlas,
                        new Rect(column * size, (Rows - 1 - row) * size, size, size),
                        new Vector2(0.5f, pivotY), 64f, 0, SpriteMeshType.FullRect);
            _renderer.sprite = _frames[0];
        }

        private int GroundedIdlePivot(int size, int pivotPixels)
        {
            // Read the source alpha, which is shared by all genus palettes.
            // One pivot for all idle poses preserves their relative bobbing.
            for (int y = 0; y < pivotPixels; y++)
                for (int row = 0; row < Rows; row++)
                    for (int x = 0; x < 2 * size; x++)
                        if (_atlas.GetPixel(x, row * size + y).a > 0f) return y;
            return pivotPixels;
        }

        internal void PresentMovement(Vector3 direction, float progress, bool walking)
        {
            _walking = walking;
            _walkProgress = progress;
            if (walking && _progress >= 1f && direction.sqrMagnitude > 0.0001f)
                _direction = new Vector2(direction.x, direction.z);
        }

        internal void Face(Vector2Int direction) => _direction = direction;

        internal void BeginAction(Vector2Int direction, bool casting)
        {
            if (_defeated) return;
            _direction = direction;
            _casting = casting;
            _progress = 0f;
        }

        internal void PresentAction(float progress) => _progress = progress;

        internal Vector2 Facing => _direction;
        internal int DefeatSpecies
        {
            get
            {
                if (_indexedSpecies != 0) return _indexedSpecies;
                int species = transform.parent ? SpeciesFromName(transform.parent.name) : 0;
                return species != 0 ? species : SpeciesFromName(name);
            }
        }

        /// <summary>Plays the extracted defeat sequence before this view is retired.</summary>
        internal float BeginDefeat()
        {
            if (!_communityFrames.TryGetValue("defeated", out var frames)) return 0f;
            int longest = 0;
            foreach (var row in frames)
                if (row != null) longest = Mathf.Max(longest, row.Length);
            if (longest == 0) return 0f;

            _defeated = true;
            _walking = false;
            _casting = false;
            _progress = 1f;
            _defeatStartedAt = AnimationTime;
            // Do not hold up a turn for unusually long source banks, while
            // still letting even a one-pose defeat read on screen.
            return Mathf.Clamp(longest / (float)CommunityFramesPerSecond, 0.18f, 0.55f);
        }

        internal bool PresentStatus(bool sleeping, bool exhausted, bool paralyzed)
        {
            bool hasCommunitySleep = _communityFrames.TryGetValue("sleep", out _);
            bool canPresent = (_frames != null && (!sleeping || _columns > SleepColumn + 1)) ||
                              (_communityFrames.Count > 0 && (!sleeping || hasCommunitySleep));
            _paralyzed = canPresent && paralyzed;
            if (!canPresent) return false;
            if (sleeping && !_sleeping) _sleepStartedAt = AnimationTime;
            _sleeping = sleeping;
            _exhausted = exhausted;
            return true;
        }

        private bool _paralyzed;

        private void LateUpdate()
        {
            if (!_renderer || (_frames == null && _communityFrames.Count == 0)) return;
            var camera = Camera.main;
            float angle = Mathf.Atan2(_direction.x, _direction.y) * Mathf.Rad2Deg;
            if (camera) angle -= camera.transform.eulerAngles.y;
            int octant = Mathf.RoundToInt(angle / 45f) & 7;
            int row = Mathf.Abs(4 - octant);
            // Weadog stores its complete retail banks back-to-front.
            if (!_usingCommunityAtlas && _indexedSpecies == 34) row = 4 - row;
            _renderer.flipX = octant is >= 1 and <= 3;
            float sleepTime = AnimationTime - _sleepStartedAt;
            if ((_exhausted || _paralyzed) && !_tinted)
            {
                _awakeColor = _renderer.color;
                _tinted = true;
            }
            if (_paralyzed)
                _renderer.color = _awakeColor * TowerStatusColors.Paralysis;
            else if (_exhausted)
                _renderer.color = Color.Lerp(_awakeColor, new Color(0.28f, 0.55f, 1f, _awakeColor.a),
                    0.48f + 0.45f * Mathf.Sin(sleepTime * 12f));
            else if (_tinted)
            {
                _renderer.color = _awakeColor;
                _tinted = false;
            }
            if (!_locomotion) _locomotion = GetComponentInParent<ActorLocomotionView>();
            bool hit = _locomotion && _locomotion.HitProgress < 1f;
            if (_communityFrames.Count > 0)
            {
                string action = _defeated ? "defeated" : hit ? "hit" : _sleeping ? "sleep" :
                    _progress < 1f ? (_casting ? "magic" : "attack") : _walking ? "walk" : "idle";
                Sprite[] frames = CommunityFrames(action, row) ?? CommunityFrames("idle", row);
                if (frames is { Length: > 0 })
                    _renderer.sprite = frames[CommunityFrameIndex(action, frames.Length, sleepTime)];
            }
            else
            {
                int column = Mathf.FloorToInt(AnimationTime * 4f) & 1;
                if (hit && UsesRetailLayout && _columns > IndexedHitColumn)
                    column = IndexedHitColumn;
                else if (_sleeping)
                    column = SleepColumn + (Mathf.FloorToInt(sleepTime * 2f) & 1);
                else if (!hit && _progress < 1f)
                    column = _casting && _columns > CastStart
                        ? CastColumn()
                        : AttackStart + Mathf.Clamp((int)(_progress * 5f), 0, 4);
                else if (!hit && _walking)
                    column = WalkStart + Mathf.Clamp((int)(_walkProgress * 5f), 0, 4);
                if (CycloneAttacking)
                {
                    // Retail Cyclone attack (advance_backward_forward_motion): it backs up, then
                    // dashes forward spinning; show the whirl by turning through all facings.
                    int spin = (Mathf.RoundToInt(angle / 45f) + Mathf.FloorToInt(_progress * 16f)) & 7;
                    row = Mathf.Abs(4 - spin);
                    _renderer.flipX = spin is >= 1 and <= 3;
                }
                _renderer.sprite = _frames[row * _columns + column];
            }
            PresentCycloneWhirl();

            if (_attackLunge > 0f || UsesRetailLayout || IsCyclone)
            {
                float distance = hit || _sleeping || _casting || _progress >= 1f ? 0f : _attackLunge * Mathf.Sin(_progress * Mathf.PI);
                if (CycloneAttacking) distance = CycloneDashOffset(_progress);
                Vector3 worldOffset = new Vector3(_direction.x, 0f, _direction.y).normalized * distance;
                // Some retail directions hold one pose. Keep the body alive during those holds.
                if (UsesRetailLayout)
                {
                    if (_sleeping) worldOffset.y = (1f - Mathf.Cos(sleepTime * 4f)) * 0.009f;
                    else if (_casting && _progress < 1f) worldOffset.y = Mathf.Sin(_progress * Mathf.PI) * 0.09f;
                    else if (!_walking && _progress >= 1f && _indexedSpecies == 28)
                        worldOffset.y = (1f - Mathf.Cos(AnimationTime * 4f)) * 0.012f;
                }
                Vector3 localOffset = transform.parent.InverseTransformVector(worldOffset);
                transform.localPosition += localOffset - _previousLunge;
                _previousLunge = localOffset;
            }
        }

        internal bool CycloneView => IsCyclone;
        private int _viewSpecies = -1;
        private bool IsCyclone
        {
            get
            {
                if (_viewSpecies < 0) _viewSpecies = DefeatSpecies;
                return !_usingCommunityAtlas && _viewSpecies == CycloneSpecies;
            }
        }
        private bool CycloneAttacking => IsCyclone && !_casting && !_sleeping && !_defeated && _progress < 1f &&
            !(_locomotion && _locomotion.HitProgress < 1f);

        // Phase 0/1 back up for six ticks; the dash then covers twice that distance and returns.
        private static float CycloneDashOffset(float progress)
        {
            if (progress < 0.3f) return -0.18f * (progress / 0.3f);
            float dash = (progress - 0.3f) / 0.7f;
            return Mathf.Lerp(-0.18f, 0.42f, Mathf.Sin(Mathf.Min(1f, dash * 1.6f) * Mathf.PI * 0.5f)) * (1f - dash * dash);
        }

        private SpriteRenderer[] _whirls;
        private Sprite _whirlSprite;
        private Sprite _whirlSource;

        /// <summary>
        /// The two wind effects create_directional_effect / create_offset_directional_effect spawn at the
        /// dash (sprite entry 0x30): they ride the dash for six ticks and spin through eight palette rows
        /// before vanishing at tick eight.  Drawn as translucent, rapidly turning copies of the body.
        /// </summary>
        private void PresentCycloneWhirl()
        {
            bool active = CycloneAttacking && _progress >= 0.3f;
            if (!active)
            {
                if (_whirls != null) foreach (var whirl in _whirls) if (whirl) whirl.enabled = false;
                return;
            }
            if (_whirls == null)
            {
                _whirls = new SpriteRenderer[2];
                for (int i = 0; i < _whirls.Length; i++)
                {
                    var child = new GameObject("Cyclone whirl " + (i + 1));
                    child.transform.SetParent(transform, false);
                    _whirls[i] = child.AddComponent<SpriteRenderer>();
                    _whirls[i].sharedMaterial = _renderer.sharedMaterial;
                    _whirls[i].sortingOrder = _renderer.sortingOrder + 1;
                }
            }
            var source = _renderer.sprite;
            if (source && source != _whirlSource)
            {
                if (_whirlSprite) Destroy(_whirlSprite);
                _whirlSource = source;
                _whirlSprite = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f),
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            float t = (_progress - 0.3f) / 0.7f;
            float centre = source ? (0.5f - source.pivot.y / source.rect.height) * source.rect.height / source.pixelsPerUnit : 0.3f;
            for (int i = 0; i < _whirls.Length; i++)
            {
                var whirl = _whirls[i];
                whirl.enabled = true;
                whirl.sprite = _whirlSprite;
                whirl.flipX = _renderer.flipX;
                // Eight palette steps of the retail effect: alternate pale wind and green tints.
                int step = (Mathf.FloorToInt(t * 8f) + i * 3) & 7;
                Color tint = step % 2 == 0 ? new Color(0.85f, 1f, 0.9f) : new Color(0.45f, 0.9f, 0.6f);
                tint.a = (t < 0.75f ? 0.55f : 0.55f * (1f - (t - 0.75f) / 0.25f)) * (i == 0 ? 1f : 0.7f);
                whirl.color = tint;
                float scale = 1.05f + 0.25f * t + 0.12f * i;
                whirl.transform.localScale = Vector3.one * scale;
                whirl.transform.localPosition = new Vector3(0f, centre, -0.01f * (i + 1));
                whirl.transform.localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? -1f : 1f) * t * 1080f + i * 45f);
            }
        }

        private int CastColumn()
        {
            if (!UsesRetailLayout) return CastStart + (_progress < 0.23f ? 0 : 1);
            int frames = IndexedSleepStart - CastStart;
            return CastStart + Mathf.Clamp((int)(_progress * frames), 0, frames - 1);
        }

        private Sprite[] CommunityFrames(string action, int row)
        {
            if (!_communityFrames.TryGetValue(action, out var rows)) return null;
            if (row >= 0 && row < rows.Length && rows[row] is { Length: > 0 }) return rows[row];
            foreach (var candidate in rows)
                if (candidate is { Length: > 0 }) return candidate;
            return null;
        }

        private int CommunityFrameIndex(string action, int frameCount, float sleepTime)
        {
            if (frameCount <= 1) return 0;
            if (action is "attack" or "magic")
                return Mathf.Clamp(Mathf.FloorToInt(_progress * frameCount), 0, frameCount - 1);
            if (action == "walk")
                return Mathf.Clamp(Mathf.FloorToInt(_walkProgress * frameCount), 0, frameCount - 1);
            if (action == "defeated")
                return Mathf.Min(Mathf.FloorToInt((AnimationTime - _defeatStartedAt) * CommunityFramesPerSecond),
                    frameCount - 1);
            float time = action == "sleep" ? sleepTime : AnimationTime;
            return Mathf.FloorToInt(time * CommunityFramesPerSecond) % frameCount;
        }

        private void OnDestroy()
        {
            if (_whirlSprite) Destroy(_whirlSprite);
            ClearFrames();
            ClearCommunityFrames();
        }

        private void ClearFrames()
        {
            if (_frames == null) return;
            foreach (var sprite in _frames) Destroy(sprite);
            _frames = null;
            _frameAtlas = null;
        }

        private void ClearCommunityFrames()
        {
            foreach (var action in _communityFrames.Values)
                foreach (var row in action)
                    if (row != null)
                        foreach (var sprite in row)
                            if (sprite) Destroy(sprite);
            _communityFrames.Clear();
        }
    }
}
