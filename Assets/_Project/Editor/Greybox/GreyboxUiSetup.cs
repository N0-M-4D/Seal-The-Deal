using CloseTheDeal.Net;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Builds the playtest menu and HUD (docs/systems/MENU_AND_HUD.md) as one "GameUI" canvas.
    /// Set Up Scene creates it when missing; "Rebuild Game UI" deletes it and builds it fresh,
    /// so change this file rather than the scene. Greybox look: flat dark card, amber primary
    /// actions, white text with a muted grey for secondary lines (both above 7:1 on the card).
    /// </summary>
    public static class GreyboxUiSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";
        const string CanvasName = "GameUI";
        const string OldCanvasName = "LobbyCanvas";

        // Palette. Contrast: Text on Card 15.6:1, Muted on Card 7.5:1, Error on Card 8:1, PrimaryText on
        // Primary 10:1, Text on Secondary 11.6:1, Placeholder on Field 5:1. HUD text sits on a dark chip.
        static readonly Color Dim = new(0.02f, 0.03f, 0.05f, 0.72f);
        static readonly Color Card = new(0.09f, 0.10f, 0.13f, 0.98f);
        static readonly Color Field = new(0.06f, 0.07f, 0.09f, 1f);
        static readonly Color Divider = new(0.17f, 0.19f, 0.23f, 1f);
        static readonly Color Text = new(0.95f, 0.95f, 0.96f, 1f);
        static readonly Color Muted = new(0.64f, 0.67f, 0.71f, 1f);
        static readonly Color Placeholder = new(0.49f, 0.52f, 0.57f, 1f);
        static readonly Color Error = new(1f, 0.56f, 0.52f, 1f);
        static readonly Color Primary = new(0.94f, 0.71f, 0.30f, 1f);
        static readonly Color PrimaryText = new(0.10f, 0.08f, 0.02f, 1f);
        static readonly Color Secondary = new(0.17f, 0.19f, 0.23f, 1f);

        const float CardWidth = 720f;
        const float PrimaryHeight = 64f;
        const float ControlHeight = 56f;
        const float SideButtonWidth = 160f;

        [MenuItem("Close the Deal/Greybox/Rebuild Game UI")]
        public static void RebuildGameUi()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SteamLobby lobby = Object.FindAnyObjectByType<SteamLobby>();
            if (lobby == null)
            {
                Debug.LogError("[UI] No SteamLobby in the scene. Run Close the Deal > Greybox > Set Up Scene first.");
                return;
            }

            DestroyNamed(CanvasName);
            DestroyNamed(OldCanvasName);
            Build(lobby);
            SaveScene();
        }

        /// <summary>Called by Set Up Scene. Builds the UI only when there is none; replaces the
        /// pre-TextMeshPro LobbyCanvas, which was tool-generated and whose script no longer exists.</summary>
        public static void Ensure(SteamLobby lobby)
        {
            if (Object.FindAnyObjectByType<GameMenu>() != null)
                return;

            DestroyNamed(OldCanvasName);
            Build(lobby);
        }

        /// <summary>TextMeshPro's essential resources (default font, shaders, settings), imported silently.</summary>
        [MenuItem("Close the Deal/Greybox/Import TextMeshPro Essentials")]
        public static void ImportTextMeshProEssentials()
        {
            if (TextMeshProReady())
            {
                Debug.Log("[UI] TextMeshPro essentials already present.");
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("[UI] Importing TextMeshPro essentials...");
        }

        static double _importDeadline;

        /// <summary>
        /// Shell-only variant. Package imports finish on later editor ticks, so this must be run
        /// with the Editor binary directly and WITHOUT -quit; it exits by itself when done:
        ///   Unity.exe -batchmode -projectPath &lt;repo&gt; -executeMethod CloseTheDeal.Editor.Greybox.GreyboxUiSetup.ImportTextMeshProEssentialsAndExit
        /// </summary>
        public static void ImportTextMeshProEssentialsAndExit()
        {
            if (TextMeshProReady())
            {
                Debug.Log("[UI] TextMeshPro essentials already present.");
                EditorApplication.Exit(0);
                return;
            }

            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageFailed += OnImportFailed;
            _importDeadline = EditorApplication.timeSinceStartup + 300.0;
            EditorApplication.update += WatchImportTimeout;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        static void OnImportCompleted(string packageName)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[UI] Imported {packageName}.");
            EditorApplication.Exit(0);
        }

        static void OnImportFailed(string packageName, string error)
        {
            Debug.LogError($"[UI] Importing {packageName} failed: {error}");
            EditorApplication.Exit(1);
        }

        static void WatchImportTimeout()
        {
            if (EditorApplication.timeSinceStartup < _importDeadline)
                return;

            Debug.LogError("[UI] Timed out waiting for the TextMeshPro import.");
            EditorApplication.Exit(2);
        }

        // ---- Build ---------------------------------------------------------------------------

        /// <summary>TMP_Settings throws rather than returning null when its settings asset doesn't exist yet.</summary>
        static bool TextMeshProReady()
        {
            return Resources.Load<TMP_Settings>("TMP Settings") != null && TMP_Settings.defaultFontAsset != null;
        }

        static void Build(SteamLobby lobby)
        {
            if (!TextMeshProReady())
            {
                Debug.LogError("[UI] TextMeshPro essentials are missing. Run Close the Deal > Greybox > Import TextMeshPro Essentials, then Rebuild Game UI.");
                return;
            }

            EnsureEventSystem();

            var canvasGo = new GameObject(CanvasName);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var refs = new MenuRefs();
            refs.Hud = BuildHud(canvasGo.transform, out TMP_Text readout);
            refs.Menu = BuildMenu(canvasGo.transform, refs);

            var menu = canvasGo.AddComponent<GameMenu>();
            Wire(menu, lobby, refs);

            var hud = canvasGo.AddComponent<PredictionDebugHud>();
            var serializedHud = new SerializedObject(hud);
            GreyboxSceneSetup.SetReference(serializedHud, "_text", readout);
            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[UI] Built the game menu and HUD.");
        }

        sealed class MenuRefs
        {
            public GameObject Hud, Menu;
            public TMP_Text Mode, Status;
            public GameObject StartSection, JoinCodeGroup, GameSection, CodeGroup;
            public UnityEngine.UI.Button Host, JoinCode, JoinLocal, Copy, Invite, Resume, Leave;
            public TMP_InputField CodeInput;
            public TMP_Text JoinError, CodeText, Players;
        }

        // ---- HUD -----------------------------------------------------------------------------

        static GameObject BuildHud(Transform canvas, out TMP_Text readout)
        {
            GameObject hud = Stretch(canvas, "Hud");

            // A white dot with a dark ring reads on both the grey walls and the sky.
            Block(hud.transform, "CrosshairRing", new Color(0f, 0f, 0f, 0.6f), new Vector2(14f, 14f));
            Block(hud.transform, "Crosshair", Text, new Vector2(6f, 6f));

            Chip(hud.transform, "MenuHint", "<b>Esc</b>  Menu", 18, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(150f, 40f));
            readout = Chip(hud.transform, "NetworkReadout", string.Empty, 16, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(470f, 64f));
            return hud;
        }

        /// <summary>HUD text on a dark translucent chip, so it reads over bright walls and sky alike.</summary>
        static TMP_Text Chip(Transform parent, string name, string content, float size, Vector2 corner, Vector2 offset, Vector2 chipSize)
        {
            var chip = new GameObject(name, typeof(RectTransform));
            chip.transform.SetParent(parent, false);
            var back = chip.AddComponent<UnityEngine.UI.Image>();
            back.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
            back.raycastTarget = false;
            Pin((RectTransform)chip.transform, corner, offset, chipSize);

            TMP_Text text = Label(chip.transform, "Text", content, size, Text);
            Fill(text.rectTransform, 14f);
            return text;
        }

        // ---- Menu ----------------------------------------------------------------------------

        static GameObject BuildMenu(Transform canvas, MenuRefs r)
        {
            GameObject menu = Stretch(canvas, "Menu");
            var dim = menu.AddComponent<UnityEngine.UI.Image>();
            dim.color = Dim; // Also blocks clicks from reaching anything behind the menu.

            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(menu.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, 0f);
            card.AddComponent<UnityEngine.UI.Image>().color = Card;
            Column(card, 14f, new RectOffset(48, 48, 44, 44));
            var fitter = card.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            Row(Label(card.transform, "Title", "<b>CLOSE THE DEAL</b>", 40, Text), 48f);
            Row(Label(card.transform, "Subtitle", "Greybox playtest", 18, Muted), 24f);
            r.Mode = Row(Label(card.transform, "Mode", "Starting...", 18, Muted), 24f);
            Rule(card.transform);

            r.Status = Label(card.transform, "Status", "Starting...", 22, Text);
            r.Status.textWrappingMode = TextWrappingModes.Normal;
            Row(r.Status, 60f); // Two lines reserved, so the card doesn't jump as messages change.

            BuildStartSection(card.transform, r);
            BuildGameSection(card.transform, r);

            Rule(card.transform);
            TMP_Text controls = Label(card.transform, "Controls",
                "<b>WASD</b> move    <b>Shift</b> sprint    <b>Space</b> jump    <b>Mouse</b> look\n" +
                "<b>Left click</b> blast, aimed at the crosshair\n" +
                "<b>Climb</b> jump at a ledge and keep holding forward    <b>Esc</b> menu",
                17, Muted);
            controls.lineSpacing = 12f;
            Row(controls, 84f);
            return menu;
        }

        static void BuildStartSection(Transform card, MenuRefs r)
        {
            r.StartSection = Section(card, "StartSection", 16f);
            Transform s = r.StartSection.transform;

            r.Host = Button(s, "HostButton", "Host game", primary: true, PrimaryHeight);

            r.JoinCodeGroup = Section(s, "JoinCodeGroup", 8f);
            Transform join = r.JoinCodeGroup.transform;
            Row(Label(join, "JoinLabel", "Join with a lobby code", 18, Muted), 24f);
            GameObject joinRow = RowGroup(join, "JoinRow");
            r.CodeInput = CodeField(joinRow.transform);
            r.JoinCode = Button(joinRow.transform, "JoinButton", "Join", primary: false, ControlHeight, SideButtonWidth);
            r.JoinError = Label(join, "JoinError", string.Empty, 18, Error);
            r.JoinError.textWrappingMode = TextWrappingModes.Normal;

            r.JoinLocal = Button(s, "JoinLocalButton", "Join game on this PC", primary: false, ControlHeight);
        }

        static void BuildGameSection(Transform card, MenuRefs r)
        {
            r.GameSection = Section(card, "GameSection", 12f);
            Transform s = r.GameSection.transform;

            r.CodeGroup = Section(s, "CodeGroup", 8f);
            Transform code = r.CodeGroup.transform;
            Row(Label(code, "CodeLabel", "Lobby code · send this to your friend", 18, Muted), 24f);
            GameObject codeRow = RowGroup(code, "CodeRow");
            GameObject codeBox = new("CodeBox", typeof(RectTransform));
            codeBox.transform.SetParent(codeRow.transform, false);
            codeBox.AddComponent<UnityEngine.UI.Image>().color = Field;
            Size(codeBox, ControlHeight, flexibleWidth: 1f);
            r.CodeText = Label(codeBox.transform, "CodeText", string.Empty, 26, Text);
            r.CodeText.characterSpacing = 4f;
            Fill(r.CodeText.rectTransform, 16f);
            r.Copy = Button(codeRow.transform, "CopyButton", "Copy", primary: false, ControlHeight, SideButtonWidth);

            r.Players = Row(Label(s, "Players", string.Empty, 20, Text), 28f);
            r.Invite = Button(s, "InviteButton", "Invite with the Steam overlay", primary: false, ControlHeight);
            r.Resume = Button(s, "ResumeButton", "Back to game", primary: true, PrimaryHeight);
            r.Leave = Button(s, "LeaveButton", "Leave game", primary: false, ControlHeight);
        }

        static void Wire(GameMenu menu, SteamLobby lobby, MenuRefs r)
        {
            var so = new SerializedObject(menu);
            GreyboxSceneSetup.SetReference(so, "_lobby", lobby);
            GreyboxSceneSetup.SetReference(so, "_menuRoot", r.Menu);
            GreyboxSceneSetup.SetReference(so, "_hudRoot", r.Hud);
            GreyboxSceneSetup.SetReference(so, "_modeText", r.Mode);
            GreyboxSceneSetup.SetReference(so, "_statusText", r.Status);
            GreyboxSceneSetup.SetReference(so, "_startSection", r.StartSection);
            GreyboxSceneSetup.SetReference(so, "_hostButton", r.Host);
            GreyboxSceneSetup.SetReference(so, "_joinCodeGroup", r.JoinCodeGroup);
            GreyboxSceneSetup.SetReference(so, "_codeInput", r.CodeInput);
            GreyboxSceneSetup.SetReference(so, "_joinCodeButton", r.JoinCode);
            GreyboxSceneSetup.SetReference(so, "_joinError", r.JoinError);
            GreyboxSceneSetup.SetReference(so, "_joinLocalButton", r.JoinLocal);
            GreyboxSceneSetup.SetReference(so, "_gameSection", r.GameSection);
            GreyboxSceneSetup.SetReference(so, "_codeGroup", r.CodeGroup);
            GreyboxSceneSetup.SetReference(so, "_codeText", r.CodeText);
            GreyboxSceneSetup.SetReference(so, "_copyButton", r.Copy);
            GreyboxSceneSetup.SetReference(so, "_playersText", r.Players);
            GreyboxSceneSetup.SetReference(so, "_inviteButton", r.Invite);
            GreyboxSceneSetup.SetReference(so, "_resumeButton", r.Resume);
            GreyboxSceneSetup.SetReference(so, "_leaveButton", r.Leave);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Controls ------------------------------------------------------------------------

        static UnityEngine.UI.Button Button(Transform parent, string name, string label, bool primary, float height, float width = -1f)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = null; // Flat.
            image.color = primary ? Primary : Secondary;

            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.colors = Tints();

            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 22;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = -2f; // The default font's synthetic bold spaces letters wide.
            text.color = primary ? PrimaryText : Text;
            text.raycastTarget = false;

            if (width > 0f)
                Size(go, height, preferredWidth: width);
            else
                Size(go, height);
            return button;
        }

        /// <summary>Resting slightly darker than the base colour so hover and keyboard focus visibly brighten it.</summary>
        static UnityEngine.UI.ColorBlock Tints()
        {
            return new UnityEngine.UI.ColorBlock
            {
                normalColor = new Color(0.86f, 0.86f, 0.86f, 1f),
                highlightedColor = Color.white,
                pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        static TMP_InputField CodeField(Transform parent)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "CodeInput";
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = null;
            image.color = Field;

            var field = go.GetComponent<TMP_InputField>();
            field.colors = Tints();
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 32;
            field.caretColor = Text;
            field.customCaretColor = true;
            field.selectionColor = new Color(Primary.r, Primary.g, Primary.b, 0.4f);
            field.pointSize = 22;

            var text = (TMP_Text)field.textComponent;
            text.color = Text;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var placeholder = (TMP_Text)field.placeholder;
            placeholder.text = "Paste the code from the host";
            placeholder.color = Placeholder;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var textArea = (RectTransform)field.textViewport;
            textArea.offsetMin = new Vector2(16f, 6f);
            textArea.offsetMax = new Vector2(-16f, -6f);

            Size(go, ControlHeight, flexibleWidth: 1f);
            return field;
        }

        // ---- Layout helpers ------------------------------------------------------------------

        static TMP_Text Label(Transform parent, string name, string content, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        static TMP_Text Row(TMP_Text text, float height)
        {
            Size(text.gameObject, height);
            return text;
        }

        static void Rule(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = Divider;
            image.raycastTarget = false;
            Size(go, 1f);
        }

        static GameObject Section(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Column(go, spacing, new RectOffset(0, 0, 0, 0));
            return go;
        }

        static GameObject RowGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var row = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 12f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Size(go, ControlHeight);
            return go;
        }

        static void Column(GameObject go, float spacing, RectOffset padding)
        {
            var column = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.spacing = spacing;
            column.padding = padding;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
        }

        static void Size(GameObject go, float height, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var element = go.GetComponent<UnityEngine.UI.LayoutElement>();
            if (element == null)
                element = go.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
            element.flexibleWidth = flexibleWidth;
        }

        static GameObject Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Fill((RectTransform)go.transform, 0f);
            return go;
        }

        static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, 0f);
            rect.offsetMax = new Vector2(-inset, 0f);
        }

        /// <summary>Anchors and pivots to the same screen corner, then offsets from it.</summary>
        static void Pin(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        static void Block(Transform parent, string name, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            Pin((RectTransform)go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        // ---- Scene helpers -------------------------------------------------------------------

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static void DestroyNamed(string name)
        {
            GameObject old = GameObject.Find(name);
            if (old != null)
                Object.DestroyImmediate(old);
        }

        static void SaveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
