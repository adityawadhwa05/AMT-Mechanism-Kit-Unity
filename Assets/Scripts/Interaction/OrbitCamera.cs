using AMT.AR.Core;
using UnityEngine;

namespace AMT.AR.Interaction
{
    // Viewer camera: orbit (one finger / left mouse), zoom (pinch / wheel), pan (two fingers /
    // right mouse), smooth focus on a part, and an off-centre projection so the model stays centred
    // in the area not covered by the side panels.
    [RequireComponent(typeof(Camera))]
    public sealed class OrbitCamera : MonoBehaviour
    {
        public GestureInput gestures;
        public bool inputEnabled = true;
        public bool autoRotate;
        public float minDistance = .25f, maxDistance = 3f;
        public float minPitch = -5f, maxPitch = 85f;

        Camera cam;
        Vector3 target, targetGoal;
        float yaw = -35f, pitch = 24f, distance = 1.2f;
        float yawGoal = -35f, pitchGoal = 24f, distanceGoal = 1.2f;
        Vector3 homeTarget; float homeDistance = 1.2f;
        // Normalised horizontal/vertical centre of the free viewport (0.5 = screen centre).
        Vector2 viewportCenter = new Vector2(.5f, .5f), viewportCenterGoal = new Vector2(.5f, .5f);
        float idle;

        public Camera Camera => cam != null ? cam : cam = GetComponent<Camera>();

        void Awake() { cam = GetComponent<Camera>(); }

        public void SetHome(Bounds bounds)
        {
            homeTarget = bounds.center;
            homeDistance = FitDistance(bounds.extents.magnitude);
            maxDistance = homeDistance * 2.6f;
            minDistance = homeDistance * .12f;
        }

        public void ResetView(bool instant = false)
        {
            targetGoal = homeTarget; distanceGoal = homeDistance; yawGoal = -35f; pitchGoal = 24f;
            if (instant) Snap();
        }

        public void Focus(Bounds bounds, bool keepAngle = true)
        {
            targetGoal = bounds.center;
            // A minimum radius keeps small parts in context instead of filling the screen.
            distanceGoal = Mathf.Clamp(FitDistance(Mathf.Max(bounds.extents.magnitude, .15f)), minDistance, maxDistance);
            if (!keepAngle) { yawGoal = -35f; pitchGoal = 28f; }
        }

        public void SetViewportCenter(Vector2 center) { viewportCenterGoal = center; }

        float FitDistance(float radius)
        {
            float fov = Camera.fieldOfView * Mathf.Deg2Rad * .5f;
            float aspectFov = Mathf.Atan(Mathf.Tan(fov) * Mathf.Min(1f, Camera.aspect * .62f));
            return radius / Mathf.Sin(Mathf.Min(fov, aspectFov)) * 1.05f;
        }

        void Snap()
        {
            target = targetGoal; distance = distanceGoal; yaw = yawGoal; pitch = pitchGoal;
            viewportCenter = viewportCenterGoal;
            Apply();
        }

        void LateUpdate()
        {
            if (inputEnabled && gestures != null) HandleInput();
            if (autoRotate && (gestures == null || !gestures.Active))
            {
                idle += Time.unscaledDeltaTime;
                if (idle > 1.5f) yawGoal += Time.unscaledDeltaTime * 8f;
            }
            float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f);
            target = Vector3.Lerp(target, targetGoal, t);
            distance = Mathf.Lerp(distance, distanceGoal, t);
            yaw = Mathf.Lerp(yaw, yawGoal, t);
            pitch = Mathf.Lerp(pitch, pitchGoal, t);
            viewportCenter = Vector2.Lerp(viewportCenter, viewportCenterGoal, t);
            Apply();
        }

        void HandleInput()
        {
            if (gestures.Active) idle = 0;
            var drag = gestures.Drag;
            float scale = 180f / Mathf.Max(400f, Screen.height);
            yawGoal += drag.x * scale;
            pitchGoal = Mathf.Clamp(pitchGoal - drag.y * scale, minPitch, maxPitch);
            if (Mathf.Abs(gestures.Pinch - 1f) > .0001f)
                distanceGoal = Mathf.Clamp(distanceGoal / gestures.Pinch, minDistance, maxDistance);
            if (gestures.Pan.sqrMagnitude > 0)
            {
                float worldPerPixel = 2f * distance * Mathf.Tan(Camera.fieldOfView * Mathf.Deg2Rad * .5f) / Screen.height;
                targetGoal -= (transform.right * gestures.Pan.x + transform.up * gestures.Pan.y) * worldPerPixel;
            }
        }

        void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            Camera.nearClipPlane = Mathf.Max(.005f, distance * .02f);
            Camera.farClipPlane = Mathf.Max(20f, distance * 20f);
            // Off-axis projection: shift the image so the orbit target sits at the free-area centre.
            Camera.ResetProjectionMatrix();
            var m = Camera.projectionMatrix;
            m[0, 2] = -(viewportCenter.x - .5f) * 2f;
            m[1, 2] = -(viewportCenter.y - .5f) * 2f;
            Camera.projectionMatrix = m;
        }
    }
}
