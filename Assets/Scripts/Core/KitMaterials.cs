using System.Collections.Generic;
using UnityEngine;

namespace AMT.AR.Core
{
    // Shaders and shared materials. The eight source material groups keep their identity; each can be
    // shown in its original CAD colour or in the calmer "studio" palette used by default.
    public static class KitMaterials
    {
        public struct Finish
        {
            public Color Studio, Original;
            public float Metallic, Smoothness;
            public Finish(Color studio, Color original, float metallic, float smoothness)
            { Studio = studio; Original = original; Metallic = metallic; Smoothness = smoothness; }
        }

        // Keyed by the glTF material name in MechanismKit.glb. Original colours are the source
        // baseColorFactor values (sRGB-ish gamma values as authored).
        static readonly Dictionary<string, Finish> Finishes = new Dictionary<string, Finish>
        {
            { "material_3",  new Finish(new Color(.20f, .21f, .24f), new Color(.187f, .015f, .049f), .15f, .45f) },
            { "material_29", new Finish(new Color(.70f, .73f, .77f), new Color(.993f, .424f, .384f), .25f, .55f) },
            { "material_41", new Finish(new Color(.76f, .69f, .58f), new Color(.956f, .806f, .624f), .20f, .50f) },
            { "material_43", new Finish(new Color(.96f, .66f, .16f), new Color(.923f, .902f, .563f), .10f, .55f) },
            { "material_44", new Finish(new Color(.82f, .85f, .88f), new Color(.784f, .924f, .995f), .00f, .35f) },
            { "material_13", new Finish(new Color(.12f, .38f, .42f), new Color(.057f, .359f, .370f), .10f, .40f) },
            { "material_20", new Finish(new Color(.34f, .38f, .34f), new Color(.366f, .446f, .339f), .10f, .40f) },
            { "material_25", new Finish(new Color(.36f, .55f, .20f), new Color(.413f, .646f, .061f), .05f, .35f) },
        };

        static Shader surface, ghost, unlit, ground, arPlane;
        static Material ghostMaterial;
        public static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        public static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");

        public static Shader Surface => surface != null ? surface : surface = Load("Shaders/AMTKitSurface", "Standard");
        public static Shader GhostShader => ghost != null ? ghost : ghost = Load("Shaders/AMTGhost", "Sprites/Default");
        public static Shader Unlit => unlit != null ? unlit : unlit = Load("Shaders/AMTUnlit", "Sprites/Default");
        public static Shader Ground => ground != null ? ground : ground = Load("Shaders/AMTGround", "Sprites/Default");
        public static Shader ARPlane => arPlane != null ? arPlane : arPlane = Load("Shaders/AMTARPlane", "Sprites/Default");

        static Shader Load(string path, string fallback)
        {
            var s = Resources.Load<Shader>(path);
            if (s == null)
            {
                Debug.LogError("AMT: shader missing from Resources: " + path + ". Using " + fallback + ".");
                s = Shader.Find(fallback);
            }
            return s;
        }

        public static bool TryGetFinish(string materialName, out Finish finish)
        {
            // glTFast may append " (Instance)" or similar suffixes.
            foreach (var pair in Finishes)
                if (materialName != null && materialName.StartsWith(pair.Key) &&
                    (materialName.Length == pair.Key.Length || !char.IsDigit(materialName[pair.Key.Length])))
                { finish = pair.Value; return true; }
            finish = default;
            return false;
        }

        public static Material CreateSurface(Color color, float metallic, float smoothness)
        {
            var m = new Material(Surface) { color = color };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        public static Material Ghost
        {
            get
            {
                if (ghostMaterial == null) ghostMaterial = new Material(GhostShader) { name = "AMT Ghost" };
                return ghostMaterial;
            }
        }

        public static Material CreateUnlit(Color color, bool additive = false, bool onTop = false)
        {
            var m = new Material(Unlit) { color = color };
            if (additive) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (onTop) m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            m.renderQueue = onTop ? 3100 : 3000;
            return m;
        }

        public static Color ReadBaseColor(Material m)
        {
            if (m == null) return Color.gray;
            foreach (var name in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                if (m.HasProperty(name)) return m.GetColor(name);
            return Color.gray;
        }
    }
}
