using System;
using System.Reflection;
using UnityEngine;

// Lets batch builds use an existing Android SDK / NDK / JDK instead of the copies Unity Hub installs.
// Set AMT_ANDROID_SDK, AMT_ANDROID_NDK and AMT_JDK (folder paths) before running Unity.
// Uses reflection so the project still compiles when Android Build Support is not installed.
public static class AMTAndroidTools
{
    public static void ApplyEnvironment()
    {
        var type = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
        if (type == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
                if (type != null) break;
            }
        }
        if (type == null) { Debug.Log("AMT: Android Build Support not loaded; tool paths unchanged."); return; }
        Set(type, "sdkRootPath", "AMT_ANDROID_SDK");
        Set(type, "ndkRootPath", "AMT_ANDROID_NDK");
        Set(type, "jdkRootPath", "AMT_JDK");
    }

    static void Set(Type type, string property, string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(value)) return;
        var p = type.GetProperty(property, BindingFlags.Public | BindingFlags.Static);
        if (p == null || !p.CanWrite) { Debug.LogWarning("AMT: cannot set Android " + property); return; }
        p.SetValue(null, value);
        Debug.Log("AMT: Android " + property + " = " + value);
    }
}
