using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;

namespace AMT.AR.Tools
{
    // Procedural, real-scale tool models. Every tool has its working tip at the local origin and
    // its turning axis along local +Y, so rotating about Y turns it like a real screw tool.
    public static class ToolModels
    {
        static Material steel, blackGrip, amberGrip, redGrip, yellowGrip;

        static void EnsureMaterials()
        {
            if (steel != null) return;
            steel = KitMaterials.CreateSurface(new Color(.80f, .82f, .85f), .45f, .65f);
            blackGrip = KitMaterials.CreateSurface(new Color(.10f, .11f, .12f), 0f, .35f);
            amberGrip = KitMaterials.CreateSurface(Theme.Accent, 0f, .45f);
            redGrip = KitMaterials.CreateSurface(new Color(.82f, .16f, .14f), 0f, .45f);
            yellowGrip = KitMaterials.CreateSurface(new Color(.98f, .80f, .10f), 0f, .45f);
        }

        public static GameObject Create(string model, Transform parent)
        {
            EnsureMaterials();
            switch (model)
            {
                case "allen": return AllenKey(parent);
                case "flat": return Screwdriver(parent, "Flat-blade screwdriver", true);
                default: return Screwdriver(parent, "Phillips screwdriver", false);
            }
        }

        // L-shaped hex key: short arm along +Y from the tip, long arm along +X as the lever.
        static GameObject AllenKey(Transform parent)
        {
            var root = new GameObject("Allen key");
            root.transform.SetParent(parent, false);
            var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>();
            const float r = .0024f, shortArm = .024f, longArm = .075f;
            MeshBuilder.Prism(v, t, n, 6, r, r, 0f, shortArm, 0f, true, Mathf.PI / 6f);
            var shortMesh = MeshBuilder.Finish("Allen short arm", v, t, n);
            MeshBuilder.Object("Short arm", root.transform, shortMesh, steel);
            v.Clear(); t.Clear(); n.Clear();
            MeshBuilder.Prism(v, t, n, 6, r, r, -r, longArm, 0f, true, Mathf.PI / 6f);
            var longGo = MeshBuilder.Object("Long arm", root.transform, MeshBuilder.Finish("Allen long arm", v, t, n), steel);
            longGo.transform.localPosition = new Vector3(0, shortArm, 0);
            longGo.transform.localRotation = Quaternion.Euler(0, 0, -90f);
            // Rounded tip chamfer
            v.Clear(); t.Clear(); n.Clear();
            MeshBuilder.Prism(v, t, n, 6, r * .75f, r, -.0012f, 0f, 0f, true, Mathf.PI / 6f);
            MeshBuilder.Object("Tip", root.transform, MeshBuilder.Finish("Allen tip", v, t, n), steel);
            return root;
        }

        static GameObject Screwdriver(Transform parent, string name, bool flat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>();
            const float tipLen = .009f, shaft = .10f, handle = .095f;
            // Blade / tip
            if (flat)
            {
                MeshBuilder.Box(v, t, n, new Vector3(0, tipLen * .5f, 0), new Vector3(.0055f, tipLen, .0009f));
            }
            else
            {
                MeshBuilder.Prism(v, t, n, 8, .0006f, .0028f, 0f, tipLen, 0f, false);
                // Four cross flutes
                MeshBuilder.Box(v, t, n, new Vector3(0, tipLen * .45f, 0), new Vector3(.0042f, tipLen * .9f, .0009f));
                MeshBuilder.Box(v, t, n, new Vector3(0, tipLen * .45f, 0), new Vector3(.0009f, tipLen * .9f, .0042f));
            }
            MeshBuilder.Prism(v, t, n, 14, .0028f, .0028f, tipLen, tipLen + shaft);
            MeshBuilder.Object("Shaft", root.transform, MeshBuilder.Finish(name + " shaft", v, t, n), steel);
            // Handle: ferrule, fluted grip and end cap.
            v.Clear(); t.Clear(); n.Clear();
            float y = tipLen + shaft;
            MeshBuilder.Prism(v, t, n, 16, .0055f, .009f, y, y + .012f);
            var ferrule = MeshBuilder.Finish("Ferrule", v, t, n);
            MeshBuilder.Object("Ferrule", root.transform, ferrule, blackGrip);
            v.Clear(); t.Clear(); n.Clear();
            MeshBuilder.Prism(v, t, n, 16, .0135f, .0135f, y + .012f, y + handle - .012f, .12f);
            MeshBuilder.Object("Grip", root.transform, MeshBuilder.Finish("Grip", v, t, n), flat ? redGrip : amberGrip);
            v.Clear(); t.Clear(); n.Clear();
            MeshBuilder.Prism(v, t, n, 16, .0135f, .009f, y + handle - .012f, y + handle);
            MeshBuilder.Object("End cap", root.transform, MeshBuilder.Finish("Cap", v, t, n), flat ? yellowGrip : blackGrip);
            return root;
        }
    }

    // Moves a tool onto screw positions and turns it: in for assembly, out for disassembly.
    public sealed class ToolRig
    {
        readonly Transform parent;
        readonly Dictionary<string, GameObject> tools = new Dictionary<string, GameObject>();
        GameObject current;

        public ToolRig(Transform parent) { this.parent = parent; }

        public GameObject Show(string model)
        {
            if (string.IsNullOrEmpty(model)) { Hide(); return null; }
            if (!tools.TryGetValue(model, out var go) || go == null)
            {
                go = ToolModels.Create(model, parent);
                tools[model] = go;
            }
            if (current != null && current != go) current.SetActive(false);
            current = go;
            current.SetActive(true);
            return current;
        }

        public void Hide()
        {
            if (current != null) current.SetActive(false);
            current = null;
        }

        // phase 0..1 over one fastener: approach, turn, retreat. axis points out of the screw head.
        public void Pose(Vector3 screwWorld, Vector3 axisWorld, float phase, bool tighten, float scale, float visualScale = 1f)
        {
            if (current == null) return;
            axisWorld = axisWorld.sqrMagnitude > 1e-6f ? axisWorld.normalized : Vector3.up;
            float lift;
            float turns;
            if (phase < .25f) { lift = 1f - Smooth(phase / .25f); turns = 0; }
            else if (phase < .8f) { lift = 0; turns = (phase - .25f) / .55f * 1.5f; }
            else { lift = Smooth((phase - .8f) / .2f); turns = 1.5f; }
            float spin = (tighten ? 1f : -1f) * turns * 360f;
            current.transform.localScale = Vector3.one * scale * visualScale;
            current.transform.position = screwWorld + axisWorld * (.035f * scale * lift);
            current.transform.rotation = Quaternion.AngleAxis(spin, axisWorld) * Quaternion.FromToRotation(Vector3.up, axisWorld);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        public void Destroy()
        {
            foreach (var go in tools.Values) if (go != null) Object.Destroy(go);
            tools.Clear();
        }
    }
}
