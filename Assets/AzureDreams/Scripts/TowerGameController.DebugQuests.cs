using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum DebugQuestValueKind { RetailFlag, TownBoolean, StoryRoute }

        private readonly struct DebugQuestVariable
        {
            public readonly DebugQuestValueKind Kind;
            public readonly int FlagId;
            public readonly string SaveKey;
            public readonly string Route;
            public readonly string English;
            public readonly string Portuguese;

            public DebugQuestVariable(int flagId, string english, string portuguese)
            {
                Kind = DebugQuestValueKind.RetailFlag;
                FlagId = flagId;
                SaveKey = null;
                Route = null;
                English = english;
                Portuguese = portuguese;
            }

            public DebugQuestVariable(string saveKey, string english, string portuguese)
            {
                Kind = DebugQuestValueKind.TownBoolean;
                FlagId = 0;
                SaveKey = saveKey;
                Route = null;
                English = english;
                Portuguese = portuguese;
            }

            public DebugQuestVariable(string route, int completionFlag, string english, string portuguese)
            {
                Kind = DebugQuestValueKind.StoryRoute;
                FlagId = completionFlag;
                SaveKey = null;
                Route = route;
                English = english;
                Portuguese = portuguese;
            }
        }

        // These are the quest flags consumed by the retail-derived Town
        // scripts plus the equivalent Unity-side quest state.  Construction
        // completion is deliberately omitted: changing a built lot requires a
        // map rebuild, while these switches are safe to apply at runtime.
        private static readonly DebugQuestVariable[] DebugQuestVariables =
        {
            new(BarryFloor10Stock, "Blacksmith: floor 10 stock", "Ferraria: estoque do andar 10"),
            new(BarryFloor15Stock, "Blacksmith: floor 15 stock", "Ferraria: estoque do andar 15"),
            new(BarryFloor20Stock, "Blacksmith: floor 20 stock", "Ferraria: estoque do andar 20"),
            new(BarryFloor25Stock, "Blacksmith: floor 25 stock", "Ferraria: estoque do andar 25"),
            new("nico", 5230, "Nico route complete", "Rota da Nico concluída"),
            new("selfi", 5234, "Selfi route complete", "Rota da Selfi concluída"),
            new("fur", 5232, "Fur route complete", "Rota da Fur concluída"),
            new("vivian", 5238, "Vivian route complete", "Rota da Vivian concluída"),
            new("cherrl", 5240, "Cherrl route complete", "Rota da Cherrl concluída"),
            new("patty", 5242, "Patty route complete", "Rota da Patty concluída"),
            new("mia", 5236, "Mia route complete", "Rota da Mia concluída"),
            new(4605, "Cherrl becomes a nurse", "Cherrl se torna enfermeira"),
            new(5211, "Selfi gave the egg", "Selfi entregou o ovo"),
            new(5202, "Nico culture step", "Etapa cultural da Nico"),
            new(4602, "Nico and Chero dispute", "Conflito entre Nico e Chero"),
            new(5213, "Chero event complete", "Evento do Chero concluído"),
            new(5205, "Nico morning event", "Evento matinal da Nico"),
            new(147, "Nico fountain thanks", "Agradecimento da fonte da Nico"),
            new(160, "Wreath home talk", "Conversa da Wreath em casa"),
            new(150, "Pool restored", "Piscina restaurada"),
            new(4806, "Tavern quest complete", "Quest da taverna concluída"),
            new(5216, "Windmill repaired", "Moinho reparado"),
            new(5001, "Beldo 31F scene seen", "Cena do Beldo no 31º andar vista"),
            new(4003, "Hospital construction requested", "Construção do hospital solicitada"),
            new(4800, "Library construction requested", "Construção da biblioteca solicitada"),
            new(4802, "Theater construction requested", "Construção do teatro solicitada"),
            new(4807, "Casino construction requested", "Construção do cassino solicitada"),
            new(4808, "Gym construction requested", "Construção da academia solicitada"),
            new(4809, "Arcade construction requested", "Construção do fliperama solicitada"),
            new(4810, "Bowling construction requested", "Construção do boliche solicitada"),
            new(2865, "Race track construction requested", "Construção da pista solicitada"),
            new("quest.cherrl.healing_herb_delivered", "Cherrl received Healing Herb", "Cherrl recebeu a Erva Curativa"),
            new("pool.request_seen", "Pool medal requested", "Medalhão da piscina solicitado"),
            new("tower.ghosh_duel_won", "Ghosh duel won", "Duelo contra Ghosh vencido"),
            new("tower.beldo_defeated", "Beldo defeated", "Beldo derrotado"),
            new("town.hush_bought_herb", "Hush herb purchased", "Erva do Hush comprada"),
            new("home.wreath_pita_available", "Wreath's Pita Fruit available", "Fruta Pita da Wreath disponível"),
            new("home.weedy_fruit_given", "Weedy's fruit already given this run", "Fruta do Weedy já entregue nesta ida"),
            new("temple.priest_blessing", "Jorda's blessing received", "Bênção do Jorda recebida")
        };

        private const float DebugQuestRowHeight = 26f;
        private bool _debugQuestPickerOpen;
        private int _debugQuestSelection;
        private Vector2 _debugQuestScroll;

        private bool TryOpenDebugQuestPicker()
        {
            if (Keyboard.current?.numpad6Key.wasPressedThisFrame != true || !_player) return false;
            if (_area == Area.Town && (_townDoorOpening || _townJumpHeight > 0f || _townJumpVelocity != 0f ||
                _activeTownNpc || _townObjectDialogueOpen ||
                (_townFlow && _townFlow.State.Current != TownGameState.State.TownIdle))) return false;

            _debugQuestSelection = 0;
            _debugQuestScroll = Vector2.zero;
            _debugQuestPickerOpen = true;
            return true;
        }

        private void HandleDebugQuestPickerInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard?.numpad6Key.wasPressedThisFrame == true || _cancel?.WasPressedThisFrame() == true)
            {
                _debugQuestPickerOpen = false;
                return;
            }
            if (keyboard == null) return;

            int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
            if (step != 0)
            {
                _debugQuestSelection = Mathf.Clamp(_debugQuestSelection + step, 0, DebugQuestVariables.Length - 1);
                float top = _debugQuestSelection * DebugQuestRowHeight;
                _debugQuestScroll.y = Mathf.Clamp(_debugQuestScroll.y, top - DebugQuestViewportHeight + DebugQuestRowHeight, top);
                PlayRetailSound(RetailSfx.Cursor);
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                ToggleDebugQuestVariable(_debugQuestSelection);
        }

        private static float DebugQuestViewportHeight
        {
            get
            {
                float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 640f, Screen.height / 480f));
                return Mathf.Min(360f, Screen.height / scale - 92f);
            }
        }

        private bool DebugQuestEnabled(DebugQuestVariable variable) => variable.Kind switch
        {
            DebugQuestValueKind.RetailFlag => TownRetailScriptFlags.CreatePersistent().IsFlagSet(variable.FlagId),
            DebugQuestValueKind.StoryRoute => StoryStage(variable.Route) >= StoryComplete ||
                                                TownRetailScriptFlags.CreatePersistent().IsFlagSet(variable.FlagId),
            _ => TownSaveVariables.GetBool(variable.SaveKey)
        };

        private void ToggleDebugQuestVariable(int index)
        {
            if (index < 0 || index >= DebugQuestVariables.Length) return;
            var variable = DebugQuestVariables[index];
            bool enabled = DebugQuestEnabled(variable);
            if (variable.Kind == DebugQuestValueKind.RetailFlag)
            {
                var flags = TownRetailScriptFlags.CreatePersistent();
                if (enabled) flags.ClearBit(variable.FlagId);
                else flags.SetBit(variable.FlagId);
            }
            else if (variable.Kind == DebugQuestValueKind.StoryRoute)
            {
                var flags = TownRetailScriptFlags.CreatePersistent();
                if (enabled)
                {
                    flags.ClearBit(variable.FlagId);
                    AdvanceTownStory(variable.Route, 0);
                }
                else
                {
                    flags.SetBit(variable.FlagId);
                    AdvanceTownStory(variable.Route, StoryComplete);
                }
            }
            else TownSaveVariables.SetBool(variable.SaveKey, !enabled);

            PersistTownProgress();
            PlayRetailSound(RetailSfx.Confirm);
            string name = T(variable.English, variable.Portuguese);
            _message = enabled ? T($"{name}: OFF", $"{name}: DESLIGADA") : T($"{name}: ON", $"{name}: LIGADA");
        }

        private void DrawDebugQuestPicker(float width, float height)
        {
            float panelWidth = Mathf.Min(490f, width - 20f);
            float listHeight = DebugQuestViewportHeight;
            float panelHeight = listHeight + 72f;
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawHudPanel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 106f, 24f),
                T("Quest variables [Numpad 6]", "Variáveis de quest [Num 6]"));
            if (GUI.Button(new Rect(panel.xMax - 90f, panel.y + 6f, 78f, 24f), T("Close", "Fechar")))
                _debugQuestPickerOpen = false;

            var viewport = new Rect(panel.x + 12f, panel.y + 34f, panel.width - 24f, listHeight);
            float rowWidth = viewport.width - 17f;
            _debugQuestScroll = GUI.BeginScrollView(viewport, _debugQuestScroll,
                new Rect(0f, 0f, rowWidth, DebugQuestVariables.Length * DebugQuestRowHeight));
            int clicked = -1;
            int first = Mathf.Max(0, Mathf.FloorToInt(_debugQuestScroll.y / DebugQuestRowHeight));
            int last = Mathf.Min(DebugQuestVariables.Length, first + Mathf.CeilToInt(listHeight / DebugQuestRowHeight) + 1);
            for (int i = first; i < last; i++)
            {
                var variable = DebugQuestVariables[i];
                string prefix = i == _debugQuestSelection ? "> " : "";
                string state = DebugQuestEnabled(variable) ? "[ON]" : "[OFF]";
                string key = variable.Kind switch
                {
                    DebugQuestValueKind.RetailFlag => variable.FlagId.ToString(),
                    DebugQuestValueKind.StoryRoute => variable.Route + "/" + variable.FlagId,
                    _ => variable.SaveKey
                };
                if (GUI.Button(new Rect(0f, i * DebugQuestRowHeight, rowWidth, DebugQuestRowHeight - 2f),
                    $"{prefix}{state} {T(variable.English, variable.Portuguese)}  [{key}]")) clicked = i;
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(panel.x + 12f, viewport.yMax + 4f, panel.width - 24f, 26f),
                T("Click a quest or use the arrow keys and Enter. Changes are saved immediately.",
                    "Clique em uma quest ou use as setas e Enter. As mudanças são salvas imediatamente."));
            if (clicked >= 0) ToggleDebugQuestVariable(clicked);
        }
    }
}
