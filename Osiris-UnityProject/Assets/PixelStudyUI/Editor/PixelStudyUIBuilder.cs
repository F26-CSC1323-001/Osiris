using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelStudy.Game;
using PixelStudy.Menus;
using PixelStudy.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// Builds every screen from the wireframes (Assets/PixelStudyUI/Wireframes):
    ///   - Assets/Scenes/MainMenu.unity  (Main Menu, Difficulty, Question Generator, Settings, Controls)
    ///   - Assets/PixelStudyUI/Prefabs/GameUI.prefab, dropped into SampleScene
    ///     (HUD, Quiz, Upgrades, Pause, Settings, Floor Cleared, Boss Warning, Game Over, Victory)
    ///   - Build Settings scene list (MainMenu first).
    /// Run it again any time from Tools > Pixel Study > Build UI Screens. It overwrites MainMenu.unity
    /// and GameUI.prefab, so make hand edits in those after you're happy with the generated layout.
    /// </summary>
    public static class PixelStudyUIBuilder
    {
        const string Root = "Assets/PixelStudyUI";
        const string ArtDir = Root + "/Art";
        const string PrefabDir = Root + "/Prefabs";
        const string PrefabPath = PrefabDir + "/GameUI.prefab";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string KnightIdlePath = "Assets/Sprites/Knight_1/Idle.png";

        // ---------- palette ----------
        static readonly Color Ink = Hex("#0E0A16");
        static readonly Color Parchment = Hex("#F3EAD7");
        static readonly Color DimText = Hex("#A597C4");
        static readonly Color Gold = Hex("#F2C14E");
        static readonly Color Green = Hex("#6BD16B");
        static readonly Color Red = Hex("#E5534B");
        static readonly Color Blue = Hex("#5EC8F2");
        static readonly Color PanelColor = Hex("#3E3163");
        static readonly Color CardColor = Hex("#4A3C75");
        static readonly Color InsetColor = Hex("#1C1530");
        static readonly Color InsetHover = Hex("#2A2147");
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color DisabledColor = Hex("#3A3448");
        static readonly Color MenuBgTint = Hex("#4E3A7D");
        static readonly Color BossBgTint = Hex("#7A1E28");
        static readonly Color GameOverTint = Hex("#5E2140");
        static readonly Color VictoryTint = Hex("#7A5A1C");

        struct Style
        {
            public Color normal, hover, pressed, text;
        }

        static readonly Style Primary = new Style { normal = Hex("#F2C14E"), hover = Hex("#FFE38F"), pressed = Hex("#C7962F"), text = Hex("#2A1C08") };
        static readonly Style Secondary = new Style { normal = Hex("#6A55A8"), hover = Hex("#8B74D4"), pressed = Hex("#4E3E80"), text = Parchment };
        static readonly Style Ghost = new Style { normal = Hex("#3C3160"), hover = Hex("#56487F"), pressed = Hex("#2C2448"), text = Hex("#D8CCF2") };
        static readonly Style Danger = new Style { normal = Hex("#D9534F"), hover = Hex("#F5776F"), pressed = Hex("#A93C39"), text = Hex("#FFF3EE") };
        static readonly Style AnswerStyle = new Style { normal = Hex("#5A4A8E"), hover = Hex("#7864B8"), pressed = Hex("#43376A"), text = Parchment };

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 TopRight = new Vector2(1f, 1f);
        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
        static readonly Vector2 RightMiddle = new Vector2(1f, 0.5f);

        static Sprite sButton, sPanel, sOutline, sBar, sWhite, sRamp;
        static Sprite iHeart, iCoin, iGem, iSword, iShield, iBoot, iSkull, iBook, iCheck, iArrow, iLock;
        static TMP_FontAsset font;
        static Material matTitle, matShadow;

        // =====================================================================
        // Entry points
        // =====================================================================

        [MenuItem("Tools/Pixel Study/Build UI Screens")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(MainMenuScenePath);
            EditorUtility.DisplayDialog("Pixel Study UI",
                "Done!\n\n• Assets/Scenes/MainMenu.unity\n• GameUI prefab added to SampleScene\n• Build Settings: MainMenu, SampleScene\n\nPress Play in MainMenu to try it.",
                "OK");
        }

        /// <summary>Also callable from the command line with -executeMethod.</summary>
        public static void BuildAll()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);
            PixelArt.GenerateAll(ArtDir);
            LoadAssets();
            BuildGameUIPrefab();
            BuildMainMenuScene();
            AddGameUIToGameScene();
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[PixelStudyUI] Built MainMenu scene, GameUI prefab and build settings.");
        }

        static void LoadAssets()
        {
            Sprite L(string n)
            {
                var s = PixelArt.Load(ArtDir, n);
                if (s == null) throw new System.Exception($"[PixelStudyUI] Missing sprite {n}");
                return s;
            }

            sButton = L("px_button");
            sPanel = L("px_panel");
            sOutline = L("px_outline");
            sBar = L("px_bar");
            sWhite = L("px_white");
            sRamp = L("px_ramp");
            iHeart = L("icon_heart");
            iCoin = L("icon_coin");
            iGem = L("icon_gem");
            iSword = L("icon_sword");
            iShield = L("icon_shield");
            iBoot = L("icon_boot");
            iSkull = L("icon_skull");
            iBook = L("icon_book");
            iCheck = L("icon_check");
            iArrow = L("icon_arrow");
            iLock = L("icon_lock");

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new System.Exception("[PixelStudyUI] TextMesh Pro Essential Resources are missing. Use Window > TextMeshPro > Import TMP Essential Resources, then run this again.");

            matTitle = TextMaterial("Text_Title", 0.22f, 0.9f);
            matShadow = TextMaterial("Text_Shadow", 0f, 0.6f);
        }

        static Material TextMaterial(string name, float outline, float underlayAlpha)
        {
            var path = $"{ArtDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = font.material.shader;
                mat.CopyPropertiesFromMaterial(font.material);
            }

            if (outline > 0f)
            {
                mat.EnableKeyword("OUTLINE_ON");
                mat.SetFloat("_OutlineWidth", outline);
                mat.SetColor("_OutlineColor", Ink);
                mat.SetFloat("_FaceDilate", outline * 0.5f);
            }
            else
            {
                mat.DisableKeyword("OUTLINE_ON");
            }
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, underlayAlpha));
            mat.SetFloat("_UnderlayOffsetX", 0.7f);
            mat.SetFloat("_UnderlayOffsetY", -0.7f);
            mat.SetFloat("_UnderlayDilate", outline);
            mat.SetFloat("_UnderlaySoftness", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =====================================================================
        // Main menu scene
        // =====================================================================

        static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            MakeEventSystem();

            var canvas = MakeCanvas("MenuCanvas", 0);
            var ct = canvas.transform;

            var bg = Backdrop(ct, MenuBgTint);
            bg.raycastTarget = false;
            Particles(ct, new Color(1f, 0.85f, 0.45f, 0.45f), 40);

            var ground = Img(ct, "Ground", sWhite, Hex("#140F20"));
            Span(ground.rectTransform, 0f, 110f);
            var groundEdge = Img(ct, "GroundEdge", sWhite, Hex("#5B4791"));
            Span(groundEdge.rectTransform, 110f, 6f);

            var footerLeft = Text(ct, "Credits", "TEAM OSIRIS  ·  CSC 1323", 20, Hex("#7D6CB0"), TextAlignmentOptions.Left, true);
            footerLeft.characterSpacing = 4;
            Place(footerLeft.rectTransform, BottomLeft, new Vector2(32f, 34f), new Vector2(600f, 30f), BottomLeft);
            var footerRight = Text(ct, "Version", "v0.1", 20, Hex("#7D6CB0"), TextAlignmentOptions.Right, true);
            Place(footerRight.rectTransform, BottomRight, new Vector2(-32f, 34f), new Vector2(200f, 30f), BottomRight);

            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            var difficulty = canvas.gameObject.AddComponent<DifficultySelectController>();
            var generator = canvas.gameObject.AddComponent<QuestionGeneratorController>();
            var settings = canvas.gameObject.AddComponent<SettingsPanel>();
            menu.difficulty = difficulty;
            menu.generator = generator;
            menu.settings = settings;

            BuildMainScreen(ct, menu);
            BuildDifficultyScreen(ct, difficulty);
            BuildGeneratorScreen(ct, generator);
            BuildSettingsScreens(ct, settings, false);
            BuildConfirmScreen(ct, menu);

            foreach (var s in canvas.GetComponentsInChildren<UIScreen>(true))
                s.gameObject.SetActive(s == menu.mainScreen);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        static void BuildMainScreen(Transform parent, MainMenuController c)
        {
            var screen = MakeScreen(parent, "MainMenuScreen");
            var t = screen.transform;
            Knight(t, new Vector2(330f, 116f), 1f);

            var titleGroup = UI("TitleGroup", t);
            Place(Rect(titleGroup), Center, new Vector2(0f, 340f), new Vector2(1700f, 150f));
            var title = Text(titleGroup.transform, "Title", "PIXEL STUDY GAME", 116, Parchment, TextAlignmentOptions.Center, true, matTitle);
            title.characterSpacing = 8;
            Stretch(title.rectTransform);
            titleGroup.AddComponent<UIFloat>().amplitude = 6f;

            Sub(t, "Study hard. Squash bugs. Climb 30 floors.", new Vector2(0f, 236f), 32);
            Divider(t, new Vector2(0f, 186f), 560f);

            var stack = UI("Buttons", t);
            Place(Rect(stack), Center, new Vector2(0f, -76f), new Vector2(560f, 480f));
            VStack(stack, 18f, null, TextAnchor.MiddleCenter);
            c.newGameButton = Btn(stack.transform, "NewGame", "New Game", Primary, new Vector2(520f, 80f), 34, true);
            c.continueButton = Btn(stack.transform, "Continue", "Continue", Secondary, new Vector2(520f, 80f), 34, true);
            c.difficultyButton = Btn(stack.transform, "Difficulty", "Difficulty", Secondary, new Vector2(520f, 80f), 34, true);
            c.generatorButton = Btn(stack.transform, "QuestionGenerator", "Question Generator", Secondary, new Vector2(520f, 80f), 34, true);
            c.settingsButton = Btn(stack.transform, "Settings", "Settings", Secondary, new Vector2(520f, 80f), 34, true);

            c.infoText = Text(t, "Info", "Difficulty: Normal", 26, DimText, TextAlignmentOptions.Center);
            Place(c.infoText.rectTransform, Center, new Vector2(0f, -352f), new Vector2(1400f, 40f));
            var hint = Text(t, "Hint", "↑ ↓  choose     Enter  select     Esc  back", 20, new Color(DimText.r, DimText.g, DimText.b, 0.6f), TextAlignmentOptions.Center);
            Place(hint.rectTransform, Center, new Vector2(0f, -396f), new Vector2(1000f, 30f));

            c.mainScreen = screen;
            screen.firstSelected = c.newGameButton;
            screen.popTarget = Rect(stack);
        }

        static void BuildDifficultyScreen(Transform parent, DifficultySelectController d)
        {
            var screen = MakeScreen(parent, "DifficultyScreen");
            var t = screen.transform;
            Title(t, "SELECT DIFFICULTY", 88, new Vector2(0f, 372f));
            Sub(t, "Pick how hard the bugs hit back. You can change it before every new run.", new Vector2(0f, 294f), 28);

            string[] names = { "EASY", "NORMAL", "HARD" };
            string[] descriptions = { "Enemies deal less damage,\nmore forgiving combat", "Balanced challenge", "Enemies hit harder\nand react faster" };
            Color[] accents = { Green, Gold, Red };
            d.cards = new DifficultyCard[3];

            for (int i = 0; i < 3; i++)
            {
                var difficulty = (Difficulty)i;
                var button = Card(t, names[i] + "Card", new Vector2((i - 1) * 440f, 24f), new Vector2(400f, 390f), CardColor, out var body);

                var skulls = UI("Skulls", body);
                Place(Rect(skulls), TopCenter, new Vector2(0f, -40f), new Vector2(300f, 48f), TopCenter);
                HStack(skulls, 10f, TextAnchor.MiddleCenter);
                for (int s = 0; s <= i; s++)
                {
                    var skull = Img(skulls.transform, "Skull", iSkull, Color.white);
                    Layout(skull.gameObject, 48f, 48f);
                }

                var name = Text(body, "Name", names[i], 50, accents[i], TextAlignmentOptions.Center, true, matShadow);
                name.characterSpacing = 6;
                Place(name.rectTransform, TopCenter, new Vector2(0f, -104f), new Vector2(360f, 60f), TopCenter);

                var desc = Text(body, "Description", descriptions[i], 27, Parchment, TextAlignmentOptions.Top);
                Place(desc.rectTransform, TopCenter, new Vector2(0f, -180f), new Vector2(350f, 80f), TopCenter);

                var chip = Img(body, "StatChip", sBar, InsetColor, true);
                Place(chip.rectTransform, BottomCenter, new Vector2(0f, 34f), new Vector2(330f, 58f), BottomCenter);
                var stat = Text(chip.transform, "Stat", $"Enemy damage  ×{GameSession.EnemyDamageMultiplier(difficulty):0.##}", 24, DimText, TextAlignmentOptions.Center, true);
                Stretch(stat.rectTransform, 8f, 4f, 8f, 4f);

                var frame = Img(body, "SelectedFrame", sOutline, Gold, true);
                Stretch(frame.rectTransform, -10f, -10f, -10f, -10f);

                var badge = Img(body, "SelectedBadge", sButton, Gold, true);
                Place(badge.rectTransform, TopCenter, new Vector2(0f, 6f), new Vector2(190f, 46f), Center);
                var badgeText = Text(badge.transform, "Label", "SELECTED", 22, Hex("#2A1C08"), TextAlignmentOptions.Center, true);
                badgeText.characterSpacing = 4;
                Stretch(badgeText.rectTransform, 4f, 2f, 4f, 8f);

                frame.gameObject.SetActive(difficulty == Difficulty.Normal);
                badge.gameObject.SetActive(difficulty == Difficulty.Normal);

                d.cards[i] = new DifficultyCard { difficulty = difficulty, button = button, selectedFrame = frame.gameObject, selectedBadge = badge.gameObject };
            }

            d.backButton = Btn(t, "Back", "Back", Ghost, new Vector2(260f, 80f), 32);
            Place(Rect(d.backButton.gameObject), Center, new Vector2(-510f, -330f), new Vector2(260f, 80f));
            d.startButton = Btn(t, "Start", "Start", Primary, new Vector2(340f, 88f), 38, true);
            Place(Rect(d.startButton.gameObject), Center, new Vector2(470f, -330f), new Vector2(340f, 88f));

            d.screen = screen;
            screen.firstSelected = d.startButton;
        }

        static void BuildGeneratorScreen(Transform parent, QuestionGeneratorController g)
        {
            var screen = MakeScreen(parent, "QuestionGeneratorScreen");
            var t = screen.transform;
            Title(t, "QUESTION GENERATOR", 72, new Vector2(0f, 458f));
            Sub(t, "Paste your class notes and turn them into quiz questions for the tower.", new Vector2(0f, 398f), 26);

            g.backButton = Btn(t, "Back", "Back", Ghost, new Vector2(200f, 64f), 28);
            Place(Rect(g.backButton.gameObject), TopLeft, new Vector2(40f, -40f), new Vector2(200f, 64f), TopLeft);

            var body = Panel(t, "Panel", PanelColor, out var root);
            Place(root, Center, new Vector2(0f, -50f), new Vector2(1400f, 840f));

            var bookIcon = Img(body, "Book", iBook, Color.white);
            Place(bookIcon.rectTransform, TopRight, new Vector2(-40f, -30f), new Vector2(64f, 64f), TopRight);

            Label(body, "SET NAME", new Vector2(44f, -40f));
            g.setNameField = InputField(body, "SetName", "e.g. Data Structures Week 3", false);
            Place(Rect(g.setNameField.gameObject), TopLeft, new Vector2(44f, -76f), new Vector2(600f, 64f), TopLeft);

            g.activeSetText = Text(body, "ActiveSet", "Active set: " + QuestionBank.BuiltInSetName, 24, DimText, TextAlignmentOptions.Right);
            Place(g.activeSetText.rectTransform, TopRight, new Vector2(-124f, -108f), new Vector2(620f, 40f), new Vector2(1f, 0.5f));

            Label(body, "PASTE YOUR NOTES", new Vector2(44f, -168f));
            g.notesField = InputField(body, "Notes", "Paste notes here...\n\nTip: lines like \"A stack is a LIFO list.\" make the best questions.", true);
            Place(Rect(g.notesField.gameObject), TopLeft, new Vector2(44f, -204f), new Vector2(1312f, 190f), TopLeft);

            g.generateButton = Btn(body, "Generate", "Generate Questions", Primary, new Vector2(400f, 76f), 30);
            Place(Rect(g.generateButton.gameObject), TopCenter, new Vector2(0f, -420f), new Vector2(400f, 76f), TopCenter);
            g.saveButton = Btn(body, "SaveSet", "Save Set", Secondary, new Vector2(240f, 76f), 30);
            Place(Rect(g.saveButton.gameObject), TopRight, new Vector2(-44f, -420f), new Vector2(240f, 76f), TopRight);

            g.statusText = Text(body, "Status", "", 22, DimText, TextAlignmentOptions.Left);
            Place(g.statusText.rectTransform, TopLeft, new Vector2(44f, -458f), new Vector2(440f, 70f), new Vector2(0f, 0.5f));

            g.listHeader = Label(body, "GENERATED QUESTIONS (0)", new Vector2(44f, -522f));

            var list = ScrollList(body, out var content);
            Place(Rect(list.gameObject), TopLeft, new Vector2(44f, -556f), new Vector2(1312f, 244f), TopLeft);

            var empty = Text(list.transform, "EmptyState", "Your questions will show up here.", 26, new Color(DimText.r, DimText.g, DimText.b, 0.7f), TextAlignmentOptions.Center);
            Stretch(empty.rectTransform);
            g.emptyState = empty.gameObject;

            // Row template (cloned for each generated question). Child text order: Prompt, Answer, Difficulty.
            var row = UI("RowTemplate", content);
            Layout(row, -1f, 76f);
            var rowBg = row.AddComponent<Image>();
            rowBg.sprite = sBar;
            rowBg.type = Image.Type.Sliced;
            rowBg.color = Hex("#2E2450");
            var prompt = Text(row.transform, "Prompt", "What is the time complexity of binary search?", 25, Parchment, TextAlignmentOptions.TopLeft);
            prompt.textWrappingMode = TextWrappingModes.NoWrap;
            prompt.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(prompt.rectTransform, 22f, 10f, 260f, 36f);
            var answer = Text(row.transform, "Answer", "Answer: O(log n)", 20, Green, TextAlignmentOptions.BottomLeft);
            answer.textWrappingMode = TextWrappingModes.NoWrap;
            answer.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(answer.rectTransform, 22f, 40f, 260f, 10f);
            var diff = Text(row.transform, "Difficulty", "Difficulty: Medium", 22, Gold, TextAlignmentOptions.Right, true);
            Place(diff.rectTransform, RightMiddle, new Vector2(-22f, 0f), new Vector2(230f, 40f), RightMiddle);
            row.SetActive(false);
            g.rowTemplate = row;
            g.listContent = content;

            g.screen = screen;
            screen.firstSelected = g.setNameField;
            screen.popTarget = root;
        }

        static void BuildConfirmScreen(Transform parent, MainMenuController c)
        {
            var screen = MakeScreen(parent, "ConfirmNewGameScreen");
            var t = screen.transform;
            Dim(t, 0.8f);
            var body = Panel(t, "Panel", PanelColor, out var root);
            Place(root, Center, Vector2.zero, new Vector2(760f, 380f));

            var title = Text(body, "Title", "START A NEW RUN?", 52, Parchment, TextAlignmentOptions.Center, true, matTitle);
            Place(title.rectTransform, TopCenter, new Vector2(0f, -56f), new Vector2(700f, 70f), TopCenter);
            c.confirmBody = Text(body, "Body", "Your saved run will be replaced.", 28, DimText, TextAlignmentOptions.Center);
            Place(c.confirmBody.rectTransform, TopCenter, new Vector2(0f, -146f), new Vector2(640f, 90f), TopCenter);

            c.confirmCancelButton = Btn(body, "Cancel", "Keep my save", Ghost, new Vector2(280f, 76f), 28);
            Place(Rect(c.confirmCancelButton.gameObject), BottomCenter, new Vector2(-160f, 48f), new Vector2(280f, 76f), BottomCenter);
            c.confirmStartButton = Btn(body, "Start", "Start new", Primary, new Vector2(280f, 76f), 30);
            Place(Rect(c.confirmStartButton.gameObject), BottomCenter, new Vector2(160f, 48f), new Vector2(280f, 76f), BottomCenter);

            c.confirmScreen = screen;
            screen.popTarget = root;
            screen.firstSelected = c.confirmCancelButton;
        }

        // =====================================================================
        // Settings + Controls (shared by main menu and pause menu)
        // =====================================================================

        static void BuildSettingsScreens(Transform parent, SettingsPanel s, bool inGame)
        {
            var screen = MakeScreen(parent, "SettingsScreen");
            var t = screen.transform;
            if (inGame) Dim(t, 0.86f);
            Title(t, "SETTINGS", 88, new Vector2(0f, 350f));

            var body = Panel(t, "Panel", PanelColor, out var root);
            Place(root, Center, new Vector2(0f, -10f), new Vector2(1040f, 500f));

            float[] rows = { -72f, -182f, -292f, -406f };
            for (int i = 0; i < rows.Length - 1; i++)
            {
                var line = Img(body, "Separator", sWhite, Hex("#2C2348"));
                Place(line.rectTransform, TopCenter, new Vector2(0f, (rows[i] + rows[i + 1]) * 0.5f), new Vector2(940f, 4f));
            }

            RowLabel(body, "Music Volume", rows[0]);
            s.musicSlider = PixelSlider(body, "MusicSlider", 0.7f);
            Place(Rect(s.musicSlider.gameObject), TopRight, new Vector2(-170f, rows[0]), new Vector2(440f, 40f), RightMiddle);
            s.musicValue = RowValue(body, "70%", rows[0]);

            RowLabel(body, "SFX Volume", rows[1]);
            s.sfxSlider = PixelSlider(body, "SfxSlider", 0.85f);
            Place(Rect(s.sfxSlider.gameObject), TopRight, new Vector2(-170f, rows[1]), new Vector2(440f, 40f), RightMiddle);
            s.sfxValue = RowValue(body, "85%", rows[1]);

            RowLabel(body, "Fullscreen", rows[2]);
            s.fullscreenToggle = PixelSwitch(body, "FullscreenSwitch", out var state);
            Place(Rect(s.fullscreenToggle.gameObject), TopRight, new Vector2(-60f, rows[2]), new Vector2(124f, 52f), RightMiddle);
            Place(state.rectTransform, TopRight, new Vector2(-200f, rows[2]), new Vector2(90f, 40f), RightMiddle);

            RowLabel(body, "Controls", rows[3]);
            s.keyBindingsButton = Btn(body, "EditKeyBindings", "Edit Key Bindings", Secondary, new Vector2(330f, 72f), 28);
            Place(Rect(s.keyBindingsButton.gameObject), TopRight, new Vector2(-60f, rows[3]), new Vector2(330f, 72f), RightMiddle);

            s.backButton = Btn(t, "Back", "Back", Ghost, new Vector2(280f, 80f), 32, true);
            Place(Rect(s.backButton.gameObject), Center, new Vector2(0f, -340f), new Vector2(280f, 80f));

            s.screen = screen;
            screen.popTarget = root;
            screen.firstSelected = s.musicSlider;

            // ----- Controls / key bindings -----
            var kb = MakeScreen(parent, "KeyBindingsScreen");
            var k = kb.transform;
            if (inGame) Dim(k, 0.86f);
            Title(k, "CONTROLS", 80, new Vector2(0f, 410f));
            Sub(k, "Mockup controls: move and jump. Keyboard or a gamepad.", new Vector2(0f, 340f), 24);

            var kbody = Panel(k, "Panel", PanelColor, out var kroot);
            Place(kroot, Center, new Vector2(0f, -40f), new Vector2(1100f, 400f));
            var bindings = new (string action, string[] keys)[]
            {
                ("Move", new[] { "A", "D", "or", "←", "→" }),
                ("Jump", new[] { "Space", "or", "W" }),
                ("Drop through platform", new[] { "S", "+", "Space" }),
                ("Pause", new[] { "Esc" }),
                ("Answer quiz", new[] { "1", "2", "3", "4" }),
            };
            for (int i = 0; i < bindings.Length; i++)
            {
                float y = -42f - i * 68f;
                RowLabel(kbody, bindings[i].action, y);
                var keys = UI("Keys", kbody);
                Place(Rect(keys), TopRight, new Vector2(-60f, y), new Vector2(560f, 60f), RightMiddle);
                HStack(keys, 10f, TextAnchor.MiddleRight);
                foreach (var key in bindings[i].keys)
                {
                    if (key == "or" || key == "hold" || key == "+") KeyWord(keys.transform, key);
                    else KeyChip(keys.transform, key);
                }
                if (i < bindings.Length - 1)
                {
                    var line = Img(kbody, "Separator", sWhite, Hex("#2C2348"));
                    Place(line.rectTransform, TopCenter, new Vector2(0f, y - 34f), new Vector2(1000f, 4f));
                }
            }

            s.keyBindingsBackButton = Btn(k, "Back", "Back", Ghost, new Vector2(280f, 80f), 32, true);
            Place(Rect(s.keyBindingsBackButton.gameObject), Center, new Vector2(0f, -448f), new Vector2(280f, 80f));
            s.keyBindingsScreen = kb;
            kb.popTarget = kroot;
            kb.firstSelected = s.keyBindingsBackButton;
        }

        // =====================================================================
        // In-game UI prefab
        // =====================================================================

        static void BuildGameUIPrefab()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = MakeCanvas("GameUI", 10);
            var ct = canvas.transform;
            var c = canvas.gameObject.AddComponent<GameUIController>();

            c.hud = BuildHUD(ct, c);
            c.quiz = canvas.gameObject.AddComponent<QuizPanel>();
            BuildQuiz(ct, c.quiz);
            c.upgrade = canvas.gameObject.AddComponent<UpgradePanel>();
            BuildUpgrade(ct, c.upgrade);
            BuildPause(ct, c);
            c.settings = canvas.gameObject.AddComponent<SettingsPanel>();
            BuildSettingsScreens(ct, c.settings, true);
            BuildFloorCleared(ct, c);
            BuildBossWarning(ct, c);
            BuildGameOver(ct, c);
            BuildVictory(ct, c);
            BuildToast(ct, c);

            foreach (var s in canvas.GetComponentsInChildren<UIScreen>(true)) s.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
        }

        static HUDController BuildHUD(Transform parent, GameUIController c)
        {
            var go = UI("HUD", parent);
            Stretch(Rect(go));
            var hud = go.AddComponent<HUDController>();
            var t = go.transform;

            // Stats panel (top left)
            var body = Panel(t, "StatsPanel", WithAlpha(PanelColor, 0.94f), out var root);
            Place(root, TopLeft, new Vector2(24f, -24f), new Vector2(580f, 200f), TopLeft);

            var heart = Img(body, "HeartIcon", iHeart, Color.white);
            Place(heart.rectTransform, TopLeft, new Vector2(52f, -56f), new Vector2(52f, 52f), Center);
            hud.heartIcon = heart.rectTransform;
            var bar = Bar(body, "HealthBar", Hex("#E5484D"), Hex("#FF9A5C"), 0.75f, out var barRect);
            Place(barRect, TopLeft, new Vector2(96f, -36f), new Vector2(450f, 40f), TopLeft);
            hud.healthBar = bar;
            hud.healthText = Text(barRect, "Value", "75 / 100", 22, Parchment, TextAlignmentOptions.Center, true, matShadow);
            Stretch(hud.healthText.rectTransform);

            var gem = Img(body, "TokenIcon", iGem, Color.white);
            Place(gem.rectTransform, TopLeft, new Vector2(52f, -112f), new Vector2(44f, 44f), Center);
            hud.tokensIcon = gem.rectTransform;
            hud.tokensText = Text(body, "Tokens", "Upgrade Tokens: 2", 28, Parchment, TextAlignmentOptions.Left, false, matShadow);
            Place(hud.tokensText.rectTransform, TopLeft, new Vector2(96f, -112f), new Vector2(440f, 40f), LeftMiddle);

            var coin = Img(body, "CoinIcon", iCoin, Color.white);
            Place(coin.rectTransform, TopLeft, new Vector2(52f, -160f), new Vector2(44f, 44f), Center);
            hud.coinsIcon = coin.rectTransform;
            hud.coinsText = Text(body, "Coins", "Coins: 140", 28, Gold, TextAlignmentOptions.Left, false, matShadow);
            Place(hud.coinsText.rectTransform, TopLeft, new Vector2(96f, -160f), new Vector2(440f, 40f), LeftMiddle);

            // Floor badge (top right)
            var badge = Panel(t, "FloorBadge", WithAlpha(PanelColor, 0.94f), out var badgeRoot);
            Place(badgeRoot, TopRight, new Vector2(-24f, -24f), new Vector2(260f, 116f), TopRight);
            hud.floorText = Text(badge, "Floor", "FLOOR 3", 46, Gold, TextAlignmentOptions.Center, true, matShadow);
            hud.floorText.characterSpacing = 4;
            Place(hud.floorText.rectTransform, TopCenter, new Vector2(0f, -20f), new Vector2(240f, 54f), TopCenter);
            hud.floorSubText = Text(badge, "Sub", "of 30", 22, DimText, TextAlignmentOptions.Center, true);
            Place(hud.floorSubText.rectTransform, BottomCenter, new Vector2(0f, 18f), new Vector2(240f, 30f), BottomCenter);
            var bossIcon = Img(badge, "BossIcon", iSkull, Color.white);
            Place(bossIcon.rectTransform, TopLeft, new Vector2(-6f, 6f), new Vector2(52f, 52f), Center);
            bossIcon.gameObject.SetActive(false);
            hud.bossFloorIcon = bossIcon.gameObject;

            // Objective line under the floor badge ("BUGS LEFT: 4", "EXIT OPEN")
            var objective = Panel(t, "Objective", WithAlpha(InsetColor, 0.92f), out var objectiveRoot);
            Place(objectiveRoot, TopRight, new Vector2(-24f, -152f), new Vector2(320f, 52f), TopRight);
            hud.objectiveText = Text(objective, "Text", "BUGS LEFT: 5", 22, Gold, TextAlignmentOptions.Center, true, matShadow);
            hud.objectiveText.characterSpacing = 4;
            Stretch(hud.objectiveText.rectTransform, 10f, 4f, 10f, 6f);
            hud.objectiveGroup = objectiveRoot.gameObject;
            objectiveRoot.gameObject.SetActive(false);

            // Skill bar (bottom left): Q Z X C V E F. The arena knight fills these in; hidden otherwise.
            hud.skillSlots = new SkillSlotView[7];
            for (int i = 0; i < hud.skillSlots.Length; i++)
            {
                var slotBody = Panel(t, "Skill" + (i + 1), WithAlpha(PanelColor, 0.95f), out var slotRoot);
                Place(slotRoot, BottomLeft, new Vector2(34f + i * 108f, 60f), new Vector2(88f, 88f), BottomLeft);
                var icon = Img(slotBody, "Icon", null, Color.white);
                icon.preserveAspect = true;
                Place(icon.rectTransform, Center, Vector2.zero, new Vector2(64f, 64f));
                var cooldown = Img(slotBody, "Cooldown", sWhite, new Color(0f, 0f, 0f, 0.62f));
                Stretch(cooldown.rectTransform, 8f, 8f, 8f, 8f);
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = (int)Image.Origin360.Top;
                cooldown.fillClockwise = false;
                cooldown.fillAmount = 0f;
                var seconds = Text(slotBody, "Seconds", "", 36, Parchment, TextAlignmentOptions.Center, true, matTitle);
                Stretch(seconds.rectTransform);
                var lockIcon = Img(slotBody, "Lock", iLock, Color.white);
                Place(lockIcon.rectTransform, Center, new Vector2(0f, -2f), new Vector2(40f, 40f));
                lockIcon.gameObject.SetActive(false);
                var keyBadge = Img(slotRoot, "Key", sButton, Gold, true);
                Place(keyBadge.rectTransform, TopLeft, new Vector2(4f, -2f), new Vector2(31f, 31f), Center);
                var keyText = Text(keyBadge.transform, "Label", "Z", 19, Hex("#2A1C08"), TextAlignmentOptions.Center, true);
                Stretch(keyText.rectTransform, 2f, 2f, 2f, 6f);
                // long names ("OVERHEAD SLASH") wrap onto two lines instead of running into the next slot
                var name = Text(slotRoot, "Name", "SKILL", 13, Gold, TextAlignmentOptions.Bottom, true, matShadow);
                name.characterSpacing = 1;
                name.lineSpacing = -18;
                name.textWrappingMode = TextWrappingModes.Normal;
                name.enableAutoSizing = true;
                name.fontSizeMin = 8;
                name.fontSizeMax = 12;
                Place(name.rectTransform, TopCenter, new Vector2(0f, 17f), new Vector2(102f, 32f), BottomCenter);
                hud.skillSlots[i] = new SkillSlotView
                {
                    root = slotRoot.gameObject,
                    frame = slotBody.GetComponent<Image>(),
                    icon = icon,
                    cooldownFill = cooldown,
                    cooldownText = seconds,
                    keyText = keyText,
                    nameText = name,
                    keyBadge = keyBadge,
                    lockIcon = lockIcon.gameObject,
                };
                slotRoot.gameObject.SetActive(false);
            }

            // Boss bar (top center, like Dead Cells)
            var boss = UI("BossBar", t);
            Place(Rect(boss), TopCenter, new Vector2(0f, -26f), new Vector2(700f, 92f), TopCenter);
            var bossName = Text(boss.transform, "Name", "THE BROOD MOTHER", 26, Hex("#FF8A80"), TextAlignmentOptions.Center, true, matShadow);
            bossName.characterSpacing = 10;
            Place(bossName.rectTransform, TopCenter, Vector2.zero, new Vector2(600f, 36f), TopCenter);
            var bossBar = Bar(boss.transform, "Bar", Hex("#B23CD9"), Hex("#FF6BD5"), 0.45f, out var bossBarRect);
            Place(bossBarRect, BottomCenter, new Vector2(20f, 0f), new Vector2(620f, 36f), BottomCenter);
            bossBar.lowThreshold = 0f;
            var bossSkull = Img(boss.transform, "Skull", iSkull, Color.white);
            Place(bossSkull.rectTransform, BottomLeft, new Vector2(40f, 18f), new Vector2(56f, 56f), Center);
            hud.bossGroup = boss;
            hud.bossBar = bossBar;
            hud.bossNameText = bossName;
            boss.SetActive(false);

            // Dev hint
            var devKeys = "DEV KEYS    F5 finish floor   ·   F7 skip ahead 10 floors   ·   F8 victory   ·   Esc pause";
            var dev = Text(t, "DevHint", devKeys, 18, WithAlpha(DimText, 0.85f), TextAlignmentOptions.Left, true, matShadow);
            Place(dev.rectTransform, BottomLeft, new Vector2(24f, 14f), new Vector2(1400f, 28f), BottomLeft);
            c.devHint = dev.gameObject;

            return hud;
        }

        static void BuildQuiz(Transform parent, QuizPanel q)
        {
            var screen = MakeScreen(parent, "QuizScreen");
            var t = screen.transform;
            Dim(t, 0.72f);

            var body = Panel(t, "Panel", PanelColor, out var root);
            Place(root, Center, new Vector2(0f, 20f), new Vector2(1620f, 580f));

            q.counterText = Text(body, "Counter", "QUESTION 2 OF 3", 24, DimText, TextAlignmentOptions.Left, true);
            q.counterText.characterSpacing = 8;
            Place(q.counterText.rectTransform, TopLeft, new Vector2(64f, -44f), new Vector2(420f, 34f), TopLeft);

            var pips = UI("Progress", body);
            Place(Rect(pips), TopCenter, new Vector2(0f, -46f), new Vector2(200f, 26f), TopCenter);
            HStack(pips, 12f, TextAnchor.MiddleCenter);
            q.progressPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pip = Img(pips.transform, "Pip" + (i + 1), sBar, i == 0 ? Green : i == 1 ? Gold : Hex("#2E2450"), true);
                Layout(pip.gameObject, 48f, 22f);
                q.progressPips[i] = pip;
            }

            q.floorText = Text(body, "Floor", "FLOOR 3", 26, Gold, TextAlignmentOptions.Right, true);
            q.floorText.characterSpacing = 6;
            Place(q.floorText.rectTransform, TopRight, new Vector2(-64f, -42f), new Vector2(240f, 36f), TopRight);

            var bossTag = Img(body, "BossTag", sButton, Red, true);
            Place(bossTag.rectTransform, TopRight, new Vector2(-224f, -38f), new Vector2(110f, 44f), TopRight);
            var bossLabel = Text(bossTag.transform, "Label", "BOSS", 22, Color.white, TextAlignmentOptions.Center, true);
            Stretch(bossLabel.rectTransform, 4f, 2f, 4f, 8f);
            bossTag.gameObject.SetActive(false);
            q.bossTag = bossTag.gameObject;

            q.questionText = Text(body, "Question", "Sample question text appears here, big and easy to read.", 54, Parchment, TextAlignmentOptions.Center, true, matShadow);
            q.questionText.enableAutoSizing = true;
            q.questionText.fontSizeMin = 30;
            q.questionText.fontSizeMax = 54;
            Place(q.questionText.rectTransform, TopCenter, new Vector2(0f, -110f), new Vector2(1460f, 200f), TopCenter);

            var answers = UI("Answers", body);
            Place(Rect(answers), BottomCenter, new Vector2(0f, 64f), new Vector2(1480f, 136f), BottomCenter);
            HStack(answers, 24f, TextAnchor.MiddleCenter);
            q.answersGroup = answers.AddComponent<CanvasGroup>();
            q.answerButtons = new Button[4];
            q.answerTexts = new TMP_Text[4];
            string[] letters = { "A", "B", "C", "D" };
            string[] samples = { "Answer one", "Answer two", "Answer three", "Answer four" };
            for (int i = 0; i < 4; i++)
            {
                var b = Btn(answers.transform, "Answer" + letters[i], samples[i], AnswerStyle, new Vector2(352f, 136f), 28);
                b.GetComponent<UIButtonHover>().hoverScale = 1.06f;
                var label = b.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.Left;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18;
                label.fontSizeMax = 28;
                Stretch(label.rectTransform, 84f, 12f, 18f, 20f);

                var badge = Img(b.transform, "Letter", sButton, Gold, true);
                Place(badge.rectTransform, LeftMiddle, new Vector2(18f, 4f), new Vector2(52f, 52f), LeftMiddle);
                var letter = Text(badge.transform, "Label", letters[i], 28, Hex("#2A1C08"), TextAlignmentOptions.Center, true);
                Stretch(letter.rectTransform, 2f, 2f, 2f, 8f);

                q.answerButtons[i] = b;
                q.answerTexts[i] = label;
            }

            q.feedbackText = Text(t, "Feedback", "Press 1-4 or click an answer", 30, DimText, TextAlignmentOptions.Center, true, matShadow);
            Place(q.feedbackText.rectTransform, Center, new Vector2(0f, -336f), new Vector2(1400f, 48f));

            q.shakeTarget = root;
            q.screen = screen;
            screen.popTarget = root;
            screen.firstSelected = q.answerButtons[0];
        }

        static void BuildUpgrade(Transform parent, UpgradePanel u)
        {
            var screen = MakeScreen(parent, "UpgradeScreen");
            var t = screen.transform;
            Dim(t, 0.8f);
            Title(t, "CHOOSE YOUR UPGRADE", 72, new Vector2(0f, 262f));
            u.tierText = Sub(t, "<color=#F2C14E>GOLD UPGRADE</color>", new Vector2(0f, 198f), 30);
            u.tierText.fontStyle = FontStyles.Bold;

            Sprite[] icons = { iSword, iShield, iBoot };
            Color[] accents = { Hex("#FF8A80"), Blue, Green };
            u.cards = new UpgradeCardView[3];
            for (int i = 0; i < 3; i++)
            {
                var up = Upgrades.All[i];
                var button = Card(t, up.Name.Replace(" ", "") + "Card", new Vector2((i - 1) * 450f, -96f), new Vector2(400f, 450f), CardColor, out var body);

                var slot = Img(body, "IconSlot", sBar, InsetColor, true);
                Place(slot.rectTransform, TopCenter, new Vector2(0f, -44f), new Vector2(136f, 136f), TopCenter);
                var icon = Img(slot.transform, "Icon", icons[i], Color.white);
                Place(icon.rectTransform, Center, Vector2.zero, new Vector2(96f, 96f));
                icon.gameObject.AddComponent<UIFloat>().amplitude = 4f;
                icon.GetComponent<UIFloat>().phase = i * 1.3f;

                var title = Text(body, "Title", up.Name, 38, Parchment, TextAlignmentOptions.Center, true, matShadow);
                Place(title.rectTransform, TopCenter, new Vector2(0f, -204f), new Vector2(370f, 50f), TopCenter);
                var desc = Text(body, "Description", up.Describe(Upgrades.MaxTier), 28, accents[i], TextAlignmentOptions.Center, true);
                Place(desc.rectTransform, TopCenter, new Vector2(0f, -262f), new Vector2(370f, 40f), TopCenter);

                var line = Img(body, "Line", sWhite, Hex("#2C2348"));
                Place(line.rectTransform, BottomCenter, new Vector2(0f, 112f), new Vector2(320f, 4f), BottomCenter);
                var current = Text(body, "Current", up.Current(new RunData()), 22, DimText, TextAlignmentOptions.Center);
                Place(current.rectTransform, BottomCenter, new Vector2(0f, 70f), new Vector2(370f, 34f), BottomCenter);
                var hint = Text(body, "Hint", "CLICK TO CHOOSE", 18, WithAlpha(DimText, 0.7f), TextAlignmentOptions.Center, true);
                hint.characterSpacing = 6;
                Place(hint.rectTransform, BottomCenter, new Vector2(0f, 30f), new Vector2(370f, 28f), BottomCenter);

                u.cards[i] = new UpgradeCardView { button = button, title = title, description = desc, current = current };
            }

            u.boostButton = Btn(t, "Boost", "Boost to Gold  ·  1 token", Secondary, new Vector2(560f, 70f), 26);
            Place(Rect(u.boostButton.gameObject), Center, new Vector2(90f, -372f), new Vector2(520f, 70f));
            u.boostLabel = u.boostButton.GetComponentInChildren<TMP_Text>();
            u.boostLabel.textWrappingMode = TextWrappingModes.NoWrap;
            u.boostLabel.enableAutoSizing = true;
            u.boostLabel.fontSizeMin = 16;
            u.boostLabel.fontSizeMax = 26;
            var gem = Img(u.boostButton.transform, "Gem", iGem, Color.white);
            Place(gem.rectTransform, LeftMiddle, new Vector2(22f, 4f), new Vector2(40f, 40f), LeftMiddle);
            Stretch(u.boostLabel.rectTransform, 64f, 4f, 16f, 12f);

            u.screen = screen;
            screen.firstSelected = u.cards[0].button;
        }

        static void BuildPause(Transform parent, GameUIController c)
        {
            var screen = MakeScreen(parent, "PauseScreen");
            var t = screen.transform;
            Dim(t, 0.72f);
            var body = Panel(t, "Panel", PanelColor, out var root);
            Place(root, Center, Vector2.zero, new Vector2(620f, 580f));

            var title = Text(body, "Title", "PAUSED", 76, Parchment, TextAlignmentOptions.Center, true, matTitle);
            title.characterSpacing = 8;
            Place(title.rectTransform, TopCenter, new Vector2(0f, -50f), new Vector2(560f, 96f), TopCenter);

            var stack = UI("Buttons", body);
            Place(Rect(stack), TopCenter, new Vector2(0f, -172f), new Vector2(480f, 290f), TopCenter);
            VStack(stack, 18f, null, TextAnchor.UpperCenter);
            c.resumeButton = Btn(stack.transform, "Resume", "Resume", Primary, new Vector2(460f, 82f), 34, true);
            c.pauseSettingsButton = Btn(stack.transform, "Settings", "Settings", Secondary, new Vector2(460f, 82f), 32, true);
            c.quitButton = Btn(stack.transform, "Quit", "Quit to Main Menu", Ghost, new Vector2(460f, 82f), 30, true);

            c.pauseInfoText = Text(body, "Info", "Floor 3 of 30    ·    Normal", 22, DimText, TextAlignmentOptions.Center, true);
            Place(c.pauseInfoText.rectTransform, BottomCenter, new Vector2(0f, 34f), new Vector2(560f, 30f), BottomCenter);

            c.pauseScreen = screen;
            screen.popTarget = root;
            screen.firstSelected = c.resumeButton;
        }

        static void BuildFloorCleared(Transform parent, GameUIController c)
        {
            var screen = MakeScreen(parent, "FloorClearedScreen");
            var t = screen.transform;
            Backdrop(t, MenuBgTint);
            Particles(t, new Color(1f, 0.85f, 0.45f, 0.55f), 50);

            var badge = Img(t, "Badge", iCheck, Color.white);
            Place(badge.rectTransform, Center, new Vector2(0f, 330f), new Vector2(112f, 112f));
            badge.gameObject.AddComponent<UIFloat>().amplitude = 6f;
            c.floorClearedTitle = Title(t, "Floor 3 Cleared!", 100, new Vector2(0f, 214f), Gold);

            var body = Panel(t, "Stats", PanelColor, out var root);
            Place(root, Center, new Vector2(0f, -22f), new Vector2(740f, 316f));
            VStack(body.gameObject, 10f, new RectOffset(40, 40, 28, 28), TextAnchor.MiddleCenter);
            c.floorCoinsText = StatRow(body, iCoin, "Coins earned this floor", "+18", out _);
            c.floorCorrectText = StatRow(body, iCheck, "Questions answered correctly", "3 of 3", out _);
            c.floorUpgradeText = StatRow(body, iGem, "Upgrade earned", "<color=#F2C14E>Gold</color>  +1 token", out _);
            c.floorSkillText = StatRow(body, iBook, "New skill unlocked!", "Whirlwind [X]", out var skillRow);
            c.floorSkillText.color = Blue;
            c.floorSkillRow = skillRow;
            skillRow.SetActive(false);

            c.floorContinueButton = Btn(t, "Continue", "Continue to Floor 4", Primary, new Vector2(500f, 90f), 34, true);
            Place(Rect(c.floorContinueButton.gameObject), Center, new Vector2(0f, -272f), new Vector2(500f, 90f));
            c.floorContinueLabel = c.floorContinueButton.GetComponentInChildren<TMP_Text>();

            c.floorClearedScreen = screen;
            screen.firstSelected = c.floorContinueButton;
            screen.popTarget = root;
        }

        static void BuildBossWarning(Transform parent, GameUIController c)
        {
            var screen = MakeScreen(parent, "BossWarningScreen");
            var t = screen.transform;
            Backdrop(t, BossBgTint);
            Particles(t, new Color(1f, 0.4f, 0.25f, 0.55f), 50);

            var skull = Img(t, "Skull", iSkull, Color.white);
            Place(skull.rectTransform, Center, new Vector2(0f, 300f), new Vector2(128f, 128f));
            var skullPulse = skull.gameObject.AddComponent<UIPulse>();
            skullPulse.scaleAmount = 0.1f;
            skullPulse.speed = 4f;

            c.bossFloorText = Text(t, "Floor", "FLOOR 10", 32, Hex("#E8A0A0"), TextAlignmentOptions.Center, true);
            c.bossFloorText.characterSpacing = 30;
            Place(c.bossFloorText.rectTransform, Center, new Vector2(0f, 196f), new Vector2(800f, 44f));

            var title = Title(t, "BOSS AHEAD", 132, new Vector2(0f, 94f), Hex("#FF5A5A"));
            var pulse = title.gameObject.AddComponent<UIPulse>();
            pulse.graphic = title;
            pulse.pulseColor = Hex("#FFC2B0");
            pulse.scaleAmount = 0.04f;

            c.bossNameText = Text(t, "BossName", "THE BROOD MOTHER", 44, Hex("#FFD7C2"), TextAlignmentOptions.Center, true, matShadow);
            c.bossNameText.characterSpacing = 12;
            Place(c.bossNameText.rectTransform, Center, new Vector2(0f, -2f), new Vector2(1200f, 56f));

            var rule = Text(t, "Rule", "Answer 3 questions correctly to proceed,\nor you'll be sent back to the start of this floor.", 30, Hex("#E6CFCF"), TextAlignmentOptions.Center, false, matShadow);
            Place(rule.rectTransform, Center, new Vector2(0f, -92f), new Vector2(1300f, 90f));

            c.bossEnterButton = Btn(t, "Enter", "Enter", Danger, new Vector2(360f, 92f), 40, true);
            Place(Rect(c.bossEnterButton.gameObject), Center, new Vector2(0f, -222f), new Vector2(360f, 92f));

            c.bossWarningScreen = screen;
            screen.firstSelected = c.bossEnterButton;
        }

        static void BuildGameOver(Transform parent, GameUIController c)
        {
            var screen = MakeScreen(parent, "GameOverScreen");
            var t = screen.transform;
            Backdrop(t, GameOverTint);

            var skull = Img(t, "Skull", iSkull, Color.white);
            Place(skull.rectTransform, Center, new Vector2(0f, 390f), new Vector2(96f, 96f));
            Title(t, "GAME OVER", 124, new Vector2(0f, 284f), Hex("#FF6B6B"));

            var body = Panel(t, "Stats", PanelColor, out var root);
            Place(root, Center, new Vector2(0f, 70f), new Vector2(740f, 250f));
            VStack(body.gameObject, 10f, new RectOffset(40, 40, 28, 28), TextAnchor.MiddleCenter);
            c.gameOverFloorText = StatRow(body, iShield, "Floor reached", "4", out _);
            c.gameOverCorrectText = StatRow(body, iCheck, "Questions answered correctly", "12", out _);
            c.gameOverCoinsText = StatRow(body, iCoin, "Coins", "140", out _);

            c.gameOverContinueButton = Btn(t, "ContinueCoins", "Continue: 25 Coins", Primary, new Vector2(560f, 92f), 34, true);
            Place(Rect(c.gameOverContinueButton.gameObject), Center, new Vector2(0f, -128f), new Vector2(560f, 92f));
            c.gameOverContinueLabel = c.gameOverContinueButton.GetComponentInChildren<TMP_Text>();
            Stretch(c.gameOverContinueLabel.rectTransform, 70f, 4f, 16f, 12f);
            var coin = Img(c.gameOverContinueButton.transform, "Coin", iCoin, Color.white);
            Place(coin.rectTransform, LeftMiddle, new Vector2(28f, 4f), new Vector2(48f, 48f), LeftMiddle);

            c.gameOverWarningText = Text(t, "NotEnoughCoins", "Not enough coins", 22, Red, TextAlignmentOptions.Center, true, matShadow);
            Place(c.gameOverWarningText.rectTransform, Center, new Vector2(0f, -196f), new Vector2(600f, 30f));
            c.gameOverWarningText.gameObject.SetActive(false);

            c.gameOverRetryButton = Btn(t, "Retry", "Retry", Secondary, new Vector2(300f, 82f), 32, true);
            Place(Rect(c.gameOverRetryButton.gameObject), Center, new Vector2(-165f, -282f), new Vector2(300f, 82f));
            c.gameOverMenuButton = Btn(t, "MainMenu", "Main Menu", Ghost, new Vector2(300f, 82f), 32, true);
            Place(Rect(c.gameOverMenuButton.gameObject), Center, new Vector2(165f, -282f), new Vector2(300f, 82f));

            c.gameOverScreen = screen;
            screen.firstSelected = c.gameOverRetryButton;
            screen.popTarget = root;
        }

        static void BuildVictory(Transform parent, GameUIController c)
        {
            var screen = MakeScreen(parent, "VictoryScreen");
            var t = screen.transform;
            Backdrop(t, VictoryTint);
            Particles(t, new Color(1f, 0.9f, 0.5f, 0.7f), 70);
            Knight(t, new Vector2(300f, 0f), 1.1f);

            var titleGroup = UI("TitleGroup", t);
            Place(Rect(titleGroup), Center, new Vector2(0f, 300f), new Vector2(1400f, 170f));
            var title = Text(titleGroup.transform, "Title", "VICTORY!", 144, Gold, TextAlignmentOptions.Center, true, matTitle);
            title.characterSpacing = 10;
            Stretch(title.rectTransform);
            titleGroup.AddComponent<UIFloat>().amplitude = 8f;
            Sub(t, "You climbed all 30 floors of the tower.", new Vector2(0f, 196f), 32).color = Parchment;

            var body = Panel(t, "Stats", PanelColor, out var root);
            Place(root, Center, new Vector2(0f, -4f), new Vector2(740f, 250f));
            VStack(body.gameObject, 10f, new RectOffset(40, 40, 28, 28), TextAnchor.MiddleCenter);
            c.victoryFloorsText = StatRow(body, iShield, "Total floors cleared", "30", out _);
            c.victoryCoinsText = StatRow(body, iCoin, "Total coins earned", "412", out _);
            c.victoryCorrectText = StatRow(body, iCheck, "Total correct answers", "87", out _);

            c.victoryNewGamePlusButton = Btn(t, "NewGamePlus", "New Game+", Primary, new Vector2(320f, 90f), 36, true);
            Place(Rect(c.victoryNewGamePlusButton.gameObject), Center, new Vector2(-180f, -222f), new Vector2(320f, 90f));
            c.victoryMenuButton = Btn(t, "MainMenu", "Main Menu", Ghost, new Vector2(320f, 90f), 34, true);
            Place(Rect(c.victoryMenuButton.gameObject), Center, new Vector2(180f, -222f), new Vector2(320f, 90f));
            var hint = Text(t, "Hint", "New Game+ keeps your upgrades and coins.", 22, Parchment, TextAlignmentOptions.Center, true, matShadow);
            Place(hint.rectTransform, Center, new Vector2(0f, -304f), new Vector2(900f, 30f));

            c.victoryScreen = screen;
            screen.firstSelected = c.victoryNewGamePlusButton;
            screen.popTarget = root;
        }

        static void BuildToast(Transform parent, GameUIController c)
        {
            var go = UI("Toast", parent);
            Place(Rect(go), TopCenter, new Vector2(0f, -160f), new Vector2(980f, 76f), TopCenter);
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var body = Panel(go.transform, "Panel", WithAlpha(InsetColor, 0.95f), out var root);
            Stretch(root);
            var text = Text(body, "Message", "Message", 28, Parchment, TextAlignmentOptions.Center, true, matShadow);
            Stretch(text.rectTransform, 24f, 6f, 24f, 10f);
            c.toastGroup = group;
            c.toastText = text;
        }

        // =====================================================================
        // Scene wiring
        // =====================================================================

        static void AddGameUIToGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            foreach (var old in scene.GetRootGameObjects().Where(g => g.GetComponent<GameUIController>() != null).ToList())
                Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GameUI";

            if (!scene.GetRootGameObjects().Any(g => g.GetComponentInChildren<EventSystem>(true) != null))
                MakeEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>MainMenu first, then the Arena (if it has been built), then the movement test scene.</summary>
        public static void SetBuildScenes()
        {
            const string arenaPath = "Assets/Scenes/Arena.unity";
            var ours = File.Exists(arenaPath)
                ? new[] { MainMenuScenePath, arenaPath, GameScenePath }
                : new[] { MainMenuScenePath, GameScenePath };
            var others = EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path));
            EditorBuildSettings.scenes = ours.Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();
        }

        // =====================================================================
        // Building blocks
        // =====================================================================

        static void MakeCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ink;
            cam.orthographic = true;
        }

        static void MakeEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static Canvas MakeCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            return canvas;
        }

        static UIScreen MakeScreen(Transform parent, string name)
        {
            var go = UI(name, parent);
            Stretch(Rect(go));
            go.AddComponent<CanvasGroup>();
            return go.AddComponent<UIScreen>();
        }

        static GameObject UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        static RectTransform Rect(GameObject go) => (RectTransform)go.transform;

        static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? Center;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Full-width horizontal strip at a height from the bottom.</summary>
        static void Span(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        static void Layout(GameObject go, float width, float height)
        {
            if (!go.TryGetComponent<LayoutElement>(out var le)) le = go.AddComponent<LayoutElement>();
            if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
            if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
        }

        static VerticalLayoutGroup VStack(GameObject go, float spacing, RectOffset padding, TextAnchor align)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            if (padding != null) v.padding = padding;
            return v;
        }

        static HorizontalLayoutGroup HStack(GameObject go, float spacing, TextAnchor align)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var go = UI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, Material material = null)
        {
            var go = UI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static TextMeshProUGUI Title(Transform parent, string content, float size, Vector2 position, Color? color = null)
        {
            var t = Text(parent, "Title", content, size, color ?? Parchment, TextAlignmentOptions.Center, true, matTitle);
            t.characterSpacing = 6;
            Place(t.rectTransform, Center, position, new Vector2(1700f, size * 1.4f));
            return t;
        }

        static TextMeshProUGUI Sub(Transform parent, string content, Vector2 position, float size = 30f)
        {
            var t = Text(parent, "Subtitle", content, size, DimText, TextAlignmentOptions.Center, false, matShadow);
            Place(t.rectTransform, Center, position, new Vector2(1500f, size * 2f));
            return t;
        }

        static TextMeshProUGUI Label(Transform parent, string content, Vector2 position)
        {
            var t = Text(parent, "Label", content, 22, DimText, TextAlignmentOptions.Left, true);
            t.characterSpacing = 6;
            Place(t.rectTransform, TopLeft, position, new Vector2(700f, 30f), TopLeft);
            return t;
        }

        static void RowLabel(Transform parent, string content, float y)
        {
            var t = Text(parent, "Label_" + content, content, 34, Parchment, TextAlignmentOptions.Left, false, matShadow);
            Place(t.rectTransform, TopLeft, new Vector2(64f, y), new Vector2(420f, 50f), LeftMiddle);
        }

        static TextMeshProUGUI RowValue(Transform parent, string content, float y)
        {
            var t = Text(parent, "Value", content, 30, Gold, TextAlignmentOptions.Right, true, matShadow);
            Place(t.rectTransform, TopRight, new Vector2(-60f, y), new Vector2(96f, 44f), RightMiddle);
            return t;
        }

        static void Divider(Transform parent, Vector2 position, float width)
        {
            var root = UI("Divider", parent);
            Place(Rect(root), Center, position, new Vector2(width, 36f));
            var left = Img(root.transform, "Left", sWhite, WithAlpha(Gold, 0.8f));
            Place(left.rectTransform, LeftMiddle, Vector2.zero, new Vector2(width * 0.5f - 34f, 4f), LeftMiddle);
            var right = Img(root.transform, "Right", sWhite, WithAlpha(Gold, 0.8f));
            Place(right.rectTransform, RightMiddle, Vector2.zero, new Vector2(width * 0.5f - 34f, 4f), RightMiddle);
            var gem = Img(root.transform, "Gem", iGem, Color.white);
            Place(gem.rectTransform, Center, Vector2.zero, new Vector2(36f, 36f));
        }

        static Image Dim(Transform parent, float alpha)
        {
            var img = Img(parent, "Dim", sWhite, WithAlpha(Ink, alpha));
            Stretch(img.rectTransform);
            img.raycastTarget = true;
            return img;
        }

        static Image Backdrop(Transform parent, Color tint)
        {
            var img = Img(parent, "Backdrop", sRamp, tint);
            Stretch(img.rectTransform);
            img.raycastTarget = true;
            return img;
        }

        static void Particles(Transform parent, Color color, int count)
        {
            var go = UI("AmbientParticles", parent);
            Stretch(Rect(go));
            var p = go.AddComponent<UIAmbientParticles>();
            p.sprite = sWhite;
            p.color = color;
            p.count = count;
        }

        static void Knight(Transform parent, Vector2 bottomLeftOffset, float scale)
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(KnightIdlePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (frames.Length == 0) return;
            var img = Img(parent, "Knight", frames[0], Color.white);
            img.preserveAspect = true;
            Place(img.rectTransform, BottomLeft, bottomLeftOffset, new Vector2(270f, 400f) * scale, new Vector2(0.5f, 0f));
            var anim = img.gameObject.AddComponent<UISpriteAnimator>();
            anim.frames = frames;
            anim.framesPerSecond = 6f;
        }

        /// <summary>Panel with a hard pixel drop shadow. Returns the Body (put content in it); root is the outer object to position.</summary>
        static RectTransform Panel(Transform parent, string name, Color color, out RectTransform root)
        {
            var go = UI(name, parent);
            root = Rect(go);
            var shadow = Img(go.transform, "Shadow", sPanel, ShadowColor, true);
            Stretch(shadow.rectTransform, 10f, 10f, -10f, -10f);
            var body = Img(go.transform, "Body", sPanel, color, true);
            Stretch(body.rectTransform);
            body.raycastTarget = true;
            return body.rectTransform;
        }

        /// <summary>A clickable card: grows, lifts off its shadow and glows on hover.</summary>
        static Button Card(Transform parent, string name, Vector2 position, Vector2 size, Color color, out RectTransform body)
        {
            var go = UI(name, parent);
            Place(Rect(go), Center, position, size);
            var hit = go.AddComponent<Image>();
            hit.sprite = sWhite;
            hit.color = Color.clear;

            var shadow = Img(go.transform, "Shadow", sPanel, ShadowColor, true);
            Stretch(shadow.rectTransform, 10f, 10f, -10f, -10f);
            var b = Img(go.transform, "Body", sPanel, color, true);
            Stretch(b.rectTransform);
            var glow = Img(b.transform, "HoverGlow", sOutline, WithAlpha(Color.white, 0f), true);
            Stretch(glow.rectTransform, -6f, -6f, -6f, -6f);

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            var hover = go.AddComponent<UIButtonHover>();
            hover.hoverScale = 1.03f;
            hover.pressScale = 0.98f;
            hover.lift = b.rectTransform;
            hover.liftAmount = 14f;
            hover.glow = glow;
            hover.glowAlpha = 0.7f;

            body = b.rectTransform;
            return button;
        }

        static Button Btn(Transform parent, string name, string label, Style style, Vector2 size, float fontSize = 30f, bool cursor = false)
        {
            var go = UI(name, parent);
            Rect(go).sizeDelta = size;
            Layout(go, size.x, size.y);
            var img = go.AddComponent<Image>();
            img.sprite = sButton;
            img.type = Image.Type.Sliced;
            img.color = style.normal;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;

            var text = Text(go.transform, "Label", label, fontSize, style.text, TextAlignmentOptions.Center, true);
            Stretch(text.rectTransform, 16f, 4f, 16f, 12f);

            var hover = go.AddComponent<UIButtonHover>();
            hover.background = img;
            hover.label = text;
            hover.normalColor = style.normal;
            hover.hoverColor = style.hover;
            hover.pressedColor = style.pressed;
            hover.disabledColor = DisabledColor;

            if (cursor)
            {
                var arrow = Img(go.transform, "Cursor", iArrow, Gold);
                Place(arrow.rectTransform, LeftMiddle, new Vector2(-16f, 2f), new Vector2(18f, 27f), RightMiddle);
                arrow.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
                hover.indicator = arrow.rectTransform;
            }
            return button;
        }

        static UIBar Bar(Transform parent, string name, Color fillColor, Color lowColor, float preview, out RectTransform root)
        {
            var go = UI(name, parent);
            root = Rect(go);
            var bg = go.AddComponent<Image>();
            bg.sprite = sBar;
            bg.type = Image.Type.Sliced;
            bg.color = InsetColor;
            bg.raycastTarget = false;

            var area = UI("FillArea", go.transform);
            Stretch(Rect(area), 6f, 6f, 6f, 6f);
            var trail = Img(area.transform, "Trail", sWhite, new Color(1f, 0.93f, 0.85f, 0.9f));
            Stretch(trail.rectTransform);
            trail.rectTransform.anchorMax = new Vector2(preview, 1f);
            var fill = Img(area.transform, "Fill", sWhite, fillColor);
            Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(preview, 1f);
            var shine = Img(fill.transform, "Shine", sWhite, new Color(1f, 1f, 1f, 0.28f));
            shine.rectTransform.anchorMin = new Vector2(0f, 1f);
            shine.rectTransform.anchorMax = new Vector2(1f, 1f);
            shine.rectTransform.pivot = new Vector2(0.5f, 1f);
            shine.rectTransform.sizeDelta = new Vector2(0f, 6f);
            shine.rectTransform.anchoredPosition = Vector2.zero;

            var bar = go.AddComponent<UIBar>();
            bar.fill = fill.rectTransform;
            bar.trail = trail.rectTransform;
            bar.fillImage = fill;
            bar.fullColor = fillColor;
            bar.lowColor = lowColor;
            return bar;
        }

        static TextMeshProUGUI StatRow(Transform parent, Sprite icon, string label, string value, out GameObject row)
        {
            row = UI("Row_" + label, parent);
            Layout(row, 660f, 56f);
            var ic = Img(row.transform, "Icon", icon, Color.white);
            Place(ic.rectTransform, LeftMiddle, new Vector2(4f, 0f), new Vector2(44f, 44f), LeftMiddle);
            var l = Text(row.transform, "Label", label, 30, DimText, TextAlignmentOptions.Left);
            Stretch(l.rectTransform, 68f, 0f, 0f, 0f);
            var v = Text(row.transform, "Value", value, 34, Parchment, TextAlignmentOptions.Right, true, matShadow);
            Stretch(v.rectTransform, 300f, 0f, 4f, 0f);
            return v;
        }

        static void KeyChip(Transform parent, string key)
        {
            var go = UI("Key_" + key, parent);
            Layout(go, Mathf.Max(64f, 30f + key.Length * 17f), 58f);
            var img = go.AddComponent<Image>();
            img.sprite = sButton;
            img.type = Image.Type.Sliced;
            img.color = Hex("#D8CCF2");
            img.raycastTarget = false;
            var t = Text(go.transform, "Label", key, 26, Ink, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform, 6f, 2f, 6f, 10f);
        }

        static void KeyWord(Transform parent, string word)
        {
            var t = Text(parent, "Word_" + word, word, 24, DimText, TextAlignmentOptions.Center);
            Layout(t.gameObject, 60f, 58f);
        }

        static Slider PixelSlider(Transform parent, string name, float value)
        {
            var res = new DefaultControls.Resources { standard = sBar, background = sBar, knob = sButton, checkmark = sWhite, dropdown = sWhite, inputField = sBar, mask = sWhite };
            var go = DefaultControls.CreateSlider(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.transition = Selectable.Transition.None;

            var bg = go.transform.Find("Background").GetComponent<Image>();
            bg.sprite = sBar;
            bg.type = Image.Type.Sliced;
            bg.color = InsetColor;
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0.2f);
            bgRect.anchorMax = new Vector2(1f, 0.8f);
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            var fillArea = (RectTransform)go.transform.Find("Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.2f);
            fillArea.anchorMax = new Vector2(1f, 0.8f);
            fillArea.offsetMin = new Vector2(6f, 5f);
            fillArea.offsetMax = new Vector2(-6f, -5f);
            var fill = fillArea.Find("Fill").GetComponent<Image>();
            fill.sprite = sWhite;
            fill.type = Image.Type.Simple;
            fill.color = Gold;
            fill.rectTransform.sizeDelta = new Vector2(0f, 0f);

            var handleArea = (RectTransform)go.transform.Find("Handle Slide Area");
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);
            var handle = handleArea.Find("Handle").GetComponent<Image>();
            handle.sprite = sButton;
            handle.type = Image.Type.Sliced;
            handle.color = Parchment;
            handle.rectTransform.sizeDelta = new Vector2(28f, 12f);

            var hover = go.AddComponent<UIButtonHover>();
            hover.scaleTarget = handle.rectTransform;
            hover.hoverScale = 1.2f;
            hover.pressScale = 1.1f;
            hover.background = handle;
            hover.normalColor = Parchment;
            hover.hoverColor = Color.white;
            hover.pressedColor = Gold;
            hover.disabledColor = DisabledColor;
            return slider;
        }

        static Toggle PixelSwitch(Transform parent, string name, out TextMeshProUGUI stateLabel)
        {
            var go = UI(name, parent);
            var track = go.AddComponent<Image>();
            track.sprite = sBar;
            track.type = Image.Type.Sliced;
            track.color = Hex("#5DB85D");
            var toggle = go.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = track;
            toggle.graphic = null;
            toggle.isOn = true;

            var knob = Img(go.transform, "Knob", sButton, Parchment, true);
            Place(knob.rectTransform, Center, new Vector2(32f, 2f), new Vector2(46f, 46f));

            stateLabel = Text(parent, "FullscreenState", "ON", 26, DimText, TextAlignmentOptions.Right, true);

            var sw = go.AddComponent<UIToggleSwitch>();
            sw.knob = knob.rectTransform;
            sw.track = track;
            sw.stateLabel = stateLabel;
            sw.onColor = Hex("#5DB85D");
            sw.offColor = Hex("#2A2147");
            sw.knobTravel = 32f;

            var hover = go.AddComponent<UIButtonHover>();
            hover.scaleTarget = knob.rectTransform;
            hover.hoverScale = 1.12f;
            hover.pressScale = 0.92f;
            return toggle;
        }

        static TMP_InputField InputField(Transform parent, string name, string placeholder, bool multiline)
        {
            var res = new TMP_DefaultControls.Resources { standard = sBar, background = sBar, inputField = sBar, knob = sWhite, checkmark = sWhite, dropdown = sWhite, mask = sWhite };
            var go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var field = go.GetComponent<TMP_InputField>();

            var bg = go.GetComponent<Image>();
            bg.sprite = sBar;
            bg.type = Image.Type.Sliced;
            bg.color = InsetColor;
            field.transition = Selectable.Transition.None;

            Stretch(field.textViewport, 18f, 12f, 18f, 12f);
            var placeholderText = (TextMeshProUGUI)field.placeholder;
            var text = (TextMeshProUGUI)field.textComponent;
            foreach (var t in new[] { placeholderText, text })
            {
                t.font = font;
                t.fontSize = 26;
                t.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
                t.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            }
            text.color = Parchment;
            placeholderText.text = placeholder;
            placeholderText.color = WithAlpha(DimText, 0.6f);
            placeholderText.fontStyle = FontStyles.Italic;

            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            field.characterLimit = multiline ? 0 : 40;
            field.customCaretColor = true;
            field.caretColor = Gold;
            field.caretWidth = 3;
            field.selectionColor = WithAlpha(Gold, 0.35f);

            var hover = go.AddComponent<UIButtonHover>();
            hover.background = bg;
            hover.normalColor = InsetColor;
            hover.hoverColor = InsetHover;
            hover.pressedColor = InsetHover;
            hover.disabledColor = InsetColor;
            hover.hoverScale = 1.005f;
            hover.pressScale = 1f;
            return field;
        }

        static ScrollRect ScrollList(Transform parent, out RectTransform content)
        {
            var res = new DefaultControls.Resources { standard = sBar, background = sBar, inputField = sBar, knob = sBar, checkmark = sWhite, dropdown = sWhite, mask = sWhite };
            var go = DefaultControls.CreateScrollView(res);
            go.name = "QuestionList";
            go.transform.SetParent(parent, false);
            var scroll = go.GetComponent<ScrollRect>();

            var bg = go.GetComponent<Image>();
            bg.sprite = sBar;
            bg.type = Image.Type.Sliced;
            bg.color = InsetColor;

            if (scroll.horizontalScrollbar != null) Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 4f;

            var viewport = scroll.viewport;
            Object.DestroyImmediate(viewport.GetComponent<Mask>());
            Object.DestroyImmediate(viewport.GetComponent<Image>());
            viewport.gameObject.AddComponent<RectMask2D>();

            content = scroll.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var v = VStack(content.gameObject, 8f, new RectOffset(10, 10, 10, 10), TextAnchor.UpperCenter);
            v.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sb = scroll.verticalScrollbar;
            var sbImg = sb.GetComponent<Image>();
            sbImg.sprite = sBar;
            sbImg.color = Hex("#120D1E");
            ((RectTransform)sb.transform).sizeDelta = new Vector2(20f, 0f);
            var handle = sb.handleRect.GetComponent<Image>();
            handle.sprite = sBar;
            handle.type = Image.Type.Sliced;
            handle.color = Hex("#6A55A8");
            return scroll;
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
