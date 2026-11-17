using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum TheaterAct { None, Dance, Comedy, Results }
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
        private bool _theaterAnswerReady, _comedyFinished;
        // TOWN.BIN 0x4B7D8A–0x4B9238.
        private static readonly (string En, string Pt, bool Agree)[] ComedyLines =
        {
            ("Hi everyone!\nWe’re Fon and Dodo! I’m Fon! And this is the Poet.", "Olá, pessoal!\nSomos Fon e Dodo! Eu sou Fon! E este é o Poeta.", false),
            ("Yes! Well, as you can see, we’ve been given the opportunity to stand up here to entertain you! Does everyone here from the town of Monsbaiya know what a performer does?",
                "Sim! Como podem ver, tivemos a oportunidade de subir aqui para divertir vocês! Todo mundo aqui em Monsbaiya sabe o que faz um artista?", true),
            ("Ah, we’re the ones behind the meat counter, separating the sirloin from the bone; grinding, cutting, or stuffing sausages to the customer’s liking, at your local meat store.",
                "Ah, somos quem fica atrás do balcão do açougue, desossando o contrafilé e moendo, cortando ou enchendo linguiças ao gosto do freguês.", false),
            ("We’ve got the best pepperoni in town! Can I see a show of hands to see how many want pepperoni?",
                "Temos o melhor pepperoni da cidade! Quem quer pepperoni? Levante a mão!", false),
            ("Performer!? You mean...\nYou and I are people that perform!?", "Artista!? Quer dizer...\nQue eu e você fazemos apresentações!?", true),
            ("Perform, right?\nIt’s a good smelling word for the things we do.", "Apresentar, certo?\nÉ uma palavra muito cheirosa para o que fazemos.", false),
            ("Fur-Worm?\nThose are the hairy worms that, ah, eat strawberries right?", "Lagarta peluda?\nDaquelas que, ah, comem morangos, certo?", false),
            ("Fur-worm dolls!  A tradition of Monsbaiya! Oh, by the way, a few years ago, on my way home from buying a hamburger for a dime, I saw a bunch of furry caterpillars hanging from a tree!",
                "Bonecas de lagarta peluda! Uma tradição de Monsbaiya! Ah, a propósito, faz uns anos eu voltava para casa depois de comprar um hambúrguer por dez centavos e vi um monte de lagartas peludas penduradas numa árvore!", false),
            ("Calm down. So, now do you understand what it is to be a performer?", "Calma. Então, agora você entendeu o que é ser artista?", false),
            ("Yes, we’re performers! In particular, we’re stand up comedians, and we’re here to provide you with laughter.",
                "Sim, somos artistas! Mais precisamente, somos comediantes e estamos aqui para fazer vocês rirem.", true),
            ("!!\nDon’t laugh! You guys looked at us and laughed, didn’t you!", "!!\nNão riam! Vocês olharam para nós e riram, não foi!", false),
            ("Don’t stop me!\nDoesn’t it bother you?\nThese guys are laughing at us!", "Não me segure!\nIsso não incomoda você?\nEstão rindo da gente!", false),
            ("Fur-Worm?", "Lagarta peluda?", false)
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
            _theaterStage = StoryRoom == BarRoom ? _theaterHome :
                RetailTownActorPosition(new Vector3(25f, 0.5f, 24f), 0f);
            _theaterPlayerHome = _player.transform.position;
            _theaterPlayerFacing = _townFacing;
            _theaterOnStage = true;
            _afterTheaterDance = afterDance;
            if (act == TheaterAct.Dance) PlayAreaMusic(VivianDanceMusicCue);
            else if (act == TheaterAct.Comedy) PlayAreaMusic(ComedyMusicCue);
            performer.transform.position = _theaterStage;
            if (act == TheaterAct.Comedy)
            {
                _player.transform.position = _theaterStage + new Vector3(-0.85f, TownActorFootOffset, 0f);
                _townFacing = Vector2.down;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            }
            UpdateCameraFollow(snap: true);
            _theaterStarted = Time.unscaledTime;
            _comedyLine = _comedyMistakes = 0;
            _comedyNextAt = _theaterStarted + 1f;
            _theaterAnswerReady = false;
            _comedyFinished = false;
            _message = string.Empty;
            PlayRetailSound(RetailSfx.Confirm);
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
                if (confirm || cancel) FinishComedyDialogue();
                return true;
            }
            if (!_theaterAnswerReady && Time.unscaledTime >= _comedyNextAt)
            {
                if (_comedyFinished)
                {
                    _theaterAct = TheaterAct.Results;
                    PlayAreaMusic(ComedyExitMusicCue, loop: false);
                    return true;
                }
                _theaterAnswerReady = true;
                _comedyNextAt = Time.unscaledTime + 5f;
            }
            if (_theaterAnswerReady && (confirm || cancel || Time.unscaledTime >= _comedyNextAt))
                ScoreComedyLine(confirm || cancel, confirm);
            return true;
        }

        private void ScoreComedyLine(bool answered, bool agree)
        {
            if (!_theaterAnswerReady || _theaterAct != TheaterAct.Comedy) return;
            bool correct = answered && agree == ComedyLines[_comedyLine].Agree;
            if (!correct) _comedyMistakes++;
            PlayRetailSound(correct ? RetailSfx.Confirm : RetailSfx.Hit);
            _comedyLine++;
            _comedyFinished = _comedyMistakes >= 3 || _comedyLine == ComedyLines.Length;
            _theaterAnswerReady = false;
            _comedyNextAt = Time.unscaledTime + 0.55f;
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
                _theaterPerformer.GetComponent<ActorLocomotionView>()?.Present(Vector3.zero, 0f, false);
            }
            if (_theaterOnStage && _player)
            {
                _player.transform.position = _theaterPlayerHome;
                _townFacing = _theaterPlayerFacing;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            }
            _theaterOnStage = false;
            _afterTheaterDance = null;
            if (_playingMusicCue is VivianDanceMusicCue or ComedyMusicCue or ComedyExitMusicCue)
                PlayAreaMusic(StoryRoom == BarRoom ? "VivianTheme.mp3" : TheaterMusicCue);
            _theaterPerformer = null;
            _theaterAct = TheaterAct.None;
        }

        private Vector3 FrameTheaterCamera(Vector3 point) => _theaterPerformer && _theaterAct != TheaterAct.None
            ? _theaterPerformer.transform.position : point;

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
            var box = new Rect(width * 0.13f, height - 124f, width * 0.74f, 108f);
            DrawDialogueFrame(box, dialoguePole: true);
            string text = _theaterAct == TheaterAct.Results
                ? (_comedyMistakes < 3 ? T("Thank you very much, we love you!", "Muito obrigado, adoramos vocês!") : T("Three bonks! The show is over.", "Três pancadas! Fim do espetáculo."))
                : _theaterAnswerReady ? T(ComedyLines[_comedyLine].En, ComedyLines[_comedyLine].Pt)
                    : T($"Fon and Dodo · Bonks: {_comedyMistakes}/3", $"Fon e Dodo · Pancadas: {_comedyMistakes}/3");
            DrawHudText(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 52f), text, _messageStyle, dialogue: true);
            if (_theaterAnswerReady)
            {
                float timeLeft = Mathf.Clamp01((_comedyNextAt - Time.unscaledTime) / 5f);
                DrawHudFill(new Rect(box.x + 12f, box.yMax - 42f, (box.width - 24f) * timeLeft, 3f),
                    new Color(0.9f, 0.67f, 0.25f));
                float button = (box.width - 32f) * 0.5f;
                if (GUI.Button(new Rect(box.x + 12f, box.yMax - 34f, button, 25f), WithInteractHint("Agree", "Concordar")))
                    ScoreComedyLine(true, true);
                if (GUI.Button(new Rect(box.xMax - button - 12f, box.yMax - 34f, button, 25f),
                    T("Talk back", "Retruque") + $" [{_cancel?.GetBindingDisplayString() ?? "Esc"}]"))
                    ScoreComedyLine(true, false);
            }
            if (_theaterAct == TheaterAct.Results && GUI.Button(new Rect(box.xMax - 135f, box.yMax - 34f, 123f, 25f), T("Continue", "Continuar")))
                FinishComedyDialogue();
            return true;
        }
    }
}
