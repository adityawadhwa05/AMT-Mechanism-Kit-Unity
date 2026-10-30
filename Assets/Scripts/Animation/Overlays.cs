using System.Collections.Generic;
using AMT.AR.Core;
using UnityEngine;

namespace AMT.AR.Animation
{
    // A path drawn as a line with a cone head; used for motion directions and paths.
    public sealed class ArrowVisual
    {
        public readonly GameObject Root;
        readonly LineRenderer line;
        readonly Transform head;
        readonly Material material;
        float width;

        public ArrowVisual(string name, Transform parent, Color color, float width)
        {
            this.width = width;
            Root = new GameObject(name);
            Root.transform.SetParent(parent, false);
            material = KitMaterials.CreateUnlit(color, false, true);
            line = Root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.numCapVertices = 4; line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            head = MeshBuilder.Object("Head", Root.transform, MeshBuilder.Cone(1f, 1f, 18), material).transform;
            SetWidth(width);
            Root.SetActive(false);
        }

        public void SetWidth(float w)
        {
            width = w;
            line.startWidth = line.endWidth = w;
            head.localScale = new Vector3(w * 2.4f, w * 4.5f, w * 2.4f);
        }

        public void SetColor(Color c) { material.color = c; }

        public void SetPath(IList<Vector3> points)
        {
            if (points.Count < 2) { Root.SetActive(false); return; }
            Root.SetActive(true);
            // Stop the line where the head starts so they do not overlap.
            var end = points[points.Count - 1];
            var dir = (end - points[points.Count - 2]).normalized;
            float headLength = width * 4.5f;
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count - 1; i++) line.SetPosition(i, points[i]);
            line.SetPosition(points.Count - 1, end - dir * headLength * .9f);
            head.position = end - dir * headLength;
            head.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        public void SetStraight(Vector3 from, Vector3 to) => SetPath(new[] { from, to });

        // Arc around a centre, in the plane perpendicular to axis, from one direction through an angle.
        public void SetArc(Vector3 center, Vector3 axis, Vector3 fromDir, float radius, float degrees, int steps = 32)
        {
            var pts = new Vector3[steps + 1];
            fromDir = Vector3.ProjectOnPlane(fromDir, axis).normalized;
            for (int i = 0; i <= steps; i++)
                pts[i] = center + Quaternion.AngleAxis(degrees * i / steps, axis) * fromDir * radius;
            SetPath(pts);
        }

        public void Hide() => Root.SetActive(false);

        public void Destroy()
        {
            Object.Destroy(Root);
            Object.Destroy(material);
        }
    }

    // Station effects for the working-cycle demonstration: RFID field rings, distance beam and
    // camera field of view. All are illustrative visuals, not measured data.
    public sealed class StationEffects
    {
        readonly GameObject root;
        readonly Transform[] rings = new Transform[3];
        readonly Material ringMaterial, beamMaterial, frustumMaterial;
        readonly Transform beam, beamSpot, frustum;
        readonly LineRenderer frustumEdges;

        public StationEffects(Transform parent)
        {
            root = new GameObject("Station effects (illustrative)");
            root.transform.SetParent(parent, false);
            ringMaterial = KitMaterials.CreateUnlit(Theme.Info.WithAlpha(.7f), true);
            var ringMesh = MeshBuilder.Annulus(.92f, 1f, 48);
            for (int i = 0; i < rings.Length; i++)
                rings[i] = MeshBuilder.Object("RFID field ring " + (i + 1), root.transform, ringMesh, ringMaterial).transform;

            beamMaterial = KitMaterials.CreateUnlit(new Color(1f, .3f, .25f, .9f), true);
            var beamMesh = new List<Vector3>(); var bt = new List<int>(); var bn = new List<Vector3>();
            MeshBuilder.Prism(beamMesh, bt, bn, 10, 1f, 1f, 0f, 1f);
            beam = MeshBuilder.Object("Distance sensor beam", root.transform, MeshBuilder.Finish("Beam", beamMesh, bt, bn), beamMaterial).transform;
            beamSpot = MeshBuilder.Object("Beam spot", root.transform, MeshBuilder.Annulus(0f, 1f, 24), beamMaterial).transform;

            frustumMaterial = KitMaterials.CreateUnlit(Theme.Info.WithAlpha(.12f), true);
            frustum = MeshBuilder.Object("Camera field of view", root.transform, MeshBuilder.Frustum(1f, 1f, 1f), frustumMaterial).transform;
            frustumEdges = frustum.gameObject.AddComponent<LineRenderer>();
            frustumEdges.sharedMaterial = KitMaterials.CreateUnlit(Theme.Info.WithAlpha(.8f), true);
            frustumEdges.useWorldSpace = false;
            frustumEdges.positionCount = 10;
            frustumEdges.SetPositions(new[] {
                Vector3.zero, new Vector3(-1, -1, -1), new Vector3(1, -1, -1), Vector3.zero, new Vector3(1, -1, 1),
                new Vector3(-1, -1, 1), Vector3.zero, new Vector3(-1, -1, -1), new Vector3(-1, -1, 1), new Vector3(1, -1, 1) });
            HideAll();
        }

        public void HideAll()
        {
            foreach (var r in rings) r.gameObject.SetActive(false);
            beam.gameObject.SetActive(false); beamSpot.gameObject.SetActive(false);
            frustum.gameObject.SetActive(false);
        }

        // Expanding rings travelling from the RFID head to the product.
        public void Rfid(Vector3 head, Vector3 target, float time, float scale)
        {
            var dir = target - head;
            for (int i = 0; i < rings.Length; i++)
            {
                float phase = Mathf.Repeat(time * .9f + i / (float)rings.Length, 1f);
                var r = rings[i];
                r.gameObject.SetActive(true);
                r.position = Vector3.Lerp(head, target, phase);
                r.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                r.localScale = Vector3.one * Mathf.Lerp(.012f, .03f, phase) * scale;
            }
            ringMaterial.color = Theme.Info.WithAlpha(.75f);
        }

        public void Beam(Vector3 from, Vector3 to, float scale, float flicker)
        {
            var dir = to - from;
            beam.gameObject.SetActive(true); beamSpot.gameObject.SetActive(true);
            beam.position = from;
            beam.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            beam.localScale = new Vector3(.0012f * scale, dir.magnitude, .0012f * scale);
            beamSpot.position = to - dir.normalized * .0008f * scale;
            beamSpot.rotation = Quaternion.FromToRotation(Vector3.up, -dir.normalized);
            beamSpot.localScale = Vector3.one * (.0045f + .0015f * flicker) * scale;
        }

        public void CameraView(Vector3 lens, Vector3 target, float scale, float pulse)
        {
            var dir = target - lens;
            frustum.gameObject.SetActive(true);
            frustum.position = lens;
            frustum.rotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
            float half = .03f * scale;
            frustum.localScale = new Vector3(half, dir.magnitude, half * .75f);
            frustumMaterial.color = Theme.Info.WithAlpha(.06f + .1f * pulse);
        }

        public void Destroy()
        {
            Object.Destroy(root);
            Object.Destroy(ringMaterial); Object.Destroy(beamMaterial); Object.Destroy(frustumMaterial);
        }
    }
}
