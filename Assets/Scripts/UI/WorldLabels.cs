using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // Name tags that follow parts on screen. Larger parts win when labels would overlap.
    public sealed class WorldLabels : MonoBehaviour
    {
        sealed class Tag { public KitPart Part; public RectTransform Rect; public Text Text; public Image Dot; }

        TrainingSession session;
        RectTransform root;
        readonly List<Tag> tags = new List<Tag>();
        readonly List<Rect> placed = new List<Rect>();
        public KitPart Emphasis { get; set; }

        public static WorldLabels Create(RectTransform parent, TrainingSession session)
        {
            var rt = UIFactory.Rect("World labels", parent).Stretch();
            var w = rt.gameObject.AddComponent<WorldLabels>();
            w.session = session; w.root = rt;
            return w;
        }

        public void Show(IEnumerable<KitPart> parts)
        {
            Clear();
            var list = new List<KitPart>(parts);
            list.Sort((a, b) => b.KitSize.magnitude.CompareTo(a.KitSize.magnitude));
            foreach (var p in list)
            {
                var bg = UIFactory.Box(root, Theme.SurfaceRaised.WithAlpha(.88f), 12, "Tag " + p.Id);
                var rt = bg.rectTransform;
                rt.pivot = new Vector2(0, .5f);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                var dot = UIFactory.Box(rt, Theme.CategoryColor(p.Info != null ? p.Info.category : ""), 0, "Dot");
                dot.sprite = UIFactory.Circle;
                dot.rectTransform.Place(new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(10, 10));
                var text = UIFactory.Label(rt, p.DisplayName, 15, Theme.Text, Weight.SemiBold, TextAnchor.MiddleLeft);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.Stretch(26, 0, 10, 0);
                rt.sizeDelta = new Vector2(text.preferredWidth + 38, 28);
                tags.Add(new Tag { Part = p, Rect = rt, Text = text, Dot = dot });
            }
        }

        public void Clear()
        {
            foreach (var t in tags) if (t.Rect != null) Destroy(t.Rect.gameObject);
            tags.Clear();
        }

        void LateUpdate()
        {
            if (tags.Count == 0 || session.View == null || session.View.Camera == null) return;
            var cam = session.View.Camera;
            var canvas = root.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            placed.Clear();
            var origin = SafeOrigin(scale);
            // Emphasised label is placed first so it is never hidden.
            for (int pass = 0; pass < 2; pass++)
            foreach (var t in tags)
            {
                bool emphasis = t.Part == Emphasis;
                if ((pass == 0) != emphasis) continue;
                bool visible = t.Part.Visual != PartVisual.Hidden && t.Part.Visual != PartVisual.Ghost;
                var screen = cam.WorldToScreenPoint(t.Part.WorldBounds().center);
                if (!visible || screen.z <= 0) { t.Rect.gameObject.SetActive(false); continue; }
                var local = new Vector2(screen.x, screen.y) / scale - origin;
                var r = new Rect(local + new Vector2(0, -14), t.Rect.sizeDelta);
                bool overlaps = false;
                foreach (var o in placed) if (o.Overlaps(r)) { overlaps = true; break; }
                if (overlaps && !emphasis) { t.Rect.gameObject.SetActive(false); continue; }
                placed.Add(r);
                t.Rect.gameObject.SetActive(true);
                t.Rect.anchoredPosition = local;
                t.Text.color = emphasis ? Theme.Accent : Theme.Text;
            }
        }

        // Screen-space origin of the safe-area root, in canvas units.
        Vector2 SafeOrigin(float scale)
        {
            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            return new Vector2(corners[0].x, corners[0].y) / scale;
        }
    }
}
