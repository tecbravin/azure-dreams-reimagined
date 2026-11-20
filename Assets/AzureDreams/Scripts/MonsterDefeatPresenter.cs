using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Retail defeat presentations for tower monsters (dungeon overlay
    /// defeat-removal callbacks).  Most species use the shared
    /// update_actor_defeat_removal: the sprite switches to additive blending
    /// and its colour sinks from 0x80 to 0x20 over 16 ticks.  A few species
    /// install their own callback; those are reproduced here:
    ///  - Cyclone (fade_reshape_and_remove_entity): 12-tick additive fade
    ///    while the sprite narrows by 3/4 and stretches by 3/2 each tick.
    ///  - Mandara / Unicorn: a short flash (6 / 4 ticks) and then an additive
    ///    fade that drops 0x10 per tick.
    ///  - Kraken / Kid: a small hop, the defeat animation, then a five-step
    ///    fade (0x80 to 0x18 by 0x18).  Kid sinks while fading.
    ///  - Arachne, Battnel, Univern, Zu, Picket, Viper: play the defeat
    ///    animation to its end before fading (Battnel simply vanishes).
    ///  - Baloon (update_monster_staged_motion): pops, spins and loops across
    ///    the screen for 45 ticks, drops onto a nearby square from high up,
    ///    lies there for 40 ticks and is removed.
    /// One retail tick is a 30 Hz frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    internal sealed class MonsterDefeatPresenter : MonoBehaviour
    {
        private const float Tick = 1f / 30f;

        private enum Style { Fade, Stretch, FlashFade, Hop, AnimateFade, AnimateVanish, Baloon }

        private static Material _additive;
        private SpriteRenderer _renderer;
        private Style _style;
        private int _species;
        private float _elapsed;
        private float _animation;
        private int _flashTicks;
        private Vector3 _forward;
        private Vector3 _offset;
        private Vector3 _baseScale;
        private Quaternion _spin = Quaternion.identity;
        private float _brightness = 1f;
        private bool _additiveOn;
        private Color _baseColor = Color.white;

        // Baloon state, in tiles (64 retail units = one square).
        private float _baloonAngle;
        private float _baloonFall;
        private float _baloonFallSpeed;
        private Vector3 _baloonLanding;

        /// <summary>Starts the presentation and returns how long the view must stay alive.</summary>
        internal static float Begin(MonsterTurnSpriteView monster, int species, Vector2 facing)
        {
            if (!monster) return 0f;
            var presenter = monster.gameObject.AddComponent<MonsterDefeatPresenter>();
            return presenter.Configure(monster, species, facing);
        }

        internal static bool UsesAlternateCue(int species) => species is 5 or 30; // Ifrit, Manoeva: SFX 0x806
        internal static bool IsBaloon(int species) => species == 25;

        private float Configure(MonsterTurnSpriteView monster, int species, Vector2 facing)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _species = species;
            _baseScale = transform.localScale;
            _baseColor = _renderer ? _renderer.color : Color.white;
            _forward = new Vector3(facing.x, 0f, facing.y);
            if (_forward.sqrMagnitude > 0f) _forward.Normalize();
            _style = species switch
            {
                29 => Style.Stretch,                         // Cyclone
                39 => Style.FlashFade,                       // Mandara
                18 => Style.FlashFade,                       // Unicorn
                33 or 4 => Style.Hop,                        // Kraken, Kid
                13 => Style.AnimateVanish,                   // Battnel
                12 or 17 or 38 or 32 or 36 => Style.AnimateFade, // Arachne, Univern, Zu, Picket, Viper
                25 => Style.Baloon,
                _ => Style.Fade
            };
            _flashTicks = species == 39 ? 6 : 4;
            if (_style is Style.Hop or Style.AnimateFade or Style.AnimateVanish or Style.Baloon)
                _animation = monster.BeginDefeat();
            if (_style == Style.Hop) _animation = Mathf.Max(_animation, 14 * Tick);
            return Duration;
        }

        private float Duration => _style switch
        {
            Style.Fade => 16 * Tick,
            Style.Stretch => 12 * Tick,
            Style.FlashFade => (_flashTicks + 8) * Tick,
            Style.Hop => _animation + 5 * Tick,
            Style.AnimateFade => _animation + 16 * Tick,
            Style.AnimateVanish => Mathf.Max(_animation, 4 * Tick),
            Style.Baloon => (46 + 30 + 10) * Tick + 0.6f,
            _ => 16 * Tick
        };

        private void LateUpdate()
        {
            if (!_renderer) return;
            float previous = _elapsed;
            _elapsed += Time.deltaTime;
            int ticksBefore = Mathf.FloorToInt(previous / Tick);
            int ticks = Mathf.FloorToInt(_elapsed / Tick);
            for (int t = ticksBefore; t < ticks; t++) Step(t);
            Apply();
        }

        private void Step(int tick)
        {
            switch (_style)
            {
                case Style.Fade:
                {
                    // color += (0x20 - color) / timer, timer 16..1
                    int timer = Mathf.Max(1, 16 - tick);
                    SetAdditive();
                    _brightness += (0.25f - _brightness) / timer;
                    break;
                }
                case Style.Stretch:
                {
                    int timer = Mathf.Max(1, 12 - tick);
                    SetAdditive();
                    _brightness -= _brightness / timer;
                    var scale = transform.localScale;
                    transform.localScale = new Vector3(scale.x * 0.75f, Mathf.Min(scale.y * 1.5f, _baseScale.y * 40f), scale.z);
                    break;
                }
                case Style.FlashFade:
                    // clut 0xFF80 (the white flash palette) for a few ticks,
                    // then additive with the colour dropping 0x10 per tick.
                    SetAdditive();
                    if (tick >= _flashTicks) _brightness = Mathf.Max(0f, _brightness - 0.125f);
                    break;
                case Style.Hop:
                {
                    // v_z = -0xC units, gravity +0x1C000 (1.75 units) per tick,
                    // drift of one unit per tick along the facing direction.
                    float t = tick + 1;
                    if (t <= 14)
                    {
                        float height = (12f * t - 0.875f * t * t) / 64f;
                        _offset = _forward * (t / 64f) + Vector3.up * Mathf.Max(0f, height);
                    }
                    if (_elapsed >= _animation)
                    {
                        SetAdditive();
                        _brightness = Mathf.Max(0f, _brightness - 0.1875f);
                        if (_species == 4) _offset += Vector3.down * 0.03f;
                    }
                    break;
                }
                case Style.AnimateFade:
                    if (_elapsed >= _animation)
                    {
                        int timer = Mathf.Max(1, 16 - Mathf.FloorToInt((_elapsed - _animation) / Tick));
                        SetAdditive();
                        _brightness += (0.25f - _brightness) / timer;
                    }
                    break;
                case Style.AnimateVanish:
                    if (_elapsed >= _animation) _brightness = 0f;
                    break;
                case Style.Baloon:
                    StepBaloon(tick);
                    break;
            }
        }

        private void StepBaloon(int tick)
        {
            if (tick < 46)
            {
                // Retail speed/spin schedule (action_timer 0..45).
                float speed; int spin;
                if (tick == 0) { speed = 0f; spin = 0; }
                else if (tick <= 2) { speed = 0x30; spin = 0x100; }
                else if (tick <= 8) { speed = 0x28; spin = 0x200; }
                else if (tick <= 10) { speed = 0x30; spin = 0x100; }
                else if (tick <= 14) { speed = 0x70; spin = 0x100; }
                else if (tick <= 17) { speed = 0x60; spin = 0x200; }
                else if (tick <= 21) { speed = 0x50; spin = 0x80; }
                else { speed = 0x30; spin = 0x20; }
                if (tick > 21) speed = 0x50 * Mathf.Max(0f, 1f - (tick - 21) / 24f) + 0x10;
                _baloonAngle = Mathf.Repeat(_baloonAngle + spin, 0x1000);
                float radians = (_baloonAngle - 0x400) / 0x1000 * Mathf.PI * 2f;
                var camera = Camera.main;
                Vector3 across = camera ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized : Vector3.right;
                // speed is in retail units per tick; 64 units per square.
                _offset += (across * Mathf.Cos(radians) + Vector3.up * -Mathf.Sin(radians)) * (speed / 64f) * 0.5f;
                _offset.y = Mathf.Clamp(_offset.y, -0.2f, 4f);
                _spin = Quaternion.Euler(0f, 0f, -_baloonAngle / 0x1000 * 360f);
                if (tick == 45)
                {
                    // Lands on a free square near where it died, dropping from
                    // 0x12C units (about 4.7 squares) above it.
                    _spin = Quaternion.identity;
                    var rng = new System.Random(GetEntityId().GetHashCode());
                    _baloonLanding = new Vector3(rng.Next(-3, 4), 0f, rng.Next(-2, 4)) * 0.5f;
                    _baloonFall = 300f / 64f;
                    _baloonFallSpeed = 0f;
                    var scale = _baseScale; scale.x *= 0.75f; transform.localScale = scale;
                    _offset = _baloonLanding + Vector3.up * _baloonFall;
                }
            }
            else if (_baloonFall > 0f)
            {
                _baloonFallSpeed += 0.6f / 64f * 8f;
                _baloonFall = Mathf.Max(0f, _baloonFall - _baloonFallSpeed);
                _offset = _baloonLanding + Vector3.up * _baloonFall;
            }
            else if (tick >= 46 + 30)
            {
                SetAdditive();
                _brightness = Mathf.Max(0f, _brightness - 0.1f);
            }
        }

        private void SetAdditive()
        {
            if (_additiveOn || !_renderer) return;
            _additiveOn = true;
            if (!_additive)
            {
                var shader = Shader.Find("AzureDreams/FusionGlow");
                if (shader) _additive = new Material(shader) { name = "Monster defeat (additive)", renderQueue = 3100 };
            }
            if (_additive) _renderer.sharedMaterial = _additive;
        }

        private Vector3 _applied;
        private Quaternion _appliedSpin = Quaternion.identity;
        private Rendering.CameraFacingSprite _facing;
        private bool _facingLooked;

        private void Apply()
        {
            transform.localPosition += _offset - _applied;
            _applied = _offset;
            if (!_facingLooked) { _facing = GetComponent<Rendering.CameraFacingSprite>(); _facingLooked = true; }
            var view = Camera.main;
            Vector3 axis = view ? view.transform.forward : Vector3.forward;
            if (_facing)
            {
                // CameraFacingSprite rewrites the rotation every frame; add the spin on top.
                if (_spin != Quaternion.identity)
                    transform.rotation = Quaternion.AngleAxis(_spin.eulerAngles.z, axis) * transform.rotation;
            }
            else if (_spin != _appliedSpin)
            {
                transform.rotation = Quaternion.AngleAxis(_spin.eulerAngles.z, axis) *
                                     Quaternion.Inverse(Quaternion.AngleAxis(_appliedSpin.eulerAngles.z, axis)) *
                                     transform.rotation;
                _appliedSpin = _spin;
            }
            float b = Mathf.Max(0f, _brightness);
            var color = _baseColor;
            if (_additiveOn) _renderer.color = new Color(color.r * b, color.g * b, color.b * b, color.a);
            else _renderer.color = new Color(color.r, color.g, color.b, color.a * b);
        }
    }
}
