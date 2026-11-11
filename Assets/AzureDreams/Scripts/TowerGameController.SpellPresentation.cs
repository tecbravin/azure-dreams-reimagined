using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private static readonly int[] BallSpellIds =
        {
            0, 1, 4, 7, 10, 13, 17, 20, 23, 26, 29, 33, 36, 39, 42, 45, 44, 49
        };
        private const int FireWallSpell = 16;
        private const int FireWallSpecies = 51;
        private static sbyte WallProtectionStatus(int spell) => spell switch
        {
            FireWallSpell => StatusId.FIRE_PROTECT,
            18 => StatusId.WIND_PROTECT,
            _ => StatusId.WATER_PROTECT
        };

        private static int SpellFamily(int spell) => spell is >= 1 and <= 48 ? (spell - 1) / 3 : spell == 49 ? 16 : -1;
        private static int DirectSpellPower(int spell) => SpellFamily(spell) switch
        {
            0 => 16, // Breath
            1 => 8,  // Sled
            2 => 10, // Brid
            3 => 19, // Rise
            14 => 24, // LoGrave and its elemental variants
            15 => 24, 16 => 32, _ => 0
        };

        // NTSC spell_mp_costs at 0x800DD880. One MP is 256 internal units.
        private static readonly byte[] SpellMpByFamily = { 12, 8, 10, 16, 8, 8, 8, 6, 10, 16, 8, 12, 10, 9, 12, 20, 32 };
        private static int NativeSpellCost(ActorRecord caster, int spell)
        {
            int family = SpellFamily(spell);
            if (family < 0) return 0;
            return SpellMpByFamily[family] * (HasTalent(caster, 256) ? 128 : 256);
        }

        private static int BallSpellLevel(int charges) => Mathf.Clamp(charges / 10, 0, 99);
        private static int EffectiveSpellLevel(ActorRecord caster, int level) =>
            Mathf.Clamp(HasTalent(caster, 128) ? level * 2 : level, 0, 99);
        private static sbyte DirectSpellStatus(int spell) => SpellFamily(spell) switch
        {
            4 => StatusId.POISON, 10 => StatusId.BLIND, 11 => StatusId.PARALYSIS,
            12 => StatusId.SLEEP, _ => 0
        };

        private sealed class SpellCue
        {
            public Vector3 From, To;
            public Vector3? ReflectFrom;
            public bool FireWall;
            public int Family;
            public Color Color;
            public string Impact;
            public float Travel, Delay;
            public SpriteRenderer Orb;
            public bool HitShown, ReflectionShown;
            public Vector3? MixSource;
            public Vector2Int MixDirection;
            public int MixPalette;
        }

        private readonly List<SpellCue> _spellCues = new();
        private Action _spellImpactAction;
        private Sprite _spellOrbSprite;
        private Texture2D _spellOrbTexture;
        private bool _spellPresentationStarted;
        private float _spellElapsed;
        private string _messageBeforeSpellResolution;
        private bool _kohMixingThisTurn;
        private Texture2D _swordMixtureAtlas;
        private Sprite[,] _swordMixtureFrames;

        private enum Mixture { None, Sword, Wave, Blade, Summon, Area }

        private static (Mixture Type, int Cost) MixtureFor(int spell) => SpellFamily(spell) switch
        {
            0 or 1 or 2 or 3 or 8 or 9 => (Mixture.Sword, 128),
            4 or 13 => (Mixture.Wave, 192),
            14 => (Mixture.Wave, 1024),
            5 or 6 => (Mixture.Blade, 128),
            7 => (Mixture.Summon, 512),
            10 or 11 or 12 => (Mixture.Area, 1024),
            _ => (Mixture.None, 0)
        };

        private static string MixtureName(Mixture type, int element, bool armed) => type switch
        {
            Mixture.Sword when element == Element.Water => armed ? "Blizzard Sword" : "Ice Finger",
            Mixture.Sword when element == Element.Wind => armed ? "Wind Cutter" : "Thunder Finger",
            Mixture.Sword => armed ? "Flame Sword" : "Flay Finger",
            Mixture.Wave => element == Element.Water ? "Snow Wave" : element == Element.Wind ? "Thunder Wave" : "Heat Wave",
            Mixture.Blade => element == Element.Water ? "Aqua Blade" : element == Element.Wind ? "Thunder Blade" : "Burning Blade",
            Mixture.Summon => element == Element.Water ? "Snow Shoot" : element == Element.Wind ? "Gaia Shoot" : "Fire Shoot",
            _ => element == Element.Water ? "Aqua Wheel" : element == Element.Wind ? "Earth Shaker" : "Mt. Burn"
        };

        private void TryManualMixMagic(ActorRecord actor, GameObject view, Vector2Int from, Vector2Int origin, int spell)
        {
            if (Distance(from, origin) > 2 || KohSkipsAction || IsFrog(_playerActor) || _carriedView ||
                !FamiliarHasSight(from, origin)) return;
            var (type, cost) = MixtureFor(spell);
            if (type == Mixture.None) return;
            if (HasTalent(actor, 256)) cost /= 2;
            if ((actor.MpRaw < cost && !_mpProtection.ContainsKey(actor)) || !SpendMonsterMp(actor, cost)) return;

            int level = actor.U8(8) == spell ? Mathf.Max(1, actor.U8(9)) : actor.Level;
            if (actor.Spells != null)
                foreach (var known in actor.Spells)
                {
                    if (known == null || SpellFamily(known.SpellId) != SpellFamily(spell)) continue;
                    level = known.StoredLevel;
                    break;
                }
            level = EffectiveSpellLevel(actor, level);
            int element = UnitElement(actor);
            var direction = _towerFacing;
            var front = origin + direction;
            bool canHitFront = FamiliarHasSight(origin, front);
            int adjacent = canHitFront ? CreatureAt(front) : -1;
            var weapon = CombatGear(_playerActor, false);
            byte wand = weapon?.Kind == ItemSystem.Kind.Wand ? weapon.Subtype : (byte)0;

            _kohMixingThisTurn = true;
            string name = MixtureName(type, element, weapon != null);
            _message = T($"Koh uses {name}.", $"Koh usa {name}.");
            QueueMonsterSpriteAction(view, origin - from, casting: true);

            if (type is Mixture.Sword or Mixture.Blade ||
                (type == Mixture.Wave && element != Element.Wind && adjacent >= 0))
            {
                QueueSwordMixtureCue(element, from, origin, canHitFront ? front : origin, direction);
                int damage = 0;
                if (adjacent >= 0 && !IsSacredTile(front))
                {
                    var target = _creatures[adjacent].Actor;
                    damage = WithItemCombatEffects(_playerActor, PlayerWeaponDamage(), target, origin, front, element,
                        type == Mixture.Sword ? level : 0);
                    QueueCreatureDamage(_playerActor, adjacent, damage, wand, element);
                    if (type == Mixture.Blade && damage > 0 && element != Element.Water)
                    {
                        sbyte status = element == Element.Wind ? StatusId.PARALYSIS : StatusId.BLIND;
                        _spellImpactAction += () => Status(target, status, 8);
                    }
                }
                if (type == Mixture.Blade && element == Element.Water && adjacent >= 0)
                {
                    QueueElementalMixtureCue(element, front, front, delay: 0.44f);
                    HitMixtureTarget(adjacent, origin, 10, level, element);
                }
                if (type is Mixture.Sword or Mixture.Blade) return;
            }

            if (type is Mixture.Wave or Mixture.Summon)
            {
                var end = origin;
                int target = -1;
                bool stopOnEnemy = type == Mixture.Wave || element == Element.Water;
                for (int step = 1; step <= 8; step++)
                {
                    var next = origin + direction * step;
                    if (!FamiliarHasSight(end, next) ||
                        (type == Mixture.Summon && element != Element.Water && TowerTileHeight(next) != TowerTileHeight(origin)))
                        break;
                    end = next;
                    target = CreatureAt(end);
                    if (stopOnEnemy && target >= 0) break;
                    if (!stopOnEnemy && target >= 0) HitMixtureTarget(target, origin, 16, level, element);
                }
                float delay = type == Mixture.Wave && adjacent >= 0 && element != Element.Wind ? 0.44f : 0f;
                QueueElementalMixtureCue(element, origin, end, delay);
                if (type == Mixture.Wave && target >= 0)
                    HitMixtureTarget(target, origin, SpellFamily(spell) == 14 ? 24 : 12, level, element);
                if (type == Mixture.Summon && element == Element.Water)
                    HitMixtureArea(origin, end, 2, BellRoomAt(end), 16, level, element);
                return;
            }

            if (element == Element.Fire)
            {
                var center = origin + direction * 4;
                HitMixtureArea(origin, center, 3, -1, 16, level, element);
                QueueElementalMixtureCue(element, origin, canHitFront ? front : origin);
            }
            else
            {
                int room = BellRoomAt(origin);
                if (room >= 0) HitMixtureArea(origin, origin, 0, room, 16, level, element);
                else if (adjacent >= 0) HitMixtureTarget(adjacent, origin, 16, level, element);
                QueueElementalMixtureCue(element, origin, room < 0 && canHitFront ? front : origin);
            }
        }

        private void HitMixtureArea(Vector2Int origin, Vector2Int center, int radius, int room,
            int power, int level, int element)
        {
            for (int i = 0; i < _creatures.Count; i++)
            {
                var cell = _creatures[i].Cell;
                if (room >= 0 ? BellRoomAt(cell) != room :
                    Mathf.Abs(cell.x - center.x) > radius || Mathf.Abs(cell.y - center.y) > radius) continue;
                HitMixtureTarget(i, origin, power, level, element);
                QueueElementalMixtureCue(element, cell, cell, delay: 0.25f);
            }
        }

        private void HitMixtureTarget(int enemy, Vector2Int origin, int power, int level, int element)
        {
            var creature = _creatures[enemy];
            if (creature.Health <= 0 || IsSacredTile(creature.Cell)) return;
            int damage = SpellOrImpactDamage(creature.Actor, power, level, element, _playerActor);
            QueueCreatureDamage(_playerActor, enemy, ScaleElevationDamage(damage,
                TowerTileHeight(origin), TowerTileHeight(creature.Cell)), element: element);
        }

        private static (Color Color, string Impact) MixtureArt(int element) => element switch
        {
            Element.Water => (new Color(0.36f, 0.78f, 1f), "FX_Water"),
            Element.Wind => (new Color(0.60f, 1f, 0.48f), "FX_Lightning"),
            _ => (new Color(1f, 0.43f, 0.12f), "FX_Fire")
        };

        private void QueueElementalMixtureCue(int element, Vector2Int origin, Vector2Int end, float delay = 0f)
        {
            var (color, impact) = MixtureArt(element);
            _spellCues.Add(new SpellCue
            {
                From = CellPosition(origin) + Vector3.up * 0.36f,
                To = CellPosition(end) + Vector3.up * 0.36f,
                Color = color, Impact = impact, Delay = delay,
                Travel = Mathf.Clamp(0.23f + Distance(origin, end) * 0.08f, 0.23f, 1.05f)
            });
        }

        private void QueueSwordMixtureCue(int element, Vector2Int familiar, Vector2Int koh, Vector2Int target, Vector2Int direction)
        {
            int palette = element == Element.Water ? 1 : element == Element.Wind ? 2 : 0;
            var (color, impact) = MixtureArt(element);
            _spellCues.Add(new SpellCue
            {
                From = CellPosition(koh) + Vector3.up * 0.52f,
                To = CellPosition(target) + Vector3.up * 0.36f,
                MixSource = CellPosition(familiar) + Vector3.up * 0.40f,
                MixDirection = direction,
                MixPalette = palette,
                Color = color,
                Impact = impact,
                Travel = 0.28f
            });
        }

        private void QueueSpellCue(int spell, int element, Vector2Int origin, Vector2Int destination,
            Vector2Int? reflectFrom = null)
        {
            int family = spell > 0 ? SpellFamily(spell) : 17;
            Color color = family switch
            {
                4 => new Color(0.38f, 0.95f, 0.2f),
                5 when spell == FireWallSpell => MixtureArt(Element.Fire).Color,
                5 or 7 or 9 => new Color(0.36f, 0.8f, 1f),
                6 => new Color(0.85f, 0.9f, 1f),
                8 => new Color(0.35f, 1f, 0.72f),
                10 or 13 or 15 or 17 => new Color(0.76f, 0.38f, 1f),
                11 => new Color(1f, 0.9f, 0.35f),
                12 => new Color(0.55f, 0.56f, 1f),
                16 => new Color(0.64f, 1f, 0.32f),
                _ => element == Element.Water ? new Color(0.34f, 0.76f, 1f) :
                    element == Element.Wind ? new Color(0.55f, 1f, 0.65f) : new Color(1f, 0.42f, 0.15f)
            };
            string impact = family switch
            {
                4 or 10 or 12 or 13 or 15 or 17 => "FX_Smoke",
                5 when spell == FireWallSpell => "FX_Fire",
                5 or 7 or 8 or 9 or 16 => "FX_Water",
                11 => "FX_Lightning",
                <= 3 when element == Element.Fire => "FX_Fire",
                _ => null
            };
            var from = CellPosition(origin) + Vector3.up * 0.3f;
            var to = CellPosition(destination) + Vector3.up * 0.3f;
            Vector3? reflection = reflectFrom.HasValue
                ? CellPosition(reflectFrom.Value) + Vector3.up * 0.3f : null;
            float distance = Vector3.Distance(from, reflection ?? to) + (reflection.HasValue ? Vector3.Distance(reflection.Value, to) : 0f);
            _spellCues.Add(new SpellCue
            {
                From = from, To = to, ReflectFrom = reflection, Family = family, Color = color, Impact = impact,
                FireWall = spell == FireWallSpell,
                Travel = Mathf.Clamp(0.30f + distance * 0.08f, 0.30f, 1.05f)
            });
        }

        private bool TickSpellPresentation(float deltaTime)
        {
            _spellElapsed += deltaTime;
            bool waiting = false;
            const float charge = 0.20f;
            const float impactHold = 0.32f;
            foreach (var cue in _spellCues)
            {
                if (cue.MixSource.HasValue)
                {
                    waiting |= PresentSwordMixture(cue);
                    continue;
                }
                float elapsed = _spellElapsed - cue.Delay;
                if (elapsed < 0f)
                {
                    waiting = true;
                    continue;
                }
                float hitTime = charge + cue.Travel;
                if (elapsed >= hitTime + impactHold)
                {
                    if (cue.Orb) RemoveGenerated(cue.Orb.gameObject);
                    cue.Orb = null;
                    continue;
                }
                waiting = true;
                if (!cue.Orb) cue.Orb = CreateSpellOrb(cue.Color);
                float t = Mathf.Clamp01((elapsed - charge) / cue.Travel);
                Vector3 position = Vector3.Lerp(cue.From, cue.To, t);
                if (cue.ReflectFrom is Vector3 mirror)
                {
                    position = t < 0.5f ? Vector3.Lerp(cue.From, mirror, t * 2f) : Vector3.Lerp(mirror, cue.To, t * 2f - 1f);
                    if (t >= 0.5f && !cue.ReflectionShown)
                    {
                        cue.ReflectionShown = true;
                        PresentImpactBurst(mirror, 0);
                    }
                }
                else if (cue.Family == 1) position.y -= 0.35f;
                else if (cue.Family is 3 or 14 or 16)
                    position.y += Mathf.Sin(t * Mathf.PI) * (cue.Family == 3 ? 1.15f : 0.75f);
                else position.y += Mathf.Sin(t * Mathf.PI) * 0.18f;
                cue.Orb.transform.position = position;
                float hold = Mathf.Clamp01((elapsed - hitTime) / impactHold);
                float size = t >= 1f ? Mathf.Lerp(0.48f, 0.82f, hold) :
                    Mathf.Lerp(0.18f, cue.Family == 2 ? 0.48f : 0.34f, Mathf.Clamp01(elapsed / charge));
                cue.Orb.transform.localScale = cue.Family switch
                {
                    0 or 1 when t < 1f => new Vector3(size * 1.65f, size * 0.65f, size),
                    3 when t >= 1f => new Vector3(size * 0.72f, size * 2.2f, size),
                    5 or 6 when t >= 1f => new Vector3(size * 1.6f, size * 1.6f, size),
                    _ => Vector3.one * size
                };
                cue.Orb.color = new Color(cue.Color.r, cue.Color.g, cue.Color.b, 1f - hold);
                if (t >= 1f && !cue.HitShown)
                {
                    cue.HitShown = true;
                    if (cue.FireWall) PresentFireWall(cue.To);
                    else if (cue.Impact != null) SpawnFx(Resources.Load<GameObject>("FX/" + cue.Impact), cue.To);
                }
            }
            if (waiting) return false;
            var impact = _spellImpactAction;
            ClearSpellPresentation();
            impact?.Invoke();
            return true;
        }

        private void PresentFireWall(Vector3 center)
        {
            var prefab = RegisteredMonsterPrefab(FireWallSpecies);
            if (prefab)
            {
                SpawnFx(prefab, center - Vector3.up * 0.3f);
                return;
            }

            // Use the project's fire particles until a renderable retail wall sprite is available.
            var fire = Resources.Load<GameObject>("FX/FX_Fire");
            var right = _gameCamera ? _gameCamera.transform.right : Vector3.right;
            for (int column = -1; column <= 1; column++)
                SpawnFx(fire, center + right * (column * 0.26f));
        }

        private bool PresentSwordMixture(SpellCue cue)
        {
            const float linkDuration = 0.24f;
            const float fadeDuration = 0.24f;
            float strike = Mathf.Clamp01((_spellElapsed - linkDuration) / cue.Travel);
            if (_kohSprite) _kohSprite.PresentAttack(cue.MixDirection, strike);
            float elapsedAfterStrike = _spellElapsed - linkDuration - cue.Travel;
            if (elapsedAfterStrike >= fadeDuration)
            {
                if (cue.Orb) RemoveGenerated(cue.Orb.gameObject);
                cue.Orb = null;
                return false;
            }
            if (!cue.Orb) cue.Orb = CreateSpellOrb(cue.Color);
            if (_spellElapsed < linkDuration)
            {
                float link = Mathf.Clamp01(_spellElapsed / linkDuration);
                cue.Orb.transform.position = Vector3.Lerp(cue.MixSource.Value, cue.From, link) +
                    Vector3.up * (Mathf.Sin(link * Mathf.PI) * 0.24f);
                cue.Orb.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.46f, link);
                return true;
            }

            BuildSwordMixtureSprites();
            float progress = Mathf.Clamp01((_spellElapsed - linkDuration) / (cue.Travel + fadeDuration));
            int frame = Mathf.Min(7, (int)(progress * 8f));
            cue.Orb.sprite = _swordMixtureFrames[cue.MixPalette, frame];
            cue.Orb.transform.position = Vector3.Lerp(cue.From, cue.To, 0.3f + strike * 0.7f);
            cue.Orb.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.55f, strike);
            if (_gameCamera)
                cue.Orb.flipX = Vector3.Dot(_gameCamera.transform.right,
                    new Vector3(cue.MixDirection.x, 0f, cue.MixDirection.y)) < 0f;
            cue.Orb.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(elapsedAfterStrike / fadeDuration));
            if (strike >= 0.7f && !cue.HitShown)
            {
                cue.HitShown = true;
                SpawnFx(Resources.Load<GameObject>("FX/" + cue.Impact), cue.To);
            }
            return true;
        }

        private void BuildSwordMixtureSprites()
        {
            if (_swordMixtureFrames != null) return;
            const int size = 32, frames = 8, palettes = 3;
            var colors = new Color32[,]
            {
                { new(205, 42, 9, 240), new(255, 138, 21, 255), new(255, 243, 153, 255) },
                { new(31, 95, 208, 240), new(76, 201, 255, 255), new(224, 250, 255, 255) },
                { new(44, 140, 65, 240), new(129, 231, 110, 255), new(234, 255, 190, 255) }
            };
            int stride = size * frames;
            var pixels = new Color32[stride * size * palettes];
            for (int palette = 0; palette < palettes; palette++)
            for (int frame = 0; frame < frames; frame++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - 14.5f, dy = y - 15.5f;
                float angle = Mathf.Atan2(dy, dx);
                float tip = -0.85f + frame * 0.32f;
                float trail = tip - angle;
                if (trail < 0f || trail > 1.85f) continue;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float wave = palette == 0 ? Mathf.Sin(angle * 14f - frame) * 0.85f :
                    palette == 1 ? Mathf.Abs(Mathf.Sin(angle * 11f)) * 1.45f : 0f;
                float edge = Mathf.Abs(radius - (12f + wave));
                float thickness = Mathf.Lerp(3.7f, 0.7f, trail / 1.85f);
                bool sparkle = edge > thickness && edge < thickness + 3f &&
                    (x * 13 + y * 7 + frame * 5) % 23 == 0;
                if (edge > thickness && !sparkle) continue;
                int shade = sparkle || edge < thickness * 0.28f ? 2 : edge < thickness * 0.65f ? 1 : 0;
                pixels[(palette * size + y) * stride + frame * size + x] = colors[palette, shade];
            }
            _swordMixtureAtlas = new Texture2D(stride, size * palettes, TextureFormat.RGBA32, false)
            {
                name = "Sword Mix Magic",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _swordMixtureAtlas.SetPixels32(pixels);
            _swordMixtureAtlas.Apply(false, true);
            _swordMixtureFrames = new Sprite[palettes, frames];
            for (int palette = 0; palette < palettes; palette++)
            for (int frame = 0; frame < frames; frame++)
                _swordMixtureFrames[palette, frame] = Sprite.Create(_swordMixtureAtlas,
                    new Rect(frame * size, palette * size, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
        }

        private void PresentImpactBurst(Vector3 center, int element)
        {
            Color color = element switch
            {
                Element.Fire or Element.Water or Element.Wind => MixtureArt(element).Color,
                _ => new Color(1f, 0.94f, 0.78f)
            };
            StartCoroutine(AnimateImpactBurst(center, color));
        }

        private System.Collections.IEnumerator AnimateImpactBurst(Vector3 center, Color color)
        {
            const int count = 5;
            var puffs = new SpriteRenderer[count];
            var right = _gameCamera ? _gameCamera.transform.right : Vector3.right;
            var up = _gameCamera ? _gameCamera.transform.up : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                puffs[i] = CreateSpellOrb(color);
                puffs[i].name = "Hit puff";
                puffs[i].sortingOrder = 4;
            }

            const float duration = 0.24f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (!puffs[0]) yield break;
                float progress = elapsed / duration;
                float spread = Mathf.Lerp(0.035f, 0.25f, Mathf.Sin(progress * Mathf.PI * 0.5f));
                color.a = 1f - progress;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    var puff = puffs[i];
                    puff.transform.position = center + spread * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle));
                    puff.transform.localScale = Vector3.one * Mathf.Lerp(0.19f, 0.10f, progress);
                    puff.color = color;
                }
                yield return null;
            }

            foreach (var puff in puffs)
                if (puff) RemoveGenerated(puff.gameObject);
        }

        private SpriteRenderer CreateSpellOrb(Color color)
        {
            if (!_spellOrbSprite)
            {
                const int size = 9;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int distance = (x - 4) * (x - 4) + (y - 4) * (y - 4);
                    if (distance <= 16) pixels[y * size + x] =
                        new Color32(255, 255, 255, distance < 10 ? (byte)255 : (byte)150);
                }
                _spellOrbTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                _spellOrbTexture.SetPixels32(pixels);
                _spellOrbTexture.Apply(false, true);
                _spellOrbSprite = Sprite.Create(_spellOrbTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            }
            var view = new GameObject("Spell projectile");
            view.transform.SetParent(transform, false);
            var sprite = view.AddComponent<SpriteRenderer>();
            sprite.sprite = _spellOrbSprite;
            sprite.color = color;
            sprite.sortingOrder = 3;
            view.AddComponent<CameraFacingSprite>();
            _generated.Add(view);
            return sprite;
        }

        private void ClearSpellPresentation()
        {
            foreach (var cue in _spellCues)
            {
                if (cue.Orb) RemoveGenerated(cue.Orb.gameObject);
                if (cue.MixSource.HasValue && _kohSprite) _kohSprite.PresentAttack(cue.MixDirection, 1f);
            }
            _spellCues.Clear();
            _spellElapsed = 0f;
            _spellPresentationStarted = false;
            _spellImpactAction = null;
        }
    }
}
