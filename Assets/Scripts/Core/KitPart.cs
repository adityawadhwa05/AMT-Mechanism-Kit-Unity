using System.Collections.Generic;
using AMT.AR.Data;
using UnityEngine;

namespace AMT.AR.Core
{
    public enum PartVisual { Normal, Highlight, Related, Ghost, Hidden }

    // One named node of MechanismKit_Parts.glb together with its training data and rest pose.
    public sealed class KitPart
    {
        public string Id;
        public ComponentInfo Info;
        public Transform Transform;
        public Renderer[] Renderers;
        public Material[][] Solid;
        public Material[][] Ghosted;
        public Vector3 HomeLocalPosition;
        public Quaternion HomeLocalRotation;
        // Centre of the part at rest, in the kit root's local space (metres, Y up).
        public Vector3 KitCenter;
        public Vector3 KitSize;
        // Starts unset so the first SetVisual call always applies.
        public PartVisual Visual = (PartVisual)(-1);
        List<Vector3> hotspots;

        public string DisplayName => Info != null && !string.IsNullOrEmpty(Info.name) ? Info.name : Id;
        public bool IsFastener => Id.EndsWith("__fasteners") || (Info != null && Info.IsFastener);

        public void ResetPose()
        {
            Transform.localPosition = HomeLocalPosition;
            Transform.localRotation = HomeLocalRotation;
        }

        public Bounds WorldBounds()
        {
            var b = new Bounds(Transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in Renderers)
            {
                if (r == null) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // Clusters of mesh vertices (in kit space) — for a fastener part, roughly one per screw.
        // Used to place the tool on real screw positions in the guide animations.
        public IReadOnlyList<Vector3> Hotspots(Transform kitRoot, int max = 6)
        {
            if (hotspots != null) return hotspots;
            hotspots = new List<Vector3>();
            const float cell = 0.008f;
            var cells = new Dictionary<Vector3Int, Vector3>();
            var counts = new Dictionary<Vector3Int, int>();
            foreach (var r in Renderers)
            {
                var filter = r != null ? r.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    var p = kitRoot.InverseTransformPoint(r.transform.TransformPoint(vertices[i]));
                    var key = new Vector3Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
                    cells[key] = cells.TryGetValue(key, out var sum) ? sum + p : p;
                    counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            }
            // Flood-fill neighbouring cells into clusters.
            var seen = new HashSet<Vector3Int>();
            var clusters = new List<(Vector3 center, float top)>();
            var stack = new Stack<Vector3Int>();
            foreach (var start in cells.Keys)
            {
                if (!seen.Add(start)) continue;
                stack.Push(start);
                Vector3 sum = Vector3.zero; int n = 0; float top = float.MinValue;
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    sum += cells[c]; n += counts[c];
                    top = Mathf.Max(top, (c.y + 1) * cell);
                    for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        var nb = new Vector3Int(c.x + x, c.y + y, c.z + z);
                        if (cells.ContainsKey(nb) && seen.Add(nb)) stack.Push(nb);
                    }
                }
                var center = sum / Mathf.Max(1, n);
                clusters.Add((new Vector3(center.x, top, center.z), top));
            }
            // Prefer the most exposed (highest) screws so the tool approach is visible.
            clusters.Sort((a, b) => b.top.CompareTo(a.top));
            for (int i = 0; i < clusters.Count && hotspots.Count < max; i++) hotspots.Add(clusters[i].center);
            if (hotspots.Count == 0) hotspots.Add(KitCenter + Vector3.up * KitSize.y * .5f);
            return hotspots;
        }
    }
}
