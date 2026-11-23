using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private bool _showTitleOnStart;
        [SerializeField] private string[] _openingFiles;
        [SerializeField] private Texture2D _titleBackground;
        [SerializeField] private Texture2D _titleMenuCrystal;
        private GUIStyle _titleOptionStyle;

        private enum TitlePage { Closed, Opening, Main, Continue, NewGame, Options, Manual, Gallery }
        private const int ManualPageCount = 32;
        private const int FamiliarPortraitCount = 50;
        private TitlePage _titlePage;
        private int _titleSelection;
        private int _titleSubSelection;
        private int _openingIndex;
        private VideoPlayer _openingPlayer;
        private AudioSource _cinematicAudio;
        private float _openingPrepareDeadline;
        private readonly SaveSystem.Checkpoint[] _titleSlots = new SaveSystem.Checkpoint[SaveSystem.SlotCount];
        private bool _titleAutosave;
        private SaveSystem.Checkpoint _titleAutomaticSummary;
        private bool _titleHasSave;
        private int _manualPage = 1;
        private int _galleryTab;
        private int _familiarPortrait = 1;
        private Vector2 _titleImageScroll;
        private bool _manualZoom;
        private float _titleMainStarted;
        private const float TitleAscentSeconds = 3.2f;
        private const float TitleMenuArrivalSeconds = TitleAscentSeconds + 105f / 60f;

        private bool HasAutomaticProgress => SaveSystem.HasSave ||
            TownSaveVariables.HasValue("gold") || TownSaveVariables.HasValue("level") ||
            TownSaveVariables.HasValue("item_Healing Draught");

        private void ShowTitle()
        {
            _titleConfirmRow = -1;
            _titleAutosave = HasAutomaticProgress;
            _titleAutomaticSummary = _titleAutosave ? ReadAutomaticSaveSummary() : null;
            _titleHasSave = _titleAutosave;
            for (int i = 0; i < _titleSlots.Length; i++)
            {
                _titleSlots[i] = SaveSystem.ReadSlot(i);
                _titleHasSave |= _titleSlots[i] != null;
            }
            _titleSelection = _titleHasSave ? 1 : 0;
            if (_openingFiles == null || _openingFiles.Length == 0)
            {
                _titlePage = TitlePage.Main;
                _titleMainStarted = Time.unscaledTime;
                return;
            }
            _titlePage = TitlePage.Opening;
            _openingPlayer = gameObject.AddComponent<VideoPlayer>();
            _openingPlayer.playOnAwake = false;
            _openingPlayer.renderMode = VideoRenderMode.APIOnly;
            _openingPlayer.source = VideoSource.Url;
            ConfigureCinematicAudio(_openingPlayer);
            _openingPlayer.prepareCompleted += PlayOpeningClip;
            _openingPlayer.loopPointReached += AdvanceOpening;
            _openingPlayer.errorReceived += OpeningFailed;
            PrepareOpeningClip();
        }

        private void PrepareOpeningClip()
        {
            if (_openingIndex >= _openingFiles.Length)
            {
                EndOpening();
                return;
            }
            _openingPlayer.url = Application.streamingAssetsPath + "/Title/" + _openingFiles[_openingIndex];
            _openingPrepareDeadline = Time.realtimeSinceStartup + 12f;
            _openingPlayer.Prepare();
        }

        private void PlayOpeningClip(VideoPlayer source)
        {
            if (_titlePage != TitlePage.Opening || source != _openingPlayer) return;
            PlayCinematic(source);
        }

        private void ConfigureCinematicAudio(VideoPlayer video)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            video.audioOutputMode = VideoAudioOutputMode.Direct;
#else
            if (!_cinematicAudio)
            {
                _cinematicAudio = gameObject.AddComponent<AudioSource>();
                _cinematicAudio.playOnAwake = false;
                _cinematicAudio.spatialBlend = 0f;
            }
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            if (video.canSetTimeUpdateMode) video.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
            if (video.canSetSkipOnDrop) video.skipOnDrop = true;
#endif
            video.controlledAudioTrackCount = 1;
            video.EnableAudioTrack(0, true);
            if (video.audioOutputMode == VideoAudioOutputMode.AudioSource)
                video.SetTargetAudioSource(0, _cinematicAudio);
        }

        private void PlayCinematic(VideoPlayer video)
        {
            if (video.audioOutputMode == VideoAudioOutputMode.AudioSource)
                _cinematicAudio.volume = _bgmVolume;
            else if (video.audioTrackCount > 0)
                video.SetDirectAudioVolume(0, _bgmVolume);
            video.Play();
        }

        private void AdvanceOpening(VideoPlayer source)
        {
            if (_titlePage != TitlePage.Opening || source != _openingPlayer) return;
            source.Stop();
            _openingIndex++;
            PrepareOpeningClip();
        }

        private void OpeningFailed(VideoPlayer source, string error)
        {
            if (_titlePage == TitlePage.Opening && source == _openingPlayer) EndOpening();
        }

        private void EndOpening()
        {
            _titlePage = TitlePage.Main;
            _titleMainStarted = Time.unscaledTime;
            ReleaseOpeningPlayer();
        }

        private void ReleaseOpeningPlayer()
        {
            if (!_openingPlayer) return;
            _openingPlayer.prepareCompleted -= PlayOpeningClip;
            _openingPlayer.loopPointReached -= AdvanceOpening;
            _openingPlayer.errorReceived -= OpeningFailed;
            _openingPlayer.Stop();
            Destroy(_openingPlayer);
            _openingPlayer = null;
        }

        private bool HandleTitleInput()
        {
            if (_titlePage == TitlePage.Closed) return false;
            if (_titleConfirmRow >= 0) { TickTitleConfirmation(); return true; }
            var keys = Keyboard.current;
            bool confirm = keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) ||
                _interact?.WasPressedThisFrame() == true;
            bool back = keys != null && (_cancel?.WasPressedThisFrame() == true);
            if (_titlePage == TitlePage.Opening)
            {
                if (confirm || back || (!_openingPlayer.isPrepared && Time.realtimeSinceStartup > _openingPrepareDeadline))
                    EndOpening();
                return true;
            }
            if (_settingsOpen) { HandleSettingsInput(); return true; }
            if (back) { BackFromTitlePage(); return true; }
            if (keys == null) return true;
            int step = (MenuDirectionPressed(2) ? 1 : 0) - (MenuDirectionPressed(1) ? 1 : 0);
            if (_titlePage == TitlePage.Main)
            {
                if (Time.unscaledTime - _titleMainStarted < TitleMenuArrivalSeconds && (confirm || step != 0))
                {
                    _titleMainStarted = Time.unscaledTime - TitleMenuArrivalSeconds - 1f;
                    return true;
                }
                int previous = _titleSelection;
                if (step != 0)
                {
                    do { _titleSelection = (_titleSelection + step + 4) % 4; }
                    while (_titleSelection == 1 && !_titleHasSave);
                }
                if (_titleSelection != previous) PlayRetailSound(RetailSfx.Cursor);
                if (confirm) SelectTitleOption(_titleSelection);
            }
            else if (_titlePage == TitlePage.Options)
            {
                if (step != 0) _titleSubSelection = 1 - _titleSubSelection;
                if (step != 0) PlayRetailSound(RetailSfx.Cursor);
                if (confirm) SelectTitleSettings(_titleSubSelection);
            }
            else if (_titlePage == TitlePage.Continue)
            {
                int previous = _titleSubSelection;
                if (step != 0)
                {
                    int count = _titleSlots.Length + 1;
                    do { _titleSubSelection = (_titleSubSelection + step + count) % count; }
                    while (!CanLoadTitleEntry(_titleSubSelection));
                }
                if (_titleSubSelection != previous) PlayRetailSound(RetailSfx.Cursor);
                if (confirm) LoadTitleEntry(_titleSubSelection);
            }
            else if (_titlePage == TitlePage.NewGame && confirm)
            {
                PlayRetailSound(RetailSfx.Confirm);
                BeginNewGame();
            }
            else if (_titlePage == TitlePage.Manual)
                ChangeManualPage((MenuDirectionPressed(4) ? 1 : 0) - (MenuDirectionPressed(3) ? 1 : 0));
            return true;
        }

        private void SelectTitleOption(int row)
        {
            if (_titleConfirmRow >= 0 || row == 1 && !_titleHasSave) return;
            _titleSelection = row;
            if (row is 0 or 1)
            {
                _titleConfirmRow = row;
                _titleConfirmStarted = Time.unscaledTime;
                PlayRetailSound(RetailSfx.TitleStart);
                return;
            }
            PlayRetailSound(RetailSfx.TitleOptions);
            CompleteTitleOption(row);
        }

        private void CompleteTitleOption(int row)
        {
            switch (row)
            {
                case 0:
                    if (_titleAutosave) _titlePage = TitlePage.NewGame;
                    else BeginNewGame();
                    break;
                case 1:
                    if (!_titleHasSave) break;
                    _titlePage = TitlePage.Continue;
                    _titleSubSelection = System.Array.FindIndex(_titleSlots, slot => slot != null) + 1;
                    ResetSaveListScroll();
                    break;
                case 2:
                    _titlePage = TitlePage.Options;
                    _titleSubSelection = 0;
                    break;
                case 3: _titlePage = TitlePage.Gallery; break;
            }
        }

        private bool CanLoadTitleEntry(int row) => row == 0 ? _titleAutosave : _titleSlots[row - 1] != null;

        private void LoadTitleEntry(int row)
        {
            if (!CanLoadTitleEntry(row)) return;
            PlayRetailSound(RetailSfx.Pickup);
            BeginTitleGame(row == 0 ? null : _titleSlots[row - 1]);
        }

        private void SelectTitleSettings(int row)
        {
            _titleSubSelection = row;
            PlayRetailSound(RetailSfx.Confirm);
            switch (row)
            {
                case 0: OpenSettings(); break;
                case 1: _titlePage = TitlePage.Manual; break;
            }
        }

        private void BeginNewGame()
        {
            _unsavedPlaySeconds = 0f;
            SaveSystem.Clear();
            TownSaveVariables.Clear();
            _titlePage = TitlePage.Closed;
            StartPrologue();
        }

        private void BeginTitleGame(SaveSystem.Checkpoint checkpoint)
        {
            _titlePage = TitlePage.Closed;
            if (checkpoint == null) StartPlayableGame();
            else StartCoroutine(LoadCheckpoint(checkpoint));
        }

        private bool _townCollectionOpen;
        private void OpenTownCollection(TitlePage page)
        {
            _townCollectionOpen = true;
            _titleImageScroll = Vector2.zero;
            if (page == TitlePage.Gallery) _galleryTab = 0;
            if (page == TitlePage.Manual) { _manualPage = 1; _manualZoom = false; }
            _titlePage = page;
        }

        private void OpenJordaManual() => OpenTownCollection(TitlePage.Manual);

        private void BackFromTitlePage()
        {
            if (_titlePage == TitlePage.Main) return;
            PlayRetailSound(RetailSfx.Cancel);
            _titleImageScroll = Vector2.zero;
            if (_townCollectionOpen)
            {
                _townCollectionOpen = false;
                _titlePage = TitlePage.Closed;
                return;
            }
            _titlePage = _titlePage == TitlePage.Manual ? TitlePage.Options : TitlePage.Main;
        }

        private void ChangeManualPage(int step)
        {
            int next = Mathf.Clamp(_manualPage + step, 1, ManualPageCount);
            if (next == _manualPage) return;
            PlayRetailSound(RetailSfx.Cursor);
            _manualPage = next;
            _titleImageScroll = Vector2.zero;
        }

        private void DrawTitle(float width, float height)
        {
            var screen = new Rect(0, 0, width, height);
            GUI.DrawTexture(screen, Texture2D.blackTexture);
            if (_titlePage == TitlePage.Opening)
            {
                if (_openingPlayer && _openingPlayer.texture)
                {
                    var texture = _openingPlayer.texture;
                    // The 4K opening was encoded with 120 pixels of side padding on each edge.
                    bool hasSidePadding = _openingPlayer.width == 3840 && _openingPlayer.height == 2160 &&
                        _openingFiles[_openingIndex] == "LOP1_WS.mp4";
                    const float sidePadding = 120f / 3840f;
                    var videoUv = hasSidePadding ? new Rect(sidePadding, 0f, 1f - 2f * sidePadding, 1f) :
                        new Rect(0f, 0f, 1f, 1f);
                    GUI.DrawTextureWithTexCoords(screen, texture, videoUv, alphaBlend: false);
                }
                return;
            }
            bool main = _titlePage == TitlePage.Main;
            if (_titleBackground)
            {
                // The original 320x480 title art contains the town beneath the
                // tower. Travel upward through it before settling on the logo.
                float artAspect = (float)_titleBackground.width / _titleBackground.height;
                float visibleHeight = Mathf.Clamp(height / width * artAspect, 0.49f, 1f);
                float ascent = main ? Mathf.Clamp01((Time.unscaledTime - _titleMainStarted) / TitleAscentSeconds) : 1f;
                ascent = 1f - Mathf.Pow(1f - ascent, 3f);
                GUI.DrawTextureWithTexCoords(screen, _titleBackground,
                    new Rect(0f, (1f - visibleHeight) * ascent, 1f, visibleHeight));
                if (main) DrawTitleFlare(screen, ascent);
                if (!main || _settingsOpen)
                    DrawHudFill(screen, new Color(0.025f, 0.035f, 0.06f, 0.76f));
            }
            if (_settingsOpen) { DrawSettingsPanel(width, height); return; }
            if (main) { DrawTitleMenu(screen); return; }

            if (_titlePage == TitlePage.Manual || _titlePage == TitlePage.Gallery)
            {
                DrawTitleCollection(width, height);
                return;
            }

            if (_titlePage == TitlePage.Continue)
            {
                DrawSaveSlotList(width, height, T("Continue", "Continuar"), _titleSlots, _titleSubSelection - 1,
                    loading: true, index => LoadTitleEntry(index + 1), BackFromTitlePage, _titleAutomaticSummary);
                return;
            }

            float panelWidth = Mathf.Min(width - 24f, 366f);
            const float panelHeight = 201f;
            float panelX = (width - panelWidth) / 2f;
            float panelY = (height - panelHeight) / 2f;
            var panel = new Rect(panelX, panelY, panelWidth, panelHeight);
            DrawHudPanel(panel);
            if (_titlePage == TitlePage.NewGame)
            {
                DrawInventoryText(new Rect(panel.x + 13f, panel.y + 10f, panel.width - 26f, 27f),
                    T("Start a new game?", "Iniciar um novo jogo?"), _bodyStyle);
                DrawInventoryText(new Rect(panel.x + 13f, panel.y + 42f, panel.width - 26f, 53f),
                    T("This replaces automatic progress. Numbered saves are kept.",
                        "O progresso automático será substituído. Os saves numerados serão mantidos."),
                    _messageStyle);
                if (DrawInventoryButton(new Rect(panel.x + 13f, panel.y + 105f, panel.width - 26f, 30f),
                    T("Start new game", "Iniciar novo jogo"))) BeginNewGame();
                if (DrawInventoryButton(new Rect(panel.x + 13f, panel.y + 142f, panel.width - 26f, 30f),
                    WithCancelHint("Back", "Voltar"))) BackFromTitlePage();
                return;
            }
            GUILayout.BeginArea(WindowDesignSystem.Content(panel, footer: true));
            switch (_titlePage)
            {
                case TitlePage.Options:
                    GUILayout.Label(T("Menu", "Menu"), _bodyStyle);
                    if (DrawTitleSubButton(0, T("Settings", "Configurações"))) SelectTitleSettings(0);
                    if (DrawTitleSubButton(1, T("Manual", "Manual"))) SelectTitleSettings(1);
                    break;
            }
            if (GUILayout.Button(WithCancelHint("Back", "Voltar"))) BackFromTitlePage();
            GUILayout.EndArea();
        }

        private void DrawTitleMenu(Rect screen)
        {
            float elapsed = Time.unscaledTime - _titleMainStarted;
            if (elapsed < TitleMenuArrivalSeconds) return;
            _titleOptionStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            float rowHeight = Mathf.Min(42f, Mathf.Min(screen.height * 0.073f, screen.width / 8f));
            float rowWidth = rowHeight * 8f;
            // Leave ~22% headroom below rowHeight: setting fontSize equal to the
            // row's pixel height left zero margin for the font's actual ascent+descent,
            // which OpenDyslexic's taller metrics overflowed (title text clipped at top).
            _titleOptionStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(rowHeight * 0.78f));
            float left = (screen.width - rowWidth) * 0.5f;
            float arrival = Mathf.Clamp01((elapsed - TitleMenuArrivalSeconds) / 0.65f);
            float lowestTop = screen.height - 4f * rowHeight - 21f;
            float top = Mathf.Lerp(lowestTop, Mathf.Min(lowestTop, screen.height * 0.59f), arrival);

            if (!_titleBackground)
                GUI.Label(new Rect(0f, screen.height * 0.30f, screen.width, rowHeight * 2f),
                    "Azure Dreams", _titleOptionStyle);

            DrawDialogueFrame(new Rect(left - 10f, top - 7f, rowWidth + 20f, 4f * rowHeight + 14f));
            DrawTitleOption(new Rect(left, top, rowWidth, rowHeight), 0, T("New Game", "Novo jogo"), arrival: arrival);
            DrawTitleOption(new Rect(left, top + rowHeight, rowWidth, rowHeight), 1, T("Continue", "Continuar"), _titleHasSave, arrival);
            DrawTitleOption(new Rect(left, top + 2f * rowHeight, rowWidth, rowHeight), 2, T("Menu", "Menu"), arrival: arrival);
            DrawTitleOption(new Rect(left, top + 3f * rowHeight, rowWidth, rowHeight), 3, T("Gallery", "Galeria"), arrival: arrival);
        }

        private void DrawTitleFlare(Rect screen, float ascent) => DrawRetailTitleLens(screen);

        private void DrawTitleOption(Rect row, int option, string label, bool enabled = true, float arrival = 1f)
        {
            if (enabled && GUI.Button(row, GUIContent.none, GUIStyle.none))
                SelectTitleOption(option);
            if (Event.current.type != EventType.Repaint) return;

            bool selected = enabled && _titleSelection == option;
            if (selected)
            {
                float bandWidth = row.height * 5.8f;
                var band = new Rect(row.center.x - bandWidth * 0.5f, row.y + 2f, bandWidth, row.height - 4f);
                DrawHudFill(band, new Color(0.08f, 0.17f, 0.31f, 0.58f));

                DrawRetailTitleCrystals(row, band);
            }

            float pulse = selected ? 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
            float flightTicks = _titleConfirmRow >= 0 ? (Time.unscaledTime - _titleConfirmStarted) * 60f : 0f;
            float departing = _titleConfirmRow < 0 ? 1f : selected ? (((int)flightTicks >> 3 & 1) == 0 ? 1f : .15f) : Mathf.Clamp01(1f-flightTicks/18f);
            float opacity = (selected ? 1f : enabled ? 0.70f : 0.38f) * arrival * pulse * departing;
            Color previousColor = GUI.contentColor;
            GUI.contentColor = new Color(0f, 0f, 0f, opacity);
            const float outline = 1.35f;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                GUI.Label(new Rect(row.x + x * outline, row.y + y * outline, row.width, row.height),
                    label, _titleOptionStyle);
            }
            GUI.contentColor = new Color(1f, 1f, 1f, opacity);
            GUI.Label(row, label, _titleOptionStyle);
            GUI.contentColor = previousColor;
        }

        private bool DrawTitleSubButton(int row, string label) =>
            GUILayout.Button((_titleSubSelection == row ? "›  " : "") + label, GUILayout.Height(29f));

        private void DrawTitleCollection(float width, float height)
        {
            bool manual = _titlePage == TitlePage.Manual;
            var panel = new Rect(12f, 10f, width - 24f, height - 20f);
            DrawHudPanel(panel);
            GUILayout.BeginArea(WindowDesignSystem.Content(panel, footer: true));
            GUILayout.BeginHorizontal();
            GUILayout.Label(manual ? T("Game Manual (English)", "Manual do jogo (em inglês)") : T("Portrait Gallery", "Galeria de retratos"), _bodyStyle);
            if (GUILayout.Button(WithCancelHint("Back", "Voltar"), GUILayout.Width(104f))) BackFromTitlePage();
            GUILayout.EndHorizontal();
            if (manual)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("‹", GUILayout.Width(35f))) ChangeManualPage(-1);
                GUILayout.Label($"{_manualPage} / {ManualPageCount}", GUILayout.Width(72f));
                if (GUILayout.Button("›", GUILayout.Width(35f))) ChangeManualPage(1);
                bool zoom = GUILayout.Toggle(_manualZoom, T("Zoom", "Ampliar"));
                if (zoom != _manualZoom) { _manualZoom = zoom; _titleImageScroll = Vector2.zero; }
                GUILayout.EndHorizontal();
                if (_titlePage == TitlePage.Manual)
                {
                    var page = StreamImage($"Manual/Page-{_manualPage:00}.jpg");
                    DrawManualPage(page);
                }
            }
            else
            {
                int tab = GUILayout.Toolbar(_galleryTab, new[] { T("Characters", "Personagens"), T("Monsters", "Monstros"),
                    T("Koh's status", "Estados de Koh"), T("Weedy's book", "Livro da Weedy") });
                if (tab != _galleryTab) { _galleryTab = tab; _titleImageScroll = Vector2.zero; }
                Texture2D image = tab switch { 0 => StreamPortrait("Gallery/dialogue"), 1 => StreamPortrait("Gallery/monsters"), 2 => StreamPortrait("Gallery/koh"), _ => null };
                if (tab == 3)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("‹", GUILayout.Width(35f))) _familiarPortrait = (_familiarPortrait + FamiliarPortraitCount - 2) % FamiliarPortraitCount + 1;
                    GUILayout.Label($"{_familiarPortrait} / {FamiliarPortraitCount}", GUILayout.Width(72f));
                    if (GUILayout.Button("›", GUILayout.Width(35f))) _familiarPortrait = _familiarPortrait % FamiliarPortraitCount + 1;
                    GUILayout.EndHorizontal();
                    image = StreamPortrait($"Book/portrait_{_familiarPortrait:00}");
                }
                DrawTitleImage(image, panel.width - 34f, panel.height - (tab == 3 ? 103f : 80f), 1f, tab < 2);
            }
            GUILayout.EndArea();
        }

        private static Rect FitManualPage(Rect viewport, Vector2 imageSize)
        {
            float scale = Mathf.Min(viewport.width / Mathf.Max(1f,imageSize.x), viewport.height / Mathf.Max(1f,imageSize.y));
            Vector2 size=imageSize*Mathf.Max(0f,scale);
            return new Rect(viewport.center-size*.5f,size);
        }

        private void DrawManualPage(Texture2D image)
        {
            // Use the space left by the actual header/control rows, not a guessed
            // height that can extend beyond the window when the font changes.
            var viewport=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            viewport=new Rect(viewport.x+8f,viewport.y+8f,Mathf.Max(1f,viewport.width-16f),Mathf.Max(1f,viewport.height-16f));
            if(!image) return;
            var fitted=FitManualPage(viewport,new Vector2(image.width,image.height));
            if(!_manualZoom) { GUI.DrawTexture(fitted,image,ScaleMode.ScaleToFit); return; }
            Vector2 enlarged=fitted.size*1.8f;
            // Leave room for both scrollbars; every edge remains reachable in zoom.
            var content=new Rect(0f,0f,Mathf.Max(viewport.width-20f,enlarged.x),Mathf.Max(viewport.height-20f,enlarged.y));
            _titleImageScroll=GUI.BeginScrollView(viewport,_titleImageScroll,content);
            GUI.DrawTexture(new Rect(content.center-enlarged*.5f,enlarged),image,ScaleMode.ScaleToFit);
            GUI.EndScrollView();
        }

        private void DrawTitleImage(Texture2D image, float width, float height, float zoom, bool scrollTall = false)
        {
            if (!image) return;
            float aspect = (float)image.width / image.height;
            float imageWidth = (scrollTall ? Mathf.Min(width, image.width * 3f) : Mathf.Min(width, height * aspect)) * zoom;
            float imageHeight = imageWidth / aspect;
            _titleImageScroll = GUILayout.BeginScrollView(_titleImageScroll, GUILayout.Height(height));
            var rect = GUILayoutUtility.GetRect(Mathf.Max(width, imageWidth), imageHeight, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(new Rect(rect.x + (rect.width - imageWidth) / 2f, rect.y, imageWidth, imageHeight), image, ScaleMode.ScaleToFit);
            GUILayout.EndScrollView();
        }
    }
}
