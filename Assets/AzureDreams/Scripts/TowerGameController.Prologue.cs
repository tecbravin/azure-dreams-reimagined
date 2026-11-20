using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum Prologue { Off, Angel, Name, Confirm, Farewell, BirthFilm, Birth, Childhood, StormFilm, Rain, TowerFilm, ReturnInRain, TimeSkipFilm, Wake, Blessing, Transition }

        private static readonly string[] PrologueFilms = { "JO001_WS.mp4", "JO002_WS.mp4", "JO003_WS.mp4", "LOP2_WS.mp4" };
        [SerializeField] private Texture2D _prologueAngel;
        private float _prologueAngelStarted;
        private Texture2D _prologueClouds;
        private const string AngelMusicCue = "AngelSong";
        [SerializeField] private GameObject _prologuePriest;

        private const string HeroNameKey = "opening.hero_name";
        private const string DefaultHeroName = "Koh";
        private const int AngelNameLine = 2;
        private const int BrokenAmuletLine = 6;
        private const string HeroNameControl = "PrologueHeroName";
        private static string HeroName => TownSaveVariables.GetString(HeroNameKey, DefaultHeroName);
        private static string ResolveHeroName(string line) =>
            (line ?? string.Empty).Replace("{name}", HeroName);

        private Prologue _prologue;
        private int _prologueLine;
        private string _prologueName = DefaultHeroName;
        private bool _focusPrologueName;
        private bool EditingPrologueName => _prologue == Prologue.Name ||
            (_prologue == Prologue.Angel && _prologueLine == AngelNameLine);
        private bool _prologueSkipNeedsRelease;
        private float _prologueHold;
        private float _prologueBlack;
        private float _prologueStarted;
        private VideoPlayer _prologuePlayer;
        // Keep prologue movies in an explicit render target.  Letting the platform
        // choose its presentation surface can leave a stale native overlay behind
        // while IMGUI is also drawing VideoPlayer.texture.
        private RenderTexture _prologueFilmTexture;
        private Coroutine _prologueRoutine;
        private GameObject _prologueCast;
        private SpriteRenderer _prologueAunt;
        private SpriteRenderer _prologueGrandpa;
        private bool _prologueWorldReady;
        private bool _prologueHomeLoaded;
        private int _prologueChoice;
        private Vector3 _prologueStanding;
        private bool _jordaImpactPlaying;
        private float _jordaImpactStarted;
        private Texture2D _jordaImpactStar;

        private static readonly (string Speaker, string En, string Pt)[] AngelLines =
        {
            ("Angel", "Oh, you're awake.", "Ah, você acordou."),
            ("Angel", "You are about to be reborn.", "Você está prestes a renascer."),
            ("Angel", "All you can decide on is your name.", "Você só pode escolher o seu nome."),
            ("Angel", "What star you will be born under can only be determined by God.", "Só Deus pode decidir sob qual estrela você vai nascer."),
            ("Angel", "Please hurry up and decide. I want to finish my job and take a nap.", "Por favor, decida logo. Quero terminar meu trabalho e tirar uma soneca.")
        };

        private static readonly (string Speaker, string En, string Pt)[] FarewellLines =
        {
            ("Angel", "All right, {name}! Your new life is about to begin!", "Tudo certo, {name}! Sua nova vida está prestes a começar!"),
            ("Angel", "Your own life. Your own story.", "Sua própria vida. Sua própria história."),
            ("Angel", "Make sure you're positive, make every effort, and live life happily and joyfully to the fullest.", "Mantenha o otimismo, dê o seu melhor e aproveite a vida com alegria."),
            ("Angel", "Or else!", "É bom mesmo!"),
            ("Angel", "On your way! Enjoy!", "Pode ir! Divirta-se!")
        };

        private static readonly (string Speaker, string En, string Pt)[] BirthLines =
        {
            ("", "Whaaaa, whaaaa.", "Uááá, uááá."),
            ("Guy", "Wreath! You did well! He’s a boy!\nHa ha, he looks just like me.",
                "Wreath! Você foi muito bem! É um menino!\nHa, ha! Ele é a minha cara."),
            ("Wreath", "Have you thought of a name?", "Você já pensou em um nome?"),
            ("Guy", "Let’s see...\nHow does Koh sound?", "Deixe-me pensar...\nQue tal Koh?"),
            ("Wreath", "Koh... no.\nI feel there is a name that better fits him.", "Koh... não.\nSinto que outro nome combina melhor com ele."),
            ("Guy", "I’ve got it!\n{name}! How’s that!?", "Já sei!\n{name}! O que acha?!"),
            ("Wreath", "That’s good! His name is {name}.\nIt’s as if God had already picked that name for him a long time ago!",
                "Que bom! O nome dele é {name}.\nÉ como se Deus já tivesse escolhido esse nome para ele há muito tempo!")
        };

        private static readonly (string Speaker, string En, string Pt)[] ChildhoodLines =
        {
            ("Nico", "{name}'s Dad!", "Pai do {name}!"),
            ("Guy", "Yeah, what’s the matter Nico?", "Sim, o que foi, Nico?"),
            ("Nico", "Sob!\n{name} is teasing me.", "Snif!\nO {name} está me provocando."),
            ("Guy", "Listen, {name}.\nA man must take good care of women.",
                "Escute, {name}.\nUm homem deve tratar bem as mulheres.")
        };

        // TOWN.BIN 0x427B17–0x427CA2.
        private const int AuntNoticesNecklace = 5;
        private static readonly (string Speaker, string En, string Pt)[] RainLines =
        {
            ("Aunt", "I wonder if Guy made it to the top level?", "Será que Guy chegou ao último andar?"),
            ("Wreath", "What?\nOh... yes.", "O quê?\nAh... sim."),
            ("Aunt", "Guy’s the only one that could possibly make it to the top.", "Guy é o único que conseguiria chegar ao topo."),
            ("Wreath", "Guy...", "Guy..."),
            ("Wreath", "Huh!?", "Hã!?"),
            ("Aunt", "What’s the matter?", "O que houve?"),
            ("Wreath", "My necklace...\nit broke...", "Meu colar...\nele quebrou...")
        };

        // TOWN.BIN 0x427CDC–0x4283EF.
        private const int AuntAnswersGrandpa = 4;
        private const int RainDoorReaction = 6;
        private const int RainFamiliarArrival = 7;
        private const int AuntAfterGuysReturn = 12;
        private static readonly (string Speaker, string En, string Pt)[] ReturnInRainLines =
        {
            ("Grandpa", "It’s been decades since it rained like this\nin this desert city of Monsbaiya", "Faz décadas que não chove assim\nem Monsbaiya, esta cidade no deserto"),
            ("Grandpa", "......", "......"),
            ("Grandpa", "Wreath is one tough woman.", "Wreath é uma mulher muito forte."),
            ("Grandpa", "She must be worried sick about those strange occurrences the day before yesterday,\nat that ancient tower Guy went to.", "Ela deve estar morrendo de preocupação com as coisas estranhas que aconteceram anteontem,\nnaquela torre antiga para onde Guy foi."),
            ("Aunt", "Don’t be silly, Grandpa.\nWreath is a woman, after all!", "Não diga bobagem, vovô.\nWreath é uma mulher, afinal!"),
            ("Aunt", "Can’t you see that she is worried out of her mind about her husband, that it’s just enough to keep those tears back!?", "Não vê que ela está tão preocupada com o marido que mal consegue conter as lágrimas!?"),
            ("Wreath", "A...\nGuy?", "A...\nGuy?"),
            ("Wreath", "......!", "......!"),
            ("Aunt", "... you’re...", "... você é..."),
            ("Grandpa", "It’s Guy’s Familiar!\nHe’s got Guy’s [Monster Tamer’s Collar] on him!", "É o familiar de Guy!\nEle está com o [Colar de Domador de Monstros] de Guy!"),
            ("Grandpa", "Why is a familiar with Guy’s collar here with no sign of Guy!", "Por que um familiar está aqui com o colar de Guy, sem nenhum sinal dele!"),
            ("Wreath", "Aaaa, ......\nGuy!", "Aaah, ......\nGuy!"),
            ("Aunt", "Guy never did return, did he?", "Guy nunca mais voltou, não é?"),
            ("Wreath", "......", "......"),
            ("Aunt", "Coming to this city, you had accumulated numerous familiars,\nand the future was looking so bright.", "Quando veio para esta cidade, você já tinha reunido muitos familiares,\ne o futuro parecia tão promissor."),
            ("Aunt", "Now you’re pregnant with your second child.", "Agora você está grávida do seu segundo filho."),
            ("Wreath", "... I was prepared...", "... Eu estava preparada..."),
            ("Wreath", "... I was prepared that this might happen someday.", "... Eu estava preparada para a possibilidade de isso acontecer algum dia.")
        };

        // TOWN.BIN 0x412CE9–0x412F1C.
        private const int WakeGreeting = 4;
        private const int WakeTomboyReply = 5;
        private static readonly (string Speaker, string En, string Pt)[] WakeLines =
        {
            ("Nico", "Get up!", "Levante!"),
            ("", "Pow!", "Paf!"),
            ("Nico", "Today is your fifteenth birthday.", "Hoje é seu aniversário de quinze anos."),
            ("Nico", "It’s the day you’ve become an adult!", "É o dia em que você vira adulto!"),
            ("Weedy", "Yup!", "Isso!"),
            ("Nico", "I can’t help my tomboy attitude, so there!", "É meu jeito de moleca, e pronto!"),
            ("Nico", "The priest is already here!", "O padre já chegou!"),
            ("Nico", "Get up and greet him, hurry!", "Levante e vá cumprimentá-lo, depressa!"),
            ("Weedy", "Yup!", "Isso!")
        };

        private (string Speaker, string En, string Pt)[] PrologueLines => _prologue switch
        {
            Prologue.Angel => AngelLines,
            Prologue.Farewell => FarewellLines,
            Prologue.Birth => BirthLines,
            Prologue.Childhood => ChildhoodLines,
            Prologue.Rain => RainLines,
            Prologue.ReturnInRain => ReturnInRainLines,
            Prologue.Wake => WakeLines,
            _ => null
        };

        private static bool IsAngelPhase(Prologue phase) =>
            phase is >= Prologue.Angel and <= Prologue.Farewell;

        private void SetPrologue(Prologue phase)
        {
            _dialogueTextScroll = Vector2.zero;
            if (IsAngelPhase(_prologue) && !IsAngelPhase(phase)) ReleaseAngelPresentation();
            _prologue = phase;
            _focusPrologueName = phase == Prologue.Name;
            _prologueLine = 0;
            _prologueChoice = 0;
            _prologueStarted = Time.unscaledTime;
            if (phase is Prologue.Rain or Prologue.ReturnInRain)
            {
                PresentRainCast();
                UpdateCameraFollow(snap: true);
            }
            if (phase == Prologue.Blessing)
            {
                _blessingRepeatingBasics = _blessingRepeatingSecrets = false;
                _blessingPitaReceiptShown = false;
            }
        }

        private void StartPrologue()
        {
            _prologueWorldReady = false;
            _prologueHomeLoaded = false;
            _prologueName = DefaultHeroName;
            _prologueSkipNeedsRelease = false;
            _prologueHold = 0f;
            _prologueBlack = 0f;
            // Textura grande (sprite sheet da animacao de voo) agora vem de StreamingAssets - pode nao
            // estar pronta neste frame; o timer so e sincronizado quando ela chega de verdade, em
            // DrawPrologueAngel (senao a animacao perderia os primeiros quadros do voo).
            if (!_prologueAngel) _prologueAngel = StreamMediaTexture("Title/AngelOpeningFrames", FilterMode.Bilinear, TextureWrapMode.Repeat);
            _prologueAngelStarted = Time.unscaledTime;
            SetPrologue(Prologue.Angel);
            PlayAreaMusic(AngelMusicCue);
        }

        private void ReleaseAngelPresentation()
        {
            if (_playingMusicCue == AngelMusicCue) ReleaseAreaMusic();
            if (_prologueClouds) Destroy(_prologueClouds);
            _prologueClouds = null;
        }

        private IEnumerator PlayPrologueFilm(int index, Prologue next)
        {
            yield return null;
            if (index >= 0 && index < PrologueFilms.Length)
            {
                // A film phase owns exactly one player. Dispose any stale player
                // before preparing the next clip so two video layers cannot remain.
                ReleasePrologueFilm();
                bool finished = false;
                _prologuePlayer = gameObject.AddComponent<VideoPlayer>();
                _prologuePlayer.playOnAwake = false;
                _prologuePlayer.renderMode = VideoRenderMode.APIOnly;
                _prologuePlayer.source = VideoSource.Url;
                _prologuePlayer.url = StreamedMediaUrl("Prologue/" + PrologueFilms[index]);
                ConfigureCinematicAudio(_prologuePlayer);
                _prologuePlayer.loopPointReached += _ => finished = true;
                _prologuePlayer.errorReceived += (_, __) => finished = true;
                _prologuePlayer.Prepare();
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!finished && !_prologuePlayer.isPrepared && Time.realtimeSinceStartup < deadline) yield return null;
                if (!finished && _prologuePlayer.isPrepared)
                {
                    ConfigurePrologueFilmTexture();
                    PlayCinematic(_prologuePlayer);
                    float duration = (float)_prologuePlayer.length;
                    deadline = Time.realtimeSinceStartup + (float.IsFinite(duration) && duration > 0f ? duration + 8f : 100f);
                    while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                }
                ReleasePrologueFilm();
            }
            _prologueRoutine = null;
            switch (next)
            {
                case Prologue.Birth: BeginPrologueTransition(EnterPrologueHome()); break;
                case Prologue.Rain: BeginPrologueTransition(StagePrologueRain()); break;
                case Prologue.Wake: StagePrologueWake(); break;
                default: SetPrologue(next); break;
            }
        }

        private void ReleasePrologueFilm()
        {
            if (_prologuePlayer)
            {
                // Disabling the component takes down any native video surface in
                // this frame; Destroy alone is deferred until the end of it.
                _prologuePlayer.Stop();
                _prologuePlayer.enabled = false;
                Destroy(_prologuePlayer);
                _prologuePlayer = null;
            }
            if (_prologueFilmTexture)
            {
                _prologueFilmTexture.Release();
                Destroy(_prologueFilmTexture);
                _prologueFilmTexture = null;
            }
        }

        private void ConfigurePrologueFilmTexture()
        {
            if (!_prologuePlayer || _prologueFilmTexture) return;

            int width = Mathf.Max(1, (int)_prologuePlayer.width);
            int height = Mathf.Max(1, (int)_prologuePlayer.height);
            _prologueFilmTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Prologue Film",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _prologueFilmTexture.Create();
            _prologuePlayer.renderMode = VideoRenderMode.RenderTexture;
            _prologuePlayer.targetTexture = _prologueFilmTexture;
        }

        private bool HandlePrologueInput()
        {
            if (_prologue == Prologue.Off) return _prologueBlack > 0f;
            // The prologue uses the same hold-to-skip control as dialogue:
            // Cancel (Q by default), rather than the map button.
            bool held = Application.isFocused && _cancel?.IsPressed() == true;
            if (!held)
            {
                _prologueHold = 0f;
                _prologueSkipNeedsRelease = false;
            }
            if (_prologue == Prologue.Transition || _prologueBlack > 0f) return true;
            if (held && !_prologueSkipNeedsRelease)
            {
                _prologueHold += Time.unscaledDeltaTime;
                if (_prologueHold >= KewneSkipHoldSeconds) SkipPrologue();
                return true;
            }
            var keys = Keyboard.current;
            if (keys != null && PrologueChoices().First != null &&
                (MenuDirectionPressed(3) || MenuDirectionPressed(4) ||
                 MenuDirectionPressed(1) || MenuDirectionPressed(2)))
                _prologueChoice = 1 - _prologueChoice;
            bool enter = keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame);
            if (enter || (!EditingPrologueName && _interact?.WasPressedThisFrame() == true))
                AdvancePrologue();
            return true;
        }

        private void SkipPrologue()
        {
            _prologueSkipNeedsRelease = true;
            _prologueHold = 0f;
            if (_prologueRoutine != null) StopCoroutine(_prologueRoutine);
            _prologueRoutine = null;
            ReleasePrologueFilm();
            if (_prologue <= Prologue.Farewell)
            {
                _prologueName = DefaultHeroName;
                BeginPrologueTransition(EnterPrologueHome());
            }
            else if (_prologue == Prologue.Blessing)
                BeginPrologueTransition(FinishPrologue());
            else
                BeginPrologueTransition(WakeFromPrologueBed(false));
        }

        private (string First, string Second) PrologueChoices()
        {
            if (_prologue == Prologue.Confirm) return (T("Yes", "Sim"), T("No", "Não"));
            if (_prologue == Prologue.Wake && _prologueLine == WakeGreeting)
                return (T("Good Morning. Nico.", "Bom dia, Nico."), T("Can’t you be bit more gentle?", "Não dá para ter um pouco mais de cuidado?"));
            if (_prologue == Prologue.Blessing) return BlessingChoices();
            return default;
        }

        private void AdvancePrologue()
        {
            if (_prologueHold > 0f || _prologueBlack > 0f) return;
            _dialogueTextScroll = Vector2.zero;
            if (EditingPrologueName)
                _prologueName = string.IsNullOrWhiteSpace(_prologueName) ? DefaultHeroName : _prologueName.Trim();
            if (_prologue == Prologue.Confirm && _prologueChoice == 1)
            {
                SetPrologue(Prologue.Name);
                return;
            }
            if (_prologue == Prologue.Wake && _prologueLine == WakeGreeting && _prologueChoice == 0)
            {
                _prologueLine = WakeTomboyReply + 1;
                return;
            }
            if (_jordaImpactPlaying) return;
            bool blessing = _prologue == Prologue.Blessing;
            if (blessing && TryBranchBlessing()) return;
            int lineCount = blessing ? BlessingPages.Length : PrologueLines?.Length ?? 0;
            if (_prologueLine + 1 < lineCount)
            {
                _prologueLine++;
                if (blessing)
                {
                    if (CurrentBlessingCue == BlessingCue.Impact) StartCoroutine(PlayJordaImpact());
                    if (CurrentBlessingCue == BlessingCue.PitaReceipt) _blessingPitaReceiptShown = true;
                }
                _focusPrologueName = EditingPrologueName;
                _prologueChoice = 0;
                if (_prologue is Prologue.Rain or Prologue.ReturnInRain)
                    PresentRainCast();
                if (_prologue == Prologue.Wake && _prologueLine == 1)
                    BeginPrologueTransition(WakeFromPrologueBed(true, continueWakeDialogue: true), fade: false);
                return;
            }

            switch (_prologue)
            {
                case Prologue.Angel:
                case Prologue.Name: SetPrologue(Prologue.Confirm); break;
                case Prologue.Confirm: SetPrologue(Prologue.Farewell); break;
                case Prologue.Farewell: StartPrologueFilm(Prologue.BirthFilm, 0, Prologue.Birth); break;
                case Prologue.Birth: BeginPrologueTransition(StagePrologueChildhood()); break;
                case Prologue.Childhood: StartPrologueFilm(Prologue.StormFilm, 1, Prologue.Rain); break;
                case Prologue.Rain: StartPrologueFilm(Prologue.TowerFilm, 2, Prologue.ReturnInRain); break;
                case Prologue.ReturnInRain: StartPrologueFilm(Prologue.TimeSkipFilm, 3, Prologue.Wake); break;
                case Prologue.Wake: BeginPrologueTransition(WakeFromPrologueBed(true), fade: false); break;
                case Prologue.Blessing: BeginPrologueTransition(FinishPrologue()); break;
            }
        }

        private void StartPrologueFilm(Prologue phase, int index, Prologue next)
        {
            if (_prologueRoutine != null || _prologue == phase) return;
            SetPrologue(phase);
            _prologueRoutine = StartCoroutine(PlayPrologueFilm(index, next));
        }

        private void BeginPrologueTransition(IEnumerator transition, bool fade = true)
        {
            SetPrologue(Prologue.Transition);
            _prologueRoutine = StartCoroutine(PlayPrologueTransition(transition, fade));
        }

        private IEnumerator PlayPrologueTransition(IEnumerator transition, bool fade)
        {
            if (fade)
            {
                while (_prologueBlack < 1f)
                {
                    _prologueBlack = Mathf.MoveTowards(_prologueBlack, 1f, Time.unscaledDeltaTime * 4f);
                    yield return null;
                }
            }
            yield return transition;
            while (_prologueBlack > 0f)
            {
                _prologueBlack = Mathf.MoveTowards(_prologueBlack, 0f, Time.unscaledDeltaTime * 4f);
                yield return null;
            }
            _prologueRoutine = null;
        }

        private IEnumerator EnterPrologueHome()
        {
            StreamPortrait(WreathBirthPortrait);
            yield return LoadPrologueBedroom();
            if (_prologueHomeLoaded)
            {
                _prologueStanding = _player.transform.position;
                _player.SetActive(false);
                SetPrologueResidentVisible("wreath", false);
                SetPrologueResidentVisible("weedy", false);
                ClearPrologueCast();
                Vector3 bed = CellPosition(HomeLayoutCell(HouseIndex, WakeUpBeds[HouseIndex]));
                _player.transform.position = bed;
                PlacePrologueActor("guy", bed + Vector3.forward * 0.65f + Vector3.right * 0.15f);
                Vector3 mattress = bed + Vector3.up * MattressHeight;
                PlaceBirthSprite("WreathResting", mattress);
                PlaceBirthSprite("Newborn", mattress - Vector3.forward * 0.38f);
                UpdateCameraFollow(snap: true);
            }
            SetPrologue(Prologue.Birth);
        }

        private void PlaceBirthSprite(string name, Vector3 position)
        {
            // WreathResting e Newborn usam spritePixelsToUnits: 96 no importer original (conferido
            // nos .meta antes de mover os arquivos) - precisa bater aqui ou a escala em cena fica errada.
            var sprite = StreamMediaSprite("Title/" + name, 96f);
            if (!sprite) return;
            var pose = new GameObject(name);
            pose.transform.SetParent(_prologueCast.transform, false);
            pose.transform.position = position;
            pose.AddComponent<Rendering.CameraFacingSprite>();
            var renderer = pose.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (name == "WreathResting")
            {
                // The resting sprite uses a centered pivot. Raise it by half its
                // height so her body lies on top of the mattress instead of
                // intersecting the bed geometry.
                pose.transform.position += Vector3.up * sprite.bounds.extents.y;
            }
        }

        private void ClearPrologueCast()
        {
            if (_prologueCast) { _prologueCast.SetActive(false); Destroy(_prologueCast); }
            _prologueCast = new GameObject("Prologue cast");
            _prologueCast.transform.SetParent(transform, false);
            _prologueAunt = null;
            _prologueGrandpa = null;
        }

        private void SetPrologueResidentVisible(string id, bool visible)
        {
            var resident = _townPeople.Find(person => person && person.npcId == id);
            if (resident) resident.gameObject.SetActive(visible);
        }

        private GameObject PlacePrologueActor(string id, Vector3 point)
        {
            var prefab = id == "jorda" && _prologuePriest ? _prologuePriest : FindStoryResidentPrefab(id);
            if (!prefab) return null;
            var actor = Instantiate(prefab, RetailTownActorPosition(point, 0f), Quaternion.identity, _prologueCast.transform);
            foreach (var collider in actor.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (id == "guy")
            {
                var facing = actor.GetComponentInChildren<Rendering.CameraFacingSprite>(true);
                if (facing) facing.FaceMonster(Camera.main ? Camera.main.transform.right : Vector3.right);
            }
            if (id is "youngkoh" or "youngnico")
            {
                var adult = _player ? _player.GetComponentInChildren<SpriteRenderer>(true) : null;
                var child = actor.GetComponentInChildren<SpriteRenderer>();
                if (adult && adult.sprite && child && child.sprite && child.bounds.size.y > 0f)
                    actor.transform.localScale *= adult.bounds.size.y * 0.5f / child.bounds.size.y;
                else actor.transform.localScale *= 0.5f;
            }
            return actor;
        }

        private IEnumerator StagePrologueChildhood()
        {
            ClearPrologueCast();
            if (_prologueHomeLoaded) yield return LeaveInterior();
            _prologueHomeLoaded = false;
            Vector3 home = HomeReturnPosition();
            _player.transform.position = home;
            _player.SetActive(false);
            var youngKoh = PlacePrologueActor("youngkoh", home + Vector3.right * 2f);
            var youngNico = PlacePrologueActor("youngnico", home + Vector3.right * 0.9f + Vector3.forward * 0.75f);
            // Establish their original facing cels before the childhood dialogue.
            youngKoh?.GetComponent<ActorLocomotionView>()?.Present(Vector3.left, 0f, false);
            youngNico?.GetComponent<ActorLocomotionView>()?.Present(Vector3.back, 0f, false);
            PlacePrologueActor("guy", home);
            StreamPortrait(GuyKneelingPortrait);
            UpdateCameraFollow(snap: true);
            SetPrologue(Prologue.Childhood);
            StartCoroutine(PlayPrologueChildrenEntrance(youngKoh, youngNico));
        }

        private IEnumerator PlayPrologueChildrenEntrance(GameObject youngKoh, GameObject youngNico)
        {
            const float duration = 0.42f;
            if (!youngKoh || !youngNico) yield break;

            Vector3 kohEnd = youngKoh.transform.position;
            Vector3 nicoEnd = youngNico.transform.position;
            Vector3 kohStart = kohEnd + Vector3.right * 0.42f;
            Vector3 nicoStart = nicoEnd + Vector3.forward * 0.46f;
            youngKoh.transform.position = kohStart;
            youngNico.transform.position = nicoStart;
            var kohMotion = youngKoh.GetComponent<ActorLocomotionView>();
            var nicoMotion = youngNico.GetComponent<ActorLocomotionView>();
            Vector3 kohDirection = kohEnd - kohStart;
            Vector3 nicoDirection = nicoEnd - nicoStart;

            for (float elapsed = 0f; elapsed < duration && _prologue == Prologue.Childhood; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                if (youngKoh) youngKoh.transform.position = Vector3.Lerp(kohStart, kohEnd, progress);
                if (youngNico) youngNico.transform.position = Vector3.Lerp(nicoStart, nicoEnd, progress);
                kohMotion?.Present(kohDirection, elapsed * 7f, true);
                nicoMotion?.Present(nicoDirection, elapsed * 7f, true);
                yield return null;
            }

            if (youngKoh)
            {
                youngKoh.transform.position = kohEnd;
                kohMotion?.Present(kohDirection, 0f, false);
            }
            if (youngNico)
            {
                youngNico.transform.position = nicoEnd;
                nicoMotion?.Present(nicoDirection, 0f, false);
            }
        }

        private IEnumerator LoadPrologueBedroom()
        {
            if (_prologueHomeLoaded) yield break;
            if (!_prologueWorldReady)
            {
                TownSaveVariables.SetString(HeroNameKey, _prologueName);
                StartPlayableGame();
                _prologueWorldReady = true;
            }
            yield return LoadInterior(CurrentHomeScene(), HomeReturnPosition());
            _prologueHomeLoaded = _interior && HouseIndex >= 0;
            if (_prologueHomeLoaded) _prologueStanding = _player.transform.position;
        }

        private IEnumerator StagePrologueRain()
        {
            yield return LoadPrologueBedroom();
            ClearPrologueCast();
            if (_prologueHomeLoaded)
            {
                _player.SetActive(false);
                SetPrologueResidentVisible("weedy", false);
            }
            FindDialoguePortrait("Aunt");
            FindDialoguePortrait("Grandpa");
            StreamPortrait(WreathPregnantPortrait);
            StreamPortrait(WreathAmuletPortrait);
            SetPrologue(Prologue.Rain);
        }

        private Vector3 RainCastPosition(float x, float z)
        {
            Vector3 point = HouseIndex == 0 ? HomeLayoutPoint(0, new Vector3(x, 0f, z)) :
                _prologueStanding + new Vector3(18.7f - x, 0f, z - 12f);
            return RetailTownActorPosition(point, 0f);
        }

        private Vector3 FrameRainPrologue(Vector3 target) =>
            _prologueHomeLoaded && (_prologue is Prologue.Rain or Prologue.ReturnInRain)
                ? RainCastPosition(18.8f, 12f)
                : target;

        private void PresentRainCast()
        {
            if (!_prologueHomeLoaded || !_prologueCast) return;
            int page = _prologueLine;
            if (_prologue == Prologue.Rain)
            {
                SetRainPose(ref _prologueAunt, "Aunt", page < AuntNoticesNecklace ? "Back" : "Side",
                    RainCastPosition(18.1f, 9.9f), 1.42f);
                SetRainPose(ref _prologueGrandpa, "Grandpa", "Back", RainCastPosition(17.25f, 10.55f), 1.25f);
                return;
            }

            if (page == 0)
            {
                var youngKoh = PlacePrologueActor("youngkoh", RainCastPosition(18.55f, 10.65f));
                youngKoh?.GetComponent<ActorLocomotionView>()?.Present(Vector3.back, 0f, false);
            }
            bool auntFacesWreath = page >= AuntAnswersGrandpa && (page < RainDoorReaction || page >= AuntAfterGuysReturn);
            SetRainPose(ref _prologueAunt, "Aunt", auntFacesWreath ? "Side" : "Front",
                RainCastPosition(17.9f, 11.5f), 1.42f);
            string grandpaPose = page == 0 ? "Side" : page < AuntAnswersGrandpa ? "Back" :
                page < RainDoorReaction ? "Side" : "Front";
            SetRainPose(ref _prologueGrandpa, "Grandpa", grandpaPose, RainCastPosition(20.75f, 12.8f),
                1.25f, flipX: page == 0);
            if (page == RainFamiliarArrival) StageGuysFamiliar();
        }

        // Directional cels isolated from the PS1 prologue capture at 05:55–07:50.
        private void SetRainPose(ref SpriteRenderer actor, string name, string direction, Vector3 position,
            float height, bool flipX = false)
        {
            // Todo o elenco de RainCast usa spritePixelsToUnits: 48 no importer original.
            var sprite = StreamMediaSprite($"Title/RainCast/{name}{direction}", 48f);
            if (!sprite) return;
            if (!actor)
            {
                var view = new GameObject(name);
                view.transform.SetParent(_prologueCast.transform, false);
                view.AddComponent<Rendering.CameraFacingSprite>();
                actor = view.AddComponent<SpriteRenderer>();
            }
            actor.sprite = sprite;
            actor.flipX = flipX;
            actor.transform.position = position;
            actor.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
        }

        private void StageGuysFamiliar()
        {
            if (!_prologueHomeLoaded || !_kewnePrefab) return;
            var familiar = Instantiate(_kewnePrefab, RainCastPosition(18.35f, 14.2f), Quaternion.identity, _prologueCast.transform);
            foreach (var collider in familiar.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        private void StagePrologueWake()
        {
            ClearPrologueCast();
            if (_prologueHomeLoaded)
            {
                var bed = HomeLayoutCell(HouseIndex, WakeUpBeds[HouseIndex]);
                _playerCell = bed;
                _player.transform.position = CellPosition(bed) + Vector3.up * (MattressHeight + TownActorFootOffset);
                _player.SetActive(true);
                SetPrologueResidentVisible("wreath", true);
                SetPrologueResidentVisible("weedy", true);
                _wakingInBed = true;
                _townFacing = Vector2.right;
                if (_kohSprite)
                {
                    _kohSprite.SetFacing(_townFacing);
                    _kohSprite.PresentSleep(true, alreadyLying: true);
                }
                PlacePrologueActor("nico", _player.transform.position + Vector3.right);
                UpdateCameraFollow(snap: true);
            }
            SetPrologue(Prologue.Wake);
        }

        private IEnumerator WakeFromPrologueBed(bool animate, bool continueWakeDialogue = false)
        {
            yield return LoadPrologueBedroom();
            if (_prologueHomeLoaded)
            {
                if (animate && !continueWakeDialogue)
                {
                    StagePrologueBlessing();
                    yield break;
                }
                if (!_wakingInBed) StagePrologueWake();
                SetPrologue(Prologue.Transition);
                var bed = HomeLayoutCell(HouseIndex, WakeUpBeds[HouseIndex]);
                var exit = FindWakeUpCell(bed, MattressHeight);
                var start = _player.transform.position;
                var end = exit.x >= 0 ? RetailTownActorPosition(CellPosition(exit), TownActorFootOffset) : _prologueStanding;
                var nico = animate && _prologueCast && _prologueCast.transform.childCount > 0
                    ? _prologueCast.transform.GetChild(0) : null;
                Vector3 nicoOrigin = nico ? nico.position : Vector3.zero;
                Vector3 strike = nicoOrigin;
                if (nico)
                {
                    strike = Vector3.Lerp(nicoOrigin, start, 0.7f);
                    strike.y = nicoOrigin.y;
                    const float strikeDuration = 0.14f;
                    for (float t = 0f; t < strikeDuration; t += Time.unscaledDeltaTime)
                    {
                        nico.position = Vector3.Lerp(nicoOrigin, strike, t / strikeDuration);
                        yield return null;
                    }
                    nico.position = strike;
                }
                if (animate)
                {
                    PlayRetailSound(RetailSfx.Hit);
                    SpawnFx(_fxMagic, start);
                }
                _wakingInBed = false;
                if (_kohSprite) _kohSprite.PresentSleep(false);
                if (animate)
                {
                    const float riseDuration = 0.35f;
                    for (float t = 0f; t < riseDuration; t += Time.unscaledDeltaTime)
                    {
                        float amount = t / riseDuration;
                        if (nico) nico.position = Vector3.Lerp(strike, nicoOrigin, amount);
                        _player.transform.position = Vector3.Lerp(start, end, amount) + Vector3.up * (0.3f * Mathf.Sin(amount * Mathf.PI));
                        yield return null;
                    }
                    if (nico) nico.position = nicoOrigin;
                }
                _player.transform.position = end;
                _playerCell = exit.x >= 0 ? exit : new Vector2Int(Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.z));
                UpdateCameraFollow(snap: true);
            }
            PlayAreaMusic("Town");
            if (continueWakeDialogue)
            {
                SetPrologue(Prologue.Wake);
                _prologueLine = 1;
            }
            else StagePrologueBlessing();
        }

        private void StagePrologueBlessing()
        {
            if (_prologueHomeLoaded)
            {
                _player.transform.position = _prologueStanding;
                _playerCell = new Vector2Int(Mathf.RoundToInt(_prologueStanding.x), Mathf.RoundToInt(_prologueStanding.z));
                ClearPrologueCast();
                PlacePrologueActor("jorda", _prologueStanding + Vector3.forward * 2f);
                PlacePrologueActor("nico", _prologueStanding + Vector3.forward * 2f + Vector3.right);
                var wreath = _townPeople.Find(person => person && person.npcId == "wreath");
                var weedy = _townPeople.Find(person => person && person.npcId == "weedy");
                if (wreath) wreath.transform.position = RetailTownActorPosition(_prologueStanding + Vector3.left, 0f);
                if (weedy) weedy.transform.position = RetailTownActorPosition(_prologueStanding + Vector3.forward + Vector3.left, 0f);
                UpdateCameraFollow(snap: true);
            }
            SetPrologue(Prologue.Blessing);
        }

        private IEnumerator PlayJordaImpact()
        {
            _jordaImpactPlaying = true;
            _jordaImpactStarted = Time.unscaledTime;
            PlayRetailSound(RetailSfx.Hit);
            if (_kohSprite) _kohSprite.PresentSleep(true);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(2f);
            if (_kohSprite) _kohSprite.PresentSleep(false);
            _jordaImpactPlaying = false;
        }

        private IEnumerator FinishPrologue()
        {
            bool showedPitaReceipt = _prologue == Prologue.Blessing && _blessingPitaReceiptShown;
            if (_prologueCast) { _prologueCast.SetActive(false); Destroy(_prologueCast); }
            if (_prologueHomeLoaded) yield return LeaveInterior();
            _townInventory.AddDungeonItem(ItemSystem.CreateFloorItem(ItemSystem.Id.PitaFruit, ItemSystem.Kind.Fruit, 40));
            TownSaveVariables.SetBool(WreathFruitAvailable, false);
            PersistTownProgress();
            _message = showedPitaReceipt ? string.Empty : T("Pita Fruit obtained.", "Fruta Pita obtida.");
            SetPrologue(Prologue.Off);
        }

        private void DrawJordaImpactStars(float width, float height)
        {
            if (!_jordaImpactPlaying || !_player || Event.current.type != EventType.Repaint) return;
            if (!_jordaImpactStar) _jordaImpactStar = StreamMediaTexture("Title/JordaImpactStar", FilterMode.Point, TextureWrapMode.Clamp);
            var camera = _gameCamera ? _gameCamera : Camera.main;
            if (!_jordaImpactStar || !camera) return;
            Vector3 point = camera.WorldToScreenPoint(_player.transform.position + Vector3.up * 1.3f);
            if (point.z <= 0f) return;
            // Prologue UI uses scaled coordinates, while the projection is in screen pixels.
            float centerX = point.x * width / Screen.width;
            float centerY = height - point.y * height / Screen.height;
            float angle = (Time.unscaledTime - _jordaImpactStarted) * 5f;
            for (int i = 0; i < 3; i++)
            {
                float orbit = angle + i * Mathf.PI * 2f / 3f;
                float x = centerX + Mathf.Cos(orbit) * 25f;
                float y = centerY + Mathf.Sin(orbit) * 9f;
                GUI.DrawTexture(new Rect(x - 9f, y - 9f, 18f, 18f), _jordaImpactStar);
            }
        }

        private void DrawPrologue(float width, float height)
        {
            var screen = new Rect(0f, 0f, width, height);
            if (IsAngelPhase(_prologue))
            {
                DrawHudFill(screen, new Color(0.10f, 0.35f, 0.66f));
                for (int i = 0; Event.current.type == EventType.Repaint && i < 36; i++)
                    DrawHudFill(new Rect(0f, height * i / 36f, width, height / 36f + 1f),
                        Color.Lerp(new Color(0.11f, 0.34f, 0.67f), new Color(0.45f, 0.83f, 0.94f), i / 35f));
                DrawPrologueClouds(screen);
                DrawPrologueAngel(screen);
            }
            else if (_prologue is Prologue.BirthFilm or Prologue.StormFilm or Prologue.TowerFilm or Prologue.TimeSkipFilm)
                DrawHudFill(screen, Color.black);
            if ((_prologue is Prologue.BirthFilm or Prologue.StormFilm or Prologue.TowerFilm or Prologue.TimeSkipFilm) &&
                _prologueFilmTexture && Event.current.type == EventType.Repaint)
                DrawPrologueFilm(screen, _prologueFilmTexture);
            DrawJordaImpactStars(width, height);
            if ((_prologue is Prologue.Rain or Prologue.ReturnInRain) && Event.current.type == EventType.Repaint)
            {
                DrawHudFill(screen, new Color(0.04f, 0.06f, 0.13f, 0.47f));
                for (int i = 0; i < 80; i++)
                {
                    float age = Time.unscaledTime - _prologueStarted;
                    float x = Mathf.Repeat(i * 137.53f - age * 105f, width);
                    float y = Mathf.Repeat(i * 83.21f + age * 380f, height);
                    DrawHudFill(new Rect(x, y, 1f, 15f), new Color(0.55f, 0.65f, 0.85f, 0.55f));
                }

            }

            var line = _prologue == Prologue.Blessing ? CurrentBlessingDialogue() : PrologueLines?[_prologueLine];
            bool editingName = EditingPrologueName;
            var choices = PrologueChoices();
            string name = _prologue <= Prologue.Farewell ? _prologueName : HeroName;
            string body = line.HasValue ? T(line.Value.En, line.Value.Pt).Replace("{name}", name) :
                editingName ? T("Have you decided on a name?", "Já escolheu um nome?") :
                T($"So you will be {_prologueName}. Is that okay?", $"Então seu nome será {_prologueName}. Tudo bem?");
            float controlsHeight = editingName ? 36f : choices.First != null ?
                2f * DialogueChoiceRowHeight + 5f : DialogueChoiceRowHeight + 4f;
            float boxWidth = width * 0.87f;
            float textHeight = _dialogueTextStyle.CalcHeight(new GUIContent(body), boxWidth - 26f);
            float boxHeight = Mathf.Clamp(textHeight + controlsHeight + 18f, height * 0.23f, height * 0.65f);
            var box = new Rect(width * 0.065f, height * 0.95f - boxHeight, boxWidth, boxHeight);
            if (line.HasValue)
            {
                string speaker = line.Value.Speaker;
                var portrait = speaker switch
                {
                    "Nico" when _prologue == Prologue.Childhood => PrologueNicoPortrait(NicoChild),
                    "Nico" when _prologue == Prologue.Wake && _prologueLine == WakeTomboyReply =>
                        PrologueNicoPortrait(NicoPlayful),
                    "Nico" => PrologueNicoPortrait(NicoNeutral),
                    "Guy" when _prologue == Prologue.Childhood && _prologueLine == ChildhoodLines.Length - 1 =>
                        GuyDialoguePortrait(GuyKneelingPortrait),
                    "Jorda" => JordaDialoguePortrait(_prologueStarted),
                    "Wreath" when _prologue == Prologue.Birth => StreamPortrait(WreathBirthPortrait),
                    // TOWN.BIN 0x427B06, 0x427C71, 0x427CD0: the retail
                    // portrait-variant changes for Wreath are 1, 2, then 1.
                    "Wreath" when _prologue == Prologue.Rain && _prologueLine >= BrokenAmuletLine =>
                        StreamPortrait(WreathAmuletPortrait) ?? StreamPortrait(WreathAmuletOriginalPortrait),
                    "Wreath" when _prologue is Prologue.Rain or Prologue.ReturnInRain =>
                        StreamPortrait(WreathPregnantPortrait),
                    _ => FindDialoguePortrait(speaker)
                };
                float portraitScale = speaker == "Wreath" && _prologue == Prologue.Birth ? 72f / 128f : 1f;
                DrawPrologueLine(box, speaker, body, portrait, controlsHeight, portraitScale);
                if (_prologue == Prologue.Childhood && _prologueLine == ChildhoodLines.Length - 1)
                    DrawDialoguePortrait(StreamPortrait("Dialogue/YoungKoh"), box, rightAligned: true, scale: 0.25f);
                if (!editingName && choices.First == null &&
                    GUI.Button(new Rect(box.xMax - 40f, box.yMax - 29f, 32f, 22f), "›"))
                    AdvancePrologue();
            }
            else if (_prologue == Prologue.Name || _prologue == Prologue.Confirm)
            {
                DrawPrologueLine(box, "Angel", body, null, controlsHeight);
            }
            if (editingName)
            {
                float y = box.yMax - 37f;
                GUI.SetNextControlName(HeroNameControl);
                _prologueName = GUI.TextField(new Rect(box.x + 14f, y, 180f, 27f), _prologueName, 12);
                if (_focusPrologueName && Event.current.type == EventType.Repaint)
                {
                    GUI.FocusControl(HeroNameControl);
                    _focusPrologueName = false;
                }
                if (GUI.Button(new Rect(box.xMax - 104f, y, 90f, 27f), T("Confirm", "Confirmar")))
                    AdvancePrologue();
            }
            if (!editingName && choices.First != null)
            {
                var rows = new Rect(box.x + 13f, box.yMax - controlsHeight - 4f, box.width - 26f, controlsHeight);
                int picked = DrawDialogueChoices(rows, _prologueChoice, choices.First, choices.Second);
                if (picked >= 0)
                {
                    _prologueChoice = picked;
                    AdvancePrologue();
                }
            }
            if (_prologue is not (Prologue.Off or Prologue.Transition)) DrawDialogueSkipPrompt(width, _prologueHold / KewneSkipHoldSeconds);
            if (_prologueBlack > 0f) DrawHudFill(screen, new Color(0f, 0f, 0f, _prologueBlack));
        }

        private static void DrawPrologueFilm(Rect screen, Texture texture)
        {
            if (!texture || texture.width <= 0 || texture.height <= 0) return;

            // Fit the actual decoded frame once inside the canvas.  This avoids
            // ScaleMode's platform-specific video texture path, which caused a
            // split/duplicated frame with JO001 on Windows Media Foundation.
            float scale = Mathf.Min(screen.width / texture.width, screen.height / texture.height);
            float width = texture.width * scale;
            float height = texture.height * scale;
            var destination = new Rect(screen.center.x - width * 0.5f,
                screen.center.y - height * 0.5f, width, height);
            GUI.DrawTextureWithTexCoords(destination, texture, new Rect(0f, 0f, 1f, 1f), alphaBlend: false);
        }

        private void DrawPrologueClouds(Rect screen)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!_prologueClouds) _prologueClouds = CreatePrologueCloudTexture();

            float age = Time.unscaledTime - _prologueAngelStarted;
            float aspect = screen.width / screen.height;
            Color previousColor = GUI.color;
            GUI.color = new Color(0.62f, 0.81f, 1f, 0.56f * previousColor.a);
            GUI.DrawTextureWithTexCoords(screen, _prologueClouds,
                new Rect(0.22f, Mathf.Repeat(0.31f - age * 0.026f, 1f), aspect * 0.86f, 1.18f));
            GUI.color = new Color(0.84f, 0.96f, 1f, 0.72f * previousColor.a);
            GUI.DrawTextureWithTexCoords(screen, _prologueClouds,
                new Rect(0.61f, Mathf.Repeat(0.74f - age * 0.041f, 1f), aspect * 0.59f, 0.91f));
            GUI.color = previousColor;
        }

        private static Texture2D CreatePrologueCloudTexture()
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float noise = TiledCloudNoise(u, v, 4f) * 0.66f +
                    TiledCloudNoise(u, v, 9f) * 0.25f + TiledCloudNoise(u, v, 18f) * 0.09f;
                float opacity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.41f, 0.68f, noise));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(opacity * 255f));
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Prologue Clouds",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static float TiledCloudNoise(float u, float v, float scale)
        {
            float x = 23f + u * scale;
            float y = 39f + v * scale;
            float blendX = Mathf.SmoothStep(0f, 1f, u);
            float blendY = Mathf.SmoothStep(0f, 1f, v);
            float bottom = Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - scale, y), blendX);
            float top = Mathf.Lerp(Mathf.PerlinNoise(x, y - scale), Mathf.PerlinNoise(x - scale, y - scale), blendX);
            return Mathf.Lerp(bottom, top, blendY);
        }

        private void DrawPrologueAngel(Rect screen)
        {
            if (Event.current.type != EventType.Repaint) return;
            bool angelWasMissing = !_prologueAngel;
            if (!_prologueAngel) _prologueAngel = StreamMediaTexture("Title/AngelOpeningFrames", FilterMode.Bilinear, TextureWrapMode.Repeat);
            if (!_prologueAngel) return;
            if (angelWasMissing) _prologueAngelStarted = Time.unscaledTime; // reinicia o timer so quando a textura chega (streaming assincrono)

            const int frameCount = 10;
            const float flapFramesPerSecond = 12f;
            const float hoverPeriod = 1.3f;
            float age = Time.unscaledTime - _prologueAngelStarted;
            int frame = Mathf.FloorToInt(age * flapFramesPerSecond) % frameCount;
            float frameUvWidth = 1f / frameCount;
            float frameAspect = _prologueAngel.width * frameUvWidth / _prologueAngel.height;
            float height = Mathf.Min(screen.height * 0.42f, screen.width * 0.65f / frameAspect);
            float width = height * frameAspect;
            float hover = Mathf.Sin(age * (2f * Mathf.PI / hoverPeriod)) * screen.height * 0.042f;
            var bounds = new Rect(screen.center.x - width * 0.5f,
                screen.height * 0.37f - height * 0.5f + hover, width, height);
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(bounds, _prologueAngel,
                new Rect(frame * frameUvWidth, 0f, frameUvWidth, 1f), alphaBlend: true);
            GUI.color = previousColor;
        }

        private void DrawPrologueLine(Rect box, string speaker, string line, Texture2D portrait, float controlsHeight, float portraitScale = 1f)
        {
            DrawDialoguePortrait(portrait, box, scale: portraitScale);
            if (speaker == "Jorda") DrawJordaBlink(portrait, box, _prologueStarted);
            DrawDialogueFrame(box, dialoguePole: true);
            var name = new Rect(box.x, box.y - 28f, 150f, 27f);
            if (!string.IsNullOrEmpty(speaker))
            {
                DrawDialogueFrame(name);
                DrawHudText(new Rect(name.x + 8f, name.y, name.width - 16f, name.height), T(speaker, speaker switch { "Angel" => "Anjo", "Aunt" => "Tia", "Grandpa" => "Vovô", _ => speaker }), _messageStyle, dialogue: true);
            }
            DrawDialogueText(new Rect(box.x + 13f, box.y + 8f, box.width - 26f, box.height - 16f - controlsHeight), line, line);
        }
    }
}
