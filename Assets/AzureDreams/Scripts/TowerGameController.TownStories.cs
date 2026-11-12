using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string StoryPrefix = "town.story.";
        private const int StoryComplete = 100;
        private const string NicoHouse = "Interior_22_nico_house";
        private const string CherrlHouse = "Interior_18_cherrl_house";
        private const string RhodesHouse = "Interior_21_rhodes_house";
        private const string LibraryRoom = "Interior_07_library";
        private const string RestaurantRoom = "Interior_0e_restaurant";
        private const string BarRoom = "Interior_20_bar";
        private const string FortuneHouseInterior = "Interior_17_fortune_house";
        private const string TheaterRoom = "Interior_0b_theater";
        private const string PoolClosedRoom = "Interior_0d_pool_quest_not_done";
        private const string PoolOpenRoom = "Interior_0d_pool_quest_done";
        private const string CasinoRoom = "Interior_2b_casino_1";

        [SerializeField] private GameObject[] _storyResidents;
        private NPCController _storySpeaker;
        private (string Label, Action Apply)[] _storyChoices;
        private int _storyChoice;
        private Vector2 _storyScroll;
        private const float StoryChoiceHeight = 27f;
        private const int VisibleStoryChoices = 7;

        private static int StoryDay => TownSaveVariables.GetInt(StoryPrefix + "day");
        private static int StoryStage(string name) => TownSaveVariables.GetInt(StoryPrefix + name + ".stage");
        private static bool StoryNewDay(string name) => StoryDay > TownSaveVariables.GetInt(StoryPrefix + name + ".day", -1);
        private static bool StoryBuilding(string name) => TownSaveVariables.GetBool(ConstructionKey + name);
        private string StoryRoom => _interior ? _interior.interiorId : string.Empty;
        private static bool PoolRestored => TownRetailScriptFlags.CreatePersistent().IsFlagSet(PoolQuestCompleteFlag);

        private void AdvanceTownStory(string name, int stage)
        {
            TownSaveVariables.SetInt(StoryPrefix + name + ".stage", stage);
            TownSaveVariables.SetInt(StoryPrefix + name + ".day", StoryDay);
            PersistTownProgress();
        }

        private bool StoryTalk(NPCController npc, string en, string pt, Action next = null)
        {
            return StoryAsk(npc, en, pt, (T("Continue", "Continuar"), next));
        }

        private bool StoryAsk(NPCController npc, string en, string pt, params (string Label, Action Apply)[] choices)
        {
            _storySpeaker = npc;
            _storyChoices = choices;
            _storyChoice = 0;
            _storyScroll = Vector2.zero;
            var stats = npc.GetComponent<CharacterStats>();
            ShowTownDialogue(stats ? stats.displayName : npc.npcId, T(en, pt), stats ? stats.portraitTexture : null);
            _message = _dialogueLine;
            return true;
        }

        private void CloseStoryPrompt()
        {
            _storySpeaker = null;
            _storyChoices = null;
            _message = string.Empty;
            FinishWakeUpVisit();
        }

        private void PickStoryChoice(int index)
        {
            if (_storyChoices == null || index < 0 || index >= _storyChoices.Length) return;
            var apply = _storyChoices[index].Apply;
            CloseStoryPrompt();
            PlayRetailSound(RetailSfx.Confirm);
            apply?.Invoke();
        }

        private bool HandleTownStory()
        {
            if (!_storySpeaker || _storyChoices == null) return false;
            if (_cancel?.WasPressedThisFrame() == true) { CloseStoryPrompt(); return true; }
            if (Keyboard.current is { } keys)
            {
                int step = (keys.downArrowKey.wasPressedThisFrame ? 1 : 0) - (keys.upArrowKey.wasPressedThisFrame ? 1 : 0);
                _storyChoice = (_storyChoice + step + _storyChoices.Length) % _storyChoices.Length;
                if (step != 0)
                {
                    float top = _storyChoice * StoryChoiceHeight;
                    _storyScroll.y = Mathf.Max(0f, Mathf.Clamp(_storyScroll.y, top - (VisibleStoryChoices - 1) * StoryChoiceHeight, top));
                    PlayRetailSound(RetailSfx.Cursor);
                }
            }
            if (_interact?.WasPressedThisFrame() == true) PickStoryChoice(_storyChoice);
            return true;
        }

        private void DrawStoryChoices(Rect box)
        {
            if (!_storySpeaker || _storyChoices == null) return;
            var choices = _storyChoices;
            float width = Mathf.Min(325f, box.width - 28f);
            float contentHeight = choices.Length * StoryChoiceHeight;
            float viewportHeight = Mathf.Min(contentHeight, VisibleStoryChoices * StoryChoiceHeight);
            var panel = new Rect(box.xMax - width, box.y - viewportHeight - 11f, width, viewportHeight + 8f);
            DrawHudPanel(panel);
            var viewport = new Rect(panel.x + 6f, panel.y + 4f, panel.width - 12f, viewportHeight);
            float rowWidth = viewport.width - (contentHeight > viewportHeight ? 18f : 0f);
            _storyScroll = GUI.BeginScrollView(viewport, _storyScroll, new Rect(0f, 0f, rowWidth, contentHeight));
            int picked = -1;
            for (int i = 0; i < choices.Length; i++)
            {
                var row = new Rect(0f, i * StoryChoiceHeight, rowWidth, StoryChoiceHeight - 2f);
                if (GUI.Button(row, (i == _storyChoice ? "› " : "") + choices[i].Label)) picked = i;
            }
            GUI.EndScrollView();
            if (picked >= 0) PickStoryChoice(picked);
        }

        private bool TryTownStory(NPCController npc)
        {
            if (TryConstructionStory(npc) || PoolService(npc)) return true;
            return npc.npcId switch
            {
                "nico" or "chero" => NicoStory(npc),
                "cherrl" or "hush" => CherrlStory(npc),
                "vivian" or "okami" => VivianStory(npc),
                "patty" => PattyStory(npc),
                "mia" => MiaStory(npc),
                "selfi" => SelfiTownStory(npc),
                "fur" when InGeneralStore => FurStory(npc),
                "shiela" when StoryRoom == FortuneHouseInterior => ShielaConsultation(npc),
                _ => false
            };
        }

        private int FindStoryItem(byte kind, byte id)
        {
            var items = _townInventory.DungeonItems;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Kind == kind && items[i].Subtype == id) return i;
            return -1;
        }

        private bool NicoStory(NPCController npc)
        {
            const string route = "nico";
            int stage = StoryStage(route);
            if (stage == 0 && StoryRoom == NicoHouse)
                return StoryTalk(npc, "Monsbaiya needs more than the Tower. I'm collecting donations for a fountain.",
                    "Monsbaiya precisa de mais do que a Torre. Estou arrecadando doações para uma fonte.",
                    () => AdvanceTownStory(route, 1));
            if (stage is 1 or 2 && StoryRoom.Length == 0 && npc.npcId == "nico" && StoryNewDay(route))
            {
                if (StoryBuilding("fountain"))
                    return StoryTalk(npc, "The fountain is finally ready. Thank you for helping!", "A fonte finalmente ficou pronta. Obrigada pela ajuda!",
                        () => AdvanceTownStory(route, 3));
                return StoryAsk(npc, "Will you donate 1,000 G for the fountain?", "Você pode doar 1.000 G para a fonte?",
                    (T("Donate 1,000 G", "Doar 1.000 G"), () =>
                    {
                        if (_gold < 1000)
                        {
                            StoryTalk(npc, "Come back when you can spare the gold.", "Volte quando puder contribuir.");
                            return;
                        }
                        _gold -= 1000;
                        if (stage == 2) TownSaveVariables.SetBool(ConstructionKey + "fountain", true);
                        AdvanceTownStory(route, stage + 1);
                        StoryTalk(npc, stage == 2 ? "We have enough to build the fountain!" : "Thank you! Ghosh said he'd give one gold more.",
                            stage == 2 ? "Já temos o suficiente para construir a fonte!" : "Obrigada! Ghosh disse que daria uma moeda a mais.");
                    }),
                    (T("Maybe later", "Talvez depois"), null));
            }
            if (stage == 3 && npc.npcId == "chero")
                return StoryAsk(npc, "I'm Chero. Ghosh says a travelling musician has no place at this fountain.",
                    "Sou Chero. Ghosh diz que um músico viajante não tem lugar nesta fonte.",
                    (T("Defend Chero and ask him to play", "Defender Chero e pedir que toque"), () => AdvanceTownStory(route, 4)),
                    (T("Leave", "Ir embora"), null));
            if (stage is 4 or 5 && StoryRoom == NicoHouse && StoryNewDay(route))
                return StoryTalk(npc, stage == 4 ? "I'm glad you stood up for Chero. I was wrong about you." : "{name}, I want to be honest about how I feel. I like you.",
                    stage == 4 ? "Ainda bem que você defendeu o Chero. Eu estava enganada sobre você." : "{name}, quero ser sincera sobre o que sinto. Eu gosto de você.",
                    () => AdvanceTownStory(route, stage == 4 ? 5 : StoryComplete));
            return false;
        }

        private bool CherrlStory(NPCController npc)
        {
            const string route = "cherrl";
            bool doctor = npc.npcId == "hush";
            int stage = StoryStage(route);
            if (!StoryBuilding("hospital")) return false;
            if (stage == 0 && !doctor && StoryRoom == CherrlHouse)
                return StoryAsk(npc, "My Chappi fell out of the window. You found him? Would you come in and be my friend?",
                    "Meu Chappi caiu da janela. Você o encontrou? Quer entrar e ser meu amigo?",
                    (T("Return Chappi and stay", "Devolver Chappi e ficar"), () => AdvanceTownStory(route, 1)),
                    (T("Come back later", "Voltar depois"), null));
            if (stage == 1 && !doctor && StoryRoom == CherrlHouse && StoryNewDay(route))
                return StoryAsk(npc, "Mother's out for a little while. Will you take me for a walk?",
                    "Minha mãe saiu por um tempo. Você quer passear comigo?",
                    (T("Go for a walk", "Ir passear"), () =>
                    {
                        AdvanceTownStory(route, 2);
                        StoryTalk(npc, "{name}... I feel faint. Could you take me to the doctor?",
                            "{name}... estou ficando tonta. Pode me levar ao médico?",
                            () => StartCoroutine(LoadInterior("Interior_10_hospital_2")));
                    }),
                    (T("Not today", "Hoje não"), null));
            if (stage is >= 2 and < 4 && doctor)
            {
                int herb = -1;
                for (int i = 0; i < _townInventory.DungeonItems.Count; i++)
                    if (TowerItemTrapManager.IsCherrlHealingHerb(_townInventory.DungeonItems[i])) { herb = i; break; }
                if (herb >= 0 && !TownSaveVariables.GetBool(CherrlHealingQuestCompleted))
                    return StoryAsk(npc, "This is the Healing Herb! It can cure Cherrl. Shall I tell her who found it?",
                        "Esta é a Erva Curativa! Ela pode curar a Cherrl. Devo dizer a ela quem a encontrou?",
                        (T("Keep it a secret", "Manter segredo"), () => DeliverCherrlHerb(herb, true)),
                        (T("Tell her", "Contar a ela"), () => DeliverCherrlHerb(herb, false)));
                if (stage == 2)
                    return StoryTalk(npc, "A Healing Herb grows on the 28th floor. Bring it to me and I can treat her.",
                        "Há uma Erva Curativa no 28º andar. Traga-a para mim e poderei tratar dela.",
                        () => AdvanceTownStory(route, 3));
            }
            if (stage < 4 && TownSaveVariables.GetBool(CherrlHealingQuestCompleted)) stage = 4;
            if (stage == 4 && !doctor && StoryNewDay(route))
                return StoryAsk(npc, "I'm feeling much better. I want to help the doctor and become a nurse.",
                    "Estou me sentindo muito melhor. Quero ajudar o médico e me tornar enfermeira.",
                    (T("You'd make a good nurse", "Você seria uma ótima enfermeira"), () => AdvanceTownStory(route, 5)),
                    (T("Let me think", "Deixe-me pensar"), null));
            if (stage == 5 && doctor && StoryNewDay(route))
                return StoryTalk(npc, "Cherrl's been a great help at the hospital. She should know what you did for her.",
                    "Cherrl tem ajudado muito no hospital. Ela merece saber o que você fez por ela.",
                    () => AdvanceTownStory(route, 6));
            if (stage == 6 && !doctor)
                return StoryTalk(npc, "The doctor told me who brought the herb. Thank you, {name}. I'm so glad we met.",
                    "O médico me contou quem trouxe a erva. Obrigada, {name}. Que bom que nos conhecemos.",
                    () => AdvanceTownStory(route, StoryComplete));
            return false;
        }

        private void DeliverCherrlHerb(int index, bool anonymous)
        {
            if (!_townInventory.ConsumeDungeonItem(index, ItemSystem.Kind.Herb)) return;
            TownSaveVariables.SetBool(CherrlHealingQuestCompleted, true);
            TownSaveVariables.SetBool(StoryPrefix + "cherrl.anonymous", anonymous);
            AdvanceTownStory("cherrl", 4);
        }

        private bool VivianStory(NPCController npc)
        {
            const string route = "vivian";
            int stage = StoryStage(route);
            if (npc.npcId == "okami")
            {
                int cape = FindStoryItem(QuestItemKind, BlueCapeId);
                if (stage < 2 && cape >= 0)
                    return StoryAsk(npc, "That blue cape... it belonged to someone I knew. May I see it?",
                        "Aquela capa azul... pertencia a alguém que eu conhecia. Posso vê-la?",
                        (T("Hand over the Blue Cape", "Entregar a Capa Azul"), () =>
                        {
                            if (!_townInventory.ConsumeDungeonItem(cape, QuestItemKind)) return;
                            TownRetailScriptFlags.CreatePersistent().SetBit(TavernQuestCompleteFlag);
                            AdvanceTownStory(route, 2);
                            StoryTalk(npc, "I'm closing the bar for a while.", "Vou fechar o bar por um tempo.");
                        }), (T("Later", "Depois"), null));
                if (stage < 2)
                    return StoryTalk(npc, "I used to know a monster tamer who wore a blue cape. I wonder what happened to him.",
                        "Eu conhecia um domador que usava uma capa azul. O que será que aconteceu com ele?",
                        stage == 0 ? () => AdvanceTownStory(route, 1) : null);
                if (stage == 2 && StoryNewDay(route))
                    return StoryAsk(npc, "It's been quiet since I closed the bar. Can I get you something to drink?",
                        "Está tudo tão quieto desde que fechei o bar. Quer alguma coisa para beber?",
                        (T("Share a drink", "Tomar algo com ela"), () =>
                        {
                            AdvanceTownStory(route, 3);
                            StoryTalk(npc, "It was only cola! I think I'm ready to reopen the bar.", "Era só refrigerante! Acho que já posso reabrir o bar.");
                        }), (T("Later", "Depois"), null));
                return false;
            }
            if (stage == 3 && StoryRoom == BarRoom)
                return StoryTalk(npc, "I'm Vivian Merca. I dance here, but I've always wanted to perform in a theater.",
                    "Sou Vivian Merca. Danço aqui, mas sempre quis me apresentar em um teatro.", () => AdvanceTownStory(route, 4));
            if (stage == 4 && StoryRoom == BarRoom && !StoryBuilding("casino") &&
                !TownRetailScriptFlags.CreatePersistent().IsFlagSet(CasinoPlanFlag))
                return OfferConstructionPlan(npc, CasinoPlanFlag, "I'd love to dance at a casino someday.",
                    "Eu adoraria dançar em um cassino um dia.");
            if (stage == 4 && StoryNewDay(route) && StoryBuilding("theater") &&
                (StoryRoom == CasinoRoom || !StoryBuilding("casino") && StoryRoom == BarRoom))
                return StoryAsk(npc, "Do you really think I'd be welcome on the theater stage?",
                    "Você acha mesmo que eu seria bem-vinda no palco do teatro?",
                    (T("I'd love to see you dance there", "Adoraria ver você dançar lá"), () => AdvanceTownStory(route, 5)),
                    (T("Leave", "Ir embora"), null));
            if (stage == 7 && StoryRoom == BarRoom)
                return StoryTalk(npc, "Dancing here again reminded me how much I love it. I'll try the theater one more time.",
                    "Dançar aqui de novo me fez lembrar de como eu gosto disso. Vou tentar mais uma vez no teatro.",
                    () => AdvanceTownStory(route, 8));
            if (stage is 5 or 6 or 8 && StoryNewDay(route) && StoryRoom == TheaterRoom)
            {
                if (stage == 6)
                    return StoryAsk(npc, "I lost my place in the troupe. I can't face that stage again.", "Perdi meu lugar na companhia. Não consigo voltar àquele palco.",
                        (T("Take a walk and dance at the bar", "Passear e dançar no bar"), () => AdvanceTownStory(route, 7)),
                        (T("Come back later", "Voltar depois"), null));

                return StoryAsk(npc, "You made it! I'm so glad. Did you see me on stage?", "Você veio! Que bom. Você me viu no palco?",
                    (T("Watch the dance", "Ver a dança"), () => BeginTheaterAct(TheaterAct.Dance, npc, npc, () =>
                        StoryTalk(npc, stage == 5 ? "I'm not that good yet. I'm sure I'll show you better dancing next time." :
                            "Thank you. And for the other... thank you. I'll be sure to show you a much better show next time.",
                            stage == 5 ? "Ainda tenho muito o que aprender. Tenho certeza de que vou dançar melhor da próxima vez." :
                            "Obrigada. E por aquilo também... obrigada. Vou fazer um espetáculo ainda melhor da próxima vez.",
                            () => AdvanceTownStory(route, stage == 5 ? 6 : StoryComplete)))),
                    (T("Later", "Depois"), null));
            }
            return false;
        }
    }
}
