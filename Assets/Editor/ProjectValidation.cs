using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using AMT.AR.Data;

public static class ProjectValidation
{
#if UNITY_EDITOR_WIN
    [InitializeOnLoadMethod]
    static void ConfigureGradleEnvironment()
    {
        // Keep Gradle's native-library paths below Windows' 260-character limit.
        // Setting this in the Editor process also covers the Build Profiles button.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string gradleHome = Path.GetFullPath(Path.Combine(projectRoot, "..", "g"));
        Directory.CreateDirectory(gradleHome);
        Environment.SetEnvironmentVariable("GRADLE_USER_HOME", gradleHome,
            EnvironmentVariableTarget.Process);
        Debug.Log("AMT Android Gradle cache: " + gradleHome);
    }
#endif

    const string PartsGlb = "Assets/StreamingAssets/Model/MechanismKit_Parts.glb";

    [MenuItem("AMT Kit/Validate Project")]
    public static void Validate() { RunValidation(); }

    public static bool RunValidationPublic() => RunValidation();

    internal static bool RunValidation()
    {
        bool valid = true;
        void Error(string message) { Debug.LogError("AMT validation: " + message); valid = false; }

        foreach (var path in new[] {
            "Assets/Scenes/Viewer.unity", "Assets/Scenes/AR.unity",
            "Assets/StreamingAssets/Model/MechanismKit.glb", PartsGlb,
            "Assets/Resources/Data/components.json", "Assets/Resources/Data/tools.json", "Assets/Resources/Data/guide.json",
            "Assets/Resources/Fonts/Inter-Regular.ttf", "Assets/link.xml" })
            if (!File.Exists(path)) Error("missing required file " + path);

        foreach (var shader in new[] { "AMTKitSurface", "AMTGhost", "AMTUnlit", "AMTGround", "AMTARPlane" })
            if (Resources.Load<Shader>("Shaders/" + shader) == null)
                Error("shader Resources/Shaders/" + shader + " is missing or failed to compile.");

        const string gltfShaderPath =
            "Packages/com.unity.cloud.gltfast/Runtime/Shader/Built-In/glTFPbrMetallicRoughness.shader";
        string gltfShaderGuid = AssetDatabase.AssetPathToGUID(gltfShaderPath);
        if (string.IsNullOrEmpty(gltfShaderGuid) ||
            !File.ReadAllText("ProjectSettings/GraphicsSettings.asset").Contains(gltfShaderGuid))
            Error("the glTF metallic-roughness shader must be in Graphics Settings > Always Included Shaders.");

        valid &= ValidateData(Error);

        if (!XRPackageMetadataStore.IsLoaderAssigned("UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
            Error("ARCore provider is not enabled for Android. Run AMT Kit > Configure Android ARCore.");
        if (EditorBuildSettings.scenes.Length < 2)
            Error("both Viewer and AR scenes must be enabled in Build Settings.");

        if (valid) Debug.Log("AMT validation passed. Test the Viewer in Play mode and AR on an ARCore phone before release.");
        return valid;
    }

    // Every model node needs a component entry, every component a node, and every guide step must
    // reference known parts and tools, with each part fitted in exactly one step.
    static bool ValidateData(Action<string> error)
    {
        bool ok = true;
        void Fail(string m) { error(m); ok = false; }
        var components = ReadJson<ComponentFile>("Assets/Resources/Data/components.json", Fail);
        var tools = ReadJson<ToolFile>("Assets/Resources/Data/tools.json", Fail);
        var guide = ReadJson<GuideFile>("Assets/Resources/Data/guide.json", Fail);
        if (components == null || tools == null || guide == null) return false;

        var ids = new HashSet<string>();
        foreach (var c in components.components)
            if (!ids.Add(c.id)) Fail("duplicate component id " + c.id);
        var toolIds = new HashSet<string>();
        foreach (var t in tools.tools) toolIds.Add(t.id);

        var nodes = GlbNodeNames(PartsGlb, Fail);
        foreach (var n in nodes)
            if (!ids.Contains(n)) Fail("model node '" + n + "' has no entry in components.json");
        foreach (var id in ids)
            if (nodes.Count > 0 && !nodes.Contains(id)) Fail("component '" + id + "' has no node in " + PartsGlb);

        foreach (var c in components.components)
        {
            foreach (var t in c.tools) if (!toolIds.Contains(t)) Fail(c.id + " lists unknown tool " + t);
            foreach (var r in c.related) if (!ids.Contains(r)) Fail(c.id + " lists unknown related part " + r);
        }
        foreach (var t in tools.tools)
            foreach (var p in t.components) if (!ids.Contains(p)) Fail("tool " + t.id + " lists unknown part " + p);

        var fitted = new Dictionary<string, string>();
        foreach (var step in guide.steps)
        {
            if (!string.IsNullOrEmpty(step.tool) && !toolIds.Contains(step.tool)) Fail("guide step '" + step.title + "' uses unknown tool " + step.tool);
            foreach (var list in new[] { step.parts, step.fasteners })
            foreach (var p in list)
            {
                if (!ids.Contains(p)) Fail("guide step '" + step.title + "' lists unknown part " + p);
                else if (fitted.ContainsKey(p)) Fail(p + " appears in guide steps '" + fitted[p] + "' and '" + step.title + "'");
                else fitted[p] = step.title;
            }
        }
        foreach (var id in ids) if (!fitted.ContainsKey(id)) Fail("part " + id + " is not fitted in any guide step");
        foreach (var s in components.stations)
            if (!ids.Contains(s.part)) Fail("station " + s.index + " refers to unknown part " + s.part);
        return ok;
    }

    static T ReadJson<T>(string path, Action<string> fail) where T : class
    {
        try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
        catch (Exception e) { fail("cannot parse " + path + ": " + e.Message); return null; }
    }

    [Serializable] class GltfNode { public string name; public int[] children; }
    [Serializable] class GltfJson { public GltfNode[] nodes; }

    // Reads part node names (leaf nodes; the root node holds the parts) from the JSON chunk of a .glb file.
    static HashSet<string> GlbNodeNames(string path, Action<string> fail)
    {
        var names = new HashSet<string>();
        try
        {
            var bytes = File.ReadAllBytes(path);
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            var json = Encoding.UTF8.GetString(bytes, 20, jsonLength);
            var gltf = JsonUtility.FromJson<GltfJson>(json);
            foreach (var n in gltf.nodes)
                if ((n.children == null || n.children.Length == 0) && !string.IsNullOrEmpty(n.name)) names.Add(n.name);
        }
        catch (Exception e) { fail("cannot read node names from " + path + ": " + e.Message); }
        return names;
    }

    [MenuItem("AMT Kit/Build Android APK")]
    public static void BuildAndroid() { AMTBuildRecovery.BuildAndroidWithRecovery(); }
}
