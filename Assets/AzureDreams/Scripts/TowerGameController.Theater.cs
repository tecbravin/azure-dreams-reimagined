using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum TheaterAct { None, Dance, Comedy, Results }
        private enum ComedyAnswer { Agree, TalkBack, Either }
        private enum ComedyBeat { Curtain, FonLine, KohReply, Laugh, Scold, Bonk, Next, Bow, CurtainClose }

        private TheaterAct _theaterAct;
        private int _comedyLine, _comedyMistakes;
        private float _theaterStarted, _comedyNextAt;
        private NPCController _theaterHost, _theaterPerformer;
        private Vector3 _theaterHome, _theaterStage, _theaterPlayerHome;
        private Vector2 _theaterPlayerFacing;
        private Action _afterTheaterDance;
        private const string TheaterMusicCue = "Theatre.mp3";
        private const string VivianDanceMusicCue = "DanceAtPub.mp3";
        private const string ComedyMusicCue = "EntranceOfComedians.mp3";
        private const string ComedyExitMusicCue = "ExitComedians.mp3";
        private bool _theaterOnStage;
        private bool _theaterAnswerReady;

        // Comedy staging state.
        private ComedyBeat _comedyBeat;
        private float _comedyBeatStarted, _comedyBeatLength;
        private string _comedySpeaker, _comedyText;
        private bool _comedySlap;
        private Vector3 _comedyKohBase, _comedyFonBase, _comedyKohScale;
        private Texture2D _spotlightTexture, _impactTexture;
        private Vector3 _comedyImpactPoint;
        private float _comedyImpactAt = -10f, _comedyLaughAt = -10f;
        private bool _comedyAnswerWasTalkBack, _comedyAnswerTimedOut;

        // TOWN.BIN 0x4B7D8A-0x4B9238 (show script).  × agrees, ○ talks back;
        // a wrong agree runs 0x8001C129, a wrong talk-back 0x8001C1E5 and no
        // answer 0x8001C29B (each ends in a bonk).  "Laugh" lines play scene
        // selector 0x701 (audience laughter) after Koh's reply.
        private static readonly (string En, string Pt, ComedyAnswer Answer,
            string AgreeEn, string AgreePt, string TalkEn, string TalkPt, bool Laugh)[] ComedyLines =
        {
            ("Hi everyone!\nWe’re Fon and Dodo! I’m Fon! And this is the Poet.",
                "Olá, pessoal!\nSomos Fon e Dodo! Eu sou Fon! E este é o Poeta.", ComedyAnswer.TalkBack,
                "[What is your name?\nAnd your blood type?]", "[Qual é o seu nome?\nE o seu tipo sanguíneo?]",
                "[Hold on!  My name’s Dodo!]", "[Peraí! Meu nome é Dodo!]", true),
            ("Yes! Well, as you can see, we’ve been given the opportunity to stand up here to entertain you!\nDoes everyone here from the town of Monsbaiya know what a performer does?",
                "Sim! Como podem ver, tivemos a oportunidade de subir aqui para divertir vocês!\nTodo mundo aqui em Monsbaiya sabe o que faz um artista?", ComedyAnswer.Agree,
                "[Well, do you know?]", "[E aí, vocês sabem?]", null, null, false),
            ("Ah, we’re the ones behind the meat counter, separating the sirloin from the bone; grinding, cutting, or stuffing sausages to the customer’s liking, at your local meat store.",
                "Ah, somos quem fica atrás do balcão do açougue, desossando o contrafilé e moendo, cortando ou enchendo linguiças ao gosto do freguês.", ComedyAnswer.TalkBack,
                null, null, "[No, that would be a Butcher!]", "[Não, isso aí é açougueiro!]", true),
            ("We’ve got the best pepperoni in town!\nCan I see a show of hands to see how many want pepperoni?",
                "Temos o melhor pepperoni da cidade!\nQuem quer pepperoni? Levante a mão!", ComedyAnswer.TalkBack,
                null, null, "[I’m telling ya!  That’s a Butcher!\nThe subject is Performer!  Performer!]",
                "[Estou dizendo! Isso é açougueiro!\nO assunto é Artista! Artista!]", true),
            ("Performer!?\nYou mean...\nYou and I are people that perform!?", "Artista!?\nQuer dizer...\nQue eu e você fazemos apresentações!?",
                ComedyAnswer.Agree, "[That’s right!]", "[Isso mesmo!]", null, null, false),
            ("Perform, right?\nIt’s a good smelling word for the things we do.", "Apresentar, certo?\nÉ uma palavra muito cheirosa para o que fazemos.",
                ComedyAnswer.TalkBack, null, null,
                "[Close but yet so way way off.  That’s Perfume!\nWe’re PERFORMERS, here to PER-FORM!]",
                "[Quase, mas muito longe. Isso é Perfume!\nSomos ARTISTAS, aqui para nos APRE-SENTAR!]", true),
            ("Fur-Worm?\nThose are the hairy worms that, ah, eat strawberries right?", "Lagarta peluda?\nDaquelas que, ah, comem morangos, certo?",
                ComedyAnswer.TalkBack, null, null, "[What in the world are you\ntalking about?]", "[Do que é que você\nestá falando?]", true),
            ("Fur-worm dolls!  A tradition of Monsbaiya!\nOh, by the way, a few years ago, on my way home from buying a hamburger for a dime, I saw a bunch of furry caterpillars hanging from a tree!",
                "Bonecas de lagarta peluda! Uma tradição de Monsbaiya!\nAh, a propósito, faz uns anos eu voltava para casa depois de comprar um hambúrguer por dez centavos e vi um monte de lagartas peludas penduradas numa árvore!",
                ComedyAnswer.TalkBack, null, null,
                "[What are you talking about!  You’re going way off the topic!\nAnd a burger cost a dime a few years ago?  Exactly how long ago?]",
                "[Do que você está falando! Está fugindo do assunto!\nE um hambúrguer custava dez centavos faz uns anos? Faz quanto tempo?]", true),
            ("Calm down.\nSo, now do you understand what it is to be a performer?", "Calma.\nEntão, agora você entendeu o que é ser artista?",
                ComedyAnswer.Either, "[Yes, but you’re the one\nconfusing the audience!]", "[Sim, mas é você quem está\nconfundindo a plateia!]",
                "[!!  You’re the one\nconfusing the audience!]", "[!! É você quem está\nconfundindo a plateia!]", true),
            ("Yes, we’re performers!\nIn particular, we’re stand up comedians, and we’re here to provide you with laughter.",
                "Sim, somos artistas!\nMais precisamente, somos comediantes e estamos aqui para fazer vocês rirem.", ComedyAnswer.Agree,
                "[That’s right!]", "[Isso mesmo!]", null, null, false),
            ("!!\nDon’t laugh!\nYou guys looked at us and laughed, didn’t you!", "!!\nNão riam!\nVocês olharam para nós e riram, não foi!",
                ComedyAnswer.TalkBack, null, null, "[Hey, hey, come on!  What’s\ngotten into you!?]", "[Ei, ei, calma aí! O que\ndeu em você!?]", true),
            ("Don’t stop me!\nDoesn’t it bother you?\nThese guys are laughing at us!", "Não me segure!\nIsso não incomoda você?\nEstão rindo da gente!",
                ComedyAnswer.Agree, "[That’s a good thing.\nAs comedians, to keep ’em laughing is the only way we’re going to survive!]",
                "[Isso é bom.\nComo comediantes, fazer eles rirem é o único jeito de sobreviver!]", null, null, true),
            ("Fur-Worm?", "Lagarta peluda?", ComedyAnswer.TalkBack, null, null, "[I give up!]", "[Eu desisto!]", true)
        };

        private bool TheaterService(NPCController npc)
        {
            if (StoryRoom != TheaterRoom) return false;
            if (npc.npcId == "theater_fon")
                return FonComedyInvitation(npc);
            if (npc.npcId == "vivian")
                return VivianAsk(npc, 1, "Did you see me on stage?", "Você me viu no palco?",
                    (T("Watch the dance", "Ver a dança"), () => WatchVivian(npc)), (T("Later", "Depois"), null));
            return false;
        }

        // TOWN.BIN 0x4B6F46–0x4B7C0B and 0x4B9BCD–0x4B9FDF.
        private bool FonComedyInvitation(NPCController npc)
        {
            bool played = TownSaveVariables.GetBool(StoryPrefix + "fon.comedyPlayed");
            return FonTalk(npc, "You’ve come at the perfect time!  {name}!", "Você chegou na hora certa! {name}!",
                () =>
                {
                    if (played)
                    {
                        FonTalkSequence(npc, () => FonAsk(npc, "Could you please be my partner again?\nI beg you!",
                                "Por favor, seja meu parceiro de novo!\nEu imploro!",
                                (T("No problem, I’ll do it", "Sem problema, eu topo"), () =>
                                    FonTalk(npc, "Really?\nGreat!\nWell, let’s go then, Dodo!", "Sério?\nÓtimo!\nEntão vamos, Dodo!", () => StartFonComedy(npc))),
                                (T("No way, not again", "De jeito nenhum, outra vez não"), () =>
                                    FonTalk(npc, "I didn’t realize you were so cold hearted!",
                                        "Eu não sabia que você tinha um coração tão frio!")),
                                (T("Watch Vivian dance", "Ver Vivian dançar"), () => WatchVivian(npc))),
                            new FonStoryLine("Could you help?  We’ve got a problem again!", "Pode ajudar? Estamos com um problema de novo!"),
                            new FonStoryLine("I still can’t find a new partner.\nSo you, {name}! Or Mr. {name}, sir!",
                                "Ainda não consegui achar um parceiro novo.\nEntão você, {name}! Ou melhor, senhor {name}!"));
                        return;
                    }
                    FonTalkSequence(npc, () => FonAsk(npc, "I’m counting on you, Dodo!", "Estou contando com você, Dodo!",
                            (T("Who’s Dodo!?", "Quem é Dodo!?"), () => FonComedyInstructions(npc)),
                            (T("Watch Vivian dance", "Ver Vivian dançar"), () => WatchVivian(npc))),
                        new FonStoryLine("Could you help?  We’ve got a problem!", "Pode ajudar? Estamos com um problema!"),
                        new FonStoryLine("My stand-up partner ran away! So voila!  {name}!  You will be my partner!",
                            "Meu parceiro de comédia fugiu! Então pronto! {name}! Você vai ser meu parceiro!"));
                });
        }

        private void FonComedyInstructions(NPCController npc)
        {
            FonTalkSequence(npc, () => StartFonComedy(npc),
                new FonStoryLine("It’s your stage name, of course!\nOkay, lets get going!",
                    "É seu nome artístico, claro!\nCerto, vamos nessa!"),
                new FonStoryLine("Dodo, there’s not much time, so I can’t teach you much!\nListen well!",
                    "Dodo, temos pouco tempo, então não posso ensinar muita coisa!\nEscute bem!"),
                new FonStoryLine("Okay?\nThere are two ways to respond to my comments, you would either let the comment pass in agreement, or talk back to me!",
                    "Certo?\nHá duas maneiras de responder ao que eu disser. Você pode concordar ou retrucar!"),
                new FonStoryLine("On stage, listen to my lines carefully and respond by agreeing, or talking back!",
                    "No palco, ouça minhas falas com atenção e responda concordando ou retrucando!"),
                new FonStoryLine("If you talk back when you shouldn’t, make a wrong comment, or take too much time responding, I’ll bonk ya on the head!",
                    "Se retrucar na hora errada, fizer um comentário errado ou demorar demais para responder, vou dar uma pancada na sua cabeça!"),
                new FonStoryLine("You’ll probably faint after three of my bonks, which would end our performance right on the spot!",
                    "Você provavelmente vai desmaiar depois de três pancadas, e aí nosso espetáculo acaba na hora!"),
                new FonStoryLine("Anyway,\nWe’re on!\nLet’s go!", "Enfim,\né a nossa vez!\nVamos!"));
        }

        private void StartFonComedy(NPCController npc)
        {
            TownSaveVariables.SetBool(StoryPrefix + "fon.comedyPlayed", true);
            PersistTownProgress();
            BeginTheaterAct(TheaterAct.Comedy, npc, npc);
        }

        private void WatchVivian(NPCController host)
        {
            var dancer = _townPeople.Find(npc => npc && npc.npcId == "vivian");
            if (!dancer || StoryStage("vivian") is 7 or 8)
            {
                if (host && host.npcId == "theater_fon")
                    FonTalk(host, "Vivian isn't performing today.", "Vivian não se apresenta hoje.");
                else StoryTalk(host, "Vivian isn't performing today.", "Vivian não se apresenta hoje.");
                return;
            }
            int stage = StoryStage("vivian");
            int dialogueStage = StoryNewDay("vivian") ? stage : StoryComplete;
            BeginTheaterAct(TheaterAct.Dance, host, dancer, () => VivianTheaterPerformance(dancer, dialogueStage));
        }

        private void BeginTheaterAct(TheaterAct act, NPCController host, NPCController performer, Action afterDance = null)
        {
            _theaterAct = act;
            _theaterHost = host;
            _theaterPerformer = performer;
            _theaterHome = performer.transform.position;
            _theaterPerformerRotation = performer.transform.rotation;
            _theaterStage = StoryRoom == BarRoom ? _theaterHome :
                RetailTownActorPosition(new Vector3(25f, 0.5f, 24f), 0f);
            // Captured theater mesh: stage X19..33/Z9..19 at Y1;
            // Z23..26 are the audience tiers, not the stage.
            if (act == TheaterAct.Comedy && StoryRoom == TheaterRoom)
                _theaterStage = new Vector3(26f, 1f, 17f);
            _theaterPlayerHome = _player.transform.position;
            _theaterPlayerFacing = _townFacing;
            _theaterOnStage = true;
            _afterTheaterDance = afterDance;
            if (act == TheaterAct.Dance) PlayAreaMusic(VivianDanceMusicCue);
            else if (act == TheaterAct.Comedy) PlayAreaMusic(ComedyMusicCue);
            performer.transform.position = _theaterStage;
            if (act == TheaterAct.Comedy)
            {
                _comedyKohBase = _theaterStage + new Vector3(-0.85f, TownActorFootOffset, 0f);
                _comedyFonBase = _theaterStage + Vector3.right * .85f;
                performer.transform.position = _comedyFonBase;
                _comedyKohScale = _player.transform.localScale;
                _player.transform.position = _comedyKohBase;
                _townFacing = Vector2.up;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
                performer.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                UpdateTownKewne();
                CaptureComedyAudience();
                SetupComedyLights();
                SetComedyBeat(ComedyBeat.Curtain, 1.4f);
                // Show start: scene selector 0x700 (applause) as the curtain opens.
                PlayTheaterSound("Theater700");
            }
            UpdateCameraFollow(snap: true);
            _theaterStarted = Time.unscaledTime;
            _comedyLine = _comedyMistakes = 0;
            _comedyNextAt = _theaterStarted + 1f;
            _theaterAnswerReady = false;
            _message = string.Empty;
            if (act != TheaterAct.Comedy) PlayRetailSound(RetailSfx.Confirm);
        }

        private void PlayTheaterSound(string clipName, float gain = 0.75f)
        {
            if (!Application.isPlaying || _sfxVolume <= 0f) return;
            var clip = AssetMode.Load<AudioClip>("Sfx/Retail/" + clipName);
            if (clip) PlayRetailClip(clip, gain);
        }

        private readonly System.Collections.Generic.List<(Transform View, Vector3 Base)> _comedyAudience = new();
        private readonly System.Collections.Generic.List<(NPCController Npc, Vector3 Position, Quaternion Rotation, bool Enabled)> _comedyAudienceHomes = new();
        private readonly System.Collections.Generic.List<NPCController> _comedyAudienceExtras = new();
        private readonly System.Collections.Generic.List<GameObject> _comedyLights = new();
        private Quaternion _theaterPerformerRotation;

        private void CaptureComedyAudience()
        {
            _comedyAudience.Clear();
            if (StoryRoom == TheaterRoom)
            {
                string[] banks = { "narder", "oyaji", "obasan", "hunter1", "nouhuko", "hunter2" };
                for (int i = 0; i < banks.Length; i++)
                {
                    string id = "comedy_guest_" + i;
                    SpawnStoryResident("monster_shop_hunter", id, T("Spectator", "Espectador"), default);
                    var guest = FindStoryActor(id);
                    if (!guest) continue;
                    RetailResidentBanks.ApplyBank(guest.gameObject, banks[i]);
                    _comedyAudienceExtras.Add(guest);
                }
            }
            foreach (var person in _townPeople)
                if (person && person != _theaterPerformer && person.npcId != "vivian" && person.gameObject.activeInHierarchy)
                {
                    _comedyAudienceHomes.Add((person, person.transform.position, person.transform.rotation, person.enabled));
                    if (StoryRoom == TheaterRoom)
                    {
                        int seat = _comedyAudience.Count;
                        int row = seat / 4;
                        person.transform.position = new Vector3(21.8f + seat % 4 * 2.8f, .5f + row * .5f, 23.45f + row);
                        person.transform.rotation = Quaternion.LookRotation(Vector3.back);
                        person.enabled = false;
                    }
                    _comedyAudience.Add((person.transform, person.transform.position));
                }
            RefreshTownNpcCells();
        }

        private void SetupComedyLights()
        {
            foreach (var target in new[] { _comedyKohBase, _comedyFonBase })
            {
                var fixture = new GameObject("Comedy stage spotlight");
                fixture.transform.SetParent(transform, false);
                fixture.transform.position = target + new Vector3(0f, 5f, 2f);
                fixture.transform.LookAt(target + Vector3.up * .6f);
                var light = fixture.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 10f;
                light.spotAngle = 38f;
                light.intensity = 2.5f;
                light.color = new Color(1f, .95f, .8f);
                light.shadows = LightShadows.None;
                _comedyLights.Add(fixture);
            }
        }

        private void SetComedyBeat(ComedyBeat beat, float length, string speaker = null, string text = null)
        {
            _comedyBeat = beat;
            _comedyBeatStarted = Time.unscaledTime;
            _comedyBeatLength = length;
            _comedySpeaker = speaker;
            _comedyText = text;
            _theaterAnswerReady = beat == ComedyBeat.FonLine;
        }

        private string FonName => _theaterPerformer && _theaterPerformer.TryGetComponent<CharacterStats>(out var stats) ? stats.displayName : "Fon";

        private void StartComedyLine()
        {
            var line = ComedyLines[_comedyLine];
            string text = T(line.En, line.Pt);
            // Long lines get extra time to be read before the answer window closes.
            SetComedyBeat(ComedyBeat.FonLine, 4f + text.Length * 0.035f, FonName, text);
        }

        private void AnswerComedyLine(bool answered, bool agree)
        {
            if (_comedyBeat != ComedyBeat.FonLine || _theaterAct != TheaterAct.Comedy) return;
            var line = ComedyLines[_comedyLine];
            _comedyAnswerTimedOut = !answered;
            _comedyAnswerWasTalkBack = answered && !agree;
            bool correct = answered && (line.Answer == ComedyAnswer.Either ||
                                        (agree ? line.Answer == ComedyAnswer.Agree : line.Answer == ComedyAnswer.TalkBack));
            if (correct)
            {
                string reply = agree ? T(line.AgreeEn, line.AgreePt) : T(line.TalkEn, line.TalkPt);
                if (string.IsNullOrEmpty(agree ? line.AgreeEn : line.TalkEn))
                {
                    SetComedyBeat(line.Laugh ? ComedyBeat.Laugh : ComedyBeat.Next, line.Laugh ? 1.5f : 0.3f);
                    if (line.Laugh) StartComedyLaugh();
                    return;
                }
                _comedySlap = !agree;
                SetComedyBeat(ComedyBeat.KohReply, 2.2f + reply.Length * 0.02f, HeroName, reply);
                if (_comedySlap) PlayRetailSound(RetailSfx.WeaponSwing, 0.45f);
                return;
            }

            _comedySlap = false;
            if (!answered)
            {
                SetComedyBeat(ComedyBeat.Scold, 2f, FonName, _comedyLine == 0
                    ? T("Hurry up and say something!\nYou’re name’s Dodo!", "Anda, diga alguma coisa!\nSeu nome é Dodo!")
                    : T("Say something!", "Diga alguma coisa!"));
                return;
            }
            string koh = agree
                ? (_comedyLine == 0 ? T(line.AgreeEn, line.AgreePt) : T("[That’s right!]", "[Isso mesmo!]"))
                : T("[You gotta be kidding!]", "[Só pode estar brincando!]");
            SetComedyBeat(ComedyBeat.KohReply, 1.8f, HeroName, koh);
        }

        private void StartComedyLaugh()
        {
            _comedyLaughAt = Time.unscaledTime;
            PlayTheaterSound("Theater701");
        }

        private bool HandleTheaterAct()
        {
            if (_theaterAct == TheaterAct.None) return false;
            bool inPerformanceRoom = StoryRoom == TheaterRoom || (_theaterAct == TheaterAct.Dance && StoryRoom == BarRoom);
            if (!inPerformanceRoom || !_theaterPerformer)
            {
                if (inPerformanceRoom) EndTheaterAct();
                else _theaterAct = TheaterAct.None;
                return false;
            }
            bool confirm = _interact?.WasPressedThisFrame() == true;
            bool cancel = _cancel?.WasPressedThisFrame() == true;
            float elapsed = Time.unscaledTime - _theaterStarted;
            if (_theaterAct == TheaterAct.Dance)
            {
                Vector3 danceDirection = new Vector3(Mathf.Cos(elapsed * 2.8f), 0f, Mathf.Cos(elapsed * 1.4f));
                _theaterPerformer.transform.position = _theaterStage +
                    new Vector3(Mathf.Sin(elapsed * 2.8f) * 0.55f, Mathf.Abs(Mathf.Sin(elapsed * 4.3f)) * 0.27f,
                        Mathf.Sin(elapsed * 1.4f) * 0.35f);
                _theaterPerformer.GetComponent<ActorLocomotionView>()?.Present(danceDirection, elapsed * 5.5f, true);
                if (elapsed >= 8f || cancel)
                {
                    Action after = _afterTheaterDance;
                    EndTheaterAct();
                    after?.Invoke();
                }
                return true;
            }

            if (_theaterAct == TheaterAct.Results)
            {
                AnimateComedyStage();
                if (_comedyBeat == ComedyBeat.CurtainClose && Time.unscaledTime - _comedyBeatStarted < _comedyBeatLength) return true;
                if (confirm || cancel) FinishComedyDialogue();
                return true;
            }

            float beatTime = Time.unscaledTime - _comedyBeatStarted;
            bool beatDone = beatTime >= _comedyBeatLength;
            switch (_comedyBeat)
            {
                case ComedyBeat.Curtain:
                    if (beatDone) StartComedyLine();
                    break;
                case ComedyBeat.FonLine:
                    if (confirm || cancel) AnswerComedyLine(true, confirm);
                    else if (beatDone) AnswerComedyLine(false, false);
                    break;
                case ComedyBeat.KohReply:
                    if (_comedySlap && _comedyImpactAt < _comedyBeatStarted && beatTime >= 0.18f)
                    {
                        _comedyImpactAt = Time.unscaledTime;
                        _comedyImpactPoint = _theaterPerformer.transform.position + Vector3.up * 0.9f;
                        PlayRetailSound(RetailSfx.Hit);
                    }
                    if (!beatDone && !(confirm && beatTime > 0.4f)) break;
                    if (IsCorrectReplyBeat())
                    {
                        var line = ComedyLines[_comedyLine];
                        if (line.Laugh) { SetComedyBeat(ComedyBeat.Laugh, 1.5f); StartComedyLaugh(); }
                        else SetComedyBeat(ComedyBeat.Next, 0.3f);
                    }
                    else
                    {
                        // 0x8001C129 / 0x8001C1E5: Fon scolds, then bonks.
                        string scold = _comedyLine == 0 && !_comedyAnswerWasTalkBack
                            ? T("No way!  Your name is Dodo!", "Nada disso! Seu nome é Dodo!")
                            : _comedyAnswerWasTalkBack
                                ? T("This is not where you talk\nbackkkkk!", "Aqui não é hora de\nretrucaaaaar!")
                                : T("What are you agreeing for!\nTalk back, why don’t you!?", "Por que está concordando!\nRetruque, ora essa!?");
                        SetComedyBeat(ComedyBeat.Scold, 2f, FonName, scold);
                    }
                    break;
                case ComedyBeat.Scold:
                    if (!beatDone && !(confirm && beatTime > 0.4f)) break;
                    SetComedyBeat(ComedyBeat.Bonk, 1f, FonName, _comedyText);
                    PlayRetailSound(RetailSfx.WeaponSwing, 0.6f);
                    break;
                case ComedyBeat.Bonk:
                    if (_comedyImpactAt < _comedyBeatStarted && beatTime >= 0.3f)
                    {
                        _comedyImpactAt = Time.unscaledTime;
                        _comedyImpactPoint = _player.transform.position + Vector3.up * 0.75f;
                        PlayRetailSound(RetailSfx.Punch);
                        _comedyMistakes++;
                    }
                    if (beatDone) SetComedyBeat(ComedyBeat.Next, 0.25f);
                    break;
                case ComedyBeat.Laugh:
                    if (beatDone) SetComedyBeat(ComedyBeat.Next, 0.2f);
                    break;
                case ComedyBeat.Next:
                    if (!beatDone) break;
                    _comedyLine++;
                    if (_comedyMistakes >= 3)
                    {
                        // Three bonks: Koh faints and the curtain falls.
                        PlayTheaterSound("Theater702", 0.6f);
                        _theaterAct = TheaterAct.Results;
                        SetComedyBeat(ComedyBeat.CurtainClose, 1.6f);
                    }
                    else if (_comedyLine >= ComedyLines.Length)
                    {
                        SetComedyBeat(ComedyBeat.Bow, 3f, FonName, T("Thank you very much, we love\nyou!", "Muito obrigado, adoramos\nvocês!"));
                        PlayTheaterSound("Theater700");
                        StartComedyLaugh();
                        PlayAreaMusic(ComedyExitMusicCue, loop: false);
                    }
                    else StartComedyLine();
                    break;
                case ComedyBeat.Bow:
                    if (!beatDone) break;
                    _theaterAct = TheaterAct.Results;
                    SetComedyBeat(ComedyBeat.CurtainClose, 1.6f, FonName, _comedyText);
                    break;
            }
            AnimateComedyStage();
            return true;
        }

        private bool IsCorrectReplyBeat()
        {
            if (_comedyAnswerTimedOut) return false;
            var line = ComedyLines[_comedyLine];
            return line.Answer == ComedyAnswer.Either ||
                   (_comedyAnswerWasTalkBack ? line.Answer == ComedyAnswer.TalkBack : line.Answer == ComedyAnswer.Agree);
        }

        /// <summary>Stage movement: Fon bobs while talking, Koh's talk-back slap, Fon's bonk, the audience laughing.</summary>
        private void AnimateComedyStage()
        {
            if (!_player || !_theaterPerformer) return;
            float t = Time.unscaledTime - _comedyBeatStarted;
            Vector3 koh = _comedyKohBase, fon = _comedyFonBase;
            Vector3 kohScale = _comedyKohScale;
            Vector3 towardFon = (_comedyFonBase - _comedyKohBase); towardFon.y = 0f; towardFon.Normalize();
            switch (_comedyBeat)
            {
                case ComedyBeat.FonLine:
                case ComedyBeat.Scold:
                    fon += Vector3.up * Mathf.Abs(Mathf.Sin(t * 9f)) * 0.06f;
                    break;
                case ComedyBeat.KohReply when _comedySlap:
                {
                    float lunge = t < 0.18f ? t / 0.18f : Mathf.Clamp01(1f - (t - 0.18f) / 0.25f);
                    koh += towardFon * 0.42f * lunge + Vector3.up * Mathf.Sin(Mathf.Clamp01(t / 0.43f) * Mathf.PI) * 0.12f;
                    if (t >= 0.18f && t < 0.4f) fon -= towardFon * 0.12f * (1f - (t - 0.18f) / 0.22f);
                    break;
                }
                case ComedyBeat.Bonk:
                {
                    float swing = t < 0.3f ? t / 0.3f : Mathf.Clamp01(1f - (t - 0.3f) / 0.35f);
                    fon -= towardFon * 0.45f * swing;
                    fon += Vector3.up * Mathf.Sin(Mathf.Clamp01(t / 0.65f) * Mathf.PI) * 0.25f;
                    if (t >= 0.3f)
                    {
                        float squash = Mathf.Clamp01(1f - (t - 0.3f) / 0.5f);
                        kohScale = new Vector3(kohScale.x * (1f + 0.18f * squash), kohScale.y * (1f - 0.28f * squash), kohScale.z);
                    }
                    break;
                }
                case ComedyBeat.Bow:
                    fon += Vector3.up * Mathf.Abs(Mathf.Sin(t * 4f)) * 0.08f;
                    koh += Vector3.up * Mathf.Abs(Mathf.Sin(t * 4f + 1f)) * 0.08f;
                    break;
                case ComedyBeat.CurtainClose when _comedyMistakes >= 3:
                    // Koh faints on the third bonk.
                    kohScale = new Vector3(kohScale.x * 1.25f, kohScale.y * 0.55f, kohScale.z);
                    break;
            }
            _player.transform.position = koh;
            _player.transform.localScale = kohScale;
            _theaterPerformer.transform.position = fon;

            float laugh = Time.unscaledTime - _comedyLaughAt;
            foreach (var (view, basePoint) in _comedyAudience)
                if (view) view.position = basePoint + Vector3.up * (laugh < 1.6f ? Mathf.Abs(Mathf.Sin(laugh * 14f + basePoint.x)) * 0.09f : 0f);
        }

        private void FinishComedyDialogue()
        {
            EndTheaterAct();
            var vivian = FindStoryActor("vivian");
            if (vivian && StoryStage("vivian") is not (7 or 8))
            {
                VivianComedyReaction(vivian, _theaterHost, _comedyMistakes < 3);
                return;
            }
            var fon = _theaterHost;
            // TOWN.BIN 0x4B94BD–0x4B9ABE.
            if (_comedyMistakes < 3)
            {
                FonTalk(fon, "Hey, great show, thanks!\nYou stayed with me right to the end!",
                    "Ei, foi um ótimo espetáculo, obrigado!\nVocê ficou comigo até o fim!",
                    () => FonAsk(fon, "Your timing in talking back was right on!\nHow did you like the content?",
                        "Você retrucou na hora certa!\nO que achou das falas?",
                        (T("They were great lines.", "As falas foram ótimas."), () =>
                            FonTalk(fon, "Really!?\nGee, thanks, that’s a real compliment!",
                                "Sério!?\nPuxa, obrigado, isso sim é um elogio!", () => FonComedyThanks(fon))),
                        (T("They were stale.", "Eram batidas."), () =>
                            FonTalk(fon, "Tsk.\nI’ve got to study much more.", "Tsc.\nTenho que estudar muito mais.", () => FonComedyThanks(fon))),
                        (T("Average. No originality.", "Na média. Sem originalidade."), () =>
                            FonTalkSequence(fon, () => FonComedyThanks(fon),
                                new FonStoryLine("... you speak to me like you’re the pro.\nBut just you see, {name}!",
                                    "... você fala comigo como se fosse o profissional.\nMas é só esperar para ver, {name}!"),
                                new FonStoryLine("By the next time you come to my show, I will have studied extensively to become Fon the Superstar Performer!",
                                    "Da próxima vez que vier ao meu espetáculo, terei estudado muito para me tornar Fon, o artista superestrela!"),
                                new FonStoryLine("So make sure you catch my next show!  Just sit back and enjoy!",
                                    "Então não perca meu próximo espetáculo! É só sentar e aproveitar!")))));
                return;
            }

            FonTalkSequence(fon, null,
                new FonStoryLine("Oh, I’m sorry!\nPlease forgive me!", "Ah, foi mal!\nPor favor, me perdoe!"),
                new FonStoryLine("When on stage, I just get carried away, and hold back nothing!",
                    "Quando estou no palco, eu me empolgo e não me contenho!"),
                new FonStoryLine("Going on stage without sufficient practice may have been a bit too much to ask for!",
                    "Talvez tenha sido pedir demais subir ao palco sem ensaiar o suficiente!"),
                new FonStoryLine("Oh well...\nIf it’s okay with you, you’re welcome to help out again!",
                    "Bom...\nSe você quiser, pode me ajudar de novo!"));
        }

        private void FonComedyThanks(NPCController fon) =>
            FonTalk(fon, "Thanks a million for today!\nCome again.", "Muitíssimo obrigado por hoje!\nVolte sempre.");

        private void EndTheaterAct()
        {
            if (_theaterPerformer)
            {
                _theaterPerformer.transform.position = _theaterHome;
                if (_theaterAct is TheaterAct.Comedy or TheaterAct.Results)
                    _theaterPerformer.transform.rotation = _theaterPerformerRotation;
                _theaterPerformer.GetComponent<ActorLocomotionView>()?.Present(Vector3.zero, 0f, false);
            }
            if (_theaterOnStage && _player)
            {
                if (_theaterAct is TheaterAct.Comedy or TheaterAct.Results && _comedyKohScale != Vector3.zero)
                    _player.transform.localScale = _comedyKohScale;
                _player.transform.position = _theaterPlayerHome;
                _townFacing = _theaterPlayerFacing;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            }
            foreach (var (view, basePoint) in _comedyAudience)
                if (view) view.position = basePoint;
            _comedyAudience.Clear();
            foreach (var home in _comedyAudienceHomes)
                if (home.Npc)
                {
                    home.Npc.transform.SetPositionAndRotation(home.Position, home.Rotation);
                    home.Npc.enabled = home.Enabled;
                }
            _comedyAudienceHomes.Clear();
            foreach (var guest in _comedyAudienceExtras)
                if (guest) { _townPeople.Remove(guest); RemoveGenerated(guest.gameObject); }
            _comedyAudienceExtras.Clear();
            foreach (var light in _comedyLights)
                if (light) { light.SetActive(false); Destroy(light); }
            _comedyLights.Clear();
            RefreshTownNpcCells();
            _theaterOnStage = false;
            _afterTheaterDance = null;
            if (_playingMusicCue is VivianDanceMusicCue or ComedyMusicCue or ComedyExitMusicCue)
                PlayAreaMusic(StoryRoom == BarRoom ? "VivianTheme.mp3" : TheaterMusicCue);
            _theaterPerformer = null;
            _theaterAct = TheaterAct.None;
        }

        private Vector3 FrameTheaterCamera(Vector3 point) => _theaterPerformer && _theaterAct != TheaterAct.None
            ? (_theaterAct == TheaterAct.Dance ? _theaterPerformer.transform.position : Vector3.Lerp(_comedyKohBase, _comedyFonBase, 0.5f))
            : point;

        // ------------------------------------------------------------ drawing

        private Texture2D SpotlightTexture()
        {
            if (_spotlightTexture) return _spotlightTexture;
            const int size = 128;
            _spotlightTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Stage spotlight" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, r)) * 200f);
                    pixels[y * size + x] = new Color32(0, 0, 8, alpha);
                }
            _spotlightTexture.SetPixels32(pixels);
            _spotlightTexture.Apply(false);
            return _spotlightTexture;
        }

        private Texture2D ImpactTexture()
        {
            if (_impactTexture) return _impactTexture;
            const int size = 64;
            _impactTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Comedy impact star" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float angle = Mathf.Atan2(dy, dx), r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = 0.55f + 0.42f * Mathf.Abs(Mathf.Cos(angle * 4f));
                    Color32 c = r > edge ? new Color32(0, 0, 0, 0)
                        : r > edge - 0.14f ? new Color32(230, 90, 20, 255)
                        : new Color32(255, 236, 90, 255);
                    pixels[y * size + x] = c;
                }
            _impactTexture.SetPixels32(pixels);
            _impactTexture.Apply(false);
            return _impactTexture;
        }

        private Vector2 TheaterGuiPoint(Vector3 world, float width, float height)
        {
            var camera = _gameCamera ? _gameCamera : Camera.main;
            if (!camera) return new Vector2(width * 0.5f, height * 0.45f);
            Vector3 screen = camera.WorldToScreenPoint(world);
            return new Vector2(screen.x * width / Screen.width, (Screen.height - screen.y) * height / Screen.height);
        }

        private void DrawComedyStage(float width, float height)
        {
            if (Event.current.type != EventType.Repaint) return;
            // Spotlight on the duo; the house lights stay down during the act.
            Vector2 center = TheaterGuiPoint(Vector3.Lerp(_comedyKohBase, _comedyFonBase, 0.5f) + Vector3.up * 0.6f, width, height);
            float radius = Mathf.Min(width, height) * 0.36f;
            var hole = new Rect(center.x - radius * 1.25f, center.y - radius, radius * 2.5f, radius * 2f);
            var dark = new Color(0f, 0f, 0.03f, 200f / 255f);
            DrawHudFill(new Rect(0f, 0f, width, Mathf.Max(0f, hole.y)), dark);
            DrawHudFill(new Rect(0f, hole.yMax, width, Mathf.Max(0f, height - hole.yMax)), dark);
            DrawHudFill(new Rect(0f, hole.y, Mathf.Max(0f, hole.x), hole.height), dark);
            DrawHudFill(new Rect(hole.xMax, hole.y, Mathf.Max(0f, width - hole.xMax), hole.height), dark);
            GUI.DrawTexture(hole, SpotlightTexture(), ScaleMode.StretchToFill, true);

            // Impact star for slaps and bonks.
            float impact = Time.unscaledTime - _comedyImpactAt;
            if (impact >= 0f && impact < 0.35f)
            {
                Vector2 p = TheaterGuiPoint(_comedyImpactPoint, width, height);
                float s = Mathf.Lerp(26f, 58f, impact / 0.35f);
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 1f - impact / 0.35f);
                GUI.DrawTexture(new Rect(p.x - s, p.y - s, s * 2f, s * 2f), ImpactTexture(), ScaleMode.StretchToFill, true);
                GUI.color = previous;
            }
            // Spinning stars over Koh after a bonk.
            if (_comedyBeat is ComedyBeat.Bonk or ComedyBeat.Next && impact >= 0f && impact < 1.1f && _comedyImpactPoint.y > 0f &&
                Vector3.Distance(_comedyImpactPoint, _player.transform.position) < 1.5f)
            {
                Vector2 head = TheaterGuiPoint(_player.transform.position + Vector3.up * 1.05f, width, height);
                for (int i = 0; i < 3; i++)
                {
                    float a = impact * 9f + i * Mathf.PI * 2f / 3f;
                    var star = new Rect(head.x + Mathf.Cos(a) * 26f - 7f, head.y + Mathf.Sin(a) * 8f - 7f, 14f, 14f);
                    GUI.DrawTexture(star, ImpactTexture(), ScaleMode.StretchToFill, true);
                }
            }
            // Audience laughter.
            float laugh = Time.unscaledTime - _comedyLaughAt;
            if (laugh >= 0f && laugh < 1.6f)
                foreach (var (view, _) in _comedyAudience)
                {
                    if (!view) continue;
                    Vector2 p = TheaterGuiPoint(view.position + Vector3.up * (1.4f + laugh * 0.3f), width, height);
                    var previous = GUI.color;
                    GUI.color = new Color(1f, 0.95f, 0.6f, 1f - laugh / 1.6f);
                    GUI.Label(new Rect(p.x - 30f, p.y - 12f, 60f, 24f), "HA HA", _bodyStyle);
                    GUI.color = previous;
                }

            // Curtains open at the start and close at the end of the act.
            float open = _comedyBeat switch
            {
                ComedyBeat.Curtain => Mathf.Clamp01((Time.unscaledTime - _comedyBeatStarted) / _comedyBeatLength),
                ComedyBeat.CurtainClose => 1f - Mathf.Clamp01((Time.unscaledTime - _comedyBeatStarted) / _comedyBeatLength),
                _ => 1f
            };
            if (open < 1f)
            {
                float half = width * 0.5f * (1f - open);
                for (int side = 0; side < 2; side++)
                {
                    float x0 = side == 0 ? 0f : width - half;
                    DrawHudFill(new Rect(x0, 0f, half, height), new Color(0.55f, 0.04f, 0.08f, 1f));
                    for (float fold = 18f; fold < half; fold += 36f)
                        DrawHudFill(new Rect(side == 0 ? x0 + fold : x0 + half - fold - 8f, 0f, 8f, height), new Color(0.38f, 0.02f, 0.05f, 1f));
                }
                DrawHudFill(new Rect(0f, 0f, width, 26f), new Color(0.75f, 0.55f, 0.15f, 1f));
            }
        }

        private bool DrawTheaterAct(float width, float height)
        {
            if (_theaterAct == TheaterAct.None) return false;
            if (_theaterAct == TheaterAct.Dance)
            {
                var caption = new Rect(width * 0.25f, height - 56f, width * 0.50f, 38f);
                DrawDialogueFrame(caption);
                DrawHudText(new Rect(caption.x + 10f, caption.y + 5f, caption.width - 20f, 26f),
                    T("Vivian's dance", "Dança da Vivian"), _messageStyle);
                if (StoryRoom == BarRoom && Event.current.type == EventType.Repaint)
                {
                    // The bar scripts select Vivian's dance portrait before the first movement.
                    int elapsed = Mathf.FloorToInt((Time.unscaledTime - _theaterStarted) * 60f);
                    int frame = StoryPortraitFrame(VivianPortraitFrameDurations[4], elapsed);
                    var portrait = StreamPortrait($"{VivianPortraitPrefix}4_frame{frame}") ?? StreamPortrait(VivianPortrait(4));
                    DrawDialoguePortrait(portrait, caption, scale: 0.66f);
                }
                return true;
            }

            DrawComedyStage(width, height);
            bool closing = _theaterAct == TheaterAct.Results && _comedyBeat == ComedyBeat.CurtainClose &&
                           Time.unscaledTime - _comedyBeatStarted < _comedyBeatLength;
            string speaker = _comedySpeaker;
            string text = _comedyText;
            if (_theaterAct == TheaterAct.Results && !closing)
            {
                speaker = FonName;
                text = _comedyMistakes < 3 ? T("Thank you very much, we love you!", "Muito obrigado, adoramos vocês!")
                    : T("Three bonks! The show is over.", "Três pancadas! Fim do espetáculo.");
            }
            var box = new Rect(width * 0.13f, height - 156f, width * 0.74f, 140f);
            if (!string.IsNullOrEmpty(text))
            {
                DrawDialogueFrame(box, dialoguePole: true);
                bool kohThanks = (_comedyBeat == ComedyBeat.Bow ||
                    _theaterAct == TheaterAct.Results && !closing && _comedyMistakes < 3);
                if (kohThanks && AssetMode.IsReimagined)
                    DrawDialoguePortrait(StreamImage("Reimagined/Portraits/Dialogue/Koh_variant0_frame0.png"), box);
                DrawSpeakerWindow(box, speaker);
                DrawDialogueText(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 52f), text, text);
            }
            DrawHudText(new Rect(width - 190f, 12f, 178f, 22f),
                T($"Bonks: {_comedyMistakes}/3", $"Pancadas: {_comedyMistakes}/3"), _bodyStyle);
            if (_theaterAnswerReady && _comedyBeat == ComedyBeat.FonLine)
            {
                float timeLeft = Mathf.Clamp01(1f - (Time.unscaledTime - _comedyBeatStarted) / _comedyBeatLength);
                DrawHudFill(new Rect(box.x + 12f, box.yMax - 68f, (box.width - 24f) * timeLeft, 3f),
                    new Color(0.9f, 0.67f, 0.25f));
                float button = (box.width - 32f) * 0.5f;
                if (GUI.Button(new Rect(box.x + 12f, box.yMax - 60f, button, 25f), "× " + WithInteractHint("Agree", "Concordar")))
                    AnswerComedyLine(true, true);
                if (GUI.Button(new Rect(box.xMax - button - 12f, box.yMax - 60f, button, 25f),
                    "○ " + T("Talk back", "Retrucar") + $" [{_cancel?.GetBindingDisplayString() ?? "Esc"}]"))
                    AnswerComedyLine(true, false);
            }
            if (_theaterAct == TheaterAct.Results && !closing) DrawContinuePrompt(box, FinishComedyDialogue);
            return true;
        }
    }
}
