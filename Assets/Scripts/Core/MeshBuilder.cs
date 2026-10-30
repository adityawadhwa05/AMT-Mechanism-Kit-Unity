using System.Collections.Generic;
using UnityEngine;

namespace AMT.AR.Core
{
    // Small procedural meshes for overlays (arrows, rings, beams) and the tool models.
    // Prisms are flat-shaded (vertices split per face) so they read well under lighting.
    public static class MeshBuilder
    {
        public static Mesh Annulus(float inner, float outer, int segments = 48)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * inner); v.Add(d * outer);
                if (i < segments)
                {
                    int k = i * 2;
                    t.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
                }
            }
            return Finish("Annulus", v, t, null);
        }

        // Cone along +Y with its base at y = 0.
        public static Mesh Cone(float radius, float height, int segments = 20)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                var top = new Vector3(0, height, 0);
                AddTri(v, t, n, p0, top, p1);
                AddTri(v, t, n, p0, p1, Vector3.zero);
            }
            return Finish("Cone", v, t, n);
        }

        // Prism along +Y from y0 to y1. Radii can differ at the two ends (frustum). Optional
        // alternating radius gives fluted handles.
        public static void Prism(List<Vector3> v, List<int> t, List<Vector3> n, int sides, float r0, float r1,
            float y0, float y1, float flute = 0f, bool caps = true, float rotation = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = rotation + i * Mathf.PI * 2f / sides, a1 = rotation + (i + 1) * Mathf.PI * 2f / sides;
                float f0 = flute > 0 && i % 2 == 0 ? 1f - flute : 1f, f1 = flute > 0 && (i + 1) % 2 == 0 ? 1f - flute : 1f;
                var b0 = new Vector3(Mathf.Cos(a0) * r0 * f0, y0, Mathf.Sin(a0) * r0 * f0);
                var b1 = new Vector3(Mathf.Cos(a1) * r0 * f1, y0, Mathf.Sin(a1) * r0 * f1);
                var c0 = new Vector3(Mathf.Cos(a0) * r1 * f0, y1, Mathf.Sin(a0) * r1 * f0);
                var c1 = new Vector3(Mathf.Cos(a1) * r1 * f1, y1, Mathf.Sin(a1) * r1 * f1);
                AddQuad(v, t, n, b0, c0, c1, b1);
                if (caps)
                {
                    AddTri(v, t, n, new Vector3(0, y1, 0), c1, c0);
                    AddTri(v, t, n, new Vector3(0, y0, 0), b0, b1);
                }
            }
        }

        public static void Box(List<Vector3> v, List<int> t, List<Vector3> n, Vector3 center, Vector3 size)
        {
            var e = size * .5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * e.x, y * e.y, z * e.z);
            AddQuad(v, t, n, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
            AddQuad(v, t, n, P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));
            AddQuad(v, t, n, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));
            AddQuad(v, t, n, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
            AddQuad(v, t, n, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            AddQuad(v, t, n, P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1));
        }

        // Square pyramid with apex at the origin opening along -Y (camera field of view).
        public static Mesh Frustum(float halfWidth, float halfDepth, float length)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>();
            var a = Vector3.zero;
            var p0 = new Vector3(-halfWidth, -length, -halfDepth); var p1 = new Vector3(halfWidth, -length, -halfDepth);
            var p2 = new Vector3(halfWidth, -length, halfDepth); var p3 = new Vector3(-halfWidth, -length, halfDepth);
            AddTri(v, t, n, a, p1, p0); AddTri(v, t, n, a, p2, p1); AddTri(v, t, n, a, p3, p2); AddTri(v, t, n, a, p0, p3);
            return Finish("Frustum", v, t, n);
        }

        public static void AddTri(List<Vector3> v, List<int> t, List<Vector3> n, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            int k = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            if (n != null) { n.Add(normal); n.Add(normal); n.Add(normal); }
            t.Add(k); t.Add(k + 1); t.Add(k + 2);
        }

        public static void AddQuad(List<Vector3> v, List<int> t, List<Vector3> n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(v, t, n, a, b, c);
            AddTri(v, t, n, a, c, d);
        }

        public static Mesh Finish(string name, List<Vector3> v, List<int> t, List<Vector3> n)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetTriangles(t, 0);
            if (n != null && n.Count == v.Count) m.SetNormals(n); else m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject Object(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
