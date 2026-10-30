using AMT.AR.Interaction;
using AMT.AR.UI;
using UnityEngine;

namespace AMT.AR.Core
{
    // Entry point of the Viewer scene (the scene file references this component). Builds the 3D
    // studio — camera, lights, ground — and the shared training session and UI.
    public sealed class ViewerBootstrap : MonoBehaviour, IViewController
    {
        OrbitCamera orbit;
        TrainingSession session;
        TrainingShell shell;
        Transform kitParent;
        Transform ground;

        public Camera Camera => orbit != null ? orbit.Camera : null;
        public bool IsAR => false;
        public float KitScale => 1f;
        public void Focus(Bounds worldBounds) { if (orbit != null) orbit.Focus(worldBounds); }
        public void ResetView() { if (orbit != null) orbit.ResetView(); }

        void Awake()
        {
            AppNavigation.ConfigureOrientation();
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 6f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.antiAliasing = 4;

            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Viewer Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.ViewerBackground;
            cam.fieldOfView = 32f;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.allowHDR = false;
            orbit = cam.gameObject.AddComponent<OrbitCamera>();

            BuildLights();
            kitParent = new GameObject("Kit").transform;
            session = TrainingSession.Create(this, kitParent);
            orbit.gestures = session.Gestures;
            shell = TrainingShell.Create(session, false);
            session.Kit.Loaded += OnLoaded;
            session.BeginLoad();
        }

        void BuildLights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.46f, .50f, .56f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .32f, .35f);
            RenderSettings.ambientGroundColor = new Color(.12f, .12f, .13f);
            RenderSettings.fog = false;

            var key = new GameObject("Key light").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.15f; key.color = new Color(1f, .97f, .92f);
            key.shadows = LightShadows.Soft; key.shadowStrength = .7f; key.shadowBias = .02f; key.shadowNormalBias = .2f;
            key.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            var fill = new GameObject("Fill light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .38f; fill.color = new Color(.78f, .86f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, 140f, 0f);
            var rim = new GameObject("Rim light").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = .45f; rim.color = new Color(1f, .86f, .65f);
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(12f, 200f, 0f);
        }

        void OnLoaded()
        {
            var kit = session.Kit;
            // Stand the kit on the ground plane, centred on the origin.
            kit.Root.localPosition = -new Vector3(kit.KitCenter.x, kit.KitCenter.y - kit.KitSize.y * .5f, kit.KitCenter.z);
            BuildGround(kit.KitSize);
            orbit.SetHome(kit.WorldBounds());
            orbit.ResetView(true);
        }

        void BuildGround(Vector3 kitSize)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Ground";
            Destroy(quad.GetComponent<Collider>());
            ground = quad.transform;
            ground.SetParent(kitParent, false);
            ground.localRotation = Quaternion.Euler(90f, 0, 0);
            ground.localPosition = new Vector3(0, -.0005f, 0);
            float size = Mathf.Max(kitSize.x, kitSize.z) * 3.2f;
            ground.localScale = new Vector3(size, size, 1);
            var mat = new Material(KitMaterials.Ground);
            mat.SetColor("_Color", new Color(.17f, .20f, .24f));
            mat.SetFloat("_BaseAlpha", 1f);
            mat.SetFloat("_ShadowStrength", .55f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        void LateUpdate()
        {
            if (orbit == null || shell == null) return;
            orbit.SetViewportCenter(shell.FreeViewportCenter());
            orbit.autoRotate = shell.Current == null;
            // The orbit camera only takes input when a module is open or the home screen is idle.
            orbit.inputEnabled = session.Kit.IsLoaded;
        }
    }
}
