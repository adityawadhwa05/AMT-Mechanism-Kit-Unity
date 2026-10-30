using System.Collections;
using System.Collections.Generic;
using AMT.AR.Core;
using AMT.AR.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace AMT.AR.AR
{
    // Entry point of the AR scene (the scene file references this component).
    // Flow: check AR support → install ARCore if needed → camera permission → scan for a surface →
    // place the kit at real size on an anchor → explore with the same modules as the 3D viewer.
    // In the Editor the camera and a floor are simulated so the flow can be tried without a phone.
    public sealed class ARBootstrap : MonoBehaviour, IViewController
    {
        enum State { Checking, Installing, Unsupported, NeedsPermission, PermissionDenied, Scanning, Placed }

        State state = State.Checking;
        Camera arCamera;
        Light sun;
        ARRaycastManager raycaster;
        ARPlaneManager planes;
        ARCameraManager cameraManager;
        GameObject rig;
        TrainingSession session;
        TrainingShell shell;
        Transform kitParent;
        ARAnchor anchor;
        ARVisuals.Reticle reticle;
        GameObject shadow;
        bool hitValid;
        Pose hitPose;
        float kitScale = 1f;
        bool dragging;
        Button scaleButton;
        RectTransform card, statusChip;
        Text cardTitle, cardBody, statusText;
        RectTransform cardActions;
        GameObject editorFloor;
        static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        public Camera Camera => arCamera;
        public bool IsAR => true;
        public float KitScale => kitScale;
        public void Focus(Bounds worldBounds) { }
        public void ResetView()
        {
            if (state != State.Placed) return;
            SetScale(1f);
            FaceCamera();
        }

        void Start()
        {
            AppNavigation.ConfigureOrientation();
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 4f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .56f, .58f);

            sun = new GameObject("AR light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1f; sun.shadows = LightShadows.Soft; sun.shadowStrength = .6f;
            sun.transform.rotation = Quaternion.Euler(60f, -30f, 0f);

            BuildCamera();
            AppNavigation.BeforeSceneSwitch += ReleaseAnchor;
            kitParent = new GameObject("Placed kit").transform;
            session = TrainingSession.Create(this, kitParent);
            shell = TrainingShell.Create(session, true);
            shell.HomeShown += () => { if (state != State.Placed) shell.HideAll(); };
            BuildCard();
            shell.AddTopAction("Move", Icons.Focus, Reposition, 140);
            scaleButton = shell.AddTopAction("Real size", Icons.Explode, ToggleScale, 170);
            session.Kit.Loaded += () =>
            {
                var kit = session.Kit;
                // Kit origin at the centre of its base so it stands on the detected surface.
                kit.Root.localPosition = -new Vector3(kit.KitCenter.x, kit.KitCenter.y - kit.KitSize.y * .5f, kit.KitCenter.z);
                reticle = new ARVisuals.Reticle(kit.KitSize);
                shadow = ARVisuals.ShadowCatcher(kitParent, kit.KitSize);
                SetKitVisible(state == State.Placed);
            };
            session.BeginLoad();
            StartCoroutine(Flow());
        }

        // ---------- setup ----------

        void BuildCamera()
        {
#if UNITY_EDITOR
            arCamera = new GameObject("Simulated AR camera") { tag = "MainCamera" }.AddComponent<Camera>();
            arCamera.transform.position = new Vector3(0, .75f, -.85f);
            arCamera.transform.LookAt(new Vector3(0, 0, .1f));
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = new Color(.20f, .22f, .24f);
            arCamera.fieldOfView = 60f; arCamera.nearClipPlane = .02f;
            editorFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            editorFloor.name = "Simulated floor (Editor only)";
            editorFloor.transform.localScale = Vector3.one * .4f;
            var floorMat = new Material(KitMaterials.ARPlane);
            floorMat.SetColor("_Fill", new Color(.35f, .37f, .40f, 1f));
            floorMat.SetColor("_Color", new Color(.5f, .52f, .55f, 1f));
            editorFloor.GetComponent<Renderer>().sharedMaterial = floorMat;
#else
            // Built inactive and enabled once support and permission are confirmed.
            // Every object is parented under the inactive rig *before* components are added, so
            // nothing wakes up (session, origin, camera) until support and permission are confirmed.
            rig = new GameObject("AR rig");
            rig.SetActive(false);
            var sessionObject = Child("AR Session", rig.transform);
            sessionObject.AddComponent<ARSession>();
            sessionObject.AddComponent<ARInputManager>();
            var originObject = Child("XR Origin (Mobile AR)", rig.transform);
            var offset = Child("Camera Offset", originObject.transform).transform;
            var cameraObject = Child("AR Camera", offset);
            cameraObject.tag = "MainCamera";
            arCamera = cameraObject.AddComponent<Camera>();
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = Color.black;
            arCamera.nearClipPlane = .02f; arCamera.farClipPlane = 50f;
            var origin = originObject.AddComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offset.gameObject;
            var poseDriver = arCamera.gameObject.AddComponent<TrackedPoseDriver>();
            var position = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotation = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            poseDriver.positionInput = new InputActionProperty(position);
            poseDriver.rotationInput = new InputActionProperty(rotation);
            cameraManager = arCamera.gameObject.AddComponent<ARCameraManager>();
            cameraManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor |
                                                     LightEstimation.MainLightDirection;
            cameraManager.frameReceived += OnFrame;
            arCamera.gameObject.AddComponent<ARCameraBackground>();
            origin.Camera = arCamera;
            var templates = new GameObject("Templates (inactive)").transform;
            templates.SetParent(transform, false);
            templates.gameObject.SetActive(false);
            planes = origin.gameObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.planePrefab = ARVisuals.PlaneTemplate(templates);
            raycaster = origin.gameObject.AddComponent<ARRaycastManager>();
            origin.gameObject.AddComponent<ARAnchorManager>();
#endif
        }

        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        IEnumerator Flow()
        {
#if UNITY_EDITOR
            state = State.Scanning;
            yield break;
#else
            Show("Checking AR support…", "Looking for Google Play Services for AR on this device.", Icons.AR);
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                state = State.Installing;
                Show("Installing AR support", "Google Play Services for AR is required. Follow the prompt to install it.", Icons.AR);
                yield return ARSession.Install();
            }
            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
            {
                state = State.Unsupported;
                Show("AR isn't available on this device",
                    "This phone does not support ARCore, or AR support was not installed. You can explore everything in the 3D viewer.",
                    Icons.Alert, ("Open 3D viewer", () => AppNavigation.SwitchScene(false, null)));
                yield break;
            }
            yield return RequestCamera();
#endif
        }

#if !UNITY_EDITOR
        IEnumerator RequestCamera()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                state = State.NeedsPermission;
                Show("Camera access", "AR needs the camera to see your table or floor.", Icons.AR);
                bool answered = false, granted = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { answered = true; granted = true; };
                callbacks.PermissionDenied += _ => { answered = true; };
                callbacks.PermissionDeniedAndDontAskAgain += _ => { answered = true; };
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!answered && Time.realtimeSinceStartup < timeout) yield return null;
                if (!granted && !Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    state = State.PermissionDenied;
                    Show("Camera permission denied",
                        "Allow camera access to use AR, or continue in the 3D viewer. If you chose \"Don't ask again\", enable it in Settings › Apps › AMT Mechanism Kit › Permissions.",
                        Icons.Alert,
                        ("Try again", () => StartCoroutine(RequestCamera())),
                        ("Open 3D viewer", () => AppNavigation.SwitchScene(false, null)));
                    yield break;
                }
            }
#endif
            rig.SetActive(true);
            state = State.Scanning;
        }
#endif

        // ---------- per frame ----------

        void Update()
        {
            if (state != State.Scanning && state != State.Placed) return;
            if (!session.Kit.IsLoaded)
            {
                Show("Loading the kit…", "Preparing 37 parts from the CAD model.", Icons.Cube);
                return;
            }
            UpdateTracking();
            if (state == State.Scanning) UpdateScanning();
            else UpdatePlaced();
        }

        void UpdateTracking()
        {
#if UNITY_EDITOR
            SetStatus(null);
#else
            if (ARSession.state == ARSessionState.SessionTracking) { SetStatus(null); return; }
            string reason;
            switch (ARSession.notTrackingReason)
            {
                case NotTrackingReason.Initializing: reason = "Starting AR — move your phone slowly."; break;
                case NotTrackingReason.Relocalizing: reason = "Re-finding the surface — point at where you placed the kit."; break;
                case NotTrackingReason.InsufficientLight: reason = "Too dark — turn on more light."; break;
                case NotTrackingReason.InsufficientFeatures: reason = "Not enough detail — aim at a textured surface."; break;
                case NotTrackingReason.ExcessiveMotion: reason = "Moving too fast — slow down."; break;
                case NotTrackingReason.CameraUnavailable: reason = "The camera is unavailable."; break;
                default: reason = "Tracking lost — move your phone slowly."; break;
            }
            SetStatus(reason);
#endif
        }

        void UpdateScanning()
        {
            var center = new Vector2(Screen.width * .5f, Screen.height * .45f);
            hitValid = Raycast(center, out hitPose);
            if (reticle != null)
            {
                reticle.SetScale(kitScale);
                if (hitValid) reticle.Place(hitPose.position, YawTowardsCamera(hitPose.position), true);
                else reticle.Hide();
            }
            if (hitValid)
            {
                scanStarted = -1f;
                Show("Tap to place the kit", "Real size: " + Mathf.RoundToInt(session.Kit.KitSize.x * 100 * kitScale) + " × " +
                     Mathf.RoundToInt(session.Kit.KitSize.z * 100 * kitScale) + " cm. The outline shows its footprint.",
                     Icons.Check, ("Place here", Place));
            }
            else ShowScanGuidance();
            if (hitValid && TappedScreen(out _)) Place();
        }

        float scanStarted = -1f;

        // Explains why no surface is found yet, using ARCore's own tracking state, and offers a
        // restart if scanning drags on (ARCore occasionally needs a fresh session to recover).
        void ShowScanGuidance()
        {
            if (scanStarted < 0) scanStarted = Time.unscaledTime;
            float waited = Time.unscaledTime - scanStarted;
#if !UNITY_EDITOR
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                string why;
                switch (ARSession.notTrackingReason)
                {
                    case NotTrackingReason.InsufficientLight: why = "It's too dark for the camera — turn on more light."; break;
                    case NotTrackingReason.InsufficientFeatures: why = "Aim at a surface with some pattern or texture; plain or shiny surfaces are hard to track."; break;
                    case NotTrackingReason.ExcessiveMotion: why = "You're moving too fast — slow down."; break;
                    case NotTrackingReason.CameraUnavailable: why = "The camera is in use by another app."; break;
                    default: why = "Move the phone slowly sideways (about half a metre), not just tilting it, so AR can measure depth."; break;
                }
                if (waited > 20f) Show("Still starting AR", why, Icons.Alert, ("Restart AR", RestartSession));
                else Show("Starting AR…", why, Icons.AR);
                return;
            }
#endif
            if (waited > 25f)
                Show("No surface found yet", "Point at a table or the floor about 1 m away and move the phone slowly sideways. Plain, glossy or very dark surfaces are hard to detect.",
                     Icons.Alert, ("Restart AR", RestartSession));
            else
                Show("Find a surface", "Point your phone at a table or the floor and move it slowly from side to side.", Icons.AR);
        }

        void RestartSession()
        {
            scanStarted = -1f;
#if !UNITY_EDITOR
            var arSession = rig != null ? rig.GetComponentInChildren<ARSession>(true) : null;
            if (arSession != null) arSession.Reset();
#endif
            shell.Toast("Restarting AR — move the phone slowly sideways");
        }

        void UpdatePlaced()
        {
            var g = session.Gestures;
            if (!g.Active) dragging = false;
            // Two fingers: twist to rotate, pinch to scale. One finger: drag the kit along the surface.
            // Editor mouse: wheel scales, right-drag rotates, left-drag moves.
            if (Mathf.Abs(g.Twist) > .01f) kitParent.Rotate(Vector3.up, -g.Twist, Space.World);
            if (Mathf.Abs(g.Pinch - 1f) > .0001f) SetScale(kitScale * g.Pinch, true);
            if (g.FingerCount == 0 && Mathf.Abs(g.Pan.x) > 0) kitParent.Rotate(Vector3.up, -g.Pan.x * .3f, Space.World);
            if (g.FingerCount <= 1 && g.Drag.sqrMagnitude > 0)
            {
                var plane = new Plane(Vector3.up, kitParent.position);
                var ray = arCamera.ScreenPointToRay(PointerPosition());
                if (plane.Raycast(ray, out float enter))
                {
                    var target = ray.GetPoint(enter);
                    if (!dragging) { dragging = true; dragOffset = kitParent.position - target; }
                    kitParent.position = target + dragOffset;
                }
            }
        }

        Vector3 dragOffset;

        Vector2 PointerPosition()
        {
            var list = new List<Vector2>();
            PointerInput.Touches(list);
            return list.Count > 0 ? list[0] : PointerInput.MousePosition;
        }

        bool Raycast(Vector2 screen, out Pose pose)
        {
#if UNITY_EDITOR
            if (editorFloor != null && Physics.Raycast(arCamera.ScreenPointToRay(PointerInput.MouseAvailable ? PointerInput.MousePosition : (Vector3)screen), out var hit, 20f) &&
                hit.collider.gameObject == editorFloor)
            { pose = new Pose(hit.point, Quaternion.identity); return true; }
            pose = default; return false;
#else
            if (raycaster != null && raycaster.Raycast(screen, hits, TrackableType.PlaneWithinPolygon))
            { pose = hits[0].pose; return true; }
            pose = default; return false;
#endif
        }

        bool tapPending;
        Vector2 tapPosition;

        bool TappedScreen(out Vector2 position)
        {
            position = tapPosition;
            if (!tapPending) return false;
            tapPending = false;
            return true;
        }

        void OnEnable() { StartCoroutine(HookTaps()); }

        IEnumerator HookTaps()
        {
            while (session == null) yield return null;
            session.Gestures.Tapped += p => { if (state == State.Scanning) { tapPending = true; tapPosition = p; } };
        }

        Quaternion YawTowardsCamera(Vector3 position)
        {
            var toCamera = arCamera.transform.position - position; toCamera.y = 0;
            // Kit front (-Z) faces the viewer.
            return toCamera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(-toCamera.normalized, Vector3.up) : Quaternion.identity;
        }

        // ---------- placement ----------

        void Place()
        {
            if (!hitValid) return;
            if (anchor != null) { kitParent.SetParent(null, true); Destroy(anchor.gameObject); anchor = null; }
            var anchorObject = new GameObject("Kit anchor");
            anchorObject.transform.SetPositionAndRotation(hitPose.position, YawTowardsCamera(hitPose.position));
#if !UNITY_EDITOR
            anchor = anchorObject.AddComponent<ARAnchor>();
#endif
            kitParent.SetParent(anchorObject.transform, false);
            kitParent.localPosition = Vector3.zero; kitParent.localRotation = Quaternion.identity;
            kitParent.localScale = Vector3.one * kitScale;
            SetKitVisible(true);
            reticle?.Hide();
            SetPlanesVisible(false);
            state = State.Placed;
            HideCard();
            shell.OnPlaced();
            shell.Toast(Mathf.Approximately(kitScale, 1f) ? "Placed at real size (1:1)" : "Placed at " + Mathf.RoundToInt(kitScale * 100) + "% scale");
            Debug.Log("AMT AR: kit placed" + (anchor != null ? " on an anchor." : "."));
        }

        void Reposition()
        {
            if (state != State.Placed) return;
            session.StopAll();
            shell.HideAll();
            SetKitVisible(false);
            SetPlanesVisible(true);
            scanStarted = -1f;
            state = State.Scanning;
        }

        // The KitModel component lives on kitParent and must stay active (it runs the loader), so only
        // the model and its shadow are hidden.
        void SetKitVisible(bool visible)
        {
            if (session.Kit.Root != null) session.Kit.Root.gameObject.SetActive(visible);
            if (shadow != null) shadow.SetActive(visible);
        }

        void FaceCamera()
        {
            var t = anchor != null ? anchor.transform : kitParent.parent;
            if (t == null) return;
            kitParent.localPosition = Vector3.zero;
            kitParent.rotation = YawTowardsCamera(kitParent.position);
        }

        void ToggleScale() => SetScale(Mathf.Approximately(kitScale, 1f) ? .5f : 1f);

        void SetScale(float s, bool fromPinch = false)
        {
            s = Mathf.Clamp(s, .2f, 3f);
            // Snap to real size when pinching close to it.
            if (fromPinch && Mathf.Abs(s - 1f) < .04f) s = 1f;
            bool wasReal = Mathf.Approximately(kitScale, 1f);
            kitScale = s;
            kitParent.localScale = Vector3.one * s;
            if (scaleButton != null) UIFactory.SetLabel(scaleButton, Mathf.Approximately(s, 1f) ? "Real size" : Mathf.RoundToInt(s * 100) + "%");
            if (fromPinch && !wasReal && Mathf.Approximately(s, 1f)) shell.Toast("Real size (1:1)", 1.2f);
        }

        void SetPlanesVisible(bool visible)
        {
            if (planes == null) return;
            foreach (var p in planes.trackables) p.gameObject.SetActive(visible);
            planesVisible = visible;
        }

        bool planesVisible = true;

        void LateUpdate()
        {
            // Newly detected planes appear active; keep them hidden while the kit is placed.
            if (!planesVisible && planes != null)
                foreach (var p in planes.trackables) if (p.gameObject.activeSelf) p.gameObject.SetActive(false);
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            var light = args.lightEstimation;
            if (light.averageBrightness.HasValue) sun.intensity = Mathf.Lerp(.5f, 1.6f, light.averageBrightness.Value);
            if (light.colorCorrection.HasValue) sun.color = light.colorCorrection.Value;
            if (light.mainLightDirection.HasValue && light.mainLightDirection.Value.sqrMagnitude > .01f)
                sun.transform.rotation = Quaternion.LookRotation(light.mainLightDirection.Value);
            if (light.averageBrightness.HasValue)
                RenderSettings.ambientLight = Color.gray * Mathf.Lerp(.35f, 1.1f, light.averageBrightness.Value);
        }

        // ---------- AR guidance card ----------

        void BuildCard()
        {
            card = UIFactory.Card(shell.OverlayRoot, "AR guidance", Theme.SurfaceRaised, 20);
            card.anchorMin = card.anchorMax = new Vector2(.5f, 0); card.pivot = new Vector2(.5f, 0);
            card.anchoredPosition = new Vector2(0, 28); card.sizeDelta = new Vector2(760, 150);
            var row = UIFactory.Rect("Row", card).Stretch(24, 18, 24, 18);
            row.HStack(18, null, TextAnchor.MiddleLeft);
            var icon = UIFactory.Icon(row, Icons.AR, Theme.Accent, 44);
            icon.name = "Card icon";
            var text = UIFactory.Rect("Text", row); text.VStack(4); text.Layout(-1, -1, 1);
            cardTitle = UIFactory.Label(text, "", 22, Theme.Text, Weight.Bold);
            cardBody = UIFactory.Label(text, "", 16, Theme.TextMuted);
            cardActions = UIFactory.Rect("Actions", row); cardActions.VStack(8); cardActions.Layout(220, -1, 0);

            statusChip = UIFactory.Card(shell.OverlayRoot, "Tracking status", Theme.WarningSoft.WithAlpha(.95f), 16);
            statusChip.anchorMin = statusChip.anchorMax = new Vector2(.5f, 1); statusChip.pivot = new Vector2(.5f, 1);
            statusChip.anchoredPosition = new Vector2(0, -TrainingShell.TopBarHeight - 24); statusChip.sizeDelta = new Vector2(640, 56);
            statusText = UIFactory.Label(statusChip, "", 17, Theme.Warning, Weight.SemiBold, TextAnchor.MiddleCenter);
            statusText.rectTransform.Stretch(16, 6, 16, 6);
            statusChip.gameObject.SetActive(false);
        }

        string shownKey;

        void Show(string heading, string body, Sprite icon, params (string label, UnityEngine.Events.UnityAction action)[] actions)
        {
            string key = heading + body + actions.Length;
            card.gameObject.SetActive(true);
            if (key == shownKey) return;
            shownKey = key;
            cardTitle.text = heading; cardBody.text = body;
            var iconImage = card.Find("Row/Card icon")?.GetComponent<Image>();
            if (iconImage != null) iconImage.sprite = icon;
            cardActions.Clear();
            for (int i = 0; i < actions.Length; i++)
                UIFactory.Button(cardActions, actions[i].label, actions[i].action, i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary, null, 50, 17);
            cardActions.gameObject.SetActive(actions.Length > 0);
        }

        void HideCard() { card.gameObject.SetActive(false); shownKey = null; }

        void SetStatus(string text)
        {
            bool show = !string.IsNullOrEmpty(text) && state == State.Placed;
            statusChip.gameObject.SetActive(show);
            if (show) statusText.text = text;
        }

        // Remove the anchor while its ARAnchorManager is alive. Left to scene teardown, the anchor can
        // be destroyed after the manager and AR Foundation throws in ARAnchorManager.TryRemoveAnchor.
        void ReleaseAnchor()
        {
            if (anchor == null) return;
            DestroyImmediate(anchor);
            anchor = null;
        }

        void OnDestroy()
        {
            AppNavigation.BeforeSceneSwitch -= ReleaseAnchor;
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
        }
    }
}
