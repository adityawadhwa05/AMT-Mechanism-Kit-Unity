using System;
using System.Collections.Generic;
using AMT.AR.Core;
using AMT.AR.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // One training module (components, tools, how it works, ...). The shell owns the regions;
    // a screen fills them on Enter and releases anything it started on Exit.
    public abstract class ModuleScreen
    {
        protected TrainingShell Shell { get; private set; }
        protected TrainingSession Session { get; private set; }
        protected KitModel Kit => Session.Kit;
        protected KitDatabase Db => Session.Db;
        public string Key { get; private set; }

        public void Attach(TrainingShell shell, string key) { Shell = shell; Session = shell.Session; Key = key; }

        public abstract void Enter();
        public virtual void Exit() { }
        public virtual void Tick() { }
        public virtual void OnPartTapped(KitPart part) { }
        public virtual void OnEmptyTapped() { }

        protected InfoCard NewInfo() => new InfoCard(Shell.Info(), this);

        // Row in a list panel: colour dot, title and a small trailing note.
        protected static Button ListItem(Transform parent, string title, string note, Color dot, UnityAction onClick,
            Sprite leading = null)
        {
            var b = UIFactory.Button(parent, null, onClick, ButtonStyle.ListItem, null, 50);
            var row = (RectTransform)b.transform;
            if (leading != null) UIFactory.Icon(row, leading, dot, 22);
            else
            {
                var d = UIFactory.Box(row, dot, 0, "Dot"); d.sprite = UIFactory.Circle; d.Layout(10, 10, 0, 0);
            }
            UIFactory.Label(row, title, 17, Theme.Text, Weight.Regular, TextAnchor.MiddleLeft).Layout(-1, -1, 1);
            if (!string.IsNullOrEmpty(note))
            {
                var n = UIFactory.Label(row, note, 13, Theme.TextFaint, Weight.SemiBold, TextAnchor.MiddleRight);
                n.horizontalOverflow = HorizontalWrapMode.Overflow;
                n.Layout(n.preferredWidth + 4, -1, 0);
            }
            return b;
        }

        protected static void SetSelected(Button b, bool selected)
        {
            if (b == null) return;
            ((Image)b.targetGraphic).color = selected ? Theme.AccentSoft : new Color(1, 1, 1, .03f);
            var texts = b.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0) texts[0].color = selected ? Theme.Accent : Theme.Text;
        }

        protected void RenderComponent(InfoCard card, KitPart part, bool showActions, Action onIsolate = null,
            bool isolated = false, Action onHide = null)
        {
            var info = part.Info;
            card.Kicker(info.category + (info.station > 0 ? "  ·  Station " + info.station : ""), Theme.CategoryColor(info.category));
            card.Title(info.name);
            card.Verification(info.verification);
            card.Lead(info.summary);
            if (showActions)
            {
                var actions = new List<(string, Sprite, UnityAction, ButtonStyle)>
                {
                    ("Focus", Icons.Focus, () => Session.FocusParts(new[] { part }), ButtonStyle.Secondary),
                };
                if (onIsolate != null) actions.Add((isolated ? "Show context" : "Isolate", Icons.Isolate, () => onIsolate(), isolated ? ButtonStyle.Primary : ButtonStyle.Secondary));
                if (onHide != null) actions.Add(("Hide", Icons.EyeOff, () => onHide(), ButtonStyle.Secondary));
                card.Actions(actions.ToArray());
            }
            card.Section("Function", info.function);
            card.Section("How it connects", info.interactions);
            card.Section("Principle", info.principle);
            card.Section("Where it is", info.location);
            var tools = new List<(string, Color, UnityAction)>();
            foreach (var id in info.tools)
            {
                var t = Db.Tool(id);
                if (t != null) { var key = id; tools.Add((t.name, Theme.Accent, () => { ToolsScreen.Pending = key; Shell.Open("tools"); })); }
            }
            if (tools.Count > 0) card.Links("Tools", tools);
            var related = new List<(string, Color, UnityAction)>();
            foreach (var id in info.related)
            {
                var r = Kit.Get(id);
                if (r != null) { var rp = r; related.Add((r.DisplayName, Theme.Info, () => OnRelatedChosen(rp))); }
            }
            if (related.Count > 0) card.Links("Works with", related);
        }

        protected virtual void OnRelatedChosen(KitPart part)
        {
            ComponentsScreen.Pending = part.Id;
            Shell.Open("components");
        }
    }

    // Fluent builder for the details card on the right.
    public sealed class InfoCard
    {
        readonly RectTransform root;
        public InfoCard(RectTransform root, ModuleScreen owner) { this.root = root; }
        public RectTransform Root => root;

        public void Kicker(string text, Color color)
        {
            UIFactory.Label(root, text.ToUpperInvariant(), 14, color, Weight.SemiBold).Layout(-1, 22, 1);
        }

        public void Title(string text)
        {
            UIFactory.Label(root, text, 30, Theme.Text, Weight.Bold);
        }

        public void Lead(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            UIFactory.Label(root, text, 19, Theme.Text).Layout(-1, -1, 1);
        }

        public void Paragraph(string text, Color? color = null, int size = 17)
        {
            if (string.IsNullOrEmpty(text)) return;
            UIFactory.Label(root, text, size, color ?? Theme.TextMuted).Layout(-1, -1, 1);
        }

        public void Section(string heading, string body)
        {
            if (string.IsNullOrEmpty(body)) return;
            UIFactory.Section(root, heading);
            UIFactory.Label(root, body, 17, Theme.TextMuted).Layout(-1, -1, 1);
        }

        public void Bullets(string heading, IEnumerable<string> items, Sprite icon, Color color)
        {
            UIFactory.Section(root, heading);
            foreach (var item in items)
            {
                var row = UIFactory.Rect("Bullet", root);
                var h = row.HStack(10, null, TextAnchor.UpperLeft);
                h.childForceExpandWidth = false;
                var i = UIFactory.Icon(row, icon, color, 20);
                var t = UIFactory.Label(row, item, 17, Theme.TextMuted);
                t.Layout(-1, -1, 1);
            }
        }

        public void Verification(string verification)
        {
            bool published = verification == "published";
            Notice(KitDatabase.VerificationLabel(verification), published ? Theme.Success : Theme.Warning,
                published ? Icons.Check : Icons.Info, true);
        }

        public void Notice(string text, Color color, Sprite icon, bool compact = false)
        {
            var bg = UIFactory.Box(root, color.WithAlpha(.12f), 12, "Notice");
            var row = bg.rectTransform;
            var h = row.HStack(10, new RectOffset(14, 14, compact ? 8 : 12, compact ? 8 : 12), TextAnchor.MiddleLeft);
            h.childForceExpandWidth = false;
            UIFactory.Icon(row, icon, color, compact ? 18 : 22);
            var t = UIFactory.Label(row, text, compact ? 15 : 16, color, Weight.SemiBold, TextAnchor.MiddleLeft);
            t.Layout(-1, -1, 1);
            row.FitHeight();
        }

        public void Links(string heading, List<(string label, Color color, UnityAction action)> links)
        {
            UIFactory.Section(root, heading);
            var flow = UIFactory.Rect("Links", root);
            var grid = flow.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(205, 44); grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            flow.Layout(-1, Mathf.Ceil(links.Count / 2f) * 52, 1);
            foreach (var l in links)
            {
                var b = UIFactory.Button(flow, l.label, l.action, ButtonStyle.Secondary, null, 44, 15);
                var t = b.GetComponentInChildren<Text>();
                t.color = l.color; t.resizeTextForBestFit = true; t.resizeTextMinSize = 11; t.resizeTextMaxSize = 15;
            }
        }

        public void Actions(params (string label, Sprite icon, UnityAction action, ButtonStyle style)[] actions)
        {
            var row = UIFactory.Rect("Actions", root);
            var h = row.HStack(8, new RectOffset(0, 0, 4, 4), TextAnchor.MiddleLeft);
            h.childForceExpandWidth = true; h.childControlWidth = true;
            row.Layout(-1, 56, 1);
            foreach (var a in actions)
            {
                var b = UIFactory.Button(row, a.label, a.action, a.style, a.icon, 48, 16);
                b.Layout(-1, 48, 1);
            }
        }

        public void Spacer(float height) => UIFactory.Rect("Spacer", root).Layout(-1, height, 1);
    }
}
