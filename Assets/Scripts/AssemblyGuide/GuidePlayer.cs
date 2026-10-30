using System;
using System.Collections.Generic;
using AMT.AR.Animation;
using AMT.AR.Core;
using AMT.AR.Data;
using AMT.AR.Tools;
using UnityEngine;

namespace AMT.AR.AssemblyGuide
{
    // Step-by-step assembly and disassembly on the real parts (guide.json). Each step moves its
    // parts in (or out) along an approach direction, then shows the tool working on the actual
    // screw positions of the step's fasteners. Disassembly plays the same steps in reverse.
    public sealed class GuidePlayer : MonoBehaviour
    {
        const float Lead = .35f, Move = 1.3f, FastenerMove = .45f, PerScrew = 1.15f;

        TrainingSession session;
        KitModel kit;
        ToolRig rig;
        ArrowVisual arrow;
        readonly List<KitPart> bodies = new List<KitPart>();
        readonly List<KitPart> fasteners = new List<KitPart>();
        readonly List<(KitPart part, Vector3 kitPoint)> screws = new List<(KitPart, Vector3)>();
        Vector3 approach;
        float distance, time;

        public bool Active { get; private set; }
        public bool Disassembly { get; private set; }
        public int Step { get; private set; }
        public bool Playing { get; private set; }
        public bool GhostRemaining { get; private set; } = true;
        public int StepCount => session.Db.Guide.steps.Length;
        public event Action Changed;

        public void Init(TrainingSession s) { session = s; kit = s.Kit; }

        int DataIndex(int step) => Disassembly ? StepCount - 1 - step : step;
        public GuideStep Current => StepCount == 0 ? null : session.Db.Guide.steps[DataIndex(Step)];
        public string Instruction => Current == null ? "" : Disassembly ? Current.disassemble : Current.assemble;
        float ToolSeconds => screws.Count * PerScrew;
        float Duration => Lead + Move + (fasteners.Count > 0 ? FastenerMove : 0) + ToolSeconds + .3f;
        public float StepProgress => Mathf.Clamp01(time / Duration);

        public void Begin(bool disassembly)
        {
            session.StopAll();
            if (!kit.IsLoaded || StepCount == 0) return;
            if (rig == null)
            {
                rig = new ToolRig(session.Overlays);
                arrow = new ArrowVisual("Guide motion arrow", session.Overlays, Theme.Accent, .003f);
            }
            Active = true; Disassembly = disassembly;
            GoTo(0);
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false; Playing = false;
            rig?.Hide(); arrow?.Hide();
            if (kit.IsLoaded) kit.ResetPoses();
            Changed?.Invoke();
        }

        public void GoTo(int step)
        {
            if (!Active) return;
            Step = Mathf.Clamp(step, 0, StepCount - 1);
            time = 0; Playing = true;
            Prepare();
            Changed?.Invoke();
        }

        public void Next() => GoTo(Step + 1);
        public void Previous() => GoTo(Step - 1);
        public void Replay() => GoTo(Step);
        public void Restart() => GoTo(0);
        public void TogglePlay()
        {
            if (!Playing && time >= Duration) { Replay(); return; }
            Playing = !Playing; Changed?.Invoke();
        }

        public void SetGhostRemaining(bool ghost)
        {
            GhostRemaining = ghost;
            if (Active) { ApplyVisuals(); Changed?.Invoke(); }
        }

        void Prepare()
        {
            bodies.Clear(); fasteners.Clear(); screws.Clear();
            var step = Current;
            foreach (var id in step.parts) { var p = kit.Get(id); if (p != null) bodies.Add(p); }
            foreach (var id in step.fasteners) { var p = kit.Get(id); if (p != null) fasteners.Add(p); }

            // Where the tool works: screw clusters of the fasteners, or the base of the first part.
            if (!string.IsNullOrEmpty(step.tool))
            {
                foreach (var f in fasteners)
                    foreach (var h in f.Hotspots(kit.Root, 3)) { screws.Add((f, h)); if (screws.Count >= 3) break; }
                if (screws.Count == 0 && bodies.Count > 0)
                {
                    var b = bodies[0];
                    screws.Add((b, b.KitCenter + Vector3.up * b.KitSize.y * .5f));
                }
            }

            var center = Vector3.zero; var size = Vector3.zero;
            foreach (var b in bodies) { center += b.KitCenter; size = Vector3.Max(size, b.KitSize); }
            if (bodies.Count > 0) center /= bodies.Count;
            approach = step.approach;
            if (approach.sqrMagnitude < 1e-4f)
            {
                var outward = center - kit.KitCenter; outward.y = 0;
                approach = (outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.back) + Vector3.up * .35f;
            }
            approach.Normalize();
            distance = .1f + Mathf.Max(size.x, size.y, size.z) * .45f;

            kit.ResetPoses();
            ApplyVisuals();
            var focus = new List<KitPart>(bodies);
            focus.AddRange(fasteners);
            session.FocusParts(focus);
        }

        void ApplyVisuals()
        {
            int current = DataIndex(Step);
            var stepOf = new Dictionary<string, int>();
            var steps = session.Db.Guide.steps;
            for (int i = 0; i < steps.Length; i++)
            {
                foreach (var id in steps[i].parts) stepOf[id] = i;
                foreach (var id in steps[i].fasteners) stepOf[id] = i;
            }
            kit.Apply(p =>
            {
                if (!stepOf.TryGetValue(p.Id, out int s)) return PartVisual.Normal;
                if (s == current) return PartVisual.Highlight;
                if (s < current) return PartVisual.Normal;
                // Later steps: not yet fitted (assembly) or already removed (disassembly).
                return GhostRemaining ? PartVisual.Ghost : PartVisual.Hidden;
            });
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void Update()
        {
            if (!Active || !kit.IsLoaded) return;
            if (Playing)
            {
                time += Time.deltaTime;
                if (time >= Duration) { time = Duration; Playing = false; Changed?.Invoke(); }
            }
            Animate();
        }

        void Animate()
        {
            float scale = session.View != null ? session.View.KitScale : 1f;
            arrow.SetWidth(.003f * scale);
            var away = approach * distance;
            var fastenerAway = Vector3.up * .05f;
            float t = time;
            float bodyOut, fastOut; // 0 = home, 1 = fully away
            int screwIndex = -1; float screwPhase = 0;
            if (!Disassembly)
            {
                bodyOut = 1f - Smooth((t - Lead) / Move);
                float tf = t - Lead - Move;
                fastOut = fasteners.Count > 0 ? 1f - Smooth(tf / FastenerMove) : 0f;
                float ts = tf - (fasteners.Count > 0 ? FastenerMove : 0);
                if (ts >= 0 && ts < ToolSeconds) { screwIndex = (int)(ts / PerScrew); screwPhase = (ts % PerScrew) / PerScrew; }
            }
            else
            {
                float ts = t - Lead;
                if (ts >= 0 && ts < ToolSeconds) { screwIndex = (int)(ts / PerScrew); screwPhase = (ts % PerScrew) / PerScrew; }
                float tf = ts - ToolSeconds;
                fastOut = fasteners.Count > 0 ? Smooth(tf / FastenerMove) : 0f;
                float tb = tf - (fasteners.Count > 0 ? FastenerMove : 0);
                bodyOut = Smooth(tb / Move);
            }
            foreach (var b in bodies) kit.OffsetPart(b, away * bodyOut);
            foreach (var f in fasteners) kit.OffsetPart(f, away * bodyOut + fastenerAway * fastOut);

            // Fully removed parts disappear (or stay as ghosts) at the end of a disassembly step.
            if (Disassembly)
            {
                var v = bodyOut >= .999f ? (GhostRemaining ? PartVisual.Ghost : PartVisual.Hidden) : PartVisual.Highlight;
                foreach (var b in bodies) kit.SetVisual(b, v);
                foreach (var f in fasteners) kit.SetVisual(f, fastOut >= .999f ? v : PartVisual.Highlight);
            }

            if (screwIndex >= 0 && screwIndex < screws.Count)
            {
                rig.Show(ModelFor(Current.tool));
                var s = screws[screwIndex];
                var offset = s.part.IsFastener ? away * bodyOut + fastenerAway * fastOut : away * bodyOut;
                rig.Pose(kit.KitToWorld(s.kitPoint + offset), kit.Root.up, screwPhase, !Disassembly, scale, 1.3f);
            }
            else rig.Hide();

            // Arrow along the motion path of the step's main parts.
            if (bodies.Count > 0 && bodyOut > .001f && bodyOut < .999f)
            {
                var c = Vector3.zero; foreach (var b in bodies) c += b.KitCenter; c /= bodies.Count;
                var from = kit.KitToWorld(c + away * (Disassembly ? 0f : 1f));
                var to = kit.KitToWorld(c + away * (Disassembly ? 1f : 0f));
                arrow.SetStraight(Vector3.Lerp(from, to, .1f), Vector3.Lerp(from, to, .9f));
            }
            else arrow.Hide();
        }

        string ModelFor(string toolId)
        {
            var info = session.Db.Tool(toolId);
            return info != null ? info.model : null;
        }

        void OnDestroy() { rig?.Destroy(); arrow?.Destroy(); }
    }
}
