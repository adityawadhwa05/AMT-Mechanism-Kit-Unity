using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AMTBuildRecovery
{
    static bool cacheMoveError;

    public static void BuildAndroidWithRecovery()
    {
        Directory.CreateDirectory("Build");
        AMTAndroidTools.ApplyEnvironment();
        ProjectSetup.ConfigureAndroid();
        if (!ProjectValidation.RunValidation())
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Viewer.unity", "Assets/Scenes/AR.unity" },
            locationPathName = "Build/AMTMechanismKit.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        for (int attempt = 1; attempt <= 8; attempt++)
        {
            cacheMoveError = false;
            Application.logMessageReceived += WatchBuildLog;
            BuildReport report = null;
            try { report = BuildPipeline.BuildPlayer(options); }
            catch (Exception error) { Debug.LogException(error); }
            finally { Application.logMessageReceived -= WatchBuildLog; }
            if (report != null && report.summary.result == BuildResult.Succeeded &&
                File.Exists(options.locationPathName))
            {
                Debug.Log("APK built on attempt " + attempt + ": " +
                    options.locationPathName + ". Install and test on an ARCore device.");
                return;
            }
            int recovered = cacheMoveError ? RecoverCompletedTransforms() : 0;
            Debug.LogWarning("AMT Android build attempt " + attempt + " failed: " +
                "Gradle cache move error=" + cacheMoveError +
                ", completed cache entries recovered=" + recovered);
            if (!cacheMoveError) break;
        }
        Debug.LogError("Android build failed. See the Console and Editor.log for exact errors.");
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }

    static void WatchBuildLog(string message, string stackTrace, LogType type)
    {
        if (message.Contains("Could not move temporary workspace"))
            cacheMoveError = true;
    }

    static int RecoverCompletedTransforms()
    {
        string home = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        if (string.IsNullOrEmpty(home)) return 0;
        string cache = Path.Combine(home, "caches", "8.11", "transforms");
        if (!Directory.Exists(cache)) return 0;
        int recovered = 0;
        var pattern = new Regex(@"^([0-9a-f]{32})-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
        foreach (var group in Directory.GetDirectories(cache).Select(path => new { path, name = Path.GetFileName(path) })
                     .Where(item => pattern.IsMatch(item.name))
                     .GroupBy(item => item.name.Substring(0, 32)))
        {
            string destination = Path.Combine(cache, group.Key);
            if (Directory.Exists(destination)) continue;
            foreach (var item in group.OrderByDescending(x => Directory.GetLastWriteTimeUtc(x.path)))
            {
                if (!File.Exists(Path.Combine(item.path, "results.bin")) ||
                    !File.Exists(Path.Combine(item.path, "metadata.bin"))) continue;
                try { Directory.Move(item.path, destination); recovered++; }
                catch (IOException) { }
                break;
            }
        }
        return recovered;
    }
}
