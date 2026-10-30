using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AMT.AR.Data;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace AMT.AR.Core
{
    // Loads MechanismKit_Parts.glb (the supplied model split into its named CAD parts), builds the
    // part registry and owns how each part is drawn: normal, highlighted, ghosted or hidden.
    public sealed class KitModel : MonoBehaviour
    {
        public const string PartsFile = "Model/MechanismKit_Parts.glb";

        public Transform Root { get; private set; }
        public Transform TablePivot { get; private set; }
        public bool IsLoaded { get; private set; }
        public float Progress { get; private set; }
        public string Error { get; private set; }
        public IReadOnlyList<KitPart> Parts => parts;
        public Vector3 KitCenter { get; private set; }
        public Vector3 KitSize { get; private set; }
        public bool StudioColors { get; private set; } = true;
        public event Action Loaded;
        public event Action<string> Failed;

        readonly List<KitPart> parts = new List<KitPart>();
        readonly Dictionary<string, KitPart> byId = new Dictionary<string, KitPart>();
        readonly Dictionary<Collider, KitPart> byCollider = new Dictionary<Collider, KitPart>();
        readonly Dictionary<Material, (Material studio, Material original)> finishes =
            new Dictionary<Material, (Material, Material)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        MaterialPropertyBlock block;
        GltfImport import;
        KitDatabase db;

        public KitPart Get(string id) => id != null && byId.TryGetValue(id, out var p) ? p : null;

        public KitPart FromCollider(Collider c)
        {
            if (c == null) return null;
            if (byCollider.TryGetValue(c, out var p)) return p;
            return null;
        }

        public void BeginLoad(KitDatabase database)
        {
            db = database;
            if (Root == null)
            {
                Root = new GameObject("AMT Kit").transform;
                Root.SetParent(transform, false);
            }
            StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            IsLoaded = false; Error = null; Progress = .05f;
            string path = Path.Combine(Application.streamingAssetsPath, PartsFile);
            // Android StreamingAssets is a jar URI; desktop paths need escaping (spaces in folders).
            string uri = path.Contains("://") ? path : new Uri(path).AbsoluteUri;
            byte[] bytes;
            using (var request = UnityWebRequest.Get(uri))
            {
                var op = request.SendWebRequest();
                while (!op.isDone) { Progress = .05f + .25f * request.downloadProgress; yield return null; }
                if (request.result != UnityWebRequest.Result.Success)
                { Fail("Could not read " + PartsFile + ": " + request.error); yield break; }
                bytes = request.downloadHandler.data;
            }
            Progress = .35f;
            import = new GltfImport();
            var loadTask = import.Load(bytes);
            while (!loadTask.IsCompleted) yield return null;
            if (loadTask.IsFaulted || loadTask.IsCanceled || !loadTask.Result)
            { Fail("The model could not be decoded. Check that glTFast and Draco are installed."); yield break; }
            Progress = .7f;
            var instanceTask = import.InstantiateMainSceneAsync(Root);
            while (!instanceTask.IsCompleted) yield return null;
            if (instanceTask.IsFaulted || instanceTask.IsCanceled || !instanceTask.Result)
            { Fail("The model could not be instantiated."); yield break; }
            Progress = .85f;
            yield return null;
            try { Register(); }
            catch (Exception e) { Debug.LogException(e); Fail("Model setup failed: " + e.Message); yield break; }
            Progress = 1f;
            IsLoaded = true;
            Loaded?.Invoke();
        }

        void Fail(string message)
        {
            Error = message;
            Debug.LogError("AMT: " + message);
            Failed?.Invoke(message);
        }

        void Register()
        {
            block = new MaterialPropertyBlock();
            var nodes = new Dictionary<string, Transform>();
            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
                if (!nodes.ContainsKey(t.name)) nodes[t.name] = t;

            foreach (var info in db.AllComponents)
            {
                if (!nodes.TryGetValue(info.id, out var node))
                {
                    Debug.LogWarning("AMT: component '" + info.id + "' has no node in " + PartsFile);
                    continue;
                }
                var renderers = node.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var part = new KitPart { Id = info.id, Info = info, Transform = node, Renderers = renderers };
                part.Solid = new Material[renderers.Length][];
                part.Ghosted = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    var source = r.sharedMaterials;
                    var mats = new Material[source.Length];
                    var ghosts = new Material[source.Length];
                    for (int m = 0; m < source.Length; m++) { mats[m] = Finish(source[m]); ghosts[m] = KitMaterials.Ghost; }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                    part.Solid[i] = mats;
                    part.Ghosted[i] = ghosts;
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        var collider = r.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                        byCollider[collider] = part;
                    }
                }
                parts.Add(part);
                byId[part.Id] = part;
            }
            foreach (var t in nodes.Values)
                if (t.GetComponent<Renderer>() != null && !byId.ContainsKey(t.name) && t != Root && !UnderPart(t))
                    Debug.LogWarning("AMT: model node '" + t.name + "' has no entry in components.json and is not selectable.");
            if (parts.Count == 0)
            {
                var sample = new System.Text.StringBuilder("AMT: no parts matched. Instantiated hierarchy (first 40):");
                int n = 0;
                foreach (var t in Root.GetComponentsInChildren<Transform>(true))
                {
                    sample.Append("\n  ").Append(t.name).Append(" (children ").Append(t.childCount)
                          .Append(", renderer ").Append(t.GetComponent<Renderer>() != null).Append(')');
                    if (++n >= 40) break;
                }
                Debug.LogWarning(sample.ToString());
            }

            // Whole-kit bounds in kit space, then each part's rest centre.
            var kitBounds = LocalBounds(parts);
            KitCenter = kitBounds.center;
            KitSize = kitBounds.size;
            foreach (var p in parts)
            {
                var b = LocalBounds(new[] { p });
                p.KitCenter = b.center;
                p.KitSize = b.size;
            }

            // The table pivot sits on the disc axis; parts that turn with the table are re-parented to it.
            var disc = Get("index_disc");
            TablePivot = new GameObject("Table pivot (disc axis)").transform;
            TablePivot.SetParent(Root, false);
            TablePivot.localPosition = disc != null ? disc.KitCenter : KitCenter;
            foreach (var p in parts)
                if (p.Info.rotatesWithTable) p.Transform.SetParent(TablePivot, true);
            foreach (var p in parts)
            {
                p.HomeLocalPosition = p.Transform.localPosition;
                p.HomeLocalRotation = p.Transform.localRotation;
                if (p.IsFastener) p.Hotspots(Root);
            }
            ApplyAll(PartVisual.Normal);
        }

        bool UnderPart(Transform t)
        {
            for (var p = t.parent; p != null && p != Root; p = p.parent) if (byId.ContainsKey(p.name)) return true;
            return false;
        }

        Bounds LocalBounds(IEnumerable<KitPart> set)
        {
            bool first = true;
            var b = new Bounds();
            foreach (var p in set)
            foreach (var r in p.Renderers)
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mb = filter.sharedMesh.bounds;
                // Transform the 8 corners of the mesh bounds into kit space.
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var k = Root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(k, Vector3.zero); first = false; }
                    else b.Encapsulate(k);
                }
            }
            return b;
        }

        Material Finish(Material source)
        {
            if (source == null) return KitMaterials.CreateSurface(Color.gray, .2f, .4f);
            if (finishes.TryGetValue(source, out var pair)) return StudioColors ? pair.studio : pair.original;
            Material studio, original;
            if (KitMaterials.TryGetFinish(source.name, out var f))
            {
                studio = KitMaterials.CreateSurface(f.Studio, f.Metallic, f.Smoothness);
                original = KitMaterials.CreateSurface(f.Original, f.Metallic, f.Smoothness);
            }
            else
            {
                var c = KitMaterials.ReadBaseColor(source);
                studio = KitMaterials.CreateSurface(c, .2f, .45f);
                original = KitMaterials.CreateSurface(c, .2f, .45f);
            }
            studio.name = source.name + " (studio)";
            original.name = source.name + " (original)";
            owned.Add(studio); owned.Add(original);
            finishes[source] = (studio, original);
            return StudioColors ? studio : original;
        }

        public void SetStudioColors(bool studio)
        {
            if (StudioColors == studio) return;
            StudioColors = studio;
            foreach (var p in parts)
                for (int i = 0; i < p.Solid.Length; i++)
                    for (int m = 0; m < p.Solid[i].Length; m++)
                        foreach (var pair in finishes.Values)
                            if (p.Solid[i][m] == pair.studio || p.Solid[i][m] == pair.original)
                            { p.Solid[i][m] = studio ? pair.studio : pair.original; break; }
            foreach (var p in parts) { var v = p.Visual; p.Visual = PartVisual.Hidden; SetVisual(p, v); }
        }

        public void SetVisual(KitPart part, PartVisual visual)
        {
            if (part == null || part.Visual == visual) return;
            part.Visual = visual;
            for (int i = 0; i < part.Renderers.Length; i++)
            {
                var r = part.Renderers[i];
                if (r == null) continue;
                r.enabled = visual != PartVisual.Hidden;
                r.sharedMaterials = visual == PartVisual.Ghost ? part.Ghosted[i] : part.Solid[i];
                r.shadowCastingMode = visual == PartVisual.Ghost
                    ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                block.Clear();
                if (visual == PartVisual.Highlight)
                {
                    block.SetFloat(KitMaterials.HighlightId, 1f);
                    block.SetColor(KitMaterials.HighlightColorId,
                        highlightOverrides.TryGetValue(part, out var custom) ? custom : Theme.Highlight);
                }
                else if (visual == PartVisual.Related)
                {
                    block.SetFloat(KitMaterials.HighlightId, .55f);
                    block.SetColor(KitMaterials.HighlightColorId, Theme.Related);
                }
                r.SetPropertyBlock(visual == PartVisual.Highlight || visual == PartVisual.Related ? block : null);
                var collider = r.GetComponent<Collider>();
                if (collider != null) collider.enabled = visual != PartVisual.Hidden && visual != PartVisual.Ghost;
            }
        }

        readonly Dictionary<KitPart, Color> highlightOverrides = new Dictionary<KitPart, Color>();

        // Draws a highlighted part in a specific colour (for example red for a rejected product).
        public void SetHighlightColor(KitPart part, Color color)
        {
            if (part == null) return;
            highlightOverrides[part] = color;
            if (part.Visual == PartVisual.Highlight) { part.Visual = PartVisual.Normal; SetVisual(part, PartVisual.Highlight); }
        }

        public void ClearHighlightOverrides()
        {
            var changed = new List<KitPart>(highlightOverrides.Keys);
            highlightOverrides.Clear();
            foreach (var p in changed)
                if (p.Visual == PartVisual.Highlight) { p.Visual = PartVisual.Normal; SetVisual(p, PartVisual.Highlight); }
        }

        public void ApplyAll(PartVisual visual)
        {
            foreach (var p in parts) SetVisual(p, visual);
        }

        public void Apply(Func<KitPart, PartVisual> rule)
        {
            foreach (var p in parts) SetVisual(p, rule(p));
        }

        public void ResetPoses()
        {
            if (TablePivot != null) TablePivot.localRotation = Quaternion.identity;
            foreach (var p in parts) p.ResetPose();
        }

        // Moves a part by an offset expressed in kit space (metres), from its rest pose.
        public void OffsetPart(KitPart p, Vector3 kitOffset)
        {
            var parent = p.Transform.parent;
            p.Transform.localPosition = p.HomeLocalPosition +
                parent.InverseTransformVector(Root.TransformVector(kitOffset));
        }

        public Bounds WorldBounds(IEnumerable<KitPart> set = null)
        {
            bool first = true;
            var b = new Bounds(Root != null ? Root.position : Vector3.zero, Vector3.zero);
            foreach (var p in set ?? parts)
            {
                if (p.Visual == PartVisual.Hidden) continue;
                var pb = p.WorldBounds();
                if (first) { b = pb; first = false; } else b.Encapsulate(pb);
            }
            return b;
        }

        public Vector3 KitToWorld(Vector3 kitPoint) => Root.TransformPoint(kitPoint);

        void OnDestroy()
        {
            foreach (var o in owned) if (o != null) Destroy(o);
            import?.Dispose();
        }
    }
}
