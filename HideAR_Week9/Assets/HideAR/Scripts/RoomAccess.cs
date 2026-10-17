using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.Meta;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace HideAR.Classroom
{
    // 职责：按 Week 09 ScenePermission 思路取得空间权限，再启用官方平面和射线组件。
    // Role: follow Week 09 ScenePermission to gate Unity plane/raycast managers on permission.
    // 房间数据来自 Quest 系统；这里不实现自己的扫描或空间识别算法。
    // Room data comes from Quest; this class does not implement scanning or scene recognition.
    public sealed class RoomAccess : MonoBehaviour
    {
        public ARPlaneManager planes;
        public ARRaycastManager raycasts;
        public ARSession session;
        public bool Granted { get; private set; }
        public string Message { get; private set; } = "Allow access to your room to begin.";
        bool roomSetupOpened;
        bool setupAfterPermission;
        bool refreshing;
        public bool SettingUpRoom => roomSetupOpened || refreshing;
#if UNITY_ANDROID
        PermissionCallbacks callbacks;
#endif

        void Start() { Request(); }
        // 真机使用 Android 权限回调；编辑器提示不能替代真机授权测试。
        // Use Android permission callbacks on device; editor messages do not test authorization.
        public void Request()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permission = "com.oculus.permission.USE_SCENE";
            if (Permission.HasUserAuthorizedPermission(permission))
            {
                Allow();
                if (setupAfterPermission) SetUpRoom();
                return;
            }
            callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ =>
            {
                Allow();
                if (setupAfterPermission) SetUpRoom();
            };
            callbacks.PermissionDenied += _ => Message = "Allow Spatial Data in the app's headset permissions, then retry.";
            Permission.RequestUserPermission(permission, callbacks);
#else
            Message = "Room access is available on Quest 3 / 3S.";
#endif
        }

        // 取得权限后才允许组件查询系统保存的房间平面。
        // Enable managers only after permission allows querying saved room surfaces.
        void Allow()
        {
            Granted = true;
            planes.enabled = true;
            raycasts.enabled = true;
            Message = "";
        }

        // 调用 Unity/Meta 官方接口打开系统 Space Setup，不在应用内自行扫描。
        // Request the system Space Setup through Unity/Meta, rather than scanning in-app.
        public void SetUpRoom()
        {
            if (SettingUpRoom) return;
            if (!Granted) { setupAfterPermission = true; Request(); return; }
            setupAfterPermission = false;
            if (session.subsystem is MetaOpenXRSessionSubsystem meta && meta.TryRequestSceneCapture())
            {
                roomSetupOpened = true;
                Message = "Opening Quest room setup...";
                Debug.Log("HideAR: Quest room setup requested.");
            }
            else
            {
                Message = "Open Space Setup in Quest Settings,\nthen return here.";
                Debug.LogWarning("HideAR: room setup request was unavailable. Use Quest Space Setup.");
            }
        }

        // 从系统界面返回时重新核对权限；房间设置和权限可能已被用户更改。
        // Recheck permission on resume because system settings may have changed.
        void OnApplicationPause(bool paused)
        {
            if (paused) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission("com.oculus.permission.USE_SCENE"))
            {
                Granted = false;
                planes.enabled = raycasts.enabled = false;
                Message = "Room permission is missing. Allow Spatial Data and retry.";
                return;
            }
            Allow();
#endif
            if (!Granted) return;
            roomSetupOpened = false;
            if (!refreshing) StartCoroutine(RefreshRoom());
        }

        IEnumerator RefreshRoom()
        {
            refreshing = true;
            planes.enabled = false;
            raycasts.enabled = false;
            Message = "Loading current room surfaces...";
            // 等一帧后重启两个管理器以重新查询房间；不表示应用持续扫描新平面。
            // Restart both managers after one frame to re-query room data, not scan continuously.
            yield return null;
            if (Granted)
            {
                planes.enabled = true;
                raycasts.enabled = true;
                Message = "";
                Debug.Log("HideAR: current room surfaces refreshed.");
            }
            refreshing = false;
        }
    }
}
