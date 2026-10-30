using System.Collections.Generic;
using AMT.AR.Core;
using AMT.AR.Data;
using UnityEngine;

namespace AMT.AR.Tools
{
    // Tools module: shows the selected tool beside the kit, highlights the parts it is used on and
    // can demonstrate it on the real screw positions of those parts.
    public sealed class ToolShowcase : MonoBehaviour
    {
        TrainingSession session;
        ToolRig rig;
        GameObject display;
        Transform displayAnchor;
        readonly Dictionary<string, GameObject> displays = new Dictionary<string, GameObject>();
        ToolInfo tool;
        bool demonstrating;
        float demoTime;
        readonly List<(KitPart part, Vector3 kitPoint)> demoPoints = new List<(KitPart, Vector3)>();

        public ToolInfo Current => tool;
        public bool Demonstrating => demonstrating;

        public void Init(TrainingSession s) { session = s; }

        public void Show(ToolInfo info)
        {
            var kit = session.Kit;
            if (!kit.IsLoaded || info == null) return;
            tool = info;
            demonstrating = false;
            rig?.Hide();
            if (displayAnchor == null)
            {
                displayAnchor = new GameObject("Tool display").transform;
                displayAnchor.SetParent(kit.Root, false);
                rig = new ToolRig(session.Overlays);
            }
            // Float the tool above the front corner of the kit, enlarged for readability.
            displayAnchor.localPosition = kit.KitCenter + new Vector3(0, kit.KitSize.y * .62f, -kit.KitSize.z * .62f);
            foreach (var d in displays.Values) d.SetActive(false);
            if (!displays.TryGetValue(info.model, out display) || display == null)
            {
                display = ToolModels.Create(info.model, displayAnchor);
                displays[info.model] = display;
            }
            display.SetActive(true);
            display.transform.localScale = Vector3.one * 2.2f;
            var linked = new HashSet<string>(info.components);
            kit.Apply(p => linked.Contains(p.Id) ? PartVisual.Highlight : PartVisual.Ghost);
        }

        public void Demonstrate()
        {
            if (tool == null) return;
            var kit = session.Kit;
            demoPoints.Clear();
            foreach (var id in tool.components)
            {
                var p = kit.Get(id);
                if (p == null) continue;
                if (p.IsFastener) foreach (var h in p.Hotspots(kit.Root, 3)) demoPoints.Add((p, h));
                else demoPoints.Add((p, p.KitCenter + Vector3.up * p.KitSize.y * .5f));
                if (demoPoints.Count >= 4) break;
            }
            if (demoPoints.Count == 0) return;
            demonstrating = true; demoTime = 0;
            rig.Show(tool.model);
            if (display != null) display.SetActive(false);
            var parts = new List<KitPart>();
            foreach (var d in demoPoints) parts.Add(d.part);
            session.FocusParts(parts);
        }

        public void Hide()
        {
            tool = null; demonstrating = false;
            rig?.Hide();
            if (display != null) display.SetActive(false);
        }

        void Update()
        {
            if (tool == null) return;
            if (!demonstrating)
            {
                if (display != null && display.activeSelf)
                    display.transform.localRotation = Quaternion.Euler(-28f, Time.time * 35f, 18f);
                return;
            }
            demoTime += Time.deltaTime;
            const float perPoint = 1.6f;
            int index = Mathf.FloorToInt(demoTime / perPoint) % demoPoints.Count;
            float phase = (demoTime % perPoint) / perPoint;
            var point = demoPoints[index];
            float scale = session.View != null ? session.View.KitScale : 1f;
            rig.Pose(session.Kit.KitToWorld(point.kitPoint), session.Kit.Root.up, phase, true, scale, 1.4f);
        }

        void OnDestroy() { rig?.Destroy(); }
    }
}
