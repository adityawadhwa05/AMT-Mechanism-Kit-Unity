using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AMT.AR.AR;
using AMT.AR.Core;
using AMT.AR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Automated Play-mode check of both scenes: waits for the model, opens every module, captures a
// screenshot of each (UI included) to Build/Screenshots, and reports every error or exception.
// Menu: AMT Kit > Run Smoke Test. Batch: -executeMethod AMTSmokeTest.RunBatch (exit code 0 = pass).
public static class AMTSmokeTest
{
    const string OutDir = "Build/Screenshots";
    static readonly List<string> errors = new List<string>();
    static readonly List<string> log = new List<string>();
    static readonly Queue<Func<bool>> steps = new Queue<Func<bool>>();
    static bool batch;
    static double waitUntil;
    static bool savedOptionsEnabled;
    static EnterPlayModeOptions savedOptions;

    [MenuItem("AMT Kit/Run Smoke Test")]
    public static void Run() => Begin(false, null);

    // Batch: add "-amtScene Viewer" or "-amtScene AR" to test one scene per Unity session.
    public static void RunBatch() => Begin(true, Argument("-amtScene"));

    static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    // Batch helper: compile happens on load; run validation and exit with its result.
    public static void ValidateBatch()
    {
        bool ok = ProjectValidation.RunValidationPublic();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void Begin(bool isBatch, string only)
    {
        bool viewer = only == null || only == "Viewer", ar = only == null || only == "AR";
        batch = isBatch;
        errors.Clear(); log.Clear(); steps.Clear();
        Directory.CreateDirectory(OutDir);
        // Keep static state alive across entering Play mode.
        savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;

        if (viewer) ViewerSteps();
        if (ar) ARSteps();
        steps.Enqueue(Finish);
    }

    static void ViewerSteps()
    {
        Scene("Assets/Scenes/Viewer.unity");
        WaitForKit(90);
        Capture("01_viewer_home");
        Module("components", 1.5f, "02_components");
        Do(() => { Shell().Current.OnPartTapped(Session().Kit.Get("rfid_head")); }, 1.2f);
        Capture("03_components_rfid_selected");
        Module("tools", 1.5f, "04_tools");
        Module("motion", 6.5f, "05_how_it_works");
        Module("exploded", 2.5f, "06_exploded");
        Module("assembly", 2.2f, "07_assembly_step1");
        Do(() => { Session().Guide.GoTo(8); }, 2.0f);
        Capture("08_assembly_step9");
        Module("disassembly", 3.5f, "09_disassembly");
        Do(() => { Shell().ShowHome(); }, .5f);
        Do(() => EditorApplication.isPlaying = false, 1f);
    }

    static void ARSteps()
    {
        Scene("Assets/Scenes/AR.unity");
        WaitForKit(90);
        Do(() => { }, 1.5f);
        Capture("10_ar_editor_scanning");
        Do(PlaceAR, 1.5f);
        Capture("11_ar_editor_placed_home");
        Module("motion", 4f, "12_ar_how_it_works");
        Do(() => EditorApplication.isPlaying = false, 1f);
    }

    // ---------- step builders ----------

    static void Scene(string path)
    {
        steps.Enqueue(() =>
        {
            if (EditorApplication.isPlaying) return false;
            EditorSceneManager.OpenScene(path);
            log.Add("Opened " + path);
            EditorApplication.isPlaying = true;
            return true;
        });
        steps.Enqueue(() => EditorApplication.isPlaying && Session() != null);
    }

    static void WaitForKit(float timeout)
    {
        double deadline = 0;
        steps.Enqueue(() =>
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + timeout;
            var s = Session();
            if (s != null && s.Kit.IsLoaded)
            {
                log.Add("Kit loaded: " + s.Kit.Parts.Count + " parts, size " + s.Kit.KitSize.ToString("F3") + " m");
                if (s.Kit.Parts.Count != 37) errors.Add("Expected 37 parts, got " + s.Kit.Parts.Count);
                return true;
            }
            if (s != null && s.Kit.Error != null) { errors.Add("Kit failed: " + s.Kit.Error); return true; }
            if (EditorApplication.timeSinceStartup > deadline) { errors.Add("Timed out waiting for the kit to load"); return true; }
            return false;
        });
    }

    static void Do(Action action, float wait)
    {
        steps.Enqueue(() =>
        {
            try { action(); }
            catch (Exception e) { errors.Add("Step failed: " + e); }
            waitUntil = EditorApplication.timeSinceStartup + wait;
            return true;
        });
        steps.Enqueue(() => EditorApplication.timeSinceStartup >= waitUntil);
    }

    static void Module(string key, float wait, string shot)
    {
        Do(() => { Shell().Open(key); log.Add("Opened module " + key); }, wait);
        Capture(shot);
    }

    // Overlay canvases do not render without a window, so for one frame the UI is drawn by the
    // main camera into a render texture.
    static void Capture(string name)
    {
        RenderTexture rt = null;
        Canvas canvas = null;
        Camera cam = null, uiCam = null;
        var layers = new Dictionary<GameObject, int>();
        Behaviour[] paused = null;
        steps.Enqueue(() =>
        {
            cam = Camera.main;
            canvas = Shell() != null ? Shell().GetComponentInChildren<Canvas>() : null;
            if (cam == null) { errors.Add("No main camera for " + name); return true; }
            paused = new Behaviour[] { cam.GetComponent("OrbitCamera") as Behaviour, UnityEngine.Object.FindFirstObjectByType<ViewerBootstrap>() };
            foreach (var b in paused) if (b != null) b.enabled = false;
            rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            if (canvas != null)
            {
                // A separate UI camera without the viewer's off-centre projection draws the canvas.
                uiCam = new GameObject("Smoke UI camera").AddComponent<Camera>();
                uiCam.clearFlags = CameraClearFlags.Depth; uiCam.cullingMask = 1 << 5;
                uiCam.orthographic = true; uiCam.targetTexture = rt; uiCam.enabled = false;
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) { layers[t.gameObject] = t.gameObject.layer; t.gameObject.layer = 5; }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCam;
                canvas.planeDistance = 1f;
            }
            return true;
        });
        int frames = 0;
        steps.Enqueue(() => ++frames >= 3);
        steps.Enqueue(() =>
        {
            if (cam == null) return true;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            if (uiCam != null) uiCam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            foreach (var pair in layers) if (pair.Key != null) pair.Key.layer = pair.Value;
            if (uiCam != null) UnityEngine.Object.DestroyImmediate(uiCam.gameObject);
            foreach (var b in paused) if (b != null) b.enabled = true;
            log.Add("Captured " + name);
            return true;
        });
    }

    static void PlaceAR()
    {
        var ar = UnityEngine.Object.FindFirstObjectByType<ARBootstrap>();
        if (ar == null) { errors.Add("ARBootstrap not found"); return; }
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ARBootstrap).GetField("hitValid", flags).SetValue(ar, true);
        typeof(ARBootstrap).GetField("hitPose", flags).SetValue(ar, new Pose(new Vector3(0, 0, .15f), Quaternion.identity));
        typeof(ARBootstrap).GetMethod("Place", flags).Invoke(ar, null);
        log.Add("AR kit placed (simulated)");
    }

    // ---------- runner ----------

    static TrainingSession Session() => UnityEngine.Object.FindFirstObjectByType<TrainingSession>();
    static TrainingShell Shell() => UnityEngine.Object.FindFirstObjectByType<TrainingShell>();

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(type + ": " + message + (type == LogType.Exception ? "\n" + stack : ""));
        else if (type == LogType.Warning && message.StartsWith("AMT")) log.Add("Warning: " + message);
    }

    static void Tick()
    {
        // Run as many ready steps as possible this tick, stopping at the first that is waiting.
        for (int guard = 0; guard < 8 && steps.Count > 0; guard++)
        {
            bool done;
            try { done = steps.Peek()(); }
            catch (Exception e) { errors.Add("Runner: " + e); done = true; }
            if (!done) return;
            steps.Dequeue();
        }
    }

    static bool Finish()
    {
        if (EditorApplication.isPlaying) return false;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        var report = new List<string> { "AMT smoke test — " + DateTime.Now.ToString("u"), "" };
        report.AddRange(log);
        report.Add("");
        report.Add(errors.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + errors.Count + " errors)");
        report.AddRange(errors);
        var tag = Argument("-amtScene");
        File.WriteAllLines(Path.Combine(OutDir, "smoke_report" + (tag != null ? "_" + tag : "") + ".txt"), report);
        Debug.Log(string.Join("\n", report));
        if (batch) EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        return true;
    }
}
