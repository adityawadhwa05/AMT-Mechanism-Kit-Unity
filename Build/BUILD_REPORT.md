# AMT Mechanism Kit — build report

_28 October 2026 · Unity 6000.0.60f1 · branch `feature/real-cad-parts-and-ui`_

## Results

| Check | Result |
| --- | --- |
| Project import and script compilation (Unity 6000.0.60f1) | Passed. No errors or warnings from project code; all five custom shaders compile. |
| `AMT Kit → Validate Project` | Passed. Covers files, shaders, the glTF shader in Always Included, data integrity, GLB node names vs `components.json`, and the ARCore loader. |
| Play-mode smoke test, Viewer scene (`AMTSmokeTest`) | Passed. The kit loads with 37 parts at 0.532 × 0.392 × 0.370 m; components, tools, how-it-works, exploded, assembly and disassembly all open and animate with no errors or exceptions. |
| Play-mode smoke test, AR scene (Editor simulation) | Passed. The kit loads, simulated placement works, and modules run in AR mode. |
| Android IL2CPP build | Passed: `Build/AMTMechanismKit.apk`. |
| Samsung Galaxy S23+ (SM-S916B, Android 16), 3D viewer | Passed. Installed over adb; landscape layout correct at 2340×1080; the kit loads; home, components and how-it-works were checked through adb-driven screenshots (station effects render); no Unity errors in logcat. |
| Same phone, AR (hands-on) | Passed. Surfaces are detected (dot grid with outline), the kit is placed at real size on an anchor, re-placed with Move, and the top-bar buttons work. AR → 3D viewer → AR switching was repeated with no errors. |

Screenshots from the smoke test are in `Docs/screenshots/`.

## APK

- `AMTMechanismKit.apk`: 33,245,514 bytes, SHA-256 `180e2c94e6fda2ed05d73a018d5501965e90e47e8579be2940a8d65925e83821`.
- Package `org.amttraining.mechanismkit`, version 1.0 (code 1).
- ARM64 IL2CPP; minimum API 24, target and compile API 36.
- Permissions: camera and internet. ARCore and depth are set to **optional**, so phones without AR can
  still install the app and use the 3D viewer. The only required feature is `android.hardware.camera`.
- Both `assets/Model/MechanismKit.glb` (supplied, unchanged) and `MechanismKit_Parts.glb` (37 parts)
  are packaged.
- `Build/AMTMechanismKit.apk` in the repo is the debug-signed development build. The **public download** on GitHub Releases is the same build re-signed with the project's release key (`CN=AMT Mechanism Kit, O=AMT Training`), using APK Signature Scheme v2/v3. The key is kept outside the repository.

## Build environment used

- The Unity Editor and Android Build Support were installed per-user in `C:\Users\rduga\Unity\Hub\Editor`.
- **Android SDK.** A Unity-specific SDK folder (`C:\Users\rduga\Unity\AndroidSDK`) with Google
  cmdline-tools 16.0, plus junctions to the existing SDK's platforms, build-tools and platform-tools.
  The 2026 cmdline-tools in the main SDK print a deprecation notice that Unity 6000.0 cannot parse.
- **NDK** r27c (27.2.12479018) and **JDK** 17 (Temurin 17.0.20.1).
- Batch builds read these from the `AMT_ANDROID_SDK`, `AMT_ANDROID_NDK` and `AMT_JDK` environment
  variables (`Assets/Editor/AMTAndroidTools.cs`). In the Editor you can set the same paths under
  **Preferences → External Tools**.
- **Target API pinned to 36.** "Highest installed" picked `android-37.0`, which this Gradle setup cannot resolve.
- The first Gradle attempt hit the known Windows transform-cache rename race. `AMTBuildRecovery`
  recovered the cache entry and the second attempt succeeded.

## Fixed during device testing

- The app opened in portrait: the manifest allowed portrait. Now landscape-only (`userLandscape`), set in Player Settings and in `ProjectSetup`.
- AR objects are created under the inactive rig before components are added (removes the "No Main Camera is found for XR Origin" warning).
- The speed slider handle overlapped its label; the control is now horizontal.
- AR home: compact module column and a light dim, so the placed kit stays visible.
- AR home: the dim layer swallowed taps meant for the top bar (3D viewer, Move, Real size). It no longer catches touches.
- Leaving AR: `ARAnchorManager.TryRemoveAnchor` threw a NullReferenceException when the anchor outlived its manager during scene teardown. The anchor is now released just before the scene switch.
- Scanning: the card shows ARCore's reason when it isn't tracking (move sideways, too dark, needs texture). After about 20–25 s it offers **Restart AR** (session reset).

Known, harmless: one Input System `NullReferenceException` in `FireStateChangeNotifications` was logged once when UI was rebuilt during a tap. It is caught by the Input System, and the app continues normally.

## Remaining

- Measure frame rate and load time on a mid-range phone.
- Check light estimation and tracking-loss messages in harder conditions (dim light, fast motion).
- Sign every future public release with the same release key, so it installs as an update.
