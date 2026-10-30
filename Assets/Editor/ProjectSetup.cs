using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using AMT.AR.Core;
using AMT.AR.AR;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
using UnityEngine.Rendering;

public static class ProjectSetup
{
    [MenuItem("AMT Kit/Create Editable Viewer Scene")]
    public static void CreateViewer()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("AMT Viewer Bootstrap").AddComponent<ViewerBootstrap>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Viewer.unity");
        var arScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("AMT AR Bootstrap").AddComponent<ARBootstrap>();
        EditorSceneManager.SaveScene(arScene, "Assets/Scenes/AR.unity");
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/Viewer.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AR.unity", true)
        };
        Debug.Log("AMT Viewer scene created and added to Build Settings.");
    }

    [InitializeOnLoadMethod]
    private static void CreateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists("Assets/Scenes/Viewer.unity") || EditorApplication.isPlaying) return;
            CreateViewer();
        };
    }

    [MenuItem("AMT Kit/Configure Android ARCore")]
    public static void ConfigureAndroid()
    {
        PlayerSettings.productName = "AMT Mechanism Kit";
        PlayerSettings.companyName = "AMT Training";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "org.amttraining.mechanismkit");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        // Landscape only (either way up). This is written into the Android manifest, so the app
        // opens in landscape even when the phone's rotation lock is on.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        // Pin the target API: "highest installed" can pick a platform this Unity/Gradle version cannot resolve.
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { GraphicsDeviceType.OpenGLES3 });
        // A fresh checkout has no XR management asset until the settings UI is opened.
        // Create and register it here so batch builds work without that manual step.
        XRGeneralSettingsPerBuildTarget buildTargetSettings;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out buildTargetSettings)
            || buildTargetSettings == null)
        {
            const string path = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
            buildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (buildTargetSettings == null)
            {
                buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(buildTargetSettings, path);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
        }
        if (!buildTargetSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var settings = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (settings == null || settings.Manager == null ||
            !XRPackageMetadataStore.AssignLoader(settings.Manager,
                "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
        {
            Debug.LogError("Could not enable ARCore automatically. Enable it in Project Settings > XR Plug-in Management > Android.");
            return;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Android ARCore configured. Verify XR Project Validation before building. Viewer input supports either active Unity input backend.");
    }
}
