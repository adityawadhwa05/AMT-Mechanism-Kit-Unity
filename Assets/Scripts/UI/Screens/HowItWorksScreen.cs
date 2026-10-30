using System.Collections.Generic;
using AMT.AR.Animation;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AMT.AR.UI
{
    // How it works: plays the four-station cycle with captions, transport controls, speed,
    // a simulated pass/fail result and per-part focus.
    public sealed class HowItWorksScreen : ModuleScreen
    {
        MechanismCycle cycle;
        Button play;
        Image progress;
        Text speedLabel;
        readonly List<Button> stationRows = new List<Button>();
        readonly Dictionary<string, Button> focusButtons = new Dictionary<string, Button>();
        Button pass, fail;
        int lastStation = -1; MechanismCycle.Phase lastPhase;

        static readonly (string id, string label)[] MovingParts =
        {
            (null, "Whole mechanism"), ("index_disc", "Indexing disc"), ("servo_motor", "Servo motor"),
            ("solenoid_plunger", "Solenoid plunger")
        };

        public override void Enter()
        {
            cycle = Session.Cycle;
            cycle.Begin();
            Shell.SetTitle("How it works", "One product through the four stations");

            var panel = Shell.LeftPanel("Working cycle");
            for (int i = 0; i < 4; i++)
            {
                var info = Db.Station(i + 1);
                int index = i;
                var row = ListItem(panel, (i + 1) + "   " + (info != null ? info.name : "Station " + (i + 1)), "", Theme.Info, () => JumpTo(index));
                stationRows.Add(row);
            }
            UIFactory.Section(panel, "Simulated inspection result");
            var seg = UIFactory.Rect("Result", panel); seg.HStack(8); seg.Layout(-1, 46, 1);
            pass = UIFactory.Button(seg, "Pass", () => cycle.SetReject(false), ButtonStyle.Secondary, Icons.Check, 46, 16);
            fail = UIFactory.Button(seg, "Fail", () => cycle.SetReject(true), ButtonStyle.Secondary, Icons.Close, 46, 16);
            pass.Layout(-1, 46, 1); fail.Layout(-1, 46, 1);
            UIFactory.Section(panel, "Inspect a moving part");
            foreach (var m in MovingParts)
            {
                var id = m.id;
                focusButtons[id ?? ""] = ListItem(panel, m.label, "", Theme.Accent, () => Focus(id), Icons.Focus);
            }

            var bar = Shell.BottomBar(900);
            UIFactory.IconButton(bar, Icons.Restart, () => cycle.Restart(), 56);
            UIFactory.IconButton(bar, Icons.StepBack, () => cycle.StepBack(), 56);
            play = UIFactory.IconButton(bar, Icons.Pause, () => cycle.TogglePlay(), 64, ButtonStyle.Primary);
            UIFactory.IconButton(bar, Icons.StepForward, () => cycle.StepForward(), 56);
            var mid = UIFactory.Rect("Progress", bar); mid.VStack(6, new RectOffset(8, 8, 14, 14)); mid.Layout(-1, 72, 1);
            UIFactory.Label(mid, "CYCLE", 12, Theme.TextFaint, Weight.SemiBold).Layout(-1, 16, 1);
            progress = UIFactory.ProgressBar(mid, Theme.Accent, 6).fill;
            // Speed: icon, slider and value side by side so the handle never covers the label.
            var speed = UIFactory.Rect("Speed", bar); speed.HStack(8, null, TextAnchor.MiddleLeft); speed.Layout(250, 56, 0);
            UIFactory.Icon(speed, Icons.Speed, Theme.TextMuted, 22);
            UIFactory.Slider(speed, .25f, 2f, 1f, v => { cycle.Speed = v; speedLabel.text = v.ToString("0.0#") + "×"; }, 40)
                .GetComponent<RectTransform>().Layout(-1, 40, 1);
            speedLabel = UIFactory.Label(speed, "1.0×", 15, Theme.Text, Weight.SemiBold, TextAnchor.MiddleRight);
            speedLabel.Layout(46, 40, 0);

            cycle.Changed += Refresh;
            Refresh();
            Session.View?.ResetView();
        }

        void JumpTo(int station)
        {
            cycle.Restart();
            for (int i = 0; i < station * 2; i++) cycle.StepForward();
            cycle.Play();
        }

        void Focus(string id)
        {
            cycle.SetFocus(id);
            var p = id != null ? Kit.Get(id) : null;
            if (p != null) Session.FocusParts(new[] { p }); else Session.View?.ResetView();
        }

        void Refresh()
        {
            if (!cycle.Active) return;
            UIFactory.SetIcon(play, cycle.Playing ? Icons.Pause : Icons.Play);
            for (int i = 0; i < stationRows.Count; i++) SetSelected(stationRows[i], i == cycle.Station && cycle.CurrentPhase == MechanismCycle.Phase.Dwell);
            UIFactory.SetButtonStyle(pass, cycle.Reject ? ButtonStyle.Secondary : ButtonStyle.Primary);
            UIFactory.SetButtonStyle(fail, cycle.Reject ? ButtonStyle.Danger : ButtonStyle.Secondary);
            foreach (var pair in focusButtons) SetSelected(pair.Value, pair.Key == (cycle.FocusId ?? ""));
            Shell.SetCaption(cycle.Title(), cycle.Caption());
            if (cycle.Station != lastStation || cycle.CurrentPhase != lastPhase)
            {
                lastStation = cycle.Station; lastPhase = cycle.CurrentPhase;
                RenderInfo();
            }
        }

        void RenderInfo()
        {
            var card = NewInfo();
            card.Notice("Illustrative animation of the published station sequence — not a validated kinematic simulation. Timing, stroke and the inspection result are demonstration values.",
                Theme.Warning, Icons.Info);
            if (cycle.CurrentPhase == MechanismCycle.Phase.Index)
            {
                var disc = Kit.Get("index_disc");
                if (disc != null) RenderComponent(card, disc, false);
            }
            else
            {
                var info = Db.Station(cycle.Station + 1);
                var part = info != null ? Kit.Get(info.part) : null;
                if (part != null) RenderComponent(card, part, false);
            }
            card.Section("Signal path", "RFID head and distance sensor → IO-Link master (PLC trainer kit) → PLC. " +
                "The camera's image result drives sorting. PLC digital output → solenoid.");
        }

        public override void Tick()
        {
            if (cycle != null && cycle.Active && progress != null) UIFactory.SetProgress(progress, cycle.CycleProgress);
        }

        public override void OnPartTapped(KitPart part)
        {
            Shell.Toast(part.DisplayName + " — " + part.Info.summary, 3.2f);
        }

        public override void Exit()
        {
            if (cycle != null) cycle.Changed -= Refresh;
            cycle?.Stop();
            Kit.ApplyAll(PartVisual.Normal);
        }
    }
}
