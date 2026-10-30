using System;
using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;

namespace AMT.AR.Animation
{
    // Illustrative four-station working cycle on the real parts. The servo turns the table so the
    // test product stops at each station in the published order (RFID → distance → vision →
    // solenoid). Index angles come from the actual station positions in the CAD model; timing,
    // the solenoid stroke and the inspection result are demonstration values.
    public sealed class MechanismCycle : MonoBehaviour
    {
        public enum Phase { Dwell, Index }

        public const float IndexSeconds = 1.8f;
        public const float DwellSeconds = 3.2f;
        const float Stroke = .018f; // illustrative plunger travel in metres

        TrainingSession session;
        KitModel kit;
        StationEffects effects;
        ArrowVisual tableArrow, pushArrow;
        readonly float[] angles = new float[5];
        readonly KitPart[] stationParts = new KitPart[4];
        KitPart product, plunger, disc, motor, coupling;
        Vector3 plungerAxisKit;
        float time;
        bool resultShown;
        public bool Active { get; private set; }
        public bool Playing { get; private set; }
        public float Speed { get; set; } = 1f;
        public bool Reject { get; private set; }
        public int Station { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public string FocusId { get; private set; }
        public event Action Changed;

        public float PhaseProgress => Mathf.Clamp01(time / (CurrentPhase == Phase.Index ? IndexSeconds : DwellSeconds));
        // 0..1 through one full cycle of four dwell + four index phases.
        public float CycleProgress => (Station * 2 + (CurrentPhase == Phase.Index ? 1 : 0) + PhaseProgress) / 8f;

        public void Init(TrainingSession s) { session = s; kit = s.Kit; }

        public void Begin()
        {
            session.StopAll();
            if (!kit.IsLoaded) return;
            if (effects == null)
            {
                effects = new StationEffects(session.Overlays);
                tableArrow = new ArrowVisual("Table rotation arrow", session.Overlays, Theme.Accent, .003f);
                pushArrow = new ArrowVisual("Solenoid push arrow", session.Overlays, Theme.Danger, .003f);
            }
            ComputeGeometry();
            Active = true;
            Station = 0; CurrentPhase = Phase.Dwell; time = 0; Playing = true;
            FocusId = null;
            ApplyPose(true);
            Changed?.Invoke();
        }

        void ComputeGeometry()
        {
            disc = kit.Get("index_disc");
            product = kit.Get("test_product");
            plunger = kit.Get("solenoid_plunger");
            motor = kit.Get("servo_motor");
            coupling = kit.Get("shaft_coupling");
            var center = disc != null ? disc.KitCenter : kit.KitCenter;
            for (int i = 0; i < 4; i++)
            {
                var info = session.Db.Station(i + 1);
                stationParts[i] = info != null ? kit.Get(info.part) : null;
            }
            Vector3 Dir(KitPart p) { var d = (p != null ? p.KitCenter : center + Vector3.forward) - center; d.y = 0; return d; }
            var productDir = Dir(product);
            angles[0] = Vector3.SignedAngle(productDir, Dir(stationParts[0]), Vector3.up);
            float sense = Mathf.Sign(Vector3.SignedAngle(Dir(stationParts[0]), Dir(stationParts[1]), Vector3.up));
            if (sense == 0) sense = 1;
            for (int i = 1; i <= 4; i++)
            {
                float delta = Vector3.SignedAngle(Dir(stationParts[i - 1]), Dir(stationParts[i % 4]), Vector3.up);
                if (Mathf.Sign(delta) != sense || Mathf.Abs(delta) < 1f) delta += 360f * sense;
                angles[i] = angles[i - 1] + delta;
            }
            var body = kit.Get("solenoid_body");
            if (plunger != null && body != null)
            {
                plungerAxisKit = plunger.KitCenter - body.KitCenter; plungerAxisKit.y = 0;
                plungerAxisKit = plungerAxisKit.sqrMagnitude > 1e-6f ? plungerAxisKit.normalized : Vector3.right;
            }
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false; Playing = false;
            effects?.HideAll(); tableArrow?.Hide(); pushArrow?.Hide();
            if (kit.IsLoaded) kit.ResetPoses();
            Changed?.Invoke();
        }

        public void Play() { if (!Active) Begin(); Playing = true; Changed?.Invoke(); }
        public void Pause() { Playing = false; Changed?.Invoke(); }
        public void TogglePlay() { if (Playing) Pause(); else Play(); }

        public void Restart()
        {
            if (!Active) { Begin(); return; }
            Station = 0; CurrentPhase = Phase.Dwell; time = 0; Playing = true;
            ApplyPose(true);
            Changed?.Invoke();
        }

        // Jump to the start of the next phase.
        public void StepForward()
        {
            if (!Active) Begin();
            Advance();
            time = 0; Playing = false;
            ApplyPose(true);
            Changed?.Invoke();
        }

        public void StepBack()
        {
            if (!Active) Begin();
            if (time > .4f) time = 0;
            else if (CurrentPhase == Phase.Index) CurrentPhase = Phase.Dwell;
            else { CurrentPhase = Phase.Index; Station = (Station + 3) % 4; }
            time = 0; Playing = false;
            ApplyPose(true);
            Changed?.Invoke();
        }

        public void SetReject(bool reject) { Reject = reject; ApplyPose(true); Changed?.Invoke(); }

        // Show one moving part on its own (others ghosted); null shows the whole mechanism.
        public void SetFocus(string id) { FocusId = id; ApplyPose(true); Changed?.Invoke(); }

        void Advance()
        {
            if (CurrentPhase == Phase.Dwell) CurrentPhase = Phase.Index;
            else { CurrentPhase = Phase.Dwell; Station = (Station + 1) % 4; }
        }

        void Update()
        {
            if (!Active || !kit.IsLoaded) return;
            if (Playing)
            {
                time += Time.deltaTime * Mathf.Clamp(Speed, .1f, 4f);
                float length = CurrentPhase == Phase.Index ? IndexSeconds : DwellSeconds;
                if (time >= length)
                {
                    time -= length;
                    Advance();
                    ApplyPose(true);
                    Changed?.Invoke();
                }
            }
            ApplyPose(false);
        }

        // The camera's result is shown from 1.2 s into station 3 until the product returns to station 1.
        bool InspectionVisible => Station == 3 || (Station == 2 && (CurrentPhase == Phase.Index || time > 1.2f));

        static float Smoother(float x) { x = Mathf.Clamp01(x); return x * x * x * (x * (6f * x - 15f) + 10f); }

        void ApplyPose(bool visualsChanged)
        {
            float p = PhaseProgress;
            float angle = CurrentPhase == Phase.Index
                ? Mathf.Lerp(angles[Station], angles[Station + 1], Smoother(p))
                : angles[Station];
            if (kit.TablePivot != null) kit.TablePivot.localRotation = Quaternion.AngleAxis(angle, Vector3.up);

            float extend = 0f;
            if (CurrentPhase == Phase.Dwell && Station == 3 && Reject)
            {
                // Out in 0.5 s, hold, back in 0.5 s (scaled to the dwell length).
                float t = time;
                extend = t < .5f ? Smoother(t / .5f) : t < 1.4f ? 1f : 1f - Smoother((t - 1.4f) / .5f);
            }
            if (plunger != null) kit.OffsetPart(plunger, plungerAxisKit * Stroke * extend);

            UpdateEffects(extend);
            bool inspected = InspectionVisible;
            if (visualsChanged || inspected != resultShown) { resultShown = inspected; UpdateVisuals(); }
        }

        Vector3 W(Vector3 kitPoint) => kit.KitToWorld(kitPoint);

        Vector3 ProductTopWorld()
        {
            if (product == null) return W(kit.KitCenter);
            var b = product.WorldBounds();
            return b.center + kit.Root.up * b.extents.y;
        }

        void UpdateEffects(float extend)
        {
            effects.HideAll(); tableArrow.Hide(); pushArrow.Hide();
            if (FocusId != null && FocusId != "index_disc" && FocusId != "solenoid_plunger") return;
            float scale = session.View != null ? session.View.KitScale : 1f;
            tableArrow.SetWidth(.0028f * scale); pushArrow.SetWidth(.0028f * scale);
            if (CurrentPhase == Phase.Index)
            {
                if (disc != null)
                {
                    // Arc ahead of the product showing the direction of the next quarter turn.
                    float span = angles[Station + 1] - angles[Station];
                    var center = W(disc.KitCenter) + kit.Root.up * .03f * scale;
                    var productDir = product != null ? product.Transform.position - kit.TablePivot.position : kit.Root.forward;
                    tableArrow.SetArc(center, kit.Root.up * Mathf.Sign(span), productDir, .1f * scale,
                        Mathf.Lerp(20f, 85f, Mathf.Clamp01(PhaseProgress * 1.5f)), 28);
                }
                return;
            }
            var stationPart = stationParts[Station];
            if (stationPart == null || product == null) return;
            var sensor = stationPart.WorldBounds();
            var target = ProductTopWorld();
            float t = Time.time;
            switch (Station)
            {
                case 0: effects.Rfid(sensor.center, target, t, scale); break;
                case 1:
                {
                    var from = sensor.center - kit.Root.up * sensor.extents.y;
                    var to = target;
                    effects.Beam(from, to, scale, Mathf.PingPong(t * 6f, 1f));
                    break;
                }
                case 2:
                {
                    var lens = sensor.center - kit.Root.up * sensor.extents.y;
                    effects.CameraView(lens, target, scale, Mathf.PingPong(t * 2f, 1f));
                    break;
                }
                case 3:
                    if (Reject && plunger != null && extend > .02f)
                    {
                        var start = plunger.WorldBounds().center + kit.Root.up * .02f * scale;
                        pushArrow.SetStraight(start, start + kit.Root.TransformVector(plungerAxisKit).normalized * .045f * scale);
                    }
                    break;
            }
        }

        void UpdateVisuals()
        {
            var active = new HashSet<KitPart>();
            var related = new HashSet<KitPart>();
            if (CurrentPhase == Phase.Index)
            {
                if (disc != null) active.Add(disc);
                if (motor != null) related.Add(motor);
                if (coupling != null) related.Add(coupling);
            }
            else
            {
                var s = stationParts[Station];
                if (s != null) active.Add(s);
                if (Station == 3 && plunger != null) active.Add(plunger);
            }
            if (product != null)
            {
                if (InspectionVisible) { kit.SetHighlightColor(product, Reject ? Theme.Danger : Theme.Success); active.Add(product); }
                else { kit.ClearHighlightOverrides(); related.Add(product); }
            }
            var focus = FocusId != null ? kit.Get(FocusId) : null;
            kit.Apply(p =>
            {
                if (focus != null && p != focus) return PartVisual.Ghost;
                if (active.Contains(p)) return PartVisual.Highlight;
                if (related.Contains(p)) return PartVisual.Related;
                return PartVisual.Normal;
            });
        }

        // Caption for the current phase, built from the station data so edits to components.json
        // flow through.
        public string Caption()
        {
            if (!Active) return "";
            if (CurrentPhase == Phase.Index)
            {
                int next = (Station + 1) % 4;
                var nextInfo = session.Db.Station(next + 1);
                return "The servo motor indexes the table a quarter turn, carrying the product to station " +
                       (next + 1) + (nextInfo != null ? " (" + nextInfo.name + ")." : ".");
            }
            var info = session.Db.Station(Station + 1);
            string text = info != null ? info.explanation : "";
            if (Station == 2) text += Reject ? " Simulated result: FAIL." : " Simulated result: PASS.";
            if (Station == 3) text += Reject ? " The rejected product is pushed by the solenoid plunger."
                                             : " The product passed, so the solenoid stays retracted.";
            return text;
        }

        public string Title()
        {
            if (!Active) return "";
            if (CurrentPhase == Phase.Index) return "Indexing to station " + ((Station + 1) % 4 + 1);
            var info = session.Db.Station(Station + 1);
            return "Station " + (Station + 1) + " · " + (info != null ? info.name : "");
        }

        void OnDestroy()
        {
            effects?.Destroy(); tableArrow?.Destroy(); pushArrow?.Destroy();
        }
    }
}
