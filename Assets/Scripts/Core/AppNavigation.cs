using UnityEngine;
using UnityEngine.SceneManagement;

namespace AMT.AR.Core
{
    // Moves between the Viewer and AR scenes and remembers which module to reopen.
    public static class AppNavigation
    {
        public const string ViewerScene = "Viewer";
        public const string ARScene = "AR";
        static string pendingModule;

        // Raised just before a scene switch, while every object in the current scene still exists.
        public static event System.Action BeforeSceneSwitch;

        public static void SwitchScene(bool toAR, string module)
        {
            pendingModule = module;
            BeforeSceneSwitch?.Invoke();
            SceneManager.LoadScene(toAR ? ARScene : ViewerScene);
        }

        public static string ConsumePendingModule()
        {
            var m = pendingModule;
            pendingModule = null;
            return m;
        }

        public static void ConfigureOrientation()
        {
#if UNITY_ANDROID || UNITY_IOS
            // Landscape only; either way up.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            // Leave portrait immediately, then let the sensor choose between the two landscapes.
            if (Screen.height > Screen.width) Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
