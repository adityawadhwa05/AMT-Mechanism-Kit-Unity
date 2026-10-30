using System.Collections.Generic;
using AMT.AR.Core;
using AMT.AR.Data;
using UnityEngine;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // Explore components: list synchronised with 3D selection, details, focus/isolate/hide.
    public sealed class ComponentsScreen : ModuleScreen
    {
        public static string Pending;

        string filter;
        KitPart selected;
        bool isolate;
        readonly HashSet<KitPart> hidden = new HashSet<KitPart>();
        readonly Dictionary<KitPart, Button> rows = new Dictionary<KitPart, Button>();
        readonly Dictionary<string, Button> filters = new Dictionary<string, Button>();
        RectTransform list;

        public override void Enter()
        {
            Session.StopAll();
            Shell.SetTitle("Explore components", "Tap a part on the model or pick it from the list");
            var panel = Shell.LeftPanel("Components");
            var chips = UIFactory.Rect("Filters", panel);
            var grid = chips.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(122, 38); grid.spacing = new Vector2(6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            chips.Layout(-1, 3 * 38 + 2 * 6, 1);
            AddFilter(chips, null, "All");
            foreach (var c in KitDatabase.CategoryOrder) AddFilter(chips, c, c);
            list = UIFactory.ScrollList(panel, out var scroll, 4);
            scroll.GetComponent<RectTransform>().Layout(-1, -1, 1, 1);
            BuildList();
            var pending = Pending; Pending = null;
            Select(pending != null ? Kit.Get(pending) : null, pending != null);
        }

        void AddFilter(Transform parent, string category, string label)
        {
            var b = UIFactory.Button(parent, label, () => { filter = category; BuildList(); }, ButtonStyle.Secondary, null, 38, 14);
            filters[category ?? ""] = b;
        }

        void BuildList()
        {
            list.Clear(); rows.Clear();
            foreach (var pair in filters) UIFactory.SetButtonStyle(pair.Value, pair.Key == (filter ?? "") ? ButtonStyle.Primary : ButtonStyle.Secondary);
            var parts = new List<KitPart>(Kit.Parts);
            parts.Sort((a, b) =>
            {
                int ca = System.Array.IndexOf(KitDatabase.CategoryOrder, a.Info.category);
                int cb = System.Array.IndexOf(KitDatabase.CategoryOrder, b.Info.category);
                if (ca != cb) return ca.CompareTo(cb);
                if (a.Info.station != b.Info.station) return a.Info.station.CompareTo(b.Info.station);
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
            string lastCategory = null;
            foreach (var p in parts)
            {
                if (filter != null && p.Info.category != filter) continue;
                if (filter == null && p.Info.category != lastCategory)
                {
                    UIFactory.Section(list, p.Info.category);
                    lastCategory = p.Info.category;
                }
                var part = p;
                string note = p.Info.station > 0 ? "S" + p.Info.station : hidden.Contains(p) ? "hidden" : "";
                rows[p] = ListItem(list, p.DisplayName, note, Theme.CategoryColor(p.Info.category), () => Select(part, true));
            }
            foreach (var pair in rows) SetSelected(pair.Value, pair.Key == selected);
        }

        void Select(KitPart part, bool focus)
        {
            selected = part;
            if (part != null) hidden.Remove(part);
            Session.Select(part);
            foreach (var pair in rows) SetSelected(pair.Value, pair.Key == selected);
            ApplyVisuals();
            RenderInfo();
            if (part != null && focus) Session.FocusParts(new[] { part });
            Shell.Labels.Clear();
            if (part != null) { Shell.Labels.Show(new[] { part }); Shell.Labels.Emphasis = part; }
        }

        void ApplyVisuals()
        {
            var related = new HashSet<string>(selected != null ? selected.Info.related : new string[0]);
            Kit.Apply(p =>
            {
                if (hidden.Contains(p)) return PartVisual.Hidden;
                if (selected == null) return PartVisual.Normal;
                if (p == selected) return PartVisual.Highlight;
                if (isolate) return PartVisual.Ghost;
                return related.Contains(p.Id) ? PartVisual.Related : PartVisual.Normal;
            });
        }

        void RenderInfo()
        {
            var card = NewInfo();
            if (selected == null)
            {
                card.Kicker("Real CAD model", Theme.Accent);
                card.Title("37 parts, 4 stations");
                card.Lead("The supplied model was split into its separate CAD bodies, so every part here is the real geometry — not a stand-in.");
                card.Section("How to explore", "Tap a part on the model or choose it from the list. Double-tap to zoom to it. Highlighted in cyan: parts it works with.");
                card.Notice(KitDatabase.VerificationLabel("published") + " — the part's role is stated in the published kit description.", Theme.Success, Icons.Check, true);
                card.Notice(KitDatabase.VerificationLabel("geometry") + ". Recognised by its shape and position only.", Theme.Warning, Icons.Info, true);
                if (hidden.Count > 0)
                    card.Actions(("Show all " + hidden.Count + " hidden", Icons.Eye, () => { hidden.Clear(); BuildList(); ApplyVisuals(); RenderInfo(); }, ButtonStyle.Primary));
                return;
            }
            RenderComponent(card, selected, true,
                () => { isolate = !isolate; ApplyVisuals(); RenderInfo(); }, isolate,
                () =>
                {
                    hidden.Add(selected);
                    Shell.Toast(selected.DisplayName + " hidden");
                    var s = selected; Select(null, false); BuildList();
                });
        }

        protected override void OnRelatedChosen(KitPart part) => Select(part, true);

        public override void OnPartTapped(KitPart part) => Select(part, false);

        public override void OnEmptyTapped()
        {
            if (selected != null) Select(null, false);
        }

        public override void Exit()
        {
            Shell.Labels.Clear();
            Session.Select(null);
            Kit.ApplyAll(PartVisual.Normal);
        }
    }
}
