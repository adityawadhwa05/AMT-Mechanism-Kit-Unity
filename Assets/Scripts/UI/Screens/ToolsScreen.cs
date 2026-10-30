using System.Collections.Generic;
using AMT.AR.Core;
using AMT.AR.Data;
using UnityEngine;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // Explore tools: purpose, correct use, reason and safety, with the parts each is used on
    // highlighted in the model and a demonstration on the real screw positions.
    public sealed class ToolsScreen : ModuleScreen
    {
        public static string Pending;
        ToolInfo current;
        readonly Dictionary<ToolInfo, Button> rows = new Dictionary<ToolInfo, Button>();

        public override void Enter()
        {
            Session.StopAll();
            Shell.SetTitle("Explore tools", "Parts each tool is used on are highlighted on the model");
            var panel = Shell.LeftPanel("Tools");
            foreach (var t in Db.Tools.tools)
            {
                var tool = t;
                var card = UIFactory.Button(panel, null, () => Choose(tool), ButtonStyle.ListItem, null, 92);
                var row = (RectTransform)card.transform;
                var iconBox = UIFactory.Box(row, Theme.AccentSoft, 12, "Icon"); iconBox.Layout(56, 56, 0, 0);
                var glyph = UIFactory.Box(iconBox.transform, Theme.Accent, 0, "Glyph"); glyph.sprite = Icons.Tools; glyph.preserveAspect = true;
                glyph.rectTransform.Stretch(12, 12, 12, 12);
                var text = UIFactory.Rect("Text", row); text.VStack(2, new RectOffset(0, 0, 10, 10)); text.Layout(-1, -1, 1);
                UIFactory.Label(text, t.name, 18, Theme.Text, Weight.SemiBold).Layout(-1, 26, 1);
                UIFactory.Label(text, t.purpose, 14, Theme.TextMuted).Layout(-1, 38, 1);
                rows[t] = card;
            }
            UIFactory.Rect("Spacer", panel).Layout(-1, -1, 1, 1);
            UIFactory.Label(panel, "Tool sizes are not published. Pairings come from the screw heads in the CAD model.", 14, Theme.TextFaint)
                .Layout(-1, 44, 1);
            var pending = Pending; Pending = null;
            var first = pending != null ? Db.Tool(pending) : (Db.Tools.tools.Length > 0 ? Db.Tools.tools[0] : null);
            if (first != null) Choose(first);
        }

        void Choose(ToolInfo tool)
        {
            current = tool;
            foreach (var pair in rows)
            {
                ((Image)pair.Value.targetGraphic).color = pair.Key == tool ? Theme.AccentSoft : new Color(1, 1, 1, .03f);
            }
            Session.ToolShowcase.Show(tool);
            Session.View?.ResetView();
            RenderInfo();
            var linked = new List<KitPart>();
            foreach (var id in tool.components)
            {
                var p = Kit.Get(id);
                if (p != null && !p.IsFastener) linked.Add(p);
            }
            Shell.Labels.Show(linked);
        }

        void RenderInfo()
        {
            var card = NewInfo();
            card.Kicker("Tool", Theme.Accent);
            card.Title(current.name);
            card.Notice(current.verification == "published" ? "Listed on the official kit page" : "Type inferred from screw heads in the CAD model",
                current.verification == "published" ? Theme.Success : Theme.Warning,
                current.verification == "published" ? Icons.Check : Icons.Info, true);
            card.Lead(current.purpose);
            bool demo = Session.ToolShowcase.Demonstrating;
            card.Actions((demo ? "Stop demonstration" : "Demonstrate on the kit", demo ? Icons.Pause : Icons.Play, () =>
            {
                if (Session.ToolShowcase.Demonstrating) { Session.ToolShowcase.Show(current); Session.View?.ResetView(); }
                else Session.ToolShowcase.Demonstrate();
                RenderInfo();
            }, ButtonStyle.Primary));
            card.Section("How to use it", current.use);
            card.Section("Why this tool", current.reason);
            card.Bullets("Safety", current.safety, Icons.Shield, Theme.Warning);
            var links = new List<(string, Color, UnityEngine.Events.UnityAction)>();
            foreach (var id in current.components)
            {
                var p = Kit.Get(id);
                if (p == null) continue;
                var part = p;
                links.Add((p.DisplayName, Theme.Info, () => { ComponentsScreen.Pending = part.Id; Shell.Open("components"); }));
            }
            if (links.Count > 0) card.Links("Used on", links);
        }

        public override void OnPartTapped(KitPart part)
        {
            if (current == null) return;
            bool linked = System.Array.IndexOf(current.components, part.Id) >= 0;
            Shell.Toast(linked ? part.DisplayName + " — uses the " + current.name.ToLowerInvariant()
                               : part.DisplayName + " is not listed for this tool");
        }

        public override void Exit()
        {
            Session.ToolShowcase.Hide();
            Shell.Labels.Clear();
            Kit.ApplyAll(PartVisual.Normal);
        }
    }
}
