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
        private Texture2D _prologueAngel;
        private float _prologueAngelStarted;
        private Texture2D _prologueClouds;
        private const string AngelMusicCue = "AngelSong";
        [SerializeField] private GameObject _prologuePriest;

        private const string HeroNameKey = "opening.hero_name";
        private const string DefaultHeroName = "Koh";
        private const int AngelNameLine = 2;
        private const int BrokenAmuletLine = 5;
        private const string BrokenAmuletPortrait = "Dialogue/WreathBrokenAmulet";
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
        private Coroutine _prologueRoutine;
        private GameObject _prologueCast;
        private bool _prologueWorldReady;
        private bool _prologueHomeLoaded;
        private int _prologueChoice;
        private Vector3 _prologueStanding;

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
            ("Guy", "Wreath! You did well! He's a boy!", "Wreath! Você foi muito bem! É um menino!"),
            ("Guy", "Ha ha, he looks just like me.", "Ha, ha! Ele é a minha cara."),
            ("Wreath", "Have you thought of a name?", "Você já pensou em um nome?"),
            ("Guy", "Let's see… How does Koh sound?", "Deixe-me pensar… Que tal Koh?"),
            ("Wreath", "Koh… no. I feel there is a name that better fits him.", "Koh… não. Sinto que outro nome combina melhor com ele."),
            ("Guy", "I've got it! {name}! How's that!?", "Já sei! {name}! O que acha?"),
            ("Wreath", "That's good! His name is {name}.", "Que bom! O nome dele é {name}."),
            ("Wreath", "It's as if God had already picked that name for him a long time ago!", "É como se Deus já tivesse escolhido esse nome para ele há muito tempo!")
        };

        private static readonly (string Speaker, string En, string Pt)[] ChildhoodLines =
        {
            ("Nico", "{name}'s Dad!", "Pai do {name}!"),
            ("Guy", "Yeah, what's the matter Nico?", "Sim, o que foi, Nico?"),
            ("Nico", "Sob! {name} is teasing me.", "Snif! O {name} está me provocando."),
            ("Guy", "Listen, {name}. A man must take good care of women.", "Escute, {name}. Um homem deve tratar bem as mulheres.")
        };

        private static readonly (string Speaker, string En, string Pt)[] RainLines =
        {
            ("Aunt", "I wonder if Guy made it to the top level?", "Será que Guy chegou ao último andar?"),
            ("Wreath", "What? Oh… yes.", "O quê? Ah… sim."),
            ("Aunt", "Guy's the only one that could possibly make it to the top.", "Guy é o único que conseguiria chegar ao topo."),
            ("Wreath", "Guy… Huh!?", "Guy… Hã!?"),
            ("Aunt", "What's the matter?", "O que houve?"),
            ("Wreath", "My necklace… it broke…", "Meu colar… ele quebrou…")
        };

        private static readonly (string Speaker, string En, string Pt)[] ReturnInRainLines =
        {
            ("Grandpa", "It's been decades since it rained like this in this desert city of Monsbaiya.", "Faz décadas que não chove assim em Monsbaiya, nossa cidade no deserto."),
            ("Grandpa", "Wreath is one tough woman.", "Wreath é uma mulher muito forte."),
            ("Grandpa", "She must be worried sick about those strange occurrences the day before yesterday, at that ancient tower Guy went to.", "Ela deve estar morrendo de preocupação com as coisas estranhas que aconteceram anteontem naquela torre antiga para onde Guy foi."),
            ("Aunt", "Don't be silly, Grandpa. Wreath is a woman, after all!", "Não diga bobagem, vovô. Wreath é uma mulher, afinal!"),
            ("Aunt", "Can't you see that she is worried out of her mind about her husband, that it's just enough to keep those tears back!?", "Não vê que ela está tão preocupada com o marido que mal consegue conter as lágrimas!?"),
            ("Wreath", "A… Guy?", "A… Guy?"),
            ("Aunt", "…you're…", "…você é…"),
            ("Grandpa", "It's Guy's Familiar! He's got Guy's Monster Tamer's Collar on him!", "É o familiar do Guy! Ele está com o colar de domador do Guy!"),
            ("Grandpa", "Why is a familiar with Guy's collar here with no sign of Guy!", "Por que o familiar está aqui com o colar, e nada do Guy?"),
            ("Wreath", "Aaaa, … Guy!", "Aaah… Guy!"),
            ("Aunt", "Guy never did return, did he?", "Guy nunca mais voltou, não é?"),
            ("Aunt", "Coming to this city, you had accumulated numerous familiars, and the future was looking so bright.", "Quando vieram para esta cidade, vocês já tinham reunido muitos familiares e o futuro parecia tão promissor."),
            ("Aunt", "Now you're pregnant with your second child.", "Agora você está grávida do seu segundo filho."),
            ("Wreath", "…I was prepared that this might happen someday.", "…Eu estava preparada para a possibilidade de isso acontecer algum dia.")
        };

        private static readonly (string Speaker, string En, string Pt)[] WakeLines =
        {
            ("Nico", "Get up!", "Levante!"),
            ("", "Pow!", "Paf!"),
            ("Nico", "Today is your fifteenth birthday. It's the day you've become an adult!", "Hoje é seu aniversário de quinze anos. É o dia em que você se torna adulto!"),
            ("Nico", "I can't help my tomboy attitude, so there!", "Não consigo evitar esse meu jeito, e pronto!"),
            ("Nico", "The priest is already here! Get up and greet him, hurry!", "O padre já chegou! Levante e vá cumprimentá-lo, depressa!")
        };

        private const int BlessingOffer = 13;
        private const int BlessingLesson = 14;
        private const int BlessingCheck = 23;
        private const int BlessingFarewell = 24;
        private const int BlessingTowerChoice = 30;

        private static readonly (string Speaker, string En, string Pt)[] BlessingLines =
        {
            ("Wreath", "Thanks again for waking him up every morning…", "Obrigada mais uma vez por acordá-lo todas as manhãs…"),
            ("Nico", "Don't mention it. He will never get up until I get a good kick into him.", "De nada. Ele nunca levanta até eu dar um bom chute nele."),
            ("Jorda", "Ho, ho, ho, ho. You've grown up considerably since I last saw you.", "Ho, ho, ho! Você cresceu bastante desde a última vez que o vi."),
            ("Jorda", "Cause you never come to the temple at all, do you?", "É que você nunca aparece no templo, não é?"),
            ("Jorda", "I hear from Wreath that you want to go to the monster tower.", "Wreath me disse que você quer ir à Torre dos Monstros."),
            ("Jorda", "You're fifteen today. You're old enough now to go to the tower.", "Você faz quinze anos hoje. Já tem idade para entrar na torre."),
            ("Jorda", "Here, just a moment. I've got a prayer for you.", "Espere só um momento. Tenho uma oração para você."),
            ("Jorda", "Mumble, mumble… Mumble, mumble…", "Resmungo, resmungo… Resmungo, resmungo…"),
            ("Jorda", "There.", "Pronto."),
            ("", "Bonk!", "Toc!"),
            ("Jorda", "There you go. We're done.", "Pronto. Terminamos."),
            ("Jorda", "What's that you say… You look as if you want to go to the tower right this moment.", "Como é que é… Parece que você quer ir à torre agora mesmo."),
            ("Jorda", "But… the tower is dangerous.", "Mas… a torre é perigosa."),
            ("Jorda", "Do you want me to give you information on the specifics of the tower?", "Quer que eu explique como funciona a torre?"),
            ("Jorda", "I'm going to teach it to you thoroughly, so listen up.", "Vou explicar tudo direitinho, então preste atenção."),
            ("Jorda", "When entering the dungeon of the tower, you'd be able to obtain rare items and monster eggs.", "Nas masmorras da torre, você pode encontrar itens raros e ovos de monstros."),
            ("Jorda", "If you bring that back with you, you'd be able to exchange these for money.", "Se trouxer tudo isso de volta, poderá vender por dinheiro."),
            ("Jorda", "But the tower is a monster nest. Danger will always follow you.", "Mas a torre é um ninho de monstros. O perigo vai acompanhar você a cada passo."),
            ("Jorda", "Whether or not you can survive the dungeon all depends on your ability to control the monsters.", "Sobreviver na masmorra depende da sua habilidade de controlar os monstros."),
            ("Jorda", "You should be able to have monster eggs hatch, and train the young monsters to fight for you.", "Você pode chocar ovos de monstros e treinar os filhotes para lutar ao seu lado."),
            ("Jorda", "There are three secrets to the tower. There is a spell cast on the inner structure so that the interior will change.", "A torre guarda três segredos. Um feitiço lançado sobre a estrutura faz o interior mudar."),
            ("Jorda", "There is a spell cast at the entrance to reduce the power of human beings. That's why everyone starts at the first level each and every time.", "Um feitiço na entrada reduz a força dos humanos. Por isso todo mundo sempre começa no primeiro andar."),
            ("Jorda", "If you try to enter with too many items, the entrance of the tower will close, not allowing you to enter.", "Se tentar entrar com itens demais, a entrada da torre vai se fechar e barrar a passagem."),
            ("Jorda", "Okay, do you understand to this point?", "Tudo bem. Entendeu até aqui?"),
            ("Jorda", "Hmm… Even your stubbornness is just like Guy.", "Hmm… Até na teimosia você é igual ao Guy."),
            ("Jorda", "Listen, {name}. Everything I told you, I heard from Guy.", "Escute, {name}. Tudo o que contei a você eu ouvi do Guy."),
            ("Jorda", "This is a magic collar, that used to belong to your father.", "Este é um colar mágico que pertenceu ao seu pai."),
            ("Jorda", "Any tamed monster with this around its neck will come to help you wherever you are in the tower.", "Qualquer monstro domado que usar este colar vai vir ajudar você onde quer que esteja na torre."),
            ("Jorda", "When you find an egg, and it hatches, put this on the monster.", "Quando encontrar um ovo e ele chocar, coloque o colar no monstro."),
            ("Weedy", "That's great news. That means that now you're able to enter that tower you always wanted to go to.", "Que notícia boa! Agora você pode entrar naquela torre que sempre quis visitar."),
            ("Wreath", "Are you heading out for the tower already?", "Já vai sair para a torre?"),
            ("Wreath", "Then take this with you.", "Então leve isto com você."),
            ("Wreath", "Go do your best!", "Dê o seu melhor!"),
            ("Nico", "Don't stumble!", "Cuidado para não tropeçar!")
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
            Prologue.Blessing => BlessingLines,
            _ => null
        };

        private static bool IsAngelPhase(Prologue phase) =>
            phase is >= Prologue.Angel and <= Prologue.Farewell;

        private void SetPrologue(Prologue phase)
        {
            if (IsAngelPhase(_prologue) && !IsAngelPhase(phase)) ReleaseAngelPresentation();
            _prologue = phase;
            _focusPrologueName = phase == Prologue.Name;
            _prologueLine = 0;
            _prologueChoice = 0;
            _prologueStarted = Time.unscaledTime;
        }

        private void StartPrologue()
        {
            _prologueWorldReady = false;
            _prologueHomeLoaded = false;
            _prologueName = DefaultHeroName;
            _prologueSkipNeedsRelease = false;
            _prologueHold = 0f;
            _prologueBlack = 0f;
            if (!_prologueAngel) _prologueAngel = Resources.Load<Texture2D>("Title/AngelOpeningFrames");
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
            if (!_prologuePlayer) return;
            _prologuePlayer.Stop();
            Destroy(_prologuePlayer);
            _prologuePlayer = null;
        }

        private bool HandlePrologueInput()
        {
            if (_prologue == Prologue.Off) return _prologueBlack > 0f;
            bool held = Application.isFocused && _mapAction?.IsPressed() == true;
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
                (keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame ||
                 keys.upArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame))
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
            if (_prologue == Prologue.Wake && _prologueLine == 2)
                return (T("Good morning, Nico.", "Bom dia, Nico."), T("Can't you be a bit more gentle?", "Não dá para ter um pouco mais de cuidado?"));
            if (_prologue == Prologue.Blessing)
                return _prologueLine switch
                {
                    9 => (T("Hey, that hurts!", "Ei, isso dói!"), T("I've gotta be patient.", "Preciso ter paciência.")),
                    BlessingOffer => (T("Yes.", "Sim."), T("I don't need you to tell me.", "Não precisa me explicar.")),
                    BlessingCheck => (T("Yes.", "Sim."), T("Not at all.", "Não entendi nada.")),
                    BlessingTowerChoice => (T("I’m going.", "Já vou."), T("Of course.", "É claro.")),
                    _ => default
                };
            return default;
        }

        private void AdvancePrologue()
        {
            if (_prologueHold > 0f || _prologueBlack > 0f) return;
            if (EditingPrologueName)
                _prologueName = string.IsNullOrWhiteSpace(_prologueName) ? DefaultHeroName : _prologueName.Trim();
            if (_prologue == Prologue.Confirm && _prologueChoice == 1)
            {
                SetPrologue(Prologue.Name);
                return;
            }
            if (_prologue == Prologue.Wake && _prologueLine == 2 && _prologueChoice == 0)
            {
                _prologueLine = 4;
                return;
            }
            if (_prologue == Prologue.Blessing && _prologueChoice == 1)
            {
                if (_prologueLine == BlessingOffer) { _prologueLine = BlessingFarewell; return; }
                if (_prologueLine == BlessingCheck) { _prologueLine = BlessingLesson; return; }
            }
            var lines = PrologueLines;
            if (lines != null && _prologueLine + 1 < lines.Length)
            {
                _prologueLine++;
                _focusPrologueName = EditingPrologueName;
                _prologueChoice = 0;
                if (_prologue == Prologue.ReturnInRain && _prologueLine == 5)
                    StageGuysFamiliar();
                if (_prologue == Prologue.Blessing && _prologueLine == 9 && _player)
                    SpawnFx(_fxMagic, _player.transform.position);
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
            yield return LoadPrologueBedroom();
            if (_prologueHomeLoaded)
            {
                _prologueStanding = _player.transform.position;
                _player.SetActive(false);
                SetPrologueResidentVisible("wreath", false);
                SetPrologueResidentVisible("weedy", false);
                ClearPrologueCast();
                Vector3 bed = CellPosition(WakeUpBeds[HouseIndex]);
                _player.transform.position = bed;
                PlacePrologueActor("guy", bed + Vector3.forward * 0.65f + Vector3.right * 0.45f);
                Vector3 mattress = bed + Vector3.up * MattressHeight;
                PlaceBirthSprite("WreathResting", mattress);
                PlaceBirthSprite("Newborn", mattress - Vector3.forward * 0.38f);
                UpdateCameraFollow(snap: true);
            }
            SetPrologue(Prologue.Birth);
        }

        private void PlaceBirthSprite(string name, Vector3 position)
        {
            var sprite = Resources.Load<Sprite>("Title/" + name);
            if (!sprite) return;
            var pose = new GameObject(name);
            pose.transform.SetParent(_prologueCast.transform, false);
            pose.transform.position = position;
            pose.AddComponent<Rendering.CameraFacingSprite>();
            pose.AddComponent<SpriteRenderer>().sprite = sprite;
        }

        private void ClearPrologueCast()
        {
            if (_prologueCast) { _prologueCast.SetActive(false); Destroy(_prologueCast); }
            _prologueCast = new GameObject("Prologue cast");
            _prologueCast.transform.SetParent(transform, false);
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
            PlacePrologueActor("youngkoh", home + Vector3.right * 2f);
            PlacePrologueActor("youngnico", home + Vector3.right);
            PlacePrologueActor("guy", home);
            UpdateCameraFollow(snap: true);
            SetPrologue(Prologue.Childhood);
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
                PlacePrologueActor("youngkoh", _prologueStanding + Vector3.forward * 2f);
                PlaceBirthSprite("AuntRain", RetailTownActorPosition(_prologueStanding + new Vector3(1.1f, 0f, -1.3f), 0f));
                PlaceBirthSprite("GrandpaRain", RetailTownActorPosition(_prologueStanding + new Vector3(-1.0f, 0f, -1.6f), 0f));
                UpdateCameraFollow(snap: true);
            }
            StreamPortrait(BrokenAmuletPortrait);
            SetPrologue(Prologue.Rain);
        }

        private void StageGuysFamiliar()
        {
            if (!_prologueHomeLoaded || !_kewnePrefab) return;
            var familiar = Instantiate(_kewnePrefab, _prologueStanding, Quaternion.identity, _prologueCast.transform);
            foreach (var collider in familiar.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        private void StagePrologueWake()
        {
            ClearPrologueCast();
            if (_prologueHomeLoaded)
            {
                var bed = WakeUpBeds[HouseIndex];
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
                var bed = WakeUpBeds[HouseIndex];
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

        private IEnumerator FinishPrologue()
        {
            if (_prologueCast) { _prologueCast.SetActive(false); Destroy(_prologueCast); }
            if (_prologueHomeLoaded) yield return LeaveInterior();
            _townInventory.AddDungeonItem(ItemSystem.CreateFloorItem(ItemSystem.Id.PitaFruit, ItemSystem.Kind.Fruit, 40));
            TownSaveVariables.SetBool(WreathFruitAvailable, false);
            PersistTownProgress();
            _message = T("Pita Fruit obtained.", "Fruta Pita obtida.");
            SetPrologue(Prologue.Off);
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
            if (_prologuePlayer && _prologuePlayer.texture)
                GUI.DrawTexture(screen, _prologuePlayer.texture, ScaleMode.ScaleToFit);
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

            var box = new Rect(width * 0.065f, height * 0.72f, width * 0.87f, height * 0.23f);
            var lines = PrologueLines;
            bool editingName = EditingPrologueName;
            if (lines != null)
            {
                var (speaker, en, pt) = lines[_prologueLine];
                string name = _prologue <= Prologue.Farewell ? _prologueName : HeroName;
                var portrait = speaker switch
                {
                    "Nico" when _prologue == Prologue.Childhood => StreamPortrait("Dialogue/YoungNico"),
                    "Wreath" when _prologue == Prologue.Birth => StreamPortrait("Dialogue/Wreath_variant3_frame0"),
                    "Wreath" when _prologue == Prologue.Rain && _prologueLine == BrokenAmuletLine =>
                        StreamPortrait(BrokenAmuletPortrait) ?? FindDialoguePortrait(speaker),
                    _ => FindDialoguePortrait(speaker)
                };
                DrawPrologueLine(box, speaker, T(en, pt).Replace("{name}", name), portrait);
                if (_prologue == Prologue.Childhood && _prologueLine == ChildhoodLines.Length - 1)
                    DrawDialoguePortrait(StreamPortrait("Dialogue/YoungKoh"), box, rightAligned: true);
                if (!editingName && PrologueChoices().First == null &&
                    GUI.Button(new Rect(box.xMax - 40f, box.yMax - 29f, 32f, 22f), "▸"))
                    AdvancePrologue();
            }
            else if (_prologue == Prologue.Name || _prologue == Prologue.Confirm)
            {
                DrawPrologueLine(box, "Angel", editingName ? T("Have you decided on a name?", "Já escolheu um nome?") :
                    T($"So you will be {_prologueName}. Is that okay?", $"Então seu nome será {_prologueName}. Tudo bem?"), null);
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
            var choices = PrologueChoices();
            if (choices.First != null)
            {
                float column = (box.width - 28f) * 0.5f;
                float y = box.yMax - 32f;
                if (GUI.Button(new Rect(box.x + 12f, y, column, 23f), (_prologueChoice == 0 ? "▸ " : "") + choices.First))
                { _prologueChoice = 0; AdvancePrologue(); }
                if (GUI.Button(new Rect(box.x + 16f + column, y, column, 23f), (_prologueChoice == 1 ? "▸ " : "") + choices.Second))
                { _prologueChoice = 1; AdvancePrologue(); }
            }
            if (_prologue is not (Prologue.Off or Prologue.Transition)) DrawDialogueSkipPrompt(width, _prologueHold / KewneSkipHoldSeconds);
            if (_prologueBlack > 0f) DrawHudFill(screen, new Color(0f, 0f, 0f, _prologueBlack));
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
            if (!_prologueAngel || Event.current.type != EventType.Repaint) return;

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
            GUI.DrawTextureWithTexCoords(bounds, _prologueAngel, new Rect(frame * frameUvWidth, 0f, frameUvWidth, 1f));
        }

        private void DrawPrologueLine(Rect box, string speaker, string line, Texture2D portrait)
        {
            DrawDialoguePortrait(portrait, box);
            DrawDialogueFrame(box, dialoguePole: true);
            var name = new Rect(box.x, box.y - 28f, 150f, 27f);
            if (!string.IsNullOrEmpty(speaker))
            {
                DrawDialogueFrame(name);
                DrawHudText(new Rect(name.x + 8f, name.y, name.width - 16f, name.height), T(speaker, speaker switch { "Angel" => "Anjo", "Aunt" => "Tia", "Grandpa" => "Vovô", _ => speaker }), _messageStyle);
            }
            DrawHudText(new Rect(box.x + 13f, box.y + 8f, box.width - 26f, box.height - 16f), line, _messageStyle);
        }
    }
}
