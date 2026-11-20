using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private Texture2D _hudGoldIcon, _hudGearIcon;
        private ArtStyle? _hudIconStyle;

        private void LoadCornerHudIcons()
        {
            if (!_hudGearIcon) _hudGearIcon = Resources.Load<Texture2D>("UI/Hud/gear");
            if (!_hudGoldIcon || _hudIconStyle != AssetMode.Current)
            {
                _hudGoldIcon = AssetMode.Load<Texture2D>("UI/Hud/gold_coin");
                _hudIconStyle = AssetMode.Current;
            }
        }

        private void DrawTownMoneyHud(float width)
        {
            LoadCornerHudIcons();
            const float moneyWidth=159f*.6f;
            var plaque = new Rect(width-16f-moneyWidth,24f,moneyWidth,34f);
            DrawRetailHudPlaque(plaque);
            if (_hudGoldIcon)
                GUI.DrawTexture(new Rect(plaque.x+5f,plaque.y+9f,16f,16f),_hudGoldIcon,ScaleMode.ScaleToFit,true);
            DrawInventoryFittedText(new Rect(plaque.x+24f,plaque.y+3f,plaque.width-29f,28f),$"{_gold}G",InventoryEmphasisStyle(_bodyStyle));
        }

        private static Rect CornerSettingsButton(float width,bool town) =>
            new Rect(width-42f,town ? 65f : 58f,26f,26f);

        private void DrawCornerSettingsButton(Rect button)
        {
            LoadCornerHudIcons();

            if (GUI.Button(button,new GUIContent(string.Empty,T("Settings [O]","Configurações [O]"))))
            {
                PlayRetailSound(RetailSfx.Confirm);
                OpenSettings();
            }
            if (_hudGearIcon)
                GUI.DrawTexture(new Rect(button.x+3f,button.y+3f,18f,18f),_hudGearIcon,ScaleMode.ScaleToFit,true);
        }
    }
}
