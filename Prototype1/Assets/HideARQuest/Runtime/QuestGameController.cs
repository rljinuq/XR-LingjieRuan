using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.Samples;
using UnityEngine.XR.ARSubsystems;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace HideAR.Quest
{
    [DefaultExecutionOrder(-100)]
    public sealed class QuestGameController : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [SerializeField] ARAnchorManager anchors;
        [SerializeField] ARPlaneManager planes;
        [SerializeField] ARRaycastManager raycasts;
        [SerializeField] ARPlaceAnchor officialPlacement;
        [SerializeField] RaycastEventController officialRaycaster;
        [SerializeField] ARRaycastHitEventAsset rawHit;
        [SerializeField] ARRaycastHitEventAsset approvedHit;
        [SerializeField] QuestRoomAccess room;
        [SerializeField] QuestPanel panel;
        [SerializeField] GameObject dinosaurPrefab;
        [SerializeField] GameObject reticle;
        [SerializeField] GameObject menuDinosaur;
        [SerializeField] UnityEngine.Rendering.RenderPipelineAsset questRenderPipeline;

        readonly QuestRound round = new();
        readonly List<ARRaycastHit> hits = new();
        readonly List<ARAnchor> removeQueue = new();
        ARAnchor placedAnchor;
        GameObject dinosaur;
        InputAction recenterAction;
        QuestPhase displayedPhase = (QuestPhase)(-1);
        bool paused;
        bool focused = true;
        bool wasTracking;
        bool releasingAnchor;
        bool lastLeftPress;
        float placementStarted;
        float uiRefreshAt;
        float initializedAt;
        string error;
        UnityEngine.Rendering.RenderPipelineAsset previousPipeline;

        void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            previousPipeline = QualitySettings.renderPipeline;
            if (questRenderPipeline != null) QualitySettings.renderPipeline = questRenderPipeline;
#endif
            officialPlacement.enabled = false;
            officialRaycaster.enabled = false;
            origin.Camera.clearFlags = CameraClearFlags.SolidColor;
            origin.Camera.backgroundColor = Color.clear;
            origin.Camera.allowHDR = false;
            panel.GetComponent<Canvas>().worldCamera = origin.Camera;
            reticle.SetActive(false);
            initializedAt = Time.unscaledTime;
        }

        void OnEnable()
        {
            rawHit.eventRaised += OnSurfaceHit;
            panel.Command += OnCommand;
            anchors.trackablesChanged.AddListener(OnAnchorsChanged);
            recenterAction = new InputAction("Bring menu here", InputActionType.Button);
            recenterAction.AddBinding("<XRController>{LeftHand}/primaryButton");
            recenterAction.AddBinding("<XRController>{RightHand}/primaryButton");
            recenterAction.Enable();
        }

        void OnDisable()
        {
            rawHit.eventRaised -= OnSurfaceHit;
            panel.Command -= OnCommand;
            anchors.trackablesChanged.RemoveListener(OnAnchorsChanged);
            officialRaycaster.enabled = false;
            recenterAction?.Dispose();
        }

        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            QualitySettings.renderPipeline = previousPipeline;
#endif
        }

        void Update()
        {
            var tracking = CanInteract();
            if (tracking && !wasTracking) { panel.Recenter(origin.Camera); round.Disarm(); }
            wasTracking = tracking;
            if (recenterAction.WasPressedThisFrame()) { panel.Recenter(origin.Camera); round.Disarm(); }

            if (anchors.subsystem != null && anchors.subsystem.running && !officialPlacement.enabled)
                officialPlacement.enabled = true;

            var inputs = officialRaycaster.inputActions;
            bool leftPressed = inputs.leftTriggerPressed.action.WasPressedThisFrame();
            bool rightPressed = inputs.rightTriggerPressed.action.WasPressedThisFrame();
            if (leftPressed) lastLeftPress = true;
            else if (rightPressed) lastLeftPress = false;
            if (tracking && round.Phase == QuestPhase.Seeking && round.InputArmed)
            {
                if (leftPressed) TryFind(true);
                if (rightPressed) TryFind(false);
            }
            round.Tick(Time.unscaledDeltaTime, tracking);
            if (tracking)
                round.UpdateRelease(inputs.leftTriggerPressed.action.IsPressed() || inputs.rightTriggerPressed.action.IsPressed(), Time.unscaledDeltaTime);

            officialRaycaster.enabled = tracking && room.Granted && officialPlacement.enabled &&
                round.Phase == QuestPhase.Placing && round.InputArmed && !releasingAnchor;
            UpdateReticle(tracking);

            // The sample creates anchors asynchronously. Do not retry while a previous request is unresolved.
            if (round.Phase == QuestPhase.Saving && Time.unscaledTime - placementStarted > 12)
                Fail("The hiding place could not be saved. Restart the round and try again.");

            DrainRemovals();
            if (displayedPhase != round.Phase)
            {
                displayedPhase = round.Phase;
                panel.Recenter(origin.Camera);
                ApplyVisibility();
                uiRefreshAt = 0;
            }
            if (Time.unscaledTime >= uiRefreshAt)
            {
                uiRefreshAt = Time.unscaledTime + 0.1f;
                UpdatePanel(tracking);
                UpdatePlaneVisuals();
            }
        }

        bool CanInteract()
        {
            if (paused || !focused) return false;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked) return false;
            if (head.TryGetFeatureValue(CommonUsages.userPresence, out var present) && !present) return false;
            return placedAnchor == null || placedAnchor.trackingState == TrackingState.Tracking;
        }

        bool TryRay(bool left, out Ray ray)
        {
            ray = default;
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked) return false;
            var inputs = officialRaycaster.inputActions;
            var position = (left ? inputs.leftHandPosition : inputs.rightHandPosition).action.ReadValue<Vector3>();
            var rotation = (left ? inputs.leftHandRotation : inputs.rightHandRotation).action.ReadValue<Quaternion>();
            // Match the official RaycastEventController's tracking-space to world-space conversion.
            ray = new Ray(origin.TrackablesParent.TransformPoint(position),
                origin.TrackablesParent.TransformDirection(rotation * Vector3.forward));
            return true;
        }

        static bool HitsPanel(Ray ray) => Physics.Raycast(ray, 20, 1 << LayerMask.NameToLayer("UI"), QueryTriggerInteraction.Collide);

        void OnSurfaceHit(object sender, ARRaycastHit hit)
        {
            if (!CanInteract() || !room.Granted || !officialPlacement.enabled || releasingAnchor) return;
            var inputs = officialRaycaster.inputActions;
            bool left = inputs.leftTriggerPressed.action.WasPressedThisFrame();
            if (!left && !inputs.rightTriggerPressed.action.WasPressedThisFrame()) return;
            if (!TryRay(left, out var ray) || HitsPanel(ray)) return;
            lastLeftPress = left;
            if (hit.trackable is not ARPlane plane || plane.alignment != PlaneAlignment.HorizontalUp) return;
            if (!round.RequestPlacement()) return;
            placementStarted = Time.unscaledTime;
            officialRaycaster.enabled = false;
            approvedHit.Raise(hit);
        }

        void OnAnchorsChanged(ARTrackablesChangedEventArgs<ARAnchor> changes)
        {
            foreach (var anchor in changes.added)
            {
                if (placedAnchor != null || !round.AcceptAnchor()) { removeQueue.Add(anchor); continue; }
                placedAnchor = anchor;
                dinosaur = QuestDinosaur.Create(dinosaurPrefab, anchor.transform, 0.28f, true);
                if (dinosaur == null) Fail("The dinosaur could not be loaded. Restart the game.");
            }
            foreach (var removed in changes.removed)
            {
                if (placedAnchor != null && removed.Key == placedAnchor.trackableId)
                {
                    placedAnchor = null;
                    if (!releasingAnchor) Fail("The hiding place was lost. Start a new round in this room.");
                }
            }
        }

        void UpdateReticle(bool tracking)
        {
            reticle.SetActive(false);
            if (!tracking || !room.Granted || round.Phase != QuestPhase.Placing || releasingAnchor) return;
            if (!TryRay(lastLeftPress, out var ray) || HitsPanel(ray)) return;
            if (!raycasts.Raycast(ray, hits, TrackableType.PlaneWithinPolygon)) return;
            var hit = hits[0];
            if (hit.trackable is not ARPlane plane || plane.alignment != PlaneAlignment.HorizontalUp) return;
            reticle.transform.SetPositionAndRotation(hit.pose.position + Vector3.up * 0.006f, hit.pose.rotation);
            reticle.SetActive(true);
        }

        void TryFind(bool left)
        {
            if (dinosaur == null || !TryRay(left, out var ray) || HitsPanel(ray)) return;
            if (!Physics.Raycast(ray, out var hit, 20, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.transform == dinosaur.transform || hit.transform.IsChildOf(dinosaur.transform)) round.Found();
        }

        void OnCommand(QuestCommand command)
        {
            if (command == QuestCommand.Reset)
            {
                if (round.AnchorPending || round.Phase == QuestPhase.Error)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                else { ClearDinosaur(); round.Reset(); }
                return;
            }
            if (command == QuestCommand.Secondary)
            {
                if (round.Phase == QuestPhase.Review) { if (round.Move()) ClearDinosaur(); }
                else if (round.Phase == QuestPhase.Intro || round.Phase == QuestPhase.Placing) room.CaptureRoom();
                round.Disarm();
                return;
            }
            if (round.Phase == QuestPhase.Error)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (!CanInteract()) return;
            switch (round.Phase)
            {
                case QuestPhase.Intro:
                case QuestPhase.Found:
                    if (!room.Granted) { room.RequestPermission(); return; }
                    if (!officialPlacement.enabled) return;
                    ClearDinosaur();
                    round.StartHiding();
                    break;
                case QuestPhase.Review: round.Confirm(); break;
                case QuestPhase.Handoff: round.Ready(); break;
                case QuestPhase.Error: SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); break;
            }
        }

        void ClearDinosaur()
        {
            if (dinosaur != null) dinosaur.SetActive(false);
            if (placedAnchor != null) removeQueue.Add(placedAnchor);
            dinosaur = null;
            placedAnchor = null;
            releasingAnchor = removeQueue.Count > 0;
            round.Disarm();
        }

        void DrainRemovals()
        {
            for (int i = removeQueue.Count - 1; i >= 0; i--)
            {
                var anchor = removeQueue[i];
                if (anchor == null || anchors.TryRemoveAnchor(anchor)) removeQueue.RemoveAt(i);
            }
            releasingAnchor = removeQueue.Count > 0;
        }

        void ApplyVisibility()
        {
            if (menuDinosaur != null)
                menuDinosaur.SetActive(round.Phase == QuestPhase.Intro || round.Phase == QuestPhase.Handoff || round.Phase == QuestPhase.Found);
            if (dinosaur != null)
                dinosaur.SetActive(round.Phase == QuestPhase.Review || round.Phase == QuestPhase.Seeking || round.Phase == QuestPhase.Found);
        }

        void UpdatePlaneVisuals()
        {
            bool visible = round.Phase == QuestPhase.Placing;
            foreach (var plane in planes.trackables)
            {
                var visualizer = plane.GetComponent<ARPlaneMeshVisualizer>();
                if (visualizer != null) visualizer.enabled = visible && plane.alignment == PlaneAlignment.HorizontalUp;
                foreach (var renderer in plane.GetComponentsInChildren<Renderer>())
                    renderer.enabled = visible && plane.alignment == PlaneAlignment.HorizontalUp;
            }
        }

        void UpdatePanel(bool tracking)
        {
            var detail = tracking ? "" : "Tracking paused. Put on the headset and look around your room.";
            if (round.Phase == QuestPhase.Error) detail = error;
            switch (round.Phase)
            {
                case QuestPhase.Intro:
                    if (!room.Granted || room.HasError) detail = room.Message;
                    else if (!officialPlacement.enabled) detail = "Starting spatial tracking...";
                    if (!officialPlacement.enabled && Time.unscaledTime - initializedAt > 15)
                        detail = "Spatial tracking is unavailable. Check OpenXR settings and restart the app.";
                    panel.Show("HideAR", "A little dinosaur. A real hiding place.\nTake turns hiding and finding it in your room.", detail,
                        room.Granted ? "Start hiding" : "Allow room access", "Set up room", tracking && (!room.Granted || officialPlacement.enabled), false);
                    break;
                case QuestPhase.Placing:
                    int surfaces = 0;
                    foreach (var p in planes.trackables) if (p.alignment == PlaneAlignment.HorizontalUp) surfaces++;
                    if (tracking) detail = !room.Granted || room.HasError ? room.Message : surfaces == 0 ? "No surfaces yet. Set up your room and include a table or floor." : "Choose a marked surface. Take your time.";
                    panel.Show("Find a hiding place", "Point at a table or floor and press the trigger.\nYou can review the dinosaur before confirming.", detail, "", "Set up room", false, true);
                    break;
                case QuestPhase.Saving:
                    panel.Show("Saving your spot", "Keep the hiding place in view for a moment.", detail, "", "", false, false);
                    break;
                case QuestPhase.Review:
                    panel.Show("Happy with this spot?", "Look around your dinosaur.\nKeep this spot, or choose a different one.", detail,
                        "Confirm hiding", "Move dinosaur", tracking, true);
                    break;
                case QuestPhase.Handoff:
                    panel.Show("Your turn to find it", "Pass this headset and the controllers to the seeker.\nWhen they are ready, begin the countdown.", detail, "I'm ready", "", tracking, true);
                    break;
                case QuestPhase.Countdown:
                    panel.Show(Mathf.CeilToInt(round.CountdownRemaining).ToString(), "The search is about to begin.", detail, "", "", false, true);
                    break;
                case QuestPhase.Seeking:
                    panel.Show("Where is the dinosaur?", "Explore the room. Point at the dinosaur and\npress the trigger when you find it.",
                        tracking ? $"Search time  {round.SearchSeconds:0.0} s" : detail, "", "", false, true);
                    break;
                case QuestPhase.Found:
                    panel.Show("You found it!", "One hiding place discovered.\nSwap roles and play another round.", $"Your time  {round.SearchSeconds:0.0} s",
                        "Play again", "", tracking, true);
                    break;
                case QuestPhase.Error:
                    panel.Show("Let's try again", error, "", "Restart game", "", true, true);
                    break;
            }
        }

        void Fail(string message) { error = message; round.Fail(); officialRaycaster.enabled = false; }
        void OnApplicationPause(bool value) { paused = value; round.Disarm(); }
        void OnApplicationFocus(bool value) { focused = value; round.Disarm(); }
    }
}
