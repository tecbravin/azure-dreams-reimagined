using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        internal const string CherrlHealingQuestCompleted = "quest.cherrl.healing_herb_delivered";
        internal const string WreathFruitAvailable = "home.wreath_pita_available";
        [Serializable] private sealed class MonsterBookPage { public int number; public string name, description, portrait, namePtBr, descriptionPtBr; }
        [Serializable] private sealed class MonsterBook { public MonsterBookPage[] pages; }
        private const string WeedyBookOfferPortrait = "Dialogue/Weedy_variant1_frame0";
        private MonsterBook _weedyBook;
        private bool _weedyOffer, _weedyReading;
        private int _weedyPage;
        private Vector2 _weedyDescriptionScroll;
        private GUIStyle _weedyDescriptionStyle;

        private void CompleteHomeResidentTalk(NPCController speaker)
        {
            if (HouseIndex < 0) return;
            if (speaker.npcId == "wreath")
            {
                _checkpointSelection = 0;
                _checkpointMenu = CheckpointMenu.Wreath;
            }
            if (speaker.npcId == "wreath" && TownSaveVariables.GetBool(WreathFruitAvailable))
            {
                if (!_townInventory.HasSpace())
                { _message = T("Make room for the Pita Fruit.", "Libere um espaço para a fruta Pita."); return; }
                _townInventory.AddDungeonItem(ItemSystem.CreateFloorItem(ItemSystem.Id.PitaFruit, ItemSystem.Kind.Fruit, 40));
                TownSaveVariables.SetBool(WreathFruitAvailable, false);
                PersistTownProgress();
                _message = T("Wreath gives you a Pita Fruit.", "Wreath dá uma fruta Pita a você.");
            }
            else if (speaker.npcId == "weedy")
            {
                _weedyOffer = true;
                _message = T("Weedy: Would you like to see my monster book?", "Weedy: Quer ver meu livro dos monstros?");
            }
        }

        private void ResetWeedyBook()
        {
            _weedyOffer = _weedyReading = false;
        }

        private bool HandleWeedyBookInput()
        {
            if (!_weedyOffer && !_weedyReading) return false;
            var keyboard = Keyboard.current;
            if (keyboard == null) return true;
            if (keyboard.escapeKey.wasPressedThisFrame) ResetWeedyBook();
            else if (_weedyOffer && _interact != null && _interact.WasPressedThisFrame()) OpenWeedyBook();
            else if (_weedyReading && keyboard.leftArrowKey.wasPressedThisFrame) SelectWeedyPage(_weedyPage - 1);
            else if (_weedyReading && keyboard.rightArrowKey.wasPressedThisFrame) SelectWeedyPage(_weedyPage + 1);
            return true;
        }

        private void OpenWeedyBook()
        {
            _weedyOffer = false;
            if (_weedyBook == null)
            {
                var asset = Resources.Load<TextAsset>("Town/WeedyBook/pages");
                if (asset) _weedyBook = JsonUtility.FromJson<MonsterBook>(asset.text);
            }
            if (_weedyBook?.pages == null || _weedyBook.pages.Length == 0)
            { _message = T("Weedy's book is unavailable.", "O livro da Weedy está indisponível."); return; }
            _weedyReading = true;
            SelectWeedyPage(0);
        }

        private void SelectWeedyPage(int index)
        {
            _weedyPage = Mathf.Clamp(index, 0, _weedyBook.pages.Length - 1);
            _weedyDescriptionScroll = Vector2.zero;
        }

        private void DrawWeedyBook()
        {
            if (_weedyOffer)
            {
                var panel = new Rect(120, 145, 400, 146);
                DrawDialoguePortrait(StreamPortrait(WeedyBookOfferPortrait), panel);
                DrawHudPanel(panel);
                GUI.Label(new Rect(141, 164, 355, 36), T("Weedy: Would you like to see my monster book?", "Weedy: Quer ver meu livro dos monstros?"));
                if (GUI.Button(new Rect(155, 225, 150, 30), WithInteractHint("Yes", "Sim"))) OpenWeedyBook();
                if (GUI.Button(new Rect(335, 225, 150, 30), T("No [Esc]", "Não [Esc]"))) ResetWeedyBook();
                return;
            }
            if (!_weedyReading) return;
            var page = _weedyBook.pages[_weedyPage];
            var portrait = StreamPortrait("Book/" + page.portrait);
            DrawHudPanel(new Rect(66, 58, 508, 324));
            GUI.Label(new Rect(90, 75, 450, 26), T("Weedy's Monster Book", "Livro dos monstros da Weedy") + $"   {page.number} / {_weedyBook.pages.Length}");
            if (portrait) GUI.DrawTexture(new Rect(98, 121, 156, 156), portrait, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(275, 121, 260, 26), T(page.name, page.namePtBr));
            _weedyDescriptionStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            var description = new GUIContent(T(page.description, page.descriptionPtBr));
            var viewport = new Rect(275, 156, 273, 148);
            float width = viewport.width - 17f;
            float height = _weedyDescriptionStyle.CalcHeight(description, width);
            _weedyDescriptionScroll = GUI.BeginScrollView(viewport, _weedyDescriptionScroll, new Rect(0f, 0f, width, height));
            GUI.Label(new Rect(0f, 0f, width, height), description, _weedyDescriptionStyle);
            GUI.EndScrollView();
            if (GUI.Button(new Rect(100, 319, 125, 32), T("Previous", "Anterior"))) SelectWeedyPage(_weedyPage - 1);
            if (GUI.Button(new Rect(250, 319, 140, 32), T("Close [Esc]", "Fechar [Esc]"))) ResetWeedyBook();
            if (_weedyReading && GUI.Button(new Rect(415, 319, 125, 32), T("Next", "Próxima"))) SelectWeedyPage(_weedyPage + 1);
        }
    }
}
