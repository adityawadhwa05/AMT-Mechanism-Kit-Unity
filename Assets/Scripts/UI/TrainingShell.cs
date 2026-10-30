using System;
using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // Application frame shared by the 3D viewer and AR: top bar, contextual list panel (left),
    // details card (right), floating control bar (bottom), caption banner, toasts, loading/error
    // overlays and the home screen. Module screens fill these regions.
    public sealed class TrainingShell : MonoBehaviour
    {
        public TrainingSession Session { get; private set; }
        public bool IsAR { get; private set; }
        public RectTransform OverlayRoot { get; private set; }
        public WorldLabels Labels { get; private set; }
        public ModuleScreen Current { get; private set; }
        public event Action HomeShown;

        RectTransform safe, hud, home, topBar, left, right, bottom, caption, toast, loading, error;
        RectTransform topRow;
        RectTransform leftContent, rightContent, bottomContent;
        Text title, subtitle, leftTitle, captionTitle, captionText, toastText, loadingText, errorText;
        Image loadingFill;
        Button backButton, paletteButton, modeButton;
        ScrollRect rightScroll;
        float toastUntil;
        Rect lastSafeArea;
        readonly Dictionary<string, Func<ModuleScreen>> screens = new Dictionary<string, Func<ModuleScreen>>();

        public const float TopBarHeight = 84f, PanelWidth = 420f, InfoWidth = 470f, Gap = 16f;

        public static TrainingShell Create(TrainingSession session, bool ar)
        {
            var go = new GameObject("Training UI");
            var shell = go.AddComponent<TrainingShell>();
            shell.Session = session; shell.IsAR = ar;
            shell.Build();
            return shell;
        }

        void Build()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
                // One EventSystem for the whole app: switching scenes must not re-create the UI input module.
                DontDestroyOnLoad(events);
            }
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .65f;

            safe = UIFactory.Rect("Safe area", canvas.transform).Stretch();
            Labels = WorldLabels.Create(safe, Session);
            hud = UIFactory.Rect("HUD", safe).Stretch();
            BuildTopBar();
            BuildPanels();
            BuildCaption();
            home = UIFactory.Rect("Home", safe).Stretch();
            OverlayRoot = UIFactory.Rect("Overlays", safe).Stretch();
            BuildToast();
            BuildLoading();
            BuildError();

            Session.PartTapped += p => Current?.OnPartTapped(p);
            Session.EmptyTapped += () => Current?.OnEmptyTapped();
            Session.PartDoubleTapped += p => Session.FocusParts(new[] { p });
            Session.Kit.Loaded += OnLoaded;
            Session.Kit.Failed += ShowError;

            screens["components"] = () => new ComponentsScreen();
            screens["tools"] = () => new ToolsScreen();
            screens["motion"] = () => new HowItWorksScreen();
            screens["exploded"] = () => new ExplodedScreen();
            screens["assembly"] = () => new GuideScreen(false);
            screens["disassembly"] = () => new GuideScreen(true);

            hud.gameObject.SetActive(false);
            home.gameObject.SetActive(false);
            loading.gameObject.SetActive(true);
        }

        // ---------- frame ----------

        void BuildTopBar()
        {
            topBar = UIFactory.Rect("Top bar", hud);
            topBar.anchorMin = new Vector2(0, 1); topBar.anchorMax = Vector2.one; topBar.pivot = new Vector2(.5f, 1);
            topBar.offsetMin = new Vector2(Gap, -TopBarHeight - Gap * .5f); topBar.offsetMax = new Vector2(-Gap, -Gap * .5f);
            var row = topBar; row.HStack(14, new RectOffset(0, 0, 8, 8), TextAnchor.MiddleLeft);
            topRow = row;
            backButton = UIFactory.IconButton(row, Icons.Home, ShowHome, 60, ButtonStyle.Secondary);
            var titles = UIFactory.Rect("Titles", row);
            titles.VStack(0, new RectOffset(4, 0, 6, 6));
            titles.Layout(-1, -1, 1);
            title = UIFactory.Label(titles, "AMT Mechanism Kit", 28, Theme.Text, Weight.Bold, TextAnchor.MiddleLeft);
            subtitle = UIFactory.Label(titles, "", 16, Theme.TextMuted, Weight.Regular, TextAnchor.MiddleLeft);
            AddShadow(title); AddShadow(subtitle);
            paletteButton = UIFactory.IconButton(row, Icons.Palette, TogglePalette, 56);
            UIFactory.IconButton(row, Icons.ResetView, () => Session.View?.ResetView(), 56);
            modeButton = UIFactory.Button(row, IsAR ? "3D viewer" : "View in AR", () => AppNavigation.SwitchScene(!IsAR, Current?.Key),
                ButtonStyle.Primary, IsAR ? Icons.Cube : Icons.AR, 56, 18);
            modeButton.Layout(210, 56, 0);
        }

        static void AddShadow(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, .6f); s.effectDistance = new Vector2(0, -2);
        }

        void BuildPanels()
        {
            float top = TopBarHeight + Gap;
            left = UIFactory.Card(hud, "List panel", Theme.Surface);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1); left.pivot = new Vector2(0, .5f);
            left.offsetMin = new Vector2(Gap, Gap); left.offsetMax = new Vector2(Gap + PanelWidth, -top);
            var leftInner = UIFactory.Rect("Inner", left).Stretch(18, 16, 18, 10);
            leftInner.VStack(8);
            leftTitle = UIFactory.Label(leftInner, "", 15, Theme.TextFaint, Weight.SemiBold, TextAnchor.MiddleLeft);
            leftTitle.Layout(-1, 26, 1);
            leftContent = UIFactory.Rect("Content", leftInner);
            leftContent.Layout(-1, -1, 1, 1);
            leftContent.VStack(8);

            right = UIFactory.Card(hud, "Details panel", Theme.Surface);
            right.anchorMin = new Vector2(1, 0); right.anchorMax = new Vector2(1, 1); right.pivot = new Vector2(1, .5f);
            right.offsetMin = new Vector2(-Gap - InfoWidth, Gap); right.offsetMax = new Vector2(-Gap, -top);
            rightContent = UIFactory.ScrollList(right, out rightScroll, 12, new RectOffset(22, 22, 20, 24));
            rightScroll.GetComponent<RectTransform>().Stretch();

            bottom = UIFactory.Card(hud, "Control bar", Theme.SurfaceRaised, 20);
            bottom.anchorMin = new Vector2(.5f, 0); bottom.anchorMax = new Vector2(.5f, 0); bottom.pivot = new Vector2(.5f, 0);
            bottom.anchoredPosition = new Vector2(0, Gap); bottom.sizeDelta = new Vector2(860, 96);
            bottomContent = UIFactory.Rect("Content", bottom).Stretch(18, 12, 18, 12);
            bottomContent.HStack(12, null, TextAnchor.MiddleCenter);
        }

        void BuildCaption()
        {
            caption = UIFactory.Card(hud, "Caption", Theme.SurfaceRaised, 18);
            caption.anchorMin = caption.anchorMax = new Vector2(.5f, 1); caption.pivot = new Vector2(.5f, 1);
            caption.anchoredPosition = new Vector2(0, -TopBarHeight - Gap);
            caption.sizeDelta = new Vector2(820, 120);
            var inner = UIFactory.Rect("Inner", caption).Stretch(22, 14, 22, 14);
            inner.VStack(4);
            captionTitle = UIFactory.Label(inner, "", 19, Theme.Accent, Weight.SemiBold);
            captionText = UIFactory.Label(inner, "", 17, Theme.Text);
            caption.gameObject.SetActive(false);
        }

        void BuildToast()
        {
            toast = UIFactory.Card(OverlayRoot, "Toast", Theme.SurfaceRaised, 16);
            toast.anchorMin = toast.anchorMax = new Vector2(.5f, 0); toast.pivot = new Vector2(.5f, 0);
            toast.anchoredPosition = new Vector2(0, 130); toast.sizeDelta = new Vector2(700, 64);
            toastText = UIFactory.Label(toast, "", 18, Theme.Text, Weight.SemiBold, TextAnchor.MiddleCenter);
            toastText.rectTransform.Stretch(20, 8, 20, 8);
            toast.gameObject.SetActive(false);
        }

        void BuildLoading()
        {
            loading = UIFactory.Rect("Loading", OverlayRoot).Stretch();
            var bg = loading.gameObject.AddComponent<Image>();
            bg.color = IsAR ? Theme.Scrim : Theme.Canvas;
            var col = UIFactory.Rect("Column", loading);
            col.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 220));
            col.VStack(14, null);
            UIFactory.Label(col, "AMT  ·  MECHANISM KIT", 16, Theme.Accent, Weight.SemiBold, TextAnchor.MiddleCenter).Layout(-1, 26, 1);
            loadingText = UIFactory.Label(col, "Loading the kit model…", 26, Theme.Text, Weight.SemiBold, TextAnchor.MiddleCenter);
            loadingText.Layout(-1, 44, 1);
            var bar = UIFactory.ProgressBar(col, Theme.Accent, 6);
            loadingFill = bar.fill;
            UIFactory.Label(col, "37 parts recovered from the supplied CAD model", 16, Theme.TextMuted, Weight.Regular, TextAnchor.MiddleCenter)
                .Layout(-1, 30, 1);
        }

        void BuildError()
        {
            error = UIFactory.Rect("Error", OverlayRoot).Stretch();
            var bg = error.gameObject.AddComponent<Image>();
            bg.color = Theme.Scrim;
            var card = UIFactory.Card(error, "Card", Theme.SurfaceRaised);
            card.Place(new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(640, 320));
            var col = UIFactory.Rect("Column", card).Stretch(32, 28, 32, 28);
            col.VStack(14);
            var head = UIFactory.Rect("Head", col); head.HStack(12); head.Layout(-1, 40, 1);
            UIFactory.Icon(head, Icons.Alert, Theme.Danger, 34);
            UIFactory.Label(head, "The model could not be loaded", 24, Theme.Text, Weight.Bold, TextAnchor.MiddleLeft).Layout(-1, -1, 1);
            errorText = UIFactory.Label(col, "", 17, Theme.TextMuted);
            errorText.Layout(-1, 120, 1);
            var row = UIFactory.Rect("Actions", col); row.HStack(12, null, TextAnchor.MiddleRight); row.Layout(-1, 52, 1);
            UIFactory.Button(row, "Try again", () =>
            {
                error.gameObject.SetActive(false); loading.gameObject.SetActive(true);
                Session.BeginLoad();
            }, ButtonStyle.Primary, Icons.Restart, 52).Layout(200, 52, 0);
            error.gameObject.SetActive(false);
        }

        void ShowError(string message)
        {
            loading.gameObject.SetActive(false);
            errorText.text = message + "\n\nCheck that Assets/StreamingAssets/Model/MechanismKit_Parts.glb is present and that the glTFast and Draco packages are installed.";
            error.gameObject.SetActive(true);
        }

        void OnLoaded()
        {
            loading.gameObject.SetActive(false);
            string pending = AppNavigation.ConsumePendingModule();
            if (!IsAR)
            {
                if (!string.IsNullOrEmpty(pending) && screens.ContainsKey(pending)) Open(pending);
                else ShowHome();
            }
            else if (!string.IsNullOrEmpty(pending) && screens.ContainsKey(pending)) queuedModule = pending;
        }

        string queuedModule;
        // AR opens the remembered module once the kit has been placed.
        public void OnPlaced()
        {
            if (Current != null || home.gameObject.activeSelf) return;
            if (!string.IsNullOrEmpty(queuedModule)) { Open(queuedModule); queuedModule = null; }
            else ShowHome();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea) ApplySafeArea();
            if (loading.gameObject.activeSelf) loadingFill.rectTransform.anchorMax = new Vector2(Session.Kit.Progress, 1);
            if (toast.gameObject.activeSelf && Time.unscaledTime > toastUntil) toast.gameObject.SetActive(false);
            CentreInFreeArea(bottom); CentreInFreeArea(caption);
            Current?.Tick();
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            var min = lastSafeArea.position; var max = lastSafeArea.position + lastSafeArea.size;
            safe.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            safe.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        // ---------- navigation ----------

        public void Open(string key)
        {
            if (!screens.TryGetValue(key, out var factory)) return;
            Current?.Exit();
            home.gameObject.SetActive(false);
            hud.gameObject.SetActive(true);
            Labels.Clear();
            ClearRegions();
            Current = factory();
            Current.Attach(this, key);
            Current.Enter();
        }

        public void ShowHome()
        {
            Current?.Exit();
            Current = null;
            Labels.Clear();
            Session.StopAll();
            Session.Select(null);
            Session.View?.ResetView();
            ClearRegions();
            // In AR the top bar stays available on the home screen for Move / scale / 3D viewer.
            hud.gameObject.SetActive(IsAR);
            if (IsAR)
            {
                SetTitle("AMT Mechanism Kit", "Choose a module");
                left.gameObject.SetActive(false); right.gameObject.SetActive(false);
            }
            BuildHome();
            home.gameObject.SetActive(true);
            HomeShown?.Invoke();
        }

        public void HideAll()
        {
            Current?.Exit(); Current = null;
            hud.gameObject.SetActive(false);
            home.gameObject.SetActive(false);
            Labels.Clear();
        }

        void ClearRegions()
        {
            leftContent.Clear(); rightContent.Clear(); bottomContent.Clear();
            SetCaption(null, null);
            left.gameObject.SetActive(true); right.gameObject.SetActive(true); bottom.gameObject.SetActive(false);
        }

        public void SetTitle(string main, string sub) { title.text = main; subtitle.text = sub; }

        // Extra top-bar action (used by AR for reposition and scale), placed before the standard buttons.
        public Button AddTopAction(string label, Sprite icon, UnityEngine.Events.UnityAction action, float width = 170)
        {
            var b = string.IsNullOrEmpty(label)
                ? UIFactory.IconButton(topRow, icon, action, 56)
                : UIFactory.Button(topRow, label, action, ButtonStyle.Secondary, icon, 56, 16);
            if (!string.IsNullOrEmpty(label)) b.Layout(width, 56, 0);
            b.transform.SetSiblingIndex(paletteButton.transform.GetSiblingIndex());
            return b;
        }

        public bool HomeVisible => home.gameObject.activeSelf;

        public RectTransform LeftPanel(string heading)
        {
            left.gameObject.SetActive(true);
            leftTitle.text = heading.ToUpperInvariant();
            leftContent.Clear();
            return leftContent;
        }

        public void HideLeft() => left.gameObject.SetActive(false);

        public RectTransform Info()
        {
            right.gameObject.SetActive(true);
            rightContent.Clear();
            rightScroll.verticalNormalizedPosition = 1f;
            return rightContent;
        }

        public void HideInfo() => right.gameObject.SetActive(false);

        public RectTransform BottomBar(float width)
        {
            bottom.gameObject.SetActive(true);
            bottom.sizeDelta = new Vector2(width, bottom.sizeDelta.y);
            desiredWidth[bottom] = width;
            bottomContent.Clear();
            return bottomContent;
        }

        public void HideBottom() => bottom.gameObject.SetActive(false);

        public void SetCaption(string heading, string text)
        {
            bool show = !string.IsNullOrEmpty(heading) || !string.IsNullOrEmpty(text);
            caption.gameObject.SetActive(show);
            if (!show) return;
            captionTitle.text = heading ?? "";
            captionText.text = text ?? "";
            float width = Mathf.Min(860f, safe.rect.width - (PanelWidth + InfoWidth + Gap * 4));
            width = Mathf.Max(width, 460f);
            // Measure at the final width; preferredHeight is stale until the next layout pass.
            var settings = captionText.GetGenerationSettings(new Vector2(width - 44f, 0f));
            float textHeight = captionText.cachedTextGeneratorForLayout.GetPreferredHeight(captionText.text, settings) /
                               captionText.pixelsPerUnit;
            caption.sizeDelta = new Vector2(width, textHeight + 34 + (string.IsNullOrEmpty(heading) ? 0 : 30));
            desiredWidth[caption] = width;
        }

        // Keeps floating cards between the side panels (their widths differ, so screen centre is off).
        readonly Dictionary<RectTransform, float> desiredWidth = new Dictionary<RectTransform, float>();

        void CentreInFreeArea(RectTransform card)
        {
            if (!card.gameObject.activeSelf) return;
            if (!desiredWidth.TryGetValue(card, out float want)) want = desiredWidth[card] = card.sizeDelta.x;
            float leftEdge = left.gameObject.activeSelf ? Gap + PanelWidth : 0f;
            float rightEdge = right.gameObject.activeSelf ? Gap + InfoWidth : 0f;
            float free = safe.rect.width - leftEdge - rightEdge - Gap * 2f;
            // Never squash below a usable width; on narrow (4:3) screens the bar may overlap a panel edge.
            float width = Mathf.Max(Mathf.Min(want, free), Mathf.Min(want, 640f));
            card.sizeDelta = new Vector2(width, card.sizeDelta.y);
            card.anchoredPosition = new Vector2((leftEdge - rightEdge) * .5f, card.anchoredPosition.y);
        }

        public void Toast(string message, float seconds = 2.6f)
        {
            toastText.text = message;
            toast.sizeDelta = new Vector2(Mathf.Clamp(toastText.preferredWidth + 60, 320, 900), 64);
            toast.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        void TogglePalette()
        {
            bool studio = !Session.Kit.StudioColors;
            Session.Kit.SetStudioColors(studio);
            Toast(studio ? "Studio colours" : "Original CAD colours");
        }

        // Centre of the 3D area not covered by panels, in normalised screen coordinates.
        public Vector2 FreeViewportCenter()
        {
            if (!hud.gameObject.activeInHierarchy)
                return home.gameObject.activeSelf ? new Vector2(.76f, .5f) : new Vector2(.5f, .5f);
            float l = 0, r = Screen.width, b = 0, t = Screen.height;
            var c = new Vector3[4];
            if (left.gameObject.activeSelf) { left.GetWorldCorners(c); l = c[2].x; }
            if (right.gameObject.activeSelf) { right.GetWorldCorners(c); r = c[0].x; }
            topBar.GetWorldCorners(c); t = c[0].y;
            if (bottom.gameObject.activeSelf) { bottom.GetWorldCorners(c); b = c[1].y; }
            return new Vector2((l + r) * .5f / Screen.width, (b + t) * .5f / Screen.height);
        }

        // ---------- home ----------

        void BuildHome()
        {
            home.Clear();
            if (IsAR)
            {
                var scrim = home.gameObject.GetComponent<Image>();
                if (scrim == null) scrim = home.gameObject.AddComponent<Image>();
                // Light dim only: the placed kit should stay visible behind the module cards.
                scrim.color = new Color(0, 0, 0, .12f);
                // Visual only: it must not swallow taps meant for the top bar or the AR view.
                scrim.raycastTarget = false;
            }
            var panel = UIFactory.Rect("Home column", home);
            panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(0, 1); panel.pivot = new Vector2(0, .5f);
            // AR: a compact card column on the left so the kit on the right stays in view.
            float columnWidth = IsAR ? 2 * 360 + 12 : 980;
            panel.offsetMin = new Vector2(IsAR ? Gap : 56, 40);
            panel.offsetMax = new Vector2((IsAR ? Gap : 56) + columnWidth, IsAR ? -(TopBarHeight + Gap * 2) : -40);
            panel.VStack(10, null);

            if (!IsAR)
            {
            var brand = UIFactory.Rect("Brand", panel); brand.HStack(10); brand.Layout(-1, 34, 1);
            UIFactory.Icon(brand, Icons.Cube, Theme.Accent, 26);
            UIFactory.Label(brand, "AMT  ·  ADVANCED MANUFACTURING TRAINING", 15, Theme.Accent, Weight.SemiBold, TextAnchor.MiddleLeft).Layout(-1, -1, 1);
            var h = UIFactory.Label(panel, "Mechanism Kit", 64, Theme.Text, Weight.Bold, TextAnchor.LowerLeft);
            h.Layout(-1, 80, 1); AddShadow(h);
            var lead = UIFactory.Label(panel,
                "Explore the real kit — a servo-indexed rotary table with RFID, distance-sensing, vision and solenoid stations. " +
                "Every part you see is from the supplied CAD model.", 21, Theme.TextMuted);
            lead.Layout(-1, 66, 1); AddShadow(lead);
            var stats = UIFactory.Rect("Stats", panel); stats.HStack(10); stats.Layout(-1, 40, 1);
            UIFactory.Chip(stats, Session.Kit.Parts.Count + " parts", Theme.Info, 15);
            UIFactory.Chip(stats, "4 stations", Theme.Info, 15);
            UIFactory.Chip(stats, "Real CAD geometry", Theme.Success, 15);
            UIFactory.Label(panel, "", 10, Theme.Text).Layout(-1, 8, 1);
            }

            var grid = UIFactory.Rect("Modules", panel);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            var cell = IsAR ? new Vector2(360, 96) : new Vector2(476, 112);
            g.cellSize = cell; g.spacing = IsAR ? new Vector2(12, 10) : new Vector2(16, 14);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 2;
            grid.Layout(-1, 4 * cell.y + 3 * g.spacing.y, 1);
            ModuleCard(grid, IsAR ? Icons.Cube : Icons.AR, IsAR ? "Back to 3D viewer" : "Explore in AR",
                IsAR ? "Return to the interactive 3D viewer." : "Place the kit at real size on a table or floor.",
                () => AppNavigation.SwitchScene(!IsAR, null), true);
            ModuleCard(grid, Icons.Components, "Explore components", "Select any of the 37 parts to learn what it does.", () => Open("components"));
            ModuleCard(grid, Icons.Tools, "Explore tools", "Allen keys and screwdrivers, and where they are used.", () => Open("tools"));
            ModuleCard(grid, Icons.Motion, "How it works", "Watch a product travel through the four stations.", () => Open("motion"));
            ModuleCard(grid, Icons.Explode, "Exploded view", "Separate the assembly level by level.", () => Open("exploded"));
            ModuleCard(grid, Icons.Assemble, "Assembly guide", "Build the kit step by step, with tools.", () => Open("assembly"));
            ModuleCard(grid, Icons.Disassemble, "Disassembly guide", "Take it apart in the reverse order.", () => Open("disassembly"));
            if (!IsAR)
                UIFactory.Label(panel, "Drag to rotate  ·  pinch or scroll to zoom  ·  two fingers or right-drag to pan  ·  double-tap a part to focus",
                    15, Theme.TextFaint).Layout(-1, 30, 1);
        }

        void ModuleCard(Transform parent, Sprite icon, string heading, string body, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var bg = UIFactory.Box(parent, primary ? Theme.Accent : Theme.Surface, 18, heading);
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.HStack(16, new RectOffset(20, 16, 14, 14));
            var iconBox = UIFactory.Box(rt, primary ? new Color(0, 0, 0, .14f) : Theme.AccentSoft, 14, "Icon box");
            iconBox.Layout(64, 64, 0, 0);
            var glyph = UIFactory.Box(iconBox.transform, primary ? Theme.TextOnAccent : Theme.Accent, 0, "Glyph");
            glyph.sprite = icon; glyph.preserveAspect = true;
            glyph.rectTransform.Stretch(14, 14, 14, 14);
            var text = UIFactory.Rect("Text", rt); text.VStack(2, new RectOffset(0, 0, 4, 4)); text.Layout(-1, -1, 1);
            UIFactory.Label(text, heading, IsAR ? 18 : 21, primary ? Theme.TextOnAccent : Theme.Text, Weight.SemiBold).Layout(-1, IsAR ? 26 : 30, 1);
            UIFactory.Label(text, body, IsAR ? 13 : 15, primary ? new Color(.1f, .08f, .03f, .85f) : Theme.TextMuted).Layout(-1, IsAR ? 36 : 44, 1);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cb = ColorBlock.defaultColorBlock;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f); cb.pressedColor = new Color(.8f, .8f, .8f);
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(action);
        }
    }
}
