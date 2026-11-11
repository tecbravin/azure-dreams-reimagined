using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string BeldoDuelMusicCue = "BeldoDuel.mp3";
        private const string BeldoClearedKey = "tower.beldo_defeated";
        private enum BeldoScene { None, Introduction, Rescue, Victory }
        private BeldoScene _beldoScene;
        private int _beldoDialoguePage;
        private bool _beldoIntroduced;
        private bool OnBeldoFloor => _area == Area.Tower && _floor == TowerFloorRules.BeldoFloor;

        private void ResetBeldoEncounter()
        {
            _beldoScene = BeldoScene.None;
            _beldoDialoguePage = 0;
            _beldoIntroduced = false;
        }

        private bool HandleBeldoEncounter()
        {
            if (!OnBeldoFloor) return false;
            if (_beldoScene != BeldoScene.None)
            {
                if (_interact?.WasPressedThisFrame() == true) AdvanceBeldoDialogue();
                return true;
            }
            if (_turnBusy || _beldoIntroduced || TownSaveVariables.GetBool(BeldoClearedKey)) return false;
            foreach (var creature in _creatures)
            {
                if (creature.Actor.Species != (byte)BeldoBossSpecies || Distance(_playerCell, creature.Cell) > 8) continue;
                _beldoIntroduced = true;
                _beldoScene = BeldoScene.Introduction;
                _beldoDialoguePage = 0;
                return true;
            }
            return false;
        }

        private bool CanKewneRescueKoh()
        {
            if (!OnBeldoFloor || !_beldoIntroduced || _health > 0 || _pendingPlayerDamage <= 0) return false;
            if (!_creatures.Exists(creature => creature.Actor.Species == (byte)BeldoBossSpecies)) return false;
            return (_familiarDefinition && !_familiarDefeated && _familiarHealth > 0 &&
                    _familiarActor.Species == (byte)MonsterSpecies.KEWNE) ||
                _bagFamiliars.Exists(familiar => !familiar.Defeated && familiar.Actor.Hp > 0 &&
                    familiar.Actor.Species == (byte)MonsterSpecies.KEWNE);
        }

        private void StartBeldoEnding(bool rescued)
        {
            if (rescued) PlayAreaMusic("HeroFinale.mp3");
            else if (_playingMusicCue == BeldoDuelMusicCue) PlayAreaMusic(TowerMusicCue(_floor));
            _beldoScene = rescued ? BeldoScene.Rescue : BeldoScene.Victory;
            _beldoDialoguePage = 0;
            if (rescued)
            {
                var boss = _creatures.Find(creature => creature.Actor.Species == (byte)BeldoBossSpecies);
                if (boss.View)
                {
                    SpawnFx(_fxMagic, CellPosition(boss.Cell));
                    RemoveGenerated(boss.View);
                }
                _creatures.RemoveAll(creature => creature.Actor.Species == (byte)BeldoBossSpecies);
                PlayRetailSound(RetailSfx.MonsterDefeat);
            }
        }

        private void AdvanceBeldoDialogue()
        {
            if (_beldoScene == BeldoScene.None) return;
            PlayRetailSound(RetailSfx.Confirm);
            _beldoDialoguePage++;
            if (_beldoDialoguePage < BeldoDialogueCount) return;
            if (_beldoScene == BeldoScene.Introduction)
            {
                _beldoScene = BeldoScene.None;
                PlayAreaMusic(BeldoDuelMusicCue);
                return;
            }
            CompleteBeldoVictory();
        }

        private int BeldoDialogueCount => _beldoScene == BeldoScene.Victory ? 1 : 3;

        private void CompleteBeldoVictory()
        {
            if (!OnBeldoFloor || TownSaveVariables.GetBool(BeldoClearedKey)) return;
            var sword = new DungeonItemEntry(SeraphimSword, ItemSystem.Kind.Sword, 0, 0);
            var egg = MonsterEgg.Create((byte)MonsterSpecies.HIKEWNE, true);
            bool sentHome = false;
            foreach (var reward in new[] { sword, egg })
            {
                if (_runInventory.HasSpace()) _runInventory.AddDungeonItem(reward);
                else
                {
                    _townStorage.AddDungeonItem(reward);
                    sentHome = true;
                }
            }
            TownSaveVariables.SetBool(BeldoClearedKey, true);
            _towerRun.Victory();
            ReturnToTown(true);
            _message = sentHome
                ? T("Beldo was defeated. Rewards that did not fit are in your item storage.",
                    "Beldo foi derrotado. As recompensas que não couberam estão no depósito da casa.")
                : T("Beldo was defeated. You obtained Seraphim and the Ultimate Egg.",
                    "Beldo foi derrotado. Você recebeu a Seraphim e o Ovo Supremo.");
        }

        private void DrawBeldoDialogue(float width, float height)
        {
            if (_beldoScene == BeldoScene.None) return;
            var (speaker, line) = (_beldoScene, _beldoDialoguePage) switch
            {
                (BeldoScene.Introduction, 0) => ("Beldo", T("You finally made it, son of Guy.", "Finalmente chegou, filho de Guy.")),
                (BeldoScene.Introduction, 1) => ("Beldo", T("Your father sealed the Ultimate Egg. Your blood will break that seal.",
                    "Seu pai selou o Ovo Supremo. Seu sangue vai romper o selo.")),
                (BeldoScene.Introduction, _) => ("Beldo", T("The egg will be mine.", "O ovo será meu.")),
                (BeldoScene.Rescue, 0) => ("Kewne", T($"{HeroName}! I'm still on your side.", $"{HeroName}! Ainda estou do seu lado.")),
                (BeldoScene.Rescue, 1) => ("Kewne", T("This is Seraphim, your father's sword. Take it!", "Esta é a Seraphim, a espada do seu pai. Pegue!")),
                (BeldoScene.Rescue, _) => ("Kewne", T("It's over. The Ultimate Egg is yours.", "Acabou. O Ovo Supremo é seu.")),
                _ => (HeroName, T("Beldo is defeated. The Ultimate Egg and my father's sword are finally safe.",
                    "Beldo foi derrotado. O Ovo Supremo e a espada do meu pai finalmente estão a salvo."))
            };
            var panel = new Rect(width * 0.1f, height * 0.75f, width * 0.8f, height * 0.21f);
            DrawHudPanel(panel);
            DrawHudText(new Rect(panel.x + 12f, panel.y + 7f, panel.width - 24f, 22f), speaker, _bodyStyle);
            DrawHudText(new Rect(panel.x + 12f, panel.y + 30f, panel.width - 24f, panel.height - 39f), line, _messageStyle);
            if (GUI.Button(new Rect(panel.xMax - 112f, panel.yMax - 26f, 103f, 21f), WithInteractHint("Next", "Avançar")))
                AdvanceBeldoDialogue();
        }
    }
}
