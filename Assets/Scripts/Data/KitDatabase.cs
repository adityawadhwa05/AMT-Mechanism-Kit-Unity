using System;
using System.Collections.Generic;
using UnityEngine;

namespace AMT.AR.Data
{
    // Editable training content. The JSON files live in Assets/Resources/Data and are loaded at
    // runtime, so names, descriptions, tools and guide steps can be changed without touching code.

    [Serializable] public class ComponentInfo
    {
        public string id;
        public string name;
        public string category;
        public int station;
        public string summary;
        [TextArea] public string function;
        [TextArea] public string interactions;
        [TextArea] public string principle;
        public string location;
        public string[] tools = new string[0];
        public string[] related = new string[0];
        // "published" = role stated in the kit description; "geometry" = identified from CAD shape only.
        public string verification;
        // Exploded-view offsets in metres at 100 %: straight up, and outward from the kit centre.
        public float explodeUp;
        public float explodeOut;
        public bool rotatesWithTable;

        public bool IsFastener => category == "Fasteners";
        public bool IsPublished => verification == "published";
    }

    [Serializable] public class StationInfo
    {
        public int index;
        public string name;
        public string part;
        [TextArea] public string explanation;
    }

    [Serializable] public class ComponentFile
    {
        public string source;
        public ComponentInfo[] components = new ComponentInfo[0];
        public StationInfo[] stations = new StationInfo[0];
    }

    [Serializable] public class ToolInfo
    {
        public string id;
        public string name;
        public string model;
        public string purpose;
        [TextArea] public string use;
        [TextArea] public string reason;
        public string[] safety = new string[0];
        public string[] components = new string[0];
        public string verification;
    }

    [Serializable] public class ToolFile
    {
        public string source;
        public ToolInfo[] tools = new ToolInfo[0];
    }

    [Serializable] public class GuideStep
    {
        public string title;
        [TextArea] public string assemble;
        [TextArea] public string disassemble;
        [TextArea] public string explanation;
        public string[] parts = new string[0];
        public string[] fasteners = new string[0];
        public string tool;
        // Direction the parts arrive from, in kit space (Y up). Zero = outward from the kit centre.
        public Vector3 approach;
    }

    [Serializable] public class GuideFile
    {
        public bool verified;
        public string source;
        public GuideStep[] steps = new GuideStep[0];
    }

    public sealed class KitDatabase
    {
        public ComponentFile Components { get; private set; }
        public ToolFile Tools { get; private set; }
        public GuideFile Guide { get; private set; }
        public readonly List<string> Errors = new List<string>();

        readonly Dictionary<string, ComponentInfo> components = new Dictionary<string, ComponentInfo>();
        readonly Dictionary<string, ToolInfo> tools = new Dictionary<string, ToolInfo>();

        public static readonly string[] CategoryOrder =
            { "Motion", "Sensing", "Actuation", "Product", "Structure", "Electrical", "Fasteners" };

        public static KitDatabase Load()
        {
            var db = new KitDatabase();
            db.Components = db.Read<ComponentFile>("Data/components") ?? new ComponentFile();
            db.Tools = db.Read<ToolFile>("Data/tools") ?? new ToolFile();
            db.Guide = db.Read<GuideFile>("Data/guide") ?? new GuideFile();
            foreach (var c in db.Components.components)
            {
                if (c == null || string.IsNullOrEmpty(c.id)) { db.Errors.Add("Component without an id."); continue; }
                if (db.components.ContainsKey(c.id)) db.Errors.Add("Duplicate component id: " + c.id);
                db.components[c.id] = c;
            }
            foreach (var t in db.Tools.tools)
                if (t != null && !string.IsNullOrEmpty(t.id)) db.tools[t.id] = t;
            return db;
        }

        T Read<T>(string path) where T : class
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) { Errors.Add("Missing Resources/" + path + ".json"); return null; }
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (Exception e) { Errors.Add("Could not parse " + path + ".json: " + e.Message); return null; }
        }

        public ComponentInfo Component(string id) =>
            id != null && components.TryGetValue(id, out var c) ? c : null;

        public ToolInfo Tool(string id) =>
            id != null && tools.TryGetValue(id, out var t) ? t : null;

        public IEnumerable<ComponentInfo> AllComponents => Components.components;

        public StationInfo Station(int index)
        {
            foreach (var s in Components.stations) if (s.index == index) return s;
            return null;
        }

        public static string VerificationLabel(string verification) =>
            verification == "published" ? "Role from kit description" :
            verification == "geometry" ? "Identified from CAD shape — confirm on kit" :
            "Unverified";
    }
}
