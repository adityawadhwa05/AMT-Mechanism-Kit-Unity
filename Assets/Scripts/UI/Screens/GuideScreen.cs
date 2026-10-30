using System.Collections.Generic;
using AMT.AR.AssemblyGuide;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // Assembly / disassembly guide: numbered steps, animation transport, progress and step details.
    public sealed class GuideScreen : ModuleScreen
    {
        readonly bool disassembly;
        GuidePlayer guide;
        Button play, ghost;
        Image stepProgress, overall;
        Text counter;
        readonly List<Button> rows = new List<Button>();
        int renderedStep = -1;

        public GuideScreen(bool disassembly) { this.disassembly = disassembly; }

        public override void Enter()
        {
            guide = Session.Guide;
            guide.Begin(disassembly);
            Shell.SetTitle(disassembly ? "Disassembly guide" : "Assembly guide",
                disassembly ? "Take the kit apart, top down" : "Build the kit from the base up");
            var panel = Shell.LeftPanel("Steps");
            var list = UIFactory.ScrollList(panel, out var scroll, 4);
            scroll.GetComponent<RectTransform>().Layout(-1, -1, 1, 1);
            for (int i = 0; i < guide.StepCount; i++)
            {
                int index = i;
                var data = Db.Guide.steps[disassembly ? guide.StepCount - 1 - i : i];
                rows.Add(ListItem(list, (i + 1).ToString("00") + "   " + data.title, "", Theme.Accent, () => guide.GoTo(index)));
            }

            var bar = Shell.BottomBar(940);
            UIFactory.IconButton(bar, Icons.Restart, () => guide.Restart(), 56);
            UIFactory.IconButton(bar, Icons.StepBack, () => guide.Previous(), 56);
            play = UIFactory.IconButton(bar, Icons.Pause, () => guide.TogglePlay(), 64, ButtonStyle.Primary);
            UIFactory.IconButton(bar, Icons.StepForward, () => guide.Next(), 56);
            var mid = UIFactory.Rect("Progress", bar); mid.VStack(6, new RectOffset(8, 8, 10, 10)); mid.Layout(-1, 72, 1);
            counter = UIFactory.Label(mid, "", 14, Theme.TextMuted, Weight.SemiBold);
            counter.Layout(-1, 20, 1);
            overall = UIFactory.ProgressBar(mid, Theme.Accent, 6).fill;
            stepProgress = UIFactory.ProgressBar(mid, Theme.Info, 3).fill;
            ghost = UIFactory.Button(bar, "Ghosts", () => guide.SetGhostRemaining(!guide.GhostRemaining), ButtonStyle.Primary, Icons.Layers, 52, 15);
            ghost.Layout(140, 52, 0);

            guide.Changed += Refresh;
            Refresh();
        }

        void Refresh()
        {
            if (!guide.Active) return;
            bool finished = !guide.Playing && guide.StepProgress >= 1f;
            UIFactory.SetIcon(play, guide.Playing ? Icons.Pause : finished ? Icons.Restart : Icons.Play);
            UIFactory.SetButtonStyle(ghost, guide.GhostRemaining ? ButtonStyle.Primary : ButtonStyle.Secondary);
            for (int i = 0; i < rows.Count; i++) SetSelected(rows[i], i == guide.Step);
            counter.text = "STEP " + (guide.Step + 1) + " OF " + guide.StepCount;
            UIFactory.SetProgress(overall, (guide.Step + 1f) / guide.StepCount);
            var step = guide.Current;
            Shell.SetCaption("Step " + (guide.Step + 1) + " · " + step.title, guide.Instruction);
            if (renderedStep != guide.Step) { renderedStep = guide.Step; RenderInfo(); }
            if (finished && guide.Step == guide.StepCount - 1)
                Shell.Toast(disassembly ? "Disassembly complete" : "Assembly complete — the kit is ready");
        }

        void RenderInfo()
        {
            var step = guide.Current;
            var card = NewInfo();
            card.Kicker((disassembly ? "Disassembly" : "Assembly") + "  ·  Step " + (guide.Step + 1) + " of " + guide.StepCount, Theme.Accent);
            card.Title(step.title);
            card.Lead(guide.Instruction);
            card.Section("Why", step.explanation);
            var tool = Db.Tool(step.tool);
            if (tool != null)
            {
                var key = step.tool;
                card.Links("Tool", new List<(string, Color, UnityEngine.Events.UnityAction)>
                    { (tool.name, Theme.Accent, () => { ToolsScreen.Pending = key; Shell.Open("tools"); }) });
                card.Bullets("Before you start", new[] { tool.safety.Length > 0 ? tool.safety[0] : "Isolate power before working on the kit." }, Icons.Shield, Theme.Warning);
            }
            var parts = new List<(string, Color, UnityEngine.Events.UnityAction)>();
            foreach (var id in step.parts)
            {
                var p = Kit.Get(id);
                if (p != null) { var part = p; parts.Add((p.DisplayName, Theme.Info, () => Session.FocusParts(new[] { part }))); }
            }
            foreach (var id in step.fasteners)
            {
                var p = Kit.Get(id);
                if (p != null) { var part = p; parts.Add((p.DisplayName, Theme.Info, () => Session.FocusParts(new[] { part }))); }
            }
            if (parts.Count > 0) card.Links("Parts in this step", parts);
            card.Notice(Db.Guide.verified ? "Verified procedure" :
                "Authored order based on the real part layout — not the manufacturer's procedure. Fastener sizes and torques are not documented.",
                Db.Guide.verified ? Theme.Success : Theme.Warning, Icons.Info);
        }

        public override void Tick()
        {
            if (guide != null && guide.Active && stepProgress != null)
            {
                UIFactory.SetProgress(stepProgress, guide.StepProgress);
            }
        }

        public override void OnPartTapped(KitPart part)
        {
            Shell.Toast(part.DisplayName + " — " + part.Info.summary, 3f);
        }

        public override void Exit()
        {
            if (guide != null) guide.Changed -= Refresh;
            guide?.Stop();
            Kit.ApplyAll(PartVisual.Normal);
        }
    }
}
