using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // Exploded view with an intensity slider, presets, part labels and selection/isolation.
    public sealed class ExplodedScreen : ModuleScreen
    {
        Slider slider;
        Text percent;
        KitPart selected;
        bool isolate, labels = true;
        Button labelsButton;
        readonly Dictionary<KitPart, Button> rows = new Dictionary<KitPart, Button>();
        bool labelsShown;

        public override void Enter()
        {
            Session.Exploded.Begin(.65f);
            Shell.SetTitle("Exploded view", "Each level lifts along its assembly direction");
            var panel = Shell.LeftPanel("Parts");
            var list = UIFactory.ScrollList(panel, out var scroll, 4);
            scroll.GetComponent<RectTransform>().Layout(-1, -1, 1, 1);
            var parts = new List<KitPart>(Kit.Parts);
            parts.Sort((a, b) => b.KitCenter.y.CompareTo(a.KitCenter.y));
            foreach (var p in parts)
            {
                if (p.IsFastener) continue;
                var part = p;
                rows[p] = ListItem(list, p.DisplayName, "", Theme.CategoryColor(p.Info.category), () => Select(part, true));
            }

            var bar = Shell.BottomBar(880);
            UIFactory.Button(bar, "Assembled", () => SetAmount(0), ButtonStyle.Secondary, Icons.Cube, 52, 16).Layout(170, 52, 0);
            var mid = UIFactory.Rect("Amount", bar); mid.VStack(2, new RectOffset(6, 6, 6, 6)); mid.Layout(-1, 72, 1);
            percent = UIFactory.Label(mid, "", 13, Theme.TextMuted, Weight.SemiBold, TextAnchor.MiddleCenter);
            percent.Layout(-1, 18, 1);
            slider = UIFactory.Slider(mid, 0, 1, Session.Exploded.Amount, v => SetAmount(v, false), 36);
            UIFactory.Button(bar, "Full", () => SetAmount(1), ButtonStyle.Secondary, Icons.Explode, 52, 16).Layout(120, 52, 0);
            labelsButton = UIFactory.IconButton(bar, Icons.List, ToggleLabels, 52, ButtonStyle.Primary);
            UpdatePercent();
            Select(null, false);
            FrameExploded();
        }

        // Fit the assembled kit plus the height parts rise to at full separation.
        void FrameExploded()
        {
            if (Session.View == null) return;
            var b = Kit.WorldBounds();
            float rise = 0f;
            foreach (var p in Kit.Parts) if (p.Info != null) rise = Mathf.Max(rise, p.Info.explodeUp);
            b.Encapsulate(b.max + Kit.Root.up * rise * Kit.Root.lossyScale.y * .8f);
            Session.View.Focus(b);
        }

        void SetAmount(float v, bool moveSlider = true)
        {
            Session.Exploded.SetAmount(v);
            if (moveSlider && slider != null) slider.SetValueWithoutNotify(v);
            UpdatePercent();
        }

        void UpdatePercent() { if (percent != null) percent.text = "SEPARATION  " + Mathf.RoundToInt(Session.Exploded.Amount * 100) + "%"; }

        void ToggleLabels()
        {
            labels = !labels;
            UIFactory.SetButtonStyle(labelsButton, labels ? ButtonStyle.Primary : ButtonStyle.Secondary);
            labelsShown = false;
            if (!labels) Shell.Labels.Clear();
        }

        void Select(KitPart part, bool focus)
        {
            selected = part;
            Session.Select(part);
            foreach (var pair in rows) SetSelected(pair.Value, pair.Key == part);
            Kit.Apply(p => part == null ? PartVisual.Normal : p == part ? PartVisual.Highlight : isolate ? PartVisual.Ghost : PartVisual.Normal);
            Shell.Labels.Emphasis = part;
            if (part != null && focus) Session.FocusParts(new[] { part });
            var card = NewInfo();
            if (part == null)
            {
                card.Kicker("Exploded view", Theme.Accent);
                card.Title("Separated level by level");
                card.Lead("Parts rise in their build order — base, frame, drive train, top plate, disc, then the stations — and move outward from the centre.");
                card.Section("Controls", "Drag the slider to change the separation. Tap a part to select it; double-tap to zoom to it. Assembled returns every part to its exact original position.");
                card.Notice("Directions are authored for clarity. They show where parts sit, not a verified removal path.", Theme.Warning, Icons.Info, true);
                return;
            }
            RenderComponent(card, part, true, () => { isolate = !isolate; Select(selected, false); }, isolate);
        }

        public override void Tick()
        {
            bool want = labels && Session.Exploded.Current > .12f;
            if (want && !labelsShown)
            {
                var list = new List<KitPart>();
                foreach (var p in Kit.Parts) if (!p.IsFastener && p.KitSize.magnitude > .04f) list.Add(p);
                Shell.Labels.Show(list);
                Shell.Labels.Emphasis = selected;
                labelsShown = true;
            }
            else if (!want && labelsShown) { Shell.Labels.Clear(); labelsShown = false; }
        }

        protected override void OnRelatedChosen(KitPart part) => Select(part, true);
        public override void OnPartTapped(KitPart part) => Select(part, false);
        public override void OnEmptyTapped() { if (selected != null) Select(null, false); }

        public override void Exit()
        {
            Shell.Labels.Clear();
            Session.Exploded.Stop();
            Session.Select(null);
            Kit.ApplyAll(PartVisual.Normal);
        }
    }
}
