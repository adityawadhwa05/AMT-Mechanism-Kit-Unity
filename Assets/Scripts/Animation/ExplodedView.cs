using AMT.AR.Core;
using UnityEngine;

namespace AMT.AR.Animation
{
    // Separates the parts along authored directions: each component rises by explodeUp and moves
    // outward from the kit centre by explodeOut (components.json, metres at 100 %). Levels are
    // ordered bottom-up so parts separate along their assembly direction instead of colliding.
    public sealed class ExplodedView : MonoBehaviour
    {
        TrainingSession session;
        float amount, goal, velocity;
        public bool Active { get; private set; }
        public float Amount => goal;
        public float Current => amount;

        public void Init(TrainingSession s) { session = s; }

        public void Begin(float initial = .6f)
        {
            session.StopAll();
            Active = true;
            amount = 0; velocity = 0; goal = initial;
        }

        public void SetAmount(float value) { goal = Mathf.Clamp01(value); }

        public void Stop()
        {
            if (!Active) return;
            Active = false; amount = goal = 0;
            if (session.Kit.IsLoaded) session.Kit.ResetPoses();
        }

        public Vector3 Offset(KitPart p, float a)
        {
            if (p.Info == null) return Vector3.zero;
            var kit = session.Kit;
            var outward = p.KitCenter - kit.KitCenter; outward.y = 0;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.zero;
            return (Vector3.up * p.Info.explodeUp + outward * p.Info.explodeOut) * a;
        }

        void Update()
        {
            if (!Active || !session.Kit.IsLoaded) return;
            float previous = amount;
            amount = Mathf.SmoothDamp(amount, goal, ref velocity, .35f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(goal - amount) < .0005f) amount = goal;
            if (Mathf.Approximately(previous, amount) && amount != 0) return;
            if (session.Kit.TablePivot != null) session.Kit.TablePivot.localRotation = Quaternion.identity;
            foreach (var p in session.Kit.Parts)
            {
                if (amount <= 0) p.ResetPose();
                else session.Kit.OffsetPart(p, Offset(p, amount));
            }
        }
    }
}
