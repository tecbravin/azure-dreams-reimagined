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
        private bool _theaterOnStage;
        private bool _theaterAnswerReady, _comedyFinished;
        private static readonly (string En, string Pt, bool Agree)[] ComedyLines =
        {
            ("Does everyone here from Monsbaiya know what a performer does?", "Todo mundo aqui em Monsbaiya sabe o que faz um artista?", true),
            ("We work behind the meat counter, grinding meat and stuffing sausages.", "Trabalhamos no açougue, moendo carne e fazendo linguiça.", false),
            ("We've got the best pepperoni in town! Who wants some?", "Temos o melhor salame da cidade! Quem quer?", true),
            ("Performer? You mean you and I are people that perform?", "Artistas? Quer dizer que nós dois fazemos apresentações?", true),
            ("Perform? That's a good-smelling word for the things we do.", "Apresentação? É uma palavra bem perfumada para o que fazemos.", false),
            ("Fur-Worm? Those hairy worms that eat strawberries, right?", "Lagarta peluda? Daquelas que comem morangos, certo?", true),
            ("Fur-Worm dolls! A tradition of Monsbaiya!", "Bonecas de lagarta! Uma tradição de Monsbaiya!", true),
            ("Years ago, I saw a bunch of furry caterpillars hanging from a tree!", "Anos atrás, vi um monte de lagartas peludas penduradas numa árvore!", true),
            ("Now do you understand what it is to be a performer?", "Agora você entende o que é ser artista?", false),
            ("We're comedians, here to provide you with laughter.", "Somos comediantes. Estamos aqui para fazer vocês rirem.", false),
            ("Don't laugh! You guys looked at us and laughed, didn't you?", "Não riam! Vocês olharam para a gente e riram, não foi?", true),
            ("Doesn't it bother you? These guys are laughing at us!", "Isso não incomoda você? Estão rindo da gente!", false),
            ("Thank you very much, we love you!", "Muito obrigado! Adoramos vocês!", true)
        };

        private bool TheaterService(NPCController npc)
        {
            if (StoryRoom != TheaterRoom) return false;
            if (npc.npcId == "theater_fon")
                return StoryAsk(npc, $"{HeroName}, you've come at the perfect time! My stand-up partner ran away!",
                    $"{HeroName}, chegou na hora certa! Meu parceiro de comédia fugiu!",
                    (T("Go on stage", "Subir ao palco"), () =>
                        StoryTalk(npc, "I'm counting on you, Dodo! It's your stage name. Agree or talk back at the right time. Three bonks and the show is over!",
                            "Estou contando com você, Dodo! É o seu nome artístico. Concorde ou retruque na hora certa. Três pancadas e o espetáculo acaba!",
                            () => BeginTheaterAct(TheaterAct.Comedy, npc, npc))),
                    (T("Watch Vivian dance", "Ver Vivian dançar"), () => WatchVivian(npc)),
                    (T("Leave", "Sair"), null));
            if (npc.npcId == "vivian")
                return StoryAsk(npc, "Did you see me on stage?", "Você me viu no palco?",
                    (T("Watch the dance", "Ver a dança"), () => WatchVivian(npc)), (T("Later", "Depois"), null));
            return false;
        }

        private void WatchVivian(NPCController host)
        {
            var dancer = _townPeople.Find(npc => npc && npc.npcId == "vivian");
            if (!dancer || StoryStage("vivian") == 7)
            {
                StoryTalk(host, "Vivian isn't performing today.", "Vivian não se apresenta hoje.");
                return;
            }
            BeginTheaterAct(TheaterAct.Dance, host, dancer);
        }

        private void BeginTheaterAct(TheaterAct act, NPCController host, NPCController performer, Action afterDance = null)
        {
            _theaterAct = act;
            _theaterHost = host;
            _theaterPerformer = performer;
            _theaterHome = performer.transform.position;
            _theaterStage = RetailTownActorPosition(new Vector3(25f, 0.5f, 24f), 0f);
            _theaterPlayerHome = _player.transform.position;
            _theaterPlayerFacing = _townFacing;
            _theaterOnStage = true;
            _afterTheaterDance = afterDance;
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
            if (StoryRoom != TheaterRoom || !_theaterPerformer)
            {
                _theaterAct = TheaterAct.None;
                return false;
            }
            bool confirm = _interact?.WasPressedThisFrame() == true;
            bool cancel = _cancel?.WasPressedThisFrame() == true;
            float elapsed = Time.unscaledTime - _theaterStarted;
            if (_theaterAct == TheaterAct.Dance)
            {
                _theaterPerformer.transform.position = _theaterStage +
                    new Vector3(Mathf.Sin(elapsed * 2.8f) * 0.55f, Mathf.Abs(Mathf.Sin(elapsed * 4.3f)) * 0.27f,
                        Mathf.Sin(elapsed * 1.4f) * 0.35f);
                if (elapsed >= 8f || cancel)
                {
                    Action after = _afterTheaterDance;
                    var dancer = _theaterPerformer;
                    EndTheaterAct();
                    if (after != null) after();
                    else StoryAsk(dancer, $"Oh, {HeroName}. Did you see me on stage?", $"Oi, {HeroName}. Você me viu no palco?",
                        (T("You did great.", "Você dançou muito bem."), () =>
                            StoryTalk(dancer, "Thank you. I'm sure I'll show you better dancing next time.",
                                "Obrigada. Tenho certeza de que vou dançar ainda melhor da próxima vez.")),
                        (T("I didn't see you.", "Eu não vi."), () =>
                            StoryTalk(dancer, "Didn't you? Awww, that's too bad. Well, next time, okay?",
                                "Não viu? Ah, que pena. Fica para a próxima, então?")));
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
            if (_comedyMistakes < 3)
                StoryAsk(_theaterHost, "Hey, great show, thanks! How did you like the content?", "Valeu pelo espetáculo! O que achou das piadas?",
                    (T("They were great lines.", "As falas foram ótimas."), () =>
                        StoryTalk(_theaterHost, "Gee, thanks, that's a real compliment!", "Puxa, obrigado. Isso sim é um elogio!")),
                    (T("They were stale.", "Eram batidas."), () =>
                        StoryTalk(_theaterHost, "I've got to study much more.", "Ainda preciso estudar muito.")),
                    (T("Average. No originality.", "Na média. Sem originalidade."), () =>
                        StoryTalk(_theaterHost, "By the next time you come, I'll be Fon the Superstar Performer!", "Quando voltar, eu serei Fon, o artista superstar!")));
            else StoryTalk(_theaterHost, "Going on stage without sufficient practice may have been a bit too much to ask for!", "Talvez subir ao palco sem ensaiar tenha sido pedir demais!");
        }

        private void EndTheaterAct()
        {
            if (_theaterPerformer) _theaterPerformer.transform.position = _theaterHome;
            if (_theaterOnStage && _player)
            {
                _player.transform.position = _theaterPlayerHome;
                _townFacing = _theaterPlayerFacing;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            }
            _theaterOnStage = false;
            _afterTheaterDance = null;
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
                return true;
            }
            var box = new Rect(width * 0.13f, height - 124f, width * 0.74f, 108f);
            DrawDialogueFrame(box, dialoguePole: true);
            string text = _theaterAct == TheaterAct.Results
                ? (_comedyMistakes < 3 ? T("The audience applauds!", "A plateia aplaude!") : T("Three bonks! The show is over.", "Três pancadas! Fim do espetáculo."))
                : _theaterAnswerReady ? T(ComedyLines[_comedyLine].En, ComedyLines[_comedyLine].Pt)
                    : T($"Fon and Dodo · Bonks: {_comedyMistakes}/3", $"Fon e Dodo · Pancadas: {_comedyMistakes}/3");
            DrawHudText(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 52f), text, _messageStyle);
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
