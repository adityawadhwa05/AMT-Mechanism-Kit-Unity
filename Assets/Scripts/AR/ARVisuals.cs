using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace AMT.AR.AR
{
    // Visual helpers for AR placement: detected-plane template, placement reticle with the kit's
    // real footprint, and a shadow catcher under the placed kit.
    public static class ARVisuals
    {
        // Inactive template the plane manager instantiates for each detected surface.
        public static GameObject PlaneTemplate(Transform templatesRoot)
        {
            var go = new GameObject("Detected plane");
            go.transform.SetParent(templatesRoot, false);
            go.AddComponent<ARPlane>();
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(KitMaterials.ARPlane);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = KitMaterials.CreateUnlit(Theme.Accent.WithAlpha(.55f));
            line.useWorldSpace = false; line.loop = true;
            line.startWidth = line.endWidth = .004f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<ARPlaneMeshVisualizer>();
            return go;
        }

        public sealed class Reticle
        {
            public readonly GameObject Root;
            readonly LineRenderer footprint;
            readonly Material ring, fill;

            public Reticle(Vector3 kitFootprint)
            {
                Root = new GameObject("Placement reticle");
                ring = KitMaterials.CreateUnlit(Theme.Accent, false, false);
                fill = KitMaterials.CreateUnlit(Theme.Accent.WithAlpha(.08f));
                MeshBuilder.Object("Ring", Root.transform, MeshBuilder.Annulus(.045f, .055f, 48), ring);
                MeshBuilder.Object("Centre", Root.transform, MeshBuilder.Annulus(0f, .008f, 20), ring);
                var area = MeshBuilder.Object("Footprint fill", Root.transform, Quad(), fill);
                area.transform.localScale = new Vector3(kitFootprint.x, 1, kitFootprint.z);
                footprint = new GameObject("Footprint").AddComponent<LineRenderer>();
                footprint.transform.SetParent(Root.transform, false);
                footprint.sharedMaterial = ring;
                footprint.useWorldSpace = false; footprint.loop = true;
                footprint.startWidth = footprint.endWidth = .003f;
                float hx = kitFootprint.x * .5f, hz = kitFootprint.z * .5f;
                footprint.positionCount = 4;
                footprint.SetPositions(new[] { new Vector3(-hx, .001f, -hz), new Vector3(hx, .001f, -hz),
                    new Vector3(hx, .001f, hz), new Vector3(-hx, .001f, hz) });
                Root.SetActive(false);
            }

            public void SetScale(float s) { Root.transform.localScale = Vector3.one * s; }

            public void Place(Vector3 position, Quaternion rotation, bool valid)
            {
                Root.SetActive(true);
                Root.transform.SetPositionAndRotation(position + Vector3.up * .002f, rotation);
                var c = valid ? Theme.Accent : Theme.TextMuted;
                ring.color = c; fill.color = c.WithAlpha(valid ? .09f : .04f);
            }

            public void Hide() => Root.SetActive(false);

            static Mesh Quad()
            {
                var v = new List<Vector3> { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
                return MeshBuilder.Finish("Quad", v, new List<int> { 0, 1, 2, 0, 2, 3 }, null);
            }
        }

        public static GameObject ShadowCatcher(Transform kitRoot, Vector3 kitSize)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Shadow catcher";
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(kitRoot, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            quad.transform.localPosition = new Vector3(0, .001f, 0);
            float size = Mathf.Max(kitSize.x, kitSize.z) * 1.9f;
            quad.transform.localScale = new Vector3(size, size, 1);
            var mat = new Material(KitMaterials.Ground);
            mat.SetFloat("_BaseAlpha", 0f);
            mat.SetFloat("_ShadowStrength", .5f);
            mat.SetFloat("_Fade", .45f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            return quad;
        }
    }
}
