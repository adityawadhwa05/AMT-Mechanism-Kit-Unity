using System;
using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    public enum ButtonStyle { Primary, Secondary, Ghost, Danger, ListItem }
    public enum Weight { Regular, SemiBold, Bold }

    // Builds uGUI widgets from code with the shared theme. Rounded corners are 9-sliced sprites
    // generated at startup, so the UI needs no imported textures.
    public static class UIFactory
    {
        static Font regular, semiBold, bold;
        static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> outlines = new Dictionary<int, Sprite>();
        static Sprite circle, softShadow;

        public static Font Font(Weight w)
        {
            if (regular == null)
            {
                regular = Resources.Load<Font>("Fonts/Inter-Regular");
                semiBold = Resources.Load<Font>("Fonts/Inter-SemiBold");
                bold = Resources.Load<Font>("Fonts/Inter-Bold");
                var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (regular == null) regular = fallback;
                if (semiBold == null) semiBold = regular;
                if (bold == null) bold = semiBold;
            }
            return w == Weight.Bold ? bold : w == Weight.SemiBold ? semiBold : regular;
        }

        // ---------- sprites ----------

        public static Sprite Rounded(int radius)
        {
            if (rounded.TryGetValue(radius, out var s) && s != null) return s;
            s = MakeRounded(radius, 0);
            rounded[radius] = s;
            return s;
        }

        public static Sprite Outline(int radius)
        {
            if (outlines.TryGetValue(radius, out var s) && s != null) return s;
            s = MakeRounded(radius, 2);
            outlines[radius] = s;
            return s;
        }

        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(32, 0, 64);

        // Large soft-edged rectangle used as a drop shadow behind floating cards.
        public static Sprite SoftShadow
        {
            get
            {
                if (softShadow != null) return softShadow;
                const int size = 64, edge = 24;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(edge - x, x - (size - 1 - edge)));
                    float dy = Mathf.Max(0, Mathf.Max(edge - y, y - (size - 1 - edge)));
                    float a = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / edge);
                    px[y * size + x] = new Color32(0, 0, 0, (byte)(a * a * 255));
                }
                tex.SetPixels32(px); tex.Apply(false, true);
                softShadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
                return softShadow;
            }
        }

        static Sprite MakeRounded(int radius, int stroke, int sizeOverride = 0)
        {
            int size = sizeOverride > 0 ? sizeOverride : radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = sizeOverride > 0 ? size * .5f : radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded rectangle that fills the texture.
                float cx = Mathf.Clamp(x + .5f, r, size - r), cy = Mathf.Clamp(y + .5f, r, size - r);
                float d = Mathf.Sqrt((x + .5f - cx) * (x + .5f - cx) + (y + .5f - cy) * (y + .5f - cy)) - r;
                float a = Mathf.Clamp01(.5f - d);
                if (stroke > 0) a *= Mathf.Clamp01(d + stroke + .5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            float b = sizeOverride > 0 ? 0 : radius + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // ---------- layout helpers ----------

        public static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        public static LayoutElement Layout(this Component c, float preferredWidth = -1, float preferredHeight = -1,
            float flexibleWidth = -1, float flexibleHeight = -1, float minHeight = -1)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth; le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth; le.flexibleHeight = flexibleHeight;
            if (minHeight >= 0) le.minHeight = minHeight;
            return le;
        }

        public static VerticalLayoutGroup VStack(this RectTransform rt, float spacing = 8, RectOffset padding = null,
            bool expandWidth = true)
        {
            if (!rt.TryGetComponent<VerticalLayoutGroup>(out var v)) v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(this RectTransform rt, float spacing = 8, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (!rt.TryGetComponent<HorizontalLayoutGroup>(out var h)) h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static ContentSizeFitter FitHeight(this RectTransform rt)
        {
            if (!rt.TryGetComponent<ContentSizeFitter>(out var f)) f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return f;
        }

        public static void Clear(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        // ---------- widgets ----------

        public static Image Box(Transform parent, Color color, int radius = 14, string name = "Box")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (radius > 0) { img.sprite = Rounded(radius); img.type = Image.Type.Sliced; }
            return img;
        }

        // Floating card: soft shadow, rounded fill and a hairline border.
        public static RectTransform Card(Transform parent, string name, Color fill, int radius = 18)
        {
            var root = Rect(name, parent);
            var shadow = Box(root, new Color(0, 0, 0, .55f), 0, "Shadow");
            shadow.sprite = SoftShadow; shadow.type = Image.Type.Sliced;
            shadow.rectTransform.Stretch(-18, -14, -18, -26);
            var body = Box(root, fill, radius, "Fill");
            body.rectTransform.Stretch();
            body.raycastTarget = true; // blocks clicks reaching the 3D view
            var border = Box(root, Theme.Stroke, 0, "Border");
            border.sprite = Outline(radius); border.type = Image.Type.Sliced;
            border.rectTransform.Stretch();
            return root;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Weight weight = Weight.Regular,
            TextAnchor anchor = TextAnchor.UpperLeft, string name = "Label")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font(weight); t.fontSize = size; t.color = color; t.text = text;
            t.alignment = anchor; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.12f;
            return t;
        }

        public static Image Icon(Transform parent, Sprite sprite, Color color, float size)
        {
            var img = Box(parent, color, 0, "Icon");
            img.sprite = sprite; img.preserveAspect = true;
            img.Layout(size, size, 0, 0);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        static ColorBlock Tint()
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(.78f, .78f, .78f, 1f);
            cb.disabledColor = new Color(1, 1, 1, .35f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = .08f;
            return cb;
        }

        public static void StyleColors(ButtonStyle style, out Color fill, out Color ink)
        {
            switch (style)
            {
                case ButtonStyle.Primary: fill = Theme.Accent; ink = Theme.TextOnAccent; break;
                case ButtonStyle.Danger: fill = Theme.DangerSoft; ink = Theme.Danger; break;
                case ButtonStyle.Ghost: fill = new Color(1, 1, 1, .001f); ink = Theme.Text; break;
                case ButtonStyle.ListItem: fill = new Color(1, 1, 1, .03f); ink = Theme.Text; break;
                default: fill = Theme.SurfaceHover; ink = Theme.Text; break;
            }
        }

        public static Button Button(Transform parent, string label, UnityAction onClick,
            ButtonStyle style = ButtonStyle.Secondary, Sprite icon = null, float height = 48, int fontSize = 18)
        {
            StyleColors(style, out var fill, out var ink);
            var bg = Box(parent, fill, 12, "Button " + label);
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.HStack(10, new RectOffset(16, 16, 0, 0), style == ButtonStyle.ListItem ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            bg.Layout(-1, height, -1, -1, height);
            if (icon != null) Icon(rt, icon, ink, Mathf.Round(height * .42f));
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(rt, label, fontSize, ink, style == ButtonStyle.ListItem ? Weight.Regular : Weight.SemiBold,
                    style == ButtonStyle.ListItem ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (style == ButtonStyle.ListItem) t.Layout(-1, -1, 1);
            }
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg; b.colors = Tint();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static Button IconButton(Transform parent, Sprite icon, UnityAction onClick, float size = 48,
            ButtonStyle style = ButtonStyle.Secondary)
        {
            StyleColors(style, out var fill, out var ink);
            var bg = Box(parent, fill, Mathf.RoundToInt(size * .3f), "Icon button");
            bg.raycastTarget = true;
            bg.Layout(size, size, 0, 0);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var img = Box(bg.transform, ink, 0, "Glyph");
            img.sprite = icon; img.preserveAspect = true;
            img.rectTransform.Stretch(size * .26f, size * .26f, size * .26f, size * .26f);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg; b.colors = Tint();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static void SetButtonStyle(Button b, ButtonStyle style)
        {
            StyleColors(style, out var fill, out var ink);
            if (b.targetGraphic is Image bg) bg.color = fill;
            foreach (var t in b.GetComponentsInChildren<Text>(true)) t.color = ink;
            foreach (var g in b.GetComponentsInChildren<Image>(true))
                if (g != b.targetGraphic) g.color = ink;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = label;
        }

        public static void SetIcon(Button b, Sprite icon)
        {
            foreach (var g in b.GetComponentsInChildren<Image>(true))
                if (g != b.targetGraphic) { g.sprite = icon; return; }
        }

        public static RectTransform Chip(Transform parent, string text, Color color, int size = 14)
        {
            var bg = Box(parent, color.WithAlpha(.16f), 11, "Chip");
            var rt = bg.rectTransform;
            rt.HStack(6, new RectOffset(10, 10, 4, 4), TextAnchor.MiddleCenter);
            var label = Label(rt, text, size, color, Weight.SemiBold, TextAnchor.MiddleCenter);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        public static Image Divider(Transform parent)
        {
            var d = Box(parent, Theme.Stroke, 0, "Divider");
            d.Layout(-1, 1, 1);
            return d;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, UnityAction<float> onChange,
            float height = 36)
        {
            var root = Rect("Slider", parent);
            root.Layout(-1, height, 1);
            var track = Box(root, Theme.Stroke, 4, "Track");
            track.rectTransform.anchorMin = new Vector2(0, .5f); track.rectTransform.anchorMax = new Vector2(1, .5f);
            track.rectTransform.sizeDelta = new Vector2(0, 6);
            var fillArea = Rect("Fill area", root);
            fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.sizeDelta = new Vector2(-20, 6);
            var fill = Box(fillArea, Theme.Accent, 4, "Fill");
            fill.rectTransform.sizeDelta = new Vector2(10, 0);
            var handleArea = Rect("Handle area", root).Stretch(10, 0, 10, 0);
            var handle = Box(handleArea, Color.white, 0, "Handle");
            handle.sprite = Circle; handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(24, 24);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.navigation = new Navigation { mode = Navigation.Mode.None };
            // A transparent hit area over the whole row makes the slider easy to grab on phones.
            var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            if (onChange != null) s.onValueChanged.AddListener(onChange);
            return s;
        }

        // Vertical scrolling list; returns the content transform to add items to.
        public static RectTransform ScrollList(Transform parent, out ScrollRect scroll, float spacing = 6,
            RectOffset padding = null)
        {
            var root = Rect("Scroll", parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30; scroll.inertia = true; scroll.decelerationRate = .12f;
            var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var viewport = Rect("Viewport", root).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            content.VStack(spacing, padding ?? new RectOffset(0, 0, 4, 12));
            content.FitHeight();
            scroll.viewport = viewport; scroll.content = content;
            return content;
        }

        public static (RectTransform root, Image fill) ProgressBar(Transform parent, Color color, float height = 6)
        {
            var track = Box(parent, Theme.Stroke, 3, "Progress");
            track.Layout(-1, height, 1);
            var fill = Box(track.transform, color, 3, "Fill");
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return (track.rectTransform, fill);
        }

        public static void SetProgress(Image fill, float value)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        }

        public static Text Section(Transform parent, string title)
        {
            var t = Label(parent, title.ToUpperInvariant(), 13, Theme.TextFaint, Weight.SemiBold, TextAnchor.LowerLeft);
            t.Layout(-1, 30, 1);
            return t;
        }
    }
}
