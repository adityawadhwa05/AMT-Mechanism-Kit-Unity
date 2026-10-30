using System;
using AMT.AR.Animation;
using AMT.AR.AssemblyGuide;
using AMT.AR.Data;
using AMT.AR.Interaction;
using AMT.AR.Tools;
using UnityEngine;

namespace AMT.AR.Core
{
    // What the 3D viewer and the AR scene each provide to the shared training modules.
    public interface IViewController
    {
        Camera Camera { get; }
        bool IsAR { get; }
        // Scale of the kit relative to real size (1 = 1:1).
        float KitScale { get; }
        void Focus(Bounds worldBounds);
        void ResetView();
    }

    // Shared state for one scene: data, model, gestures, selection and the animation systems.
    // Only one system may move parts at a time; Stop() on the others restores the rest pose.
    public sealed class TrainingSession : MonoBehaviour
    {
        public KitDatabase Db { get; private set; }
        public KitModel Kit { get; private set; }
        public GestureInput Gestures { get; private set; }
        public IViewController View { get; private set; }
        public ExplodedView Exploded { get; private set; }
        public MechanismCycle Cycle { get; private set; }
        public GuidePlayer Guide { get; private set; }
        public ToolShowcase ToolShowcase { get; private set; }
        public KitPart Selected { get; private set; }
        // Unscaled parent for overlays and tool models; their sizes are computed from View.KitScale.
        public Transform Overlays { get; private set; }

        public event Action<KitPart> SelectionChanged;
        public event Action<KitPart> PartTapped;
        public event Action<KitPart> PartDoubleTapped;
        public event Action EmptyTapped;

        public static TrainingSession Create(IViewController view, Transform kitParent)
        {
            var go = new GameObject("Training Session");
            var s = go.AddComponent<TrainingSession>();
            s.View = view;
            s.Db = KitDatabase.Load();
            foreach (var e in s.Db.Errors) Debug.LogError("AMT data: " + e);
            s.Overlays = new GameObject("Overlays (unscaled)").transform;
            s.Gestures = go.AddComponent<GestureInput>();
            s.Kit = kitParent.gameObject.AddComponent<KitModel>();
            s.Exploded = go.AddComponent<ExplodedView>();
            s.Cycle = go.AddComponent<MechanismCycle>();
            s.Guide = go.AddComponent<GuidePlayer>();
            s.ToolShowcase = go.AddComponent<ToolShowcase>();
            s.Exploded.Init(s);
            s.Cycle.Init(s);
            s.Guide.Init(s);
            s.ToolShowcase.Init(s);
            s.Gestures.Tapped += s.OnTap;
            s.Gestures.DoubleTapped += s.OnDoubleTap;
            return s;
        }

        public void BeginLoad() => Kit.BeginLoad(Db);

        KitPart Pick(Vector2 screen)
        {
            if (!Kit.IsLoaded || View?.Camera == null) return null;
            var ray = View.Camera.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 100f);
            KitPart best = null; float bestDistance = float.MaxValue;
            foreach (var h in hits)
            {
                var p = Kit.FromCollider(h.collider);
                if (p == null || h.distance >= bestDistance) continue;
                best = p; bestDistance = h.distance;
            }
            return best;
        }

        void OnTap(Vector2 screen)
        {
            var p = Pick(screen);
            if (p != null) PartTapped?.Invoke(p); else EmptyTapped?.Invoke();
        }

        void OnDoubleTap(Vector2 screen)
        {
            var p = Pick(screen);
            if (p != null) PartDoubleTapped?.Invoke(p);
            else View?.ResetView();
        }

        public void Select(KitPart part)
        {
            Selected = part;
            SelectionChanged?.Invoke(part);
        }

        // Stops every animation system and returns the kit to its assembled rest state.
        public void StopAll()
        {
            Exploded.Stop();
            Cycle.Stop();
            Guide.Stop();
            ToolShowcase.Hide();
            if (Kit.IsLoaded)
            {
                Kit.ResetPoses();
                Kit.ClearHighlightOverrides();
                Kit.ApplyAll(PartVisual.Normal);
            }
        }

        public void FocusParts(System.Collections.Generic.IEnumerable<KitPart> parts)
        {
            if (View == null || !Kit.IsLoaded) return;
            View.Focus(Kit.WorldBounds(parts));
        }
    }
}
