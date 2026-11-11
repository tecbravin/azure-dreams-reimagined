using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string BgmVolumePref = "ad_settings_bgm";
        private const string SfxVolumePref = "ad_settings_sfx";
        private const float DefaultMusicVolume = 0.32f;
        private const float MappingRowHeight = 28f;
        private float _bgmVolume = 1f, _sfxVolume = 1f;
        private bool _audioSettingsDirty, _settingsOpen, _mappingOpen, _graphicsSettingsOpen;
        private int _settingsRow, _mappingRow, _rebindingRow = -1, _rebindStartFrame;
        private string _mappingNotice;
        private Vector2 _mappingScroll;
        private GUIStyle _settingsHeadingStyle;
        private InputAction _attackAction, _returnAction, _faceAction, _rotateLeftAction, _rotateRightAction;
        private InputAction[] _settingsOwnedActions;
        private (InputAction action, int index, string en, string pt)[] _mappingRows;

        private void InitializeSettings()
        {
            _bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePref, 1f));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePref, 1f));
            ApplyAudioSettings();

            _attackAction = new InputAction("Attack", InputActionType.Button, "<Keyboard>/f");
            _returnAction = new InputAction("Return to town", InputActionType.Button, "<Keyboard>/r");
            _faceAction = new InputAction("Face direction", InputActionType.Button, "<Keyboard>/rightCtrl");
            _rotateLeftAction = new InputAction("Rotate left", InputActionType.Button, "<Keyboard>/pageUp");
            _rotateRightAction = new InputAction("Rotate right", InputActionType.Button, "<Keyboard>/pageDown");
            _settingsOwnedActions = new[] { _attackAction, _returnAction, _faceAction, _rotateLeftAction, _rotateRightAction };
            _mappingRows = new (InputAction, int, string, string)[]
            {
                (_move, 1, "Move up", "Mover para cima"),
                (_move, 2, "Move down", "Mover para baixo"),
                (_move, 3, "Move left", "Mover para a esquerda"),
                (_move, 4, "Move right", "Mover para a direita"),
                (_move, 6, "Move up (alternate)", "Mover para cima (alternativo)"),
                (_move, 7, "Move down (alternate)", "Mover para baixo (alternativo)"),
                (_move, 8, "Move left (alternate)", "Mover para a esquerda (alternativo)"),
                (_move, 9, "Move right (alternate)", "Mover para a direita (alternativo)"),
                (_interact, 0, "Interact / confirm / face", "Interagir / confirmar / virar"),
                (_wait, 0, "Jump / wait", "Pular / esperar"),
                (_runAction, 0, "Run", "Correr"),
                (_liftAction, 0, "Lift / put down / sort", "Levantar / baixar / ordenar"),
                (_attackAction, 0, "Attack / throw", "Atacar / arremessar"),
                (_inventoryAction, 0, "Inventory", "Inventário"),
                (_familiarCommand, 0, "Familiar follow / wait", "Familiar seguir / esperar"),
                (_mapAction, 0, "Map", "Mapa"),
                (_faceAction, 0, "Face direction", "Virar sem andar"),
                (_returnAction, 0, "Return to town", "Voltar à cidade"),
                (_rotateLeftAction, 0, "Rotate camera left", "Girar câmera à esquerda"),
                (_rotateRightAction, 0, "Rotate camera right", "Girar câmera à direita"),
            };
            foreach (var row in _mappingRows)
            {
                string path = PlayerPrefs.GetString(BindingPreference(row.action, row.index));
                if (!string.IsNullOrEmpty(path)) row.action.ApplyBindingOverride(row.index, path);
            }
            foreach (var action in _settingsOwnedActions) action.Enable();
        }

        private static string BindingPreference(InputAction action, int index) => $"ad_settings_key_{action.name}_{index}";
        private string InteractKey => _interact?.GetBindingDisplayString() ?? "E";
        private string WithInteractHint(string en, string pt) => $"{T(en, pt)} [{InteractKey}]";

        private void ApplyAudioSettings()
        {
            SetAreaMusicVolume();
            if (_levelUpMusic) _levelUpMusic.volume = DefaultMusicVolume * _bgmVolume;
            if (_entranceAudio) _entranceAudio.volume = _sfxVolume;
            if (_elevatorAudio) _elevatorAudio.volume = _sfxVolume;
            if (_gameplayAudio) _gameplayAudio.volume = _sfxVolume;
        }

        private void SetAudioSettings(float bgm, float sfx)
        {
            if (bgm == _bgmVolume && sfx == _sfxVolume) return;
            _bgmVolume = bgm;
            _sfxVolume = sfx;
            PlayerPrefs.SetFloat(BgmVolumePref, bgm);
            PlayerPrefs.SetFloat(SfxVolumePref, sfx);
            _audioSettingsDirty = true;
            ApplyAudioSettings();
        }

        private void SaveAudioSettings()
        {
            if (!_audioSettingsDirty) return;
            PlayerPrefs.Save();
            _audioSettingsDirty = false;
        }

        private void ReleaseSettings()
        {
            CloseSettings();
            if (_settingsOwnedActions == null) return;
            foreach (var action in _settingsOwnedActions) action.Dispose();
            _settingsOwnedActions = null;
            _mappingRows = null;
        }

        private void ResetSettingsInput()
        {
            _rebindingRow = -1;
            SaveAudioSettings();
            if (_settingsOwnedActions == null) return;
            foreach (var action in _settingsOwnedActions) action.Reset();
        }

        private void OpenSettings()
        {
            _settingsOpen = true;
            _settingsRow = 0;
            _towerInteractPending = false;
            ResetTowerPushHold();
            if (_area == Area.Town)
            {
                ResetPush();
                if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false);
            }
        }

        private void CloseSettings()
        {
            _settingsOpen = _mappingOpen = _graphicsSettingsOpen = false;
            _rebindingRow = -1;
            SaveAudioSettings();
        }

        private bool HandleSettingsInput()
        {
            var keys = Keyboard.current;
            if (keys == null) return _settingsOpen;
            if (!_settingsOpen)
            {
                if (!keys.f5Key.wasPressedThisFrame) return false;
                OpenSettings();
                return true;
            }
            if (keys.f5Key.wasPressedThisFrame) { CloseSettings(); return true; }
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (_rebindingRow >= 0) _rebindingRow = -1;
                else if (_mappingOpen || _graphicsSettingsOpen) _mappingOpen = _graphicsSettingsOpen = false;
                else CloseSettings();
                return true;
            }
            if (_rebindingRow >= 0)
            {
                if (Time.frameCount > _rebindStartFrame)
                {
                    foreach (var key in keys.allKeys)
                    {
                        if (!key.wasPressedThisFrame) continue;
                        RebindSettingsKey(key);
                        break;
                    }
                }
                return true;
            }

            if (_graphicsSettingsOpen) return true;

            int vertical = keys.upArrowKey.wasPressedThisFrame ? -1 : keys.downArrowKey.wasPressedThisFrame ? 1 : 0;
            bool confirm = keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame;
            if (_mappingOpen)
            {
                if (vertical != 0)
                {
                    _mappingRow = Mathf.Clamp(_mappingRow + vertical, 0, _mappingRows.Length - 1);
                    _mappingScroll.y = _mappingRow * MappingRowHeight;
                }
                if (confirm) BeginRebinding(_mappingRow);
            }
            else
            {
                _settingsRow = Mathf.Clamp(_settingsRow + vertical, 0, 4);
                int horizontal = keys.leftArrowKey.wasPressedThisFrame ? -1 : keys.rightArrowKey.wasPressedThisFrame ? 1 : 0;
                if (_settingsRow == 0 && horizontal != 0)
                    SetAudioSettings(Mathf.Clamp01(_bgmVolume + horizontal * 0.05f), _sfxVolume);
                if (_settingsRow == 1 && horizontal != 0)
                    SetAudioSettings(_bgmVolume, Mathf.Clamp01(_sfxVolume + horizontal * 0.05f));
                if (_settingsRow == 2 && (horizontal != 0 || confirm))
                    GameLanguage.Select(GameLanguage.Portuguese ? "en" : "pt-BR");
                if (_settingsRow == 3 && confirm) OpenGraphicsSettings();
                if (_settingsRow == 4 && confirm) OpenInputMapping();
            }
            return true;
        }

        private void OpenGraphicsSettings()
        {
            _settingsRow = 3;
            _graphicsScroll = Vector2.zero;
            _graphicsSettingsOpen = true;
        }

        private void OpenInputMapping()
        {
            _mappingOpen = true;
            _mappingRow = 0;
            _mappingScroll = Vector2.zero;
            _mappingNotice = null;
        }

        private void BeginRebinding(int row)
        {
            _mappingRow = _rebindingRow = row;
            _mappingNotice = null;
            _rebindStartFrame = Time.frameCount;
        }

        private void RebindSettingsKey(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            var row = _mappingRows[_rebindingRow];
            Key code = key.keyCode;
            bool navigationKey = code is Key.UpArrow or Key.DownArrow or Key.LeftArrow or Key.RightArrow;
            if ((navigationKey && row.action != _move) ||
                code is Key.Enter or Key.NumpadEnter or Key.LeftAlt or Key.RightAlt or Key.LeftMeta or Key.RightMeta
                or Key.F1 or Key.F2 or Key.F5 or Key.Numpad7 or Key.Numpad8 or Key.Numpad9)
            {
                _mappingNotice = T("This key is reserved. Try another key.", "Essa tecla é reservada. Tente outra.");
                return;
            }
            if (InputControlPath.Matches(row.action.bindings[row.index].effectivePath, key))
            {
                _rebindingRow = -1;
                _mappingNotice = null;
                return;
            }
            foreach (var other in _mappingRows)
            {
                if (!InputControlPath.Matches(other.action.bindings[other.index].effectivePath, key)) continue;
                _mappingNotice = T($"Already assigned to {other.en}.", $"Já atribuída a: {other.pt}.");
                return;
            }
            string path = "<Keyboard>/" + key.name;
            row.action.ApplyBindingOverride(row.index, path);
            PlayerPrefs.SetString(BindingPreference(row.action, row.index), path);
            PlayerPrefs.Save();
            _rebindingRow = -1;
            _mappingNotice = null;
        }

        private void RestoreDefaultBindings()
        {
            foreach (var row in _mappingRows)
            {
                row.action.RemoveBindingOverride(row.index);
                PlayerPrefs.DeleteKey(BindingPreference(row.action, row.index));
            }
            PlayerPrefs.Save();
            _rebindingRow = -1;
            _mappingNotice = T("Default keys restored.", "Teclas padrão restauradas.");
        }

        private void DrawSettingsPanel(float width, float height)
        {
            DrawHudFill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, 0.45f));
            float panelWidth = Mathf.Min(width - 20f, 405f);
            float panelHeight = Mathf.Min(height - 20f, _mappingOpen || _graphicsSettingsOpen ? 402f : 220f);
            var panel = new Rect((width - panelWidth) / 2f, (height - panelHeight) / 2f, panelWidth, panelHeight);
            DrawHudPanel(panel);
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f));
            string heading = _mappingOpen ? T("Input Mapping", "Mapeamento de controles") :
                _graphicsSettingsOpen ? T("Graphics Effects", "Efeitos gráficos") : T("Settings", "Configurações");
            _settingsHeadingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            GUILayout.Label(heading, _settingsHeadingStyle);
            if (_mappingOpen) DrawInputMappingRows();
            else if (_graphicsSettingsOpen)
            {
                DrawGraphicsSettings();
                if (GUILayout.Button(T("Back [Esc]", "Voltar [Esc]"))) _graphicsSettingsOpen = false;
            }
            else
            {
                float bgm = DrawVolumeSlider(0, T("BGM", "Música"), _bgmVolume);
                float sfx = DrawVolumeSlider(1, T("SFX", "Efeitos sonoros"), _sfxVolume);
                SetAudioSettings(bgm, sfx);
                GUILayout.BeginHorizontal();
                GUILayout.Label((_settingsRow == 2 ? "› " : "") + T("Language", "Idioma"), GUILayout.Width(110f));
                DrawSettingsLanguage("en", "English", !GameLanguage.Portuguese);
                DrawSettingsLanguage("pt-BR", "Português (Brasil)", GameLanguage.Portuguese);
                GUILayout.EndHorizontal();
                if (GUILayout.Button((_settingsRow == 3 ? "› " : "") + T("Graphics Effects", "Efeitos gráficos")))
                    OpenGraphicsSettings();
                if (GUILayout.Button((_settingsRow == 4 ? "› " : "") + T("Input Mapping", "Mapeamento de controles")))
                    OpenInputMapping();
                if (GUILayout.Button(T("Close [Esc]", "Fechar [Esc]"))) CloseSettings();
            }
            GUILayout.EndArea();
        }

        private void DrawSettingsLanguage(string code, string label, bool selected)
        {
            if (!GUILayout.Button((selected ? "› " : "") + label)) return;
            _settingsRow = 2;
            GameLanguage.Select(code);
        }

        private float DrawVolumeSlider(int row, string label, float volume)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label((_settingsRow == row ? "› " : "") + label, GUILayout.Width(110f));
            float updated = GUILayout.HorizontalSlider(volume, 0f, 1f);
            if (updated != volume) _settingsRow = row;
            GUILayout.Label($"{Mathf.RoundToInt(updated * 100f)}%", GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            return updated;
        }

        private void DrawInputMappingRows()
        {
            GUILayout.Label(_mappingNotice ?? (_rebindingRow >= 0
                ? T("Press a new key, or Esc to cancel.", "Pressione uma nova tecla ou Esc para cancelar.")
                : T("Choose a control to change its key.", "Escolha um controle para trocar sua tecla.")));
            _mappingScroll = GUILayout.BeginScrollView(_mappingScroll);
            for (int i = 0; i < _mappingRows.Length; i++)
            {
                var row = _mappingRows[i];
                GUILayout.BeginHorizontal(GUILayout.Height(MappingRowHeight - 4f));
                GUILayout.Label((i == _mappingRow ? "› " : "") + T(row.en, row.pt));
                string binding = i == _rebindingRow ? "…" : row.action.GetBindingDisplayString(row.index);
                if (GUILayout.Button(binding, GUILayout.Width(125f))) BeginRebinding(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(T("Fixed controls: Esc (back), mouse wheel (zoom)", "Controles fixos: Esc (voltar), roda do mouse (zoom)"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Restore default keys", "Restaurar teclas padrão"))) RestoreDefaultBindings();
            if (GUILayout.Button(T("Back [Esc]", "Voltar [Esc]")))
            {
                _rebindingRow = -1;
                _mappingOpen = false;
            }
            GUILayout.EndHorizontal();
        }

        private string ControlsHint()
        {
            string move = string.Join("/", _move.GetBindingDisplayString(1), _move.GetBindingDisplayString(3),
                _move.GetBindingDisplayString(2), _move.GetBindingDisplayString(4));
            string interact = InteractKey;
            string inventory = _inventoryAction.GetBindingDisplayString();
            string wait = _wait.GetBindingDisplayString();
            string run = _runAction.GetBindingDisplayString();
            string lift = _liftAction.GetBindingDisplayString();
            string attack = _attackAction.GetBindingDisplayString();
            string map = _mapAction.GetBindingDisplayString();
            string familiar = _familiarCommand.GetBindingDisplayString();
            string turn = _faceAction.GetBindingDisplayString();
            string leave = _returnAction.GetBindingDisplayString();
            string rotate = _rotateLeftAction.GetBindingDisplayString() + "/" + _rotateRightAction.GetBindingDisplayString();
            return _area == Area.Town
                ? T($"{move}: move   {interact}: interact   {wait}: jump\n{run}: run   {lift}: lift   {inventory}: inventory\nWheel: zoom   {rotate}: rotate camera\nF5: settings",
                    $"{move}: mover   {interact}: interagir   {wait}: pular\n{run}: correr   {lift}: levantar   {inventory}: inventário\nRoda do mouse: zoom   {rotate}: girar câmera\nF5: configurações")
                : T($"{move}: move   {interact} or {turn} + direction: face\n{attack}: attack   {wait}: wait   {interact}: interact   {leave}: return\n{map}: map   {lift}: lift   {inventory}: inventory   {familiar}: familiar\nHold toward an enemy to push; hold {interact} too to stay in place\nHold {run}+{lift} to pass turns   F5: settings",
                    $"{move}: mover   {interact} ou {turn} + direção: virar\n{attack}: atacar   {wait}: esperar   {interact}: interagir   {leave}: voltar\n{map}: mapa   {lift}: levantar   {inventory}: inventário   {familiar}: familiar\nSegure na direção do inimigo para empurrar. Segure {interact} junto para ficar no lugar\nSegure {run}+{lift} para passar turnos   F5: opções");
        }
    }
}
