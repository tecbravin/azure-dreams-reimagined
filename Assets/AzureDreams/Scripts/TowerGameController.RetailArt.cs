using System.Collections.Generic;
using UnityEngine;
using AzureDreamsReimagined.Rendering;
using CoreItem = AzureDreams.Core.DungeonItemEntry;
using Items = AzureDreams.Core.ItemSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private Texture2D _retailFont;
        [SerializeField] private Sprite[] _groundItemSprites;
        private const float WorldItemScale = 1.8f;
        private const float WorldItemHeight = 0.18f * WorldItemScale;
        private Dictionary<string, Sprite> _itemSpritesByName;
        private const int GroundIconColumns = 16;
        private const int GroundIconRows = 8;
        private const int RedCollarIconSlot = 16;
        private const int BlueCollarIconSlot = 17;
        private static byte[] _groundIconSlots;
        private static Texture2D _groundIconAtlas;
        private static Texture2D _retailHudFrames;
        private GUIStyle _portugueseHpStyle;
        private static readonly Sprite[] GroundIcons = new Sprite[GroundIconColumns * GroundIconRows];
        // Slots in GroundItemMap, ordered by the NTSC icon descriptors.
        private static readonly string[] GroundIconNames =
        {
            null, "herb", "red_fruit", "seed", "ball", "scroll", "crystal", "bell",
            "sand", "gold_coin", "silver_coin", "glasses", "loupe", "sword", "wand", "shield",
            "red_collar", "blue_collar", "pot", "water_medal", "chest", "dumplings", "troll_sword", "troll_hammer",
            "troll_bow_gun", "gift_box", "koh_head", "bronze_coin", "roses", "blue_cape", "meat", "blue_fruit", "duck"
        };

        private Sprite ItemDisplaySprite(CoreItem item) => ItemDisplaySprite(item.Kind, item.Subtype);

        private Sprite ItemDisplaySprite(int kind, int subtype)
        {
            if (kind == Items.Kind.Herb && subtype == ShopMedicinalHerbSubtype) subtype = 1;
            int slot = GroundIconSlot(kind, subtype);
            string name = kind switch
            {
                Items.Kind.Egg => "egg",
                Items.Kind.Trap => "trap",
                _ => slot < GroundIconNames.Length ? GroundIconNames[slot] : null
            };
            var supplied = name == null ? null : SuppliedItemSprite(name);
            return supplied ? supplied : RetailGroundItemSprite(slot);
        }

        private Sprite SuppliedItemSprite(string name)
        {
            if (_itemSpritesByName == null)
            {
                _itemSpritesByName = new Dictionary<string, Sprite>();
                if (_groundItemSprites != null)
                    foreach (var sprite in _groundItemSprites)
                    {
                        if (!sprite) continue;
                        _itemSpritesByName.TryAdd(sprite.name, sprite);
                        if (sprite.texture) _itemSpritesByName.TryAdd(sprite.texture.name, sprite);
                    }
            }
            return _itemSpritesByName.TryGetValue(name, out var itemSprite) ? itemSprite : null;
        }

        private static int GroundIconSlot(int kind, int subtype)
        {
            _groundIconSlots ??= Resources.Load<TextAsset>("Tower/GroundItemMap")?.bytes
                ?? System.Array.Empty<byte>();
            int key = kind * 256 + subtype;
            return key >= 0 && key < _groundIconSlots.Length ? _groundIconSlots[key] : 0;
        }

        private static Sprite RetailGroundItemSprite(int slot)
        {
            if (slot == 0 || slot >= GroundIcons.Length) return null;
            if (GroundIcons[slot]) return GroundIcons[slot];

            if (!_groundIconAtlas) _groundIconAtlas = Resources.Load<Texture2D>("Tower/GroundItemIcons");
            if (!_groundIconAtlas) return null;
            float cellWidth = _groundIconAtlas.width / (float)GroundIconColumns;
            float cellHeight = _groundIconAtlas.height / (float)GroundIconRows;
            var rect = new Rect(slot % GroundIconColumns * cellWidth,
                _groundIconAtlas.height - (slot / GroundIconColumns + 1) * cellHeight, cellWidth, cellHeight);
            return GroundIcons[slot] = Sprite.Create(_groundIconAtlas, rect, new Vector2(0.5f, 0.5f),
                cellWidth, 0, SpriteMeshType.FullRect);
        }

        private GameObject CreateGroundItemView(Vector2Int cell, CoreItem item)
        {
            const float groundEggScale = 1.5f;
            float sizeMultiplier = item.Kind == MonsterEgg.Kind ? groundEggScale : 1f;
            if (item.Kind == MonsterEgg.Kind)
            {
                var egg = CreateEggView(cell, item, WorldItemHeight + TowerGroundY(cell));
                if (egg)
                {
                    egg.transform.localScale *= sizeMultiplier;
                    return egg;
                }
            }
            var sprite = ItemDisplaySprite(item);
            if (!sprite)
            {
                var marker = CreateItemMarker(cell, "Tower Item", PrimitiveType.Sphere);
                marker.transform.localScale *= sizeMultiplier;
                return marker;
            }

            var view = new GameObject($"Tower Item - {item.Kind:X2}:{item.Subtype:X2}");
            view.transform.SetParent(transform, false);
            view.transform.position = new Vector3(cell.x, WorldItemHeight + TowerGroundY(cell), cell.y);
            view.AddComponent<SpriteRenderer>().sprite = sprite;
            view.transform.localScale = Vector3.one * (0.32f * WorldItemScale * sizeMultiplier / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            view.AddComponent<CameraFacingSprite>();
            _generated.Add(view);
            return view;
        }

        private GameObject CreateItemMarker(Vector2Int cell, string name, PrimitiveType shape) =>
            CreateMarker(cell, name, _accentMaterial, shape, 0.28f * WorldItemScale, WorldItemHeight);

        private static bool CanDrawRetailText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char character in text)
                if (character != '\n' && RetailCharacter(character) is < ' ' or > '~') return false;
            return true;
        }

        private static char RetailCharacter(char character)
        {
            return character switch
            {
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '–' or '—' => '-',
                '▸' => '>',
                _ => character
            };
        }

        private void DrawRetailText(Rect rect, string text, GUIStyle style)
        {
            float glyphHeight = Mathf.Max(16f, Mathf.Round((style.fontSize + 3f) / 16f) * 16f);
            float advance = glyphHeight * 0.5f;
            float y = rect.y + (style.wordWrap ? 0f : (rect.height - glyphHeight) * 0.5f);
            float x = rect.x;
            var previous = GUI.color;
            // Use the sheet's authored palettes instead of tinting white pixels.
            GUI.color = Color.white;
            Color requested = style.normal.textColor;
            int palette = requested.g > requested.r * 1.25f ? 4 :
                requested.r > requested.b * 1.25f ? 3 : 0;
            int wordLength = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char character = RetailCharacter(text[i]);
                if (style.wordWrap && (i == 0 || text[i - 1] == ' '))
                {
                    wordLength = 0;
                    while (i + wordLength < text.Length && text[i + wordLength] != ' ' && text[i + wordLength] != '\n')
                        wordLength++;
                    if (x > rect.x && x + wordLength * advance > rect.xMax)
                    {
                        x = rect.x;
                        y += glyphHeight;
                    }
                }
                if (character == '\n' || (style.wordWrap && x + advance > rect.xMax))
                {
                    x = rect.x;
                    y += glyphHeight;
                    if (character == '\n' || character == ' ') continue;
                }
                if (y + glyphHeight > rect.yMax + 1f) break;
                if (character != ' ')
                {
                    int code = character >= ' ' && character <= '~' ? character : '?';
                    int u = palette * 128 + (code & 15) * 8;
                    int v = 80 + (code / 16 - 2) * 16;
                    var uv = new Rect(u / (float)_retailFont.width, 1f - (v + 16f) / _retailFont.height,
                        8f / _retailFont.width, 16f / _retailFont.height);
                    GUI.DrawTextureWithTexCoords(new Rect(x, y, advance, glyphHeight), _retailFont, uv);
                }
                x += advance;
            }
            GUI.color = previous;
        }

        private static bool HasRetailHudFrames
        {
            get
            {
                if (!_retailHudFrames) _retailHudFrames = Resources.Load<Texture2D>("Tower/RetailHudFrames");
                return _retailHudFrames && _retailHudFrames.width == 128 && _retailHudFrames.height == 768;
            }
        }

        private static void DrawFramePart(Rect destination, int band, float x, float y, float width,
            float height, bool flipX = false)
        {
            const float atlasWidth = 128f, atlasHeight = 768f;
            var uv = new Rect((x + (flipX ? width : 0f)) / atlasWidth,
                1f - (band * 128f + y + height) / atlasHeight,
                (flipX ? -width : width) / atlasWidth, height / atlasHeight);
            GUI.DrawTextureWithTexCoords(destination, _retailHudFrames, uv);
        }

        private static void DrawRetailHudPlaque(Rect rect)
        {
            if (!HasRetailHudFrames)
            {
                DrawHudPanel(rect);
                return;
            }
            DrawHudFill(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f),
                new Color(0.015f, 0.045f, 0.15f, 0.96f));
            const float cap = 12f;
            DrawFramePart(new Rect(rect.x, rect.y, cap, rect.height), 5, 34, 32, 8, 16);
            DrawFramePart(new Rect(rect.x + cap, rect.y, rect.width - cap * 2f, rect.height), 5, 44, 32, 48, 16);
            DrawFramePart(new Rect(rect.xMax - cap, rect.y, cap, rect.height), 5, 34, 32, 8, 16, flipX: true);
        }

        private void DrawRetailTowerHud(float width)
        {
            int maxHp = KohMaxHealth;
            int level = Mathf.Max(1, _playerActor.Level);
            var floor = new Rect(16f, 15f, 74f, 35f);
            var gold = new Rect(width - 146f, 15f, 130f, 35f);
            float center = width * 0.5f;
            var hp = new Rect(center - 49f, 19f, 159f, 31f);
            var levelDial = new Rect(center - 116f, 12f, 68f, 61f);
            var gauge = new Rect(hp.x + 4f, hp.yMax + 3f, hp.width - 8f, 14f);
            DrawRetailHudPlaque(floor);
            DrawRetailHudPlaque(gold);
            DrawRetailHudPlaque(hp);

            if (HasRetailHudFrames)
            {
                float half = levelDial.width * 0.5f;
                DrawHudFill(new Rect(levelDial.x + 15f, levelDial.y + 12f, 38f, 38f), new Color(0.015f, 0.045f, 0.15f));
                DrawFramePart(new Rect(levelDial.x, levelDial.y, half, levelDial.height), 4, 8, 80, 16, 32);
                DrawFramePart(new Rect(levelDial.x + half, levelDial.y, half, levelDial.height),
                    4, 8, 80, 16, 32, flipX: true);
                var color = GUI.color;
                GUI.color *= new Color(0.48f, 0.48f, 0.48f);
                DrawFramePart(gauge, 0, 8, 56, 96, 8);
                GUI.color = color;
                float fill = Mathf.Clamp01(_health / (float)Mathf.Max(1, maxHp));
                if (fill > 0f)
                    DrawFramePart(new Rect(gauge.x, gauge.y, gauge.width * fill, gauge.height), 3, 8, 56, 96 * fill, 8);
            }
            else
            {
                DrawHudPanel(levelDial);
                DrawHudPanel(gauge);
                float fill = (gauge.width - 4f) * Mathf.Clamp01(_health / (float)Mathf.Max(1, maxHp));
                DrawHudFill(new Rect(gauge.x + 2f, gauge.y + 2f, fill, gauge.height - 4f), new Color(0.35f, 0.76f, 0.93f));
            }
            _bodyStyle.normal.textColor = new Color(0.51f, 1f, 0.83f);
            DrawHudText(new Rect(floor.x + 12f, floor.y, 55f, floor.height), T($"{_floor}F", $"{_floor}º"), _bodyStyle);
            float levelInset = level < 10 ? 30f : 26f;
            DrawHudText(new Rect(levelDial.x + levelInset, levelDial.y + 17f, 40f, 26f), $"{level}", _bodyStyle);
            DrawHudText(new Rect(levelDial.x + 48f, levelDial.y - 6f, 30f, 23f), T("LV", "NÍV"), _messageStyle);
            int currentHp = Mathf.Max(0, _health);
            if (GameLanguage.Portuguese)
            {
                _portugueseHpStyle ??= new GUIStyle(_bodyStyle)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false
                };
                DrawHudText(new Rect(hp.x + 10f, hp.y, hp.width - 20f, hp.height),
                    $"{currentHp:000} / {maxHp:000} MÁX", _portugueseHpStyle);
            }
            else
            {
                DrawHudText(new Rect(hp.x + 16f, hp.y, 57f, hp.height), $"{currentHp:000} /", _bodyStyle);
                DrawHudText(new Rect(hp.x + 78f, hp.y + 2f, 67f, 16f), $"MAX {maxHp:000}", _bodyStyle);
            }
            DrawHudText(new Rect(gold.x + 11f, gold.y, gold.width - 20f, gold.height), $"{_runLoot}G", _bodyStyle);
        }

        private static void DrawDialogueFrame(Rect rect, bool dialoguePole = false)
        {
            if (HasRetailHudFrames)
            {
                DrawHudFill(rect, new Color(0.025f, 0.027f, 0.12f, 0.74f));
                const float corner = 10f;
                const float edge = 8f;
                DrawFramePart(new Rect(rect.x, rect.y - edge * 0.5f, rect.width, edge), 2, 0, 112, 128, 8);
                DrawFramePart(new Rect(rect.x, rect.yMax - edge * 0.5f, rect.width, edge), 2, 0, 120, 128, 8);
                DrawFramePart(new Rect(rect.x - edge * 0.5f, rect.y, edge, rect.height), 2, 0, 48, 8, 64);
                DrawFramePart(new Rect(rect.xMax - edge * 0.5f, rect.y, edge, rect.height), 2, 0, 48, 8, 64, flipX: true);
                for (int i = 0; i < 4; i++)
                {
                    float x = (i & 1) == 0 ? rect.x : rect.xMax;
                    float y = i < 2 ? rect.y : rect.yMax;
                    DrawFramePart(new Rect(x - corner * 0.5f, y - corner * 0.5f, corner, corner),
                        2, 16 + (i & 1) * 8, 32 + (i / 2) * 8, 8, 8);
                }
                if (dialoguePole)
                {
                    float x = rect.x - 23f;
                    DrawFramePart(new Rect(x + 6f, rect.y - 11f, 8f, rect.height + 26f), 1, 120, 0, 8, 112);
                    DrawFramePart(new Rect(x, rect.y - 12f, 20f, 20f), 0, 0, 0, 16, 16);
                    DrawFramePart(new Rect(x, rect.yMax - 10f, 20f, 20f), 0, 0, 16, 16, 16);
                }
                return;
            }
            DrawHudPanel(rect);
            var bronze = new Color(0.49f, 0.28f, 0.10f);
            var gold = new Color(0.93f, 0.78f, 0.42f);
            var light = new Color(1f, 0.94f, 0.70f);
            DrawHudFill(new Rect(rect.x - 4f, rect.y, 4f, rect.height), bronze);
            DrawHudFill(new Rect(rect.x - 4f, rect.y, 1f, rect.height), gold);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? rect.x : rect.xMax;
                float y = corner < 2 ? rect.y : rect.yMax;
                DrawHudFill(new Rect(x - 4f, y - 4f, 8f, 8f), bronze);
                DrawHudFill(new Rect(x - 3f, y - 3f, 6f, 6f), gold);
                DrawHudFill(new Rect(x - 2f, y - 2f, 2f, 2f), light);
            }
        }
    }
}
