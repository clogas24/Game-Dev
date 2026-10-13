using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LanternKeeper.EditorTools
{
    // Step 5: the themed HUD and menus. Same prefabs, same wiring, same object names as
    // before (so file IDs stay put); what changed is the look: Creepster for headings and
    // Lato for everything meant to be read, a pumpkin-orange / deep-purple / bone palette, a
    // candle-shaped candle meter, a candy icon, the note reader, and the two volume sliders.
    // Canvases scale from 1920 x 1080, so at 1280 x 720 every size below shrinks to two thirds:
    // the smallest text here, 24, is still 16 px tall there.
    public static partial class LanternKeeperSetup
    {
        /// <summary>
        /// Regenerates only the HUD and GameMenus prefabs (in place: scenes keep their instances).
        /// "Create Placeholder Assets" and "Create World Prefabs" do the same as part of a full run.
        /// </summary>
        [MenuItem(Menu + "Create UI Prefabs", priority = 6)]
        public static void CreateUiPrefabs()
        {
            if (!EnsureTmpEssentials())
            {
                Debug.LogWarning("[Lantern Keeper] UI prefabs skipped: TMP Essential Resources are still importing. Run this menu item again.");
                return;
            }
            EnsureFolder(PrefabsDir);
            CreateHudPrefab();
            CreateGameMenusPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lantern Keeper] HUD and GameMenus prefabs created.");
        }

        private static TextMeshProUGUI AsTitle(TextMeshProUGUI label)
        {
            TMP_FontAsset font = TitleFont();
            if (font != null) label.font = font;
            return label;
        }

        private static Image NewImage(RectTransform rect, string sprite, Color color, bool sliced)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprite(sprite);
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform NewStretched(string name, Transform parent, Vector2 insetMin, Vector2 insetMax)
        {
            RectTransform rect = NewRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = insetMin;
            rect.offsetMax = -insetMax;
            return rect;
        }

        // ------------------------------------------------------------------ HUD

        private static void CreateHudPrefab()
        {
            EnsurePolishAssets();

            var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Candle meter, bottom-left: a candle standing in a brass dish. The bar is the same
            // horizontal bar HUD.cs has always driven (it shortens the Fill along its own X),
            // turned a quarter turn so that X points up the screen. The flame rides the end of
            // the Fill, so it sinks as the candle burns down.
            RectTransform dish = NewRect("CandleDish", root.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(86f, 50f), new Vector2(110f, 24f));
            NewImage(dish, "UI_Rounded", new Color(0.62f, 0.45f, 0.18f), true);

            RectTransform bar = NewRect("CandleBar", root.transform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(86f, 58f), new Vector2(250f, 52f));
            bar.localRotation = Quaternion.Euler(0f, 0f, 90f);
            NewImage(bar, "UI_Rounded", new Color(0.04f, 0.02f, 0.07f, 0.72f), true);

            RectTransform fill = NewRect("Fill", bar, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(5f, 5f);
            fill.offsetMax = new Vector2(-5f, -5f);
            Image fillImage = NewImage(fill, "UI_Rounded", new Color(1f, 0.6f, 0.1f), true);

            RectTransform flame = NewRect("CandleFlame", fill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.1f),
                Vector2.zero, new Vector2(54f, 81f));
            flame.localRotation = Quaternion.Euler(0f, 0f, -90f); // undo the bar's turn: the flame points up
            NewImage(flame, "UI_Flame", Color.white, false);

            RectTransform candleLabelRect = NewRect("CandleLabel", root.transform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(136f, 58f), new Vector2(340f, 44f));
            TextMeshProUGUI candleLabel = NewText(candleLabelRect, "Candle 100%", 30f, TextAlignmentOptions.Left);
            candleLabel.color = UiBone;

            RectTransform candyIcon = NewRect("CandyIcon", root.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(164f, 118f), new Vector2(64f, 64f));
            NewImage(candyIcon, "UI_Candy", Color.white, false);
            RectTransform candyRect = NewRect("CandyLabel", root.transform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(204f, 118f), new Vector2(320f, 44f));
            TextMeshProUGUI candyLabel = NewText(candyRect, "Candy 0/5", 32f, TextAlignmentOptions.Left);
            candyLabel.color = UiBone;

            // Run clock, top-right. HUD.cs hides it outside the timed levels.
            RectTransform timerRect = NewRect("RunTimer", root.transform, Vector2.one, Vector2.one, new Vector2(1f, 1f),
                new Vector2(-36f, -28f), new Vector2(260f, 68f));
            NewImage(timerRect, "UI_Rounded", new Color(0.04f, 0.02f, 0.08f, 0.6f), true);
            TextMeshProUGUI timerLabel = NewText(NewStretched("RunTimerLabel", timerRect, new Vector2(16f, 4f), new Vector2(16f, 4f)),
                "0:00.0", 44f, TextAlignmentOptions.Center);
            timerLabel.color = UiBone;

            // Centre prompt on a dark band, so it reads over a bright flare as well as over the night.
            RectTransform promptRect = NewRect("Prompt", root.transform, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 130f));
            var promptGroup = promptRect.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            promptGroup.interactable = false;
            promptGroup.blocksRaycasts = false;
            NewImage(NewStretched("PromptBack", promptRect, Vector2.zero, Vector2.zero), "UI_Rounded", new Color(0.04f, 0.02f, 0.08f, 0.6f), true);
            TextMeshProUGUI promptLabel = NewText(NewStretched("PromptLabel", promptRect, new Vector2(30f, 8f), new Vector2(30f, 8f)),
                "", 36f, TextAlignmentOptions.Center);
            promptLabel.color = UiBone;

            // Story notes: a small "read" prompt while one is in range, and the note on parchment.
            RectTransform notePrompt = NewRect("NotePrompt", root.transform, new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.17f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 66f));
            var notePromptGroup = notePrompt.gameObject.AddComponent<CanvasGroup>();
            notePromptGroup.alpha = 0f;
            notePromptGroup.interactable = false;
            notePromptGroup.blocksRaycasts = false;
            NewImage(NewStretched("NotePromptBack", notePrompt, Vector2.zero, Vector2.zero), "UI_Rounded", new Color(0.2f, 0.1f, 0.03f, 0.85f), true);
            TextMeshProUGUI notePromptLabel = NewText(NewStretched("NotePromptLabel", notePrompt, Vector2.zero, Vector2.zero),
                "E   Read the note", 30f, TextAlignmentOptions.Center);
            notePromptLabel.color = UiOrangeLight;

            RectTransform notePanel = NewRect("NotePanel", root.transform, new Vector2(0.5f, 0.57f), new Vector2(0.5f, 0.57f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 740f));
            var notePanelGroup = notePanel.gameObject.AddComponent<CanvasGroup>();
            notePanelGroup.alpha = 0f;
            notePanelGroup.interactable = false;
            notePanelGroup.blocksRaycasts = false;
            NewImage(notePanel, "UI_Parchment", Color.white, true);
            RectTransform noteTitleRect = NewRect("NoteTitle", notePanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(-140f, 90f));
            TextMeshProUGUI noteTitle = AsTitle(NewText(noteTitleRect, "A note", 58f, TextAlignmentOptions.Center));
            noteTitle.color = new Color(0.45f, 0.16f, 0.05f);
            TextMeshProUGUI noteBody = NewText(NewStretched("NoteBody", notePanel, new Vector2(80f, 96f), new Vector2(80f, 136f)),
                "", 30f, TextAlignmentOptions.TopLeft);
            noteBody.color = UiInk;
            noteBody.lineSpacing = 4f;
            // The longest note fits at 30; a longer one written later shrinks instead of spilling over the hint.
            noteBody.enableAutoSizing = true;
            noteBody.fontSizeMax = 30f;
            noteBody.fontSizeMin = 24f;
            RectTransform noteHintRect = NewRect("NoteHint", notePanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(-140f, 46f));
            TextMeshProUGUI noteHint = NewText(noteHintRect, "", 24f, TextAlignmentOptions.Center);
            noteHint.color = new Color(UiInk.r, UiInk.g, UiInk.b, 0.8f);

            var hud = root.AddComponent<HUD>();
            SetRef(hud, "candleFill", fill);
            SetRef(hud, "candleFillImage", fillImage);
            SetRef(hud, "candleFlame", flame.gameObject);
            SetRef(hud, "candleLabel", candleLabel);
            SetRef(hud, "candyLabel", candyLabel);
            SetRef(hud, "timerLabel", timerLabel);
            SetRef(hud, "timerRoot", timerRect.gameObject);
            SetRef(hud, "promptLabel", promptLabel);
            SetRef(hud, "promptGroup", promptGroup);

            var reader = root.AddComponent<NoteReader>();
            SetRef(reader, "promptGroup", notePromptGroup);
            SetRef(reader, "promptLabel", notePromptLabel);
            SetRef(reader, "panelGroup", notePanelGroup);
            SetRef(reader, "titleLabel", noteTitle);
            SetRef(reader, "bodyLabel", noteBody);
            SetRef(reader, "hintLabel", noteHint);
            SetClip(reader, "openClip", "ThirdParty/KenneyRPGAudio/bookOpen");
            SetClip(reader, "closeClip", "ThirdParty/KenneyRPGAudio/bookClose");

            SavePrefab(root, HudPrefabPath);
        }

        // ------------------------------------------------------------------ menus (Title / Pause / Win)

        private static void CreateGameMenusPrefab()
        {
            EnsurePolishAssets();

            var root = new GameObject("GameMenus", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above the HUD
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Buttons and the sliders need an EventSystem; it travels with the prefab.
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(root.transform, false);

            var soft = new Color(UiBone.r, UiBone.g, UiBone.b, 0.75f);

            // Title
            RectTransform title = NewPanel("TitlePanel", root.transform, new Color(UiPurpleDeep.r, UiPurpleDeep.g, UiPurpleDeep.b, 0.9f));
            AsTitle(NewText(NewAnchored("GameName", title, 0.8f, new Vector2(1800f, 200f)), "Lantern Keeper", 170f, TextAlignmentOptions.Center))
                .color = UiOrange;
            NewText(NewAnchored("Tagline", title, 0.665f, new Vector2(1600f, 60f)),
                "Cross the haunted patch before your candle burns out.", 36f, TextAlignmentOptions.Center).color = UiBone;
            TextMeshProUGUI controls = NewText(NewAnchored("Controls", title, 0.41f, new Vector2(1100f, 400f)), "", 32f, TextAlignmentOptions.Center);
            controls.color = UiBone;
            // GameMenus.cs adds how to skip to this line in the tutorial.
            TextMeshProUGUI startHint = NewText(NewAnchored("PressAnyKey", title, 0.12f, new Vector2(1600f, 60f)),
                "Press any key to start", 42f, TextAlignmentOptions.Center);
            startHint.color = UiOrangeLight;

            // Pause
            RectTransform pause = NewPanel("PausePanel", root.transform, new Color(0.03f, 0.015f, 0.06f, 0.86f));
            AsTitle(NewText(NewAnchored("PauseHeading", pause, 0.915f, new Vector2(1200f, 140f)), "Paused", 116f, TextAlignmentOptions.Center))
                .color = UiOrange;
            Button resume = NewButton("ResumeButton", pause, 0.81f, "Resume");
            Button restart = NewButton("RestartButton", pause, 0.735f, "Restart");
            Button quit = NewButton("QuitButton", pause, 0.66f, "Quit");
            // Last of the four, so the other three do not move when GameMenus.cs hides it outside the tutorial.
            Button skipTutorial = NewButton("SkipTutorialButton", pause, 0.585f, "Skip tutorial");
            Slider slider = NewSliderRow("Sensitivity", pause, 0.5f, "Camera sensitivity", 0.1f, 3f, 1f, "1.0", out TextMeshProUGUI sensitivityValue);
            Slider music = NewSliderRow("Music", pause, 0.44f, "Music volume", 0f, 1f, GameAudio.DefaultMusicVolume, "70%", out TextMeshProUGUI musicValue);
            Slider sfx = NewSliderRow("Sfx", pause, 0.38f, "Sound volume", 0f, 1f, GameAudio.DefaultSfxVolume, "90%", out TextMeshProUGUI sfxValue);
            NewText(NewAnchored("PauseHint", pause, 0.315f, new Vector2(1400f, 50f)),
                "Esc resumes  -  arrow keys and Enter work in this menu", 26f, TextAlignmentOptions.Center).color = soft;

            // Win. The panel fades in (UiFadeIn) instead of snapping on.
            RectTransform win = NewPanel("WinPanel", root.transform, new Color(0.07f, 0.035f, 0.02f, 0.9f));
            win.gameObject.AddComponent<CanvasGroup>();
            win.gameObject.AddComponent<UiFadeIn>();
            // GameMenus.cs sets the heading, the button's label and the hint from the scene's place in LevelFlow.
            TextMeshProUGUI winHeading = AsTitle(NewText(NewAnchored("WinHeading", win, 0.83f, new Vector2(1800f, 150f)), "You reached the farmhouse!", 104f, TextAlignmentOptions.Center));
            winHeading.color = UiOrange;
            // Tall enough for the end of the run: two splits, the run time, times extinguished, candle left.
            TextMeshProUGUI stats = NewText(NewAnchored("Stats", win, 0.6f, new Vector2(1200f, 300f)), "", 44f, TextAlignmentOptions.Center);
            stats.color = UiBone;
            Button winRestart = NewButton("PlayAgainButton", win, 0.41f, "Play again");
            TextMeshProUGUI winHint = NewText(NewAnchored("WinHint", win, 0.32f, new Vector2(1600f, 50f)),
                "Press R to play again", 30f, TextAlignmentOptions.Center);
            winHint.color = soft;

            // GameMenus shows the right panel in Start; saved hidden so nothing flashes.
            title.gameObject.SetActive(false);
            pause.gameObject.SetActive(false);
            win.gameObject.SetActive(false);

            var menus = root.AddComponent<GameMenus>();
            SetRef(menus, "titlePanel", title.gameObject);
            SetRef(menus, "pausePanel", pause.gameObject);
            SetRef(menus, "winPanel", win.gameObject);
            SetRef(menus, "controlsLabel", controls);
            SetRef(menus, "startHintLabel", startHint);
            SetRef(menus, "resumeButton", resume);
            SetRef(menus, "restartButton", restart);
            SetRef(menus, "quitButton", quit);
            SetRef(menus, "skipTutorialButton", skipTutorial);
            SetRef(menus, "sensitivitySlider", slider);
            SetRef(menus, "sensitivityValueLabel", sensitivityValue);
            SetRef(menus, "musicSlider", music);
            SetRef(menus, "musicValueLabel", musicValue);
            SetRef(menus, "sfxSlider", sfx);
            SetRef(menus, "sfxValueLabel", sfxValue);
            SetRef(menus, "winStatsLabel", stats);
            SetRef(menus, "winRestartButton", winRestart);
            SetRef(menus, "winHeadingLabel", winHeading);
            SetRef(menus, "winHintLabel", winHint);

            var sounds = root.AddComponent<UiSounds>();
            SetClip(sounds, "hoverClip", "ThirdParty/KenneyInterfaceSounds/tick_001");
            SetClip(sounds, "clickClip", "ThirdParty/KenneyInterfaceSounds/click_001");
            SetClip(sounds, "startClip", "ThirdParty/KenneyInterfaceSounds/confirmation_002");

            SavePrefab(root, GameMenusPrefabPath);
        }

        private static Button NewButton(string name, Transform parent, float height, string label)
        {
            RectTransform rect = NewAnchored(name, parent, height, new Vector2(420f, 74f));
            Image image = NewImage(rect, "UI_Rounded", Color.white, true);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // Strong highlight and selected tints, so the keyboard selection is easy to see.
            ColorBlock colors = button.colors;
            colors.normalColor = UiPurple;
            colors.highlightedColor = new Color(0.85f, 0.45f, 0.08f);
            colors.selectedColor = new Color(0.85f, 0.45f, 0.08f);
            colors.pressedColor = UiOrangeLight;
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            // Every object in the prefab needs its own name: when the prefab is regenerated Unity
            // matches old and new objects by name, and duplicates would get new file IDs each time.
            RectTransform textRect = NewRect(name + "Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI text = NewText(textRect, label, 34f, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        // One row of the pause menu: a label on the left, the slider, and its value on the right.
        private static Slider NewSliderRow(string name, Transform parent, float height, string label, float min, float max, float value,
            string valueText, out TextMeshProUGUI valueLabel)
        {
            RectTransform labelRect = NewAnchored(name + "Label", parent, height, new Vector2(420f, 50f));
            labelRect.anchoredPosition = new Vector2(-480f, 0f);
            NewText(labelRect, label, 30f, TextAlignmentOptions.Right).color = UiBone;

            Slider slider = NewSlider(name + "Slider", parent, height);
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            RectTransform valueRect = NewAnchored(name + "Value", parent, height, new Vector2(140f, 50f));
            valueRect.anchoredPosition = new Vector2(340f, 0f);
            valueLabel = NewText(valueRect, valueText, 30f, TextAlignmentOptions.Left);
            valueLabel.color = UiOrangeLight;
            return slider;
        }

        private static Slider NewSlider(string name, Transform parent, float height)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var anchor = new Vector2(0.5f, height);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(480f, 30f);

            // The default slider's parts are all called Background, Fill and Handle. Three
            // sliders share this prefab, so each part takes its slider's name: Unity matches
            // objects by name when the prefab is regenerated.
            foreach (Transform part in go.GetComponentsInChildren<Transform>(true))
            {
                if (part != go.transform) part.name = name + " " + part.name;
            }

            // No sprites in a code-built slider, so tell the parts apart by colour.
            TintChild(go.transform, $"{name} Background", UiPurple);
            TintChild(go.transform, $"{name} Fill Area/{name} Fill", new Color(0.85f, 0.45f, 0.08f));
            TintChild(go.transform, $"{name} Handle Slide Area/{name} Handle", UiBone);

            var slider = go.GetComponent<Slider>();
            ColorBlock colors = slider.colors;
            colors.highlightedColor = UiOrangeLight;
            colors.selectedColor = new Color(1f, 0.75f, 0.35f);
            slider.colors = colors;
            return slider;
        }
    }
}
