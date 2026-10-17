using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.Meta;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace HideAR.Quest
{
    public sealed class QuestRoomAccess : MonoBehaviour
    {
        public const string ScenePermission = "com.oculus.permission.USE_SCENE";
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARSession session;
        public bool Granted { get; private set; }
        public bool HasError { get; private set; }
        public string Message { get; private set; } = "Allow room access to choose a hiding place.";
        bool captureRequested;
#if UNITY_ANDROID
        PermissionCallbacks callbacks;
#endif

        void Awake() { planeManager.enabled = false; }
        void Start() { RequestPermission(); }

        public void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(ScenePermission)) { PermissionGranted(); return; }
            callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => PermissionGranted();
            callbacks.PermissionDenied += _ => PermissionDenied();
            Permission.RequestUserPermission(ScenePermission, callbacks);
#else
            Message = "Room scanning is available on Quest 3 / 3S.";
#endif
        }

        void PermissionGranted()
        {
            Granted = true;
            HasError = false;
            planeManager.enabled = true;
            Message = "Choose a surface in your room.";
        }

        void PermissionDenied()
        {
            Granted = false;
            HasError = true;
            planeManager.enabled = false;
            Message = "Room access was declined. Allow Spatial Data in this app's headset permissions, then retry.";
        }

        public void CaptureRoom()
        {
            if (!Granted) { RequestPermission(); return; }
            if (session.subsystem is not MetaOpenXRSessionSubsystem meta || !meta.TryRequestSceneCapture())
            {
                HasError = true;
                Message = "Room setup could not open. Complete Space Setup in headset Settings, then return here.";
                return;
            }
            captureRequested = true;
            HasError = false;
            Message = "Complete the headset's room setup, including your table or floor.";
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(ScenePermission)) PermissionGranted();
            else { PermissionDenied(); return; }
#endif
            if (!captureRequested || !Granted) return;
            captureRequested = false;
            // Restart only the plane provider query after Space Setup; never reset the AR session here.
            planeManager.enabled = false;
            planeManager.enabled = true;
        }
    }
}
