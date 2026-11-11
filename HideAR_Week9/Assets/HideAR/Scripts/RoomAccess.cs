// RoomAccess.cs：检查 Spatial Data 权限，并请求 Quest 打开系统 Space Setup。
// 它启用 AR Foundation 的平面/射线组件；不会自己扫描房间、生成平面或放置模型。
// GameFlow 读取这里的权限和刷新状态；Unity/Meta XR 提供实际房间数据。
// Manages access to Quest room data; it does not implement a room-scanning algorithm.
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.Meta;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace HideAR.Classroom
{
    public sealed class RoomAccess : MonoBehaviour
    {
        // ===== 官方组件引用与本脚本的状态 =====
        public ARPlaneManager planes; // AR Foundation 收到 Quest 提供的现实房间平面，例如地板/桌面。
        public ARRaycastManager raycasts; // 用手柄射线查询这些 AR 平面，不是 Physics Collider 检测。
        public ARSession session; // 当前 AR 会话；用于取得 Meta OpenXR 的 Space Setup 功能。
        public bool Granted { get; private set; } // Android 权限是否允许；不表示系统已返回平面。
        public int RefreshCount { get; private set; } // 每次刷新流程结束加一，GameFlow 用它发现返回事件。
        public string Message { get; private set; } = "Allow access to your room to begin."; // GameUI 显示的权限或设置提示。
        bool roomSetupOpened; // 已请求 Quest 打开设置，等用户回到应用。
        bool setupAfterPermission; // 用户先点设置但未授权；授权后继续完成设置请求。
        bool refreshing; // 正在跨一帧重新启用平面和射线组件。
        public bool SettingUpRoom => roomSetupOpened || refreshing; // 任一流程仍在进行时，GameFlow 暂停放置。
#if UNITY_ANDROID
        PermissionCallbacks callbacks; // Android 系统回调：通知本应用用户允许还是拒绝权限。
#endif

        // ===== 场景开始时检查/申请房间数据权限 =====
        void Start() { Request(); }
        // Unity 加载场景后自动调用；先处理权限，再允许 AR 平面组件提供数据。
        // Unity calls this when the scene starts.

        // ===== 已授权就启用查询；未授权则显示系统权限请求 =====
        // Android calls the OS permission API. Other targets only show a Quest requirement.
        public void Request()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permission = "com.oculus.permission.USE_SCENE";
            if (Permission.HasUserAuthorizedPermission(permission))
            {
                Allow();
                // 已经授权时不再弹窗；如果用户之前先点了 Set up room，就接着打开系统设置。
                if (setupAfterPermission) SetUpRoom();
                return;
            }
            // Android 真机没有授权时会到这里；先注册系统回调，再请求权限。
            callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ =>
            {
                Allow();
                // 如果授权前用户已点设置，授权成功后继续原来的设置请求。
                if (setupAfterPermission) SetUpRoom();
            };
            callbacks.PermissionDenied += _ => Message = "Allow Spatial Data in the app's headset permissions, then retry.";
            // 拒绝时仅改变提示；不会调用 Allow，所以平面/射线仍不会启用。
            // PermissionGranted/Denied 是系统弹窗返回后的回调；Request 不会在这里同步等到用户选择。
            Permission.RequestUserPermission(permission, callbacks);
            // RequestUserPermission 会显示 Android/Quest 权限对话框；结果之后由上面的回调处理。
            // Granting access enables room queries; it does not mean a plane has been found.
#else
            Message = "Room access is available on Quest 3 / 3S.";
#endif
            // 在 Unity Editor 或非 Android 构建里只更新说明，不会真的请求 Quest 权限。
        }

        // ===== 权限允许后启动 AR Foundation 查询 =====
        void Allow()
        {
            Granted = true;
            planes.enabled = true;
            // 打开 ARPlaneManager，让它开始接收 Quest 当前可用的房间平面。
            raycasts.enabled = true;
            // 打开 ARRaycastManager，使 PlaceOnPlane 后续可以检测射线是否打到 AR 平面。
            Message = "";
        }
        // Message 清空后由 GameUI 根据授权/平面数量显示下一步提示。
        // Enabling these official components does not itself scan the room or create planes.

        // ===== 响应 Set up room 按钮并请求 Quest 系统页面 =====
        // Uses Meta's system scene-capture request; the user completes setup in Quest.
        public void SetUpRoom()
        {
            if (SettingUpRoom) return;
            // 已经请求打开系统页或正在刷新时直接退出，避免重复创建设置流程。
            if (!Granted) { setupAfterPermission = true; Request(); return; }
            // 没权限时记下“授权后继续”，先弹权限请求，然后在授权回调中重进本方法。
            setupAfterPermission = false;
            // 权限已具备，清除待办标记，准备直接请求 Quest 设置页。
            if (session.subsystem is MetaOpenXRSessionSubsystem meta && meta.TryRequestSceneCapture())
            {
                // is ... meta 先确认子系统类型并取出引用；&& 再调用 Meta 接口，任一步不成立就走备用提示。
                roomSetupOpened = true;
                Message = "Opening Quest room setup...";
                Debug.Log("HideAR: Quest room setup requested.");
            }
            // 只有当前 AR 子系统是 Meta OpenXR，且 Quest 接受了请求，才显示正在打开。
            else
            {
                Message = "Open Space Setup in Quest Settings,\nthen return here.";
                Debug.LogWarning("HideAR: room setup request was unavailable. Use Quest Space Setup.");
            }
            // 如果接口不可用，提示用户手动打开设置；无论哪种分支，本脚本都不生成房间几何。
            // A successful request only opens Quest setup; the user must finish it and return.
        }

        // ===== 从 Quest 系统界面返回时重新检查权限 =====
        void OnApplicationPause(bool paused)
        {
            if (paused) return;
            // paused=true 是离开应用去系统界面，不在那时刷新；paused=false 表示回到游戏。
            roomSetupOpened = false;
            // 用户已回到本应用，清除“Quest 设置页仍打开”的状态。
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission("com.oculus.permission.USE_SCENE"))
            {
                Granted = false;
                planes.enabled = raycasts.enabled = false;
                Message = "Room permission is missing. Allow Spatial Data and retry.";
                // 权限被撤销就关闭两个查询组件，并提醒 GameFlow 本次返回处理已经结束。
                RefreshCount++;
                return;
            }
            Allow();
#endif
            // Editor/其他平台若没有走 Android Allow，或设备仍未授权，就不能重启平面查询。
            if (!Granted) return;
            if (!refreshing) StartCoroutine(RefreshRoom());
            // 只在尚无刷新协程时启动一个，防止快速返回多次导致并行关闭/开启组件。
            // On resume, permission is checked again before the room-query components restart.
        }

        // ===== 关闭再开启平面/射线组件，刷新当前查询 =====
        // This coroutine restarts the components; it does not wait for a completed room scan.
        IEnumerator RefreshRoom()
        {
            refreshing = true;
            // 告诉 GameFlow 暂停放置和卡片移动，直到本次关闭/开启流程结束。
            planes.enabled = false;
            raycasts.enabled = false;
            Message = "Loading current room surfaces...";
            yield return null;
            // yield return null 暂停到下一帧；这样组件有机会先完成关闭再重新启动。
            // 注意这里只等一帧，不是在等 Quest 完成扫描或返回平面。
            if (Granted)
            {
                planes.enabled = true;
                raycasts.enabled = true;
                Message = "";
                Debug.Log("HideAR: current room surfaces refreshed.");
            }
            // 只有权限仍允许时才重新启用组件；权限被撤销就保持关闭。
            refreshing = false;
            RefreshCount++;
            // 计数表示组件开关流程结束，GameFlow 可召回卡片；不代表已取得任何平面。
            // A completed refresh does not prove that Quest returned usable room planes.
        }
    }
}
