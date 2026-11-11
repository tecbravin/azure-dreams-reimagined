using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string PlayedSecondsKey = "save.played_seconds";
        private const string ManualSaveCountKey = "save.manual_count";
        private static readonly string[] SaveIconNames =
            { "Nico", "Selfi", "Fur", "Vivian", "Cherrl", "Patty", "Mia", "Beldo" };

        [SerializeField] private Texture2D[] _saveSlotIcons;

        private readonly SaveSystem.Checkpoint[] _checkpointSlots = new SaveSystem.Checkpoint[SaveSystem.SlotCount];
        private float _unsavedPlaySeconds;
        private Vector2 _saveListScroll;
        private int _lastVisibleSaveSelection = -1;
        private GUIStyle _saveSlotLabel, _saveSlotHeading;
        private const float SaveSlotPitch = 89f;

        private void RecordPlayTime()
        {
            TownSaveVariables.SetFloat(PlayedSecondsKey,
                TownSaveVariables.GetFloat(PlayedSecondsKey) + _unsavedPlaySeconds);
            _unsavedPlaySeconds = 0f;
        }

        private void RefreshCheckpointSlots()
        {
            for (int i = 0; i < _checkpointSlots.Length; i++) _checkpointSlots[i] = SaveSystem.ReadSlot(i);
            ResetSaveListScroll();
        }

        private void ResetSaveListScroll()
        {
            _saveListScroll = Vector2.zero;
            _lastVisibleSaveSelection = -1;
        }

        private SaveSystem.Checkpoint ReadAutomaticSaveSummary()
        {
            int gold = TownSaveVariables.Gold;
            if (SaveSystem.HasSave) SaveSystem.Load(out gold, out _, out _, out _);
            return new SaveSystem.Checkpoint { gold = gold, town = TownSaveVariables.Capture() };
        }

        private void DrawSaveSlotList(float width, float height, string title, SaveSystem.Checkpoint[] slots,
            int selection, bool loading, Action<int> select, Action cancel, SaveSystem.Checkpoint automatic = null)
        {
            int count = slots.Length + (automatic != null ? 1 : 0);
            float panelWidth = Mathf.Min(width - 16f, 508f);
            float panelHeight = Mathf.Min(height - 16f, count * SaveSlotPitch + 59f);
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawHudPanel(panel);
            _saveSlotLabel ??= new GUIStyle(_messageStyle) { fontSize = 13, wordWrap = false, clipping = TextClipping.Clip };
            _saveSlotHeading ??= new GUIStyle(_saveSlotLabel) { fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 22f), title, _saveSlotHeading);

            var viewport = new Rect(panel.x + 9f, panel.y + 30f, panel.width - 18f, panel.height - 59f);
            float contentHeight = count * SaveSlotPitch;
            float rowWidth = viewport.width - (contentHeight > viewport.height ? 18f : 1f);
            int selectedRow = selection + (automatic != null ? 1 : 0);
            if (_lastVisibleSaveSelection != selectedRow)
            {
                float rowTop = selectedRow * SaveSlotPitch;
                _saveListScroll.y = Mathf.Clamp(_saveListScroll.y,
                    Mathf.Max(0f, rowTop + SaveSlotPitch - viewport.height), Mathf.Max(0f, rowTop));
                _lastVisibleSaveSelection = selectedRow;
            }

            int clicked = int.MinValue;
            _saveListScroll = GUI.BeginScrollView(viewport, _saveListScroll, new Rect(0f, 0f, rowWidth, contentHeight));
            for (int row = 0; row < count; row++)
            {
                int index = row - (automatic != null ? 1 : 0);
                var data = index < 0 ? automatic : slots[index];
                var rect = new Rect(0f, row * SaveSlotPitch, rowWidth, SaveSlotPitch - 4f);
                string label = index < 0 ? T("Automatic progress", "Progresso automático") :
                    T($"Slot {index + 1}", $"Espaço {index + 1}");
                DrawSaveSlotCard(rect, label, data, selection == index);
                if ((!loading || data != null) && GUI.Button(rect, GUIContent.none, GUIStyle.none)) clicked = index;
            }
            GUI.EndScrollView();

            if (clicked != int.MinValue) select(clicked);
            else if (GUI.Button(new Rect(panel.x + 10f, panel.yMax - 25f, panel.width - 20f, 20f),
                T("Back [Esc]", "Voltar [Esc]"))) cancel();
        }

        private void DrawSaveSlotCard(Rect card, string label, SaveSystem.Checkpoint data, bool selected)
        {
            DrawHudPanel(card);
            if (selected) DrawHudFill(new Rect(card.x + 2f, card.y + 2f, card.width - 4f, card.height - 4f),
                new Color(0.22f, 0.36f, 0.65f, 0.36f));
            string marker = selected ? "\u25b8 " : "";
            if (data == null)
            {
                GUI.Label(new Rect(card.x + 10f, card.y + 8f, card.width - 20f, 21f),
                    marker + label + "   " + T("Empty", "Vazio"), _saveSlotHeading);
                DrawSaveProgressIcons(card, 0);
                return;
            }

            int Read(string key, int fallback = 0) => (int)(data.ProgressValue(key) ?? fallback);
            int reached = Mathf.Max(Read(TownReputation.HighestFloorKey), data.floor > 0 ? Mathf.Max(1, data.floor - 1) : 0);
            var flags = new TownRetailScriptFlags(index => unchecked((uint)Read("retail_flag_word_" + index)), (_, _) => { });
            int milestones = TownReputation.CompletedRoutes(key => Read(key), flags.IsFlagSet);
            if (Read(BeldoClearedKey) == 1)
            {
                milestones |= 1 << (SaveIconNames.Length - 1);
                reached = TowerFloorRules.BeldoFloor;
            }
            int buildings = 0;
            int homeRank = Read(HomeRankKey);
            int hutRank = Read(HutRankKey);
            foreach (var project in BuildingProjects)
                if (ProjectBuilt(project, homeRank, hutRank, Read(ConstructionKey + project.Id) == 1)) buildings++;

            string hero = data.ProgressText(HeroNameKey) ?? DefaultHeroName;
            long gold = (long)data.gold + data.loot;
            string saves = data.ProgressValue(ManualSaveCountKey) is double saveCount ? $"{(int)saveCount}" : "\u2014";
            string playTime = "\u2014";
            if (data.ProgressValue(PlayedSecondsKey) is double seconds)
            {
                var elapsed = TimeSpan.FromSeconds(Math.Clamp(seconds, 0d, int.MaxValue));
                playTime = $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
            }
            string highest = reached > 0 ? $"{reached}" : data.ProgressValue(TownReputation.HighestFloorKey).HasValue ? "0" : "\u2014";
            string location = data.floor == 0 ? T("Town (Monsbaiya)", "Cidade (Monsbaiya)") :
                T($"Tower {Mathf.Max(1, data.floor - 1)}F  \u2192  Continue at {data.floor}F",
                    $"Torre {Mathf.Max(1, data.floor - 1)}\u00ba andar  \u2192  Retomar no {data.floor}\u00ba");

            float half = (card.width - 22f) * 0.5f;
            GUI.Label(new Rect(card.x + 10f, card.y + 3f, half, 18f), marker + label + " " + hero + "  \u00b7  " + T($"Gold {gold} G", $"Ouro {gold} G"), _saveSlotHeading);
            GUI.Label(new Rect(card.x + half + 10f, card.y + 3f, half, 18f), T($"Time {playTime}", $"Tempo {playTime}"), _saveSlotLabel);
            GUI.Label(new Rect(card.x + 10f, card.y + 19f, half, 18f), T($"Saves {saves}  \u00b7  Top floor {highest}", $"Salvamentos {saves}  \u00b7  Maior andar {highest}"), _saveSlotLabel);
            GUI.Label(new Rect(card.x + half + 10f, card.y + 19f, half, 18f), T($"Buildings {buildings}", $"Obras {buildings}"), _saveSlotLabel);
            DrawSaveProgressIcons(card, milestones);
            GUI.Label(new Rect(card.x + 10f, card.y + 63f, card.width - 20f, 19f), location, _saveSlotLabel);
        }

        private void DrawSaveProgressIcons(Rect card, int milestones)
        {
            float size = Mathf.Min(25f, (card.width - 24f) / 16f);
            for (int i = 0; i < SaveIconNames.Length; i++)
            {
                bool unlocked = (milestones & (1 << i)) != 0;
                int textureIndex = unlocked ? i + 1 : 0;
                var texture = _saveSlotIcons != null && textureIndex < _saveSlotIcons.Length ? _saveSlotIcons[textureIndex] : null;
                var rect = new Rect(card.x + 10f + i * (size + 2f), card.y + 39f, size, size);
                if (texture) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                else GUI.Label(rect, unlocked ? SaveIconNames[i].Substring(0, 1) : "?", _saveSlotLabel);
            }
        }
    }
}
