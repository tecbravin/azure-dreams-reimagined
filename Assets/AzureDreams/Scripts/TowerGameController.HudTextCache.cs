namespace AzureDreamsReimagined
{
    // The tower HUD is IMGUI: OnGUI runs at least twice per frame (Layout + Repaint).
    // Rebuild its number strings only when the value (or language) changes instead of
    // interpolating new strings on every GUI event.
    public sealed partial class TowerGameController
    {
        private int _hudLevelValue = int.MinValue;
        private string _hudLevelText;
        private int _hudHpValue = int.MinValue, _hudMaxHpValue = int.MinValue;
        private bool _hudHpPortuguese;
        private string _hudHpText, _hudMaxHpText;
        private int _hudLootValue = int.MinValue;
        private string _hudLootText;
        private int _hudFloorValue = int.MinValue;
        private bool _hudFloorPortuguese, _hudFloorSecondTower;
        private string _hudFloorText;

        private string HudLevelText(int level)
        {
            if (_hudLevelText == null || level != _hudLevelValue)
            {
                _hudLevelValue = level;
                _hudLevelText = $"{level}";
            }
            return _hudLevelText;
        }

        private void RefreshHudHpText(int currentHp, int maxHp)
        {
            bool portuguese = GameLanguage.Portuguese;
            if (_hudHpText != null && currentHp == _hudHpValue && maxHp == _hudMaxHpValue && portuguese == _hudHpPortuguese)
                return;
            _hudHpValue = currentHp;
            _hudMaxHpValue = maxHp;
            _hudHpPortuguese = portuguese;
            if (portuguese)
            {
                _hudHpText = $"{currentHp:000} / {maxHp:000} M\u00c1X";
                _hudMaxHpText = null;
            }
            else
            {
                _hudHpText = $"{currentHp:000} /";
                _hudMaxHpText = $"MAX {maxHp:000}";
            }
        }

        private string HudLootText()
        {
            if (_hudLootText == null || _runLoot != _hudLootValue)
            {
                _hudLootValue = _runLoot;
                _hudLootText = $"{_runLoot}G";
            }
            return _hudLootText;
        }

        private string CachedHudFloorText()
        {
            bool portuguese = GameLanguage.Portuguese;
            bool secondTower = InSecondTower;
            if (_hudFloorText == null || _floor != _hudFloorValue || portuguese != _hudFloorPortuguese ||
                secondTower != _hudFloorSecondTower)
            {
                _hudFloorValue = _floor;
                _hudFloorPortuguese = portuguese;
                _hudFloorSecondTower = secondTower;
                _hudFloorText = HudFloorText;
            }
            return _hudFloorText;
        }
    }
}
