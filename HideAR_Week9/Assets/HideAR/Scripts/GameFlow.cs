// GameFlow.cs：HideAR 的流程协调器，读取头显/手柄状态，并把操作交给其他脚本。
// RoundState 管阶段规则；RoomAccess 管空间权限；PlaceOnPlane 管放置；GameUI 管文字、按钮和卡片位置。
// It connects Unity/Meta tracking and input to the game's own round rules.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace HideAR.Classroom
{
    // Unity 会自动调用 Update/LateUpdate。-20 让本脚本先检查追踪，再由 PlaceOnPlane 处理射线。
    [DefaultExecutionOrder(-20)]
    public sealed class GameFlow : MonoBehaviour
    {
        // ===== Inspector 引用与协调状态 =====
        public RoomAccess room; // RoomAccess：Spatial Data 权限、Space Setup 和 ARPlaneManager。
        public PlaceOnPlane placement; // PlaceOnPlane：手柄射线、模型选择、预览、Anchor 和命中判断。
        public GameUI ui; // GameUI：显示当前阶段、按钮和菜单预览，并移动任务卡。
        public InputActionReference recenterAction; // Unity Input System 中绑定手柄 A 键的动作。
        public RoundState Round { get; } = new(); // 本局游戏规则；new() 创建一个 RoundState 实例。
        public bool Tracked { get; private set; } // 本帧是否允许交互；放置后还要同时确认 Anchor 正在追踪。
        public string Error { get; private set; } // Error 阶段显示的原因，例如 Anchor 丢失。
        Stage shownStage = (Stage)(-1); // 上次处理的阶段；-1 确保场景首次运行时进入阶段更新。
        bool paused; // 应用是否暂停，例如切到 Quest 系统设置。
        bool focused = true; // 应用是否拥有系统输入焦点。
        bool menuReturnPending; // 卡片召回请求；等追踪恢复/Space Setup 结束后再执行。
        int shownRoomRefresh; // 已处理过的 RoomAccess 刷新次数，用于发现用户返回应用。
        float nextUIUpdate; // 下一次更新 UI 的时间；卡片跟随仍每帧检查。

        // ===== 每帧读取设备状态并推进流程 =====
        // Called every frame: check tracking, update input rules, then update the menu and UI.
        void Update()
        {
            // ===== 检查头显是否可用 =====
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked = !paused && focused && head.TryGetFeatureValue(CommonUsages.isTracked, out bool valid) && valid;
            // 这里的 && 要求所有项都成立：应用未暂停、有焦点、XR 系统成功读到头显且 isTracked=true。
            // out bool valid 接收 XR 系统返回的追踪值；读取失败也会使 tracked=false。
            if (head.TryGetFeatureValue(CommonUsages.userPresence, out bool present) && !present) tracked = false;
            // 读到了 userPresence 且 present=false 时暂停；它不判断当前是不是另一位玩家。
            if (Round.Stage == Stage.Review || Round.Stage == Stage.Handoff || Round.Stage == Stage.Countdown || Round.Stage == Stage.Seeking)
            {
                // 这些阶段需要上一步创建的藏匿 Anchor；保存仍在进行时暂时允许异步结果返回。
                if (!placement.Busy && placement.Anchor == null) Fail("The hiding spot was lost. Restart the game.");
                // Busy=false 且 Anchor=null 表示没有保存任务、也没有藏匿位置，本轮不能安全继续。
                tracked &= placement.Tracking;
                // &= 把 Anchor 状态也并入 tracked：头显和藏匿 Anchor 都被追踪才可计时/操作。
            }

            // ===== 更新 A 键、扳机防误触和计时 =====
            bool movingMenu = tracked && recenterAction.action.IsPressed();
            // 只有追踪有效且 A 键按住时，才把这一帧视为移动菜单请求。
            if (tracked && !Tracked) Round.Disarm();
            // 本帧刚从无追踪恢复时锁住扳机，避免设备恢复前一直按着的 Trigger 立刻触发。
            Tracked = tracked;
            Round.Tick(Time.unscaledDeltaTime, tracked);
            // 把未缩放的本帧时间和追踪结果交给 RoundState；丢失追踪时它暂停倒数/寻找计时。
            if (movingMenu) Round.Disarm();
            // A 键移动卡片时不允许同一段扳机按压再放置或报找到。
            else if (tracked) Round.UpdateInput(placement.placeAction.action.IsPressed(), Time.unscaledDeltaTime);
            // 只有追踪有效且没有移动菜单时，才把扳机松开时间交给 RoundState 累计。

            // ===== 阶段改变时更新正式物品显隐和菜单召回 =====
            if (shownStage != Round.Stage)
            {
                shownStage = Round.Stage;
                // 每次只在阶段改变时运行一次，避免每帧都重复安排菜单召回。
                if (placement.Dinosaur != null)
                    placement.Dinosaur.SetActive(shownStage == Stage.Review || shownStage == Stage.Seeking || shownStage == Stage.Found);
                // Dinosaur 是 Anchor 下的正式模型，不是 UI 预览；仅检查、寻找、结果阶段显示。
                if (shownStage == Stage.Welcome || shownStage == Stage.Placing || shownStage == Stage.Review ||
                    shownStage == Stage.Handoff || shownStage == Stage.Found || shownStage == Stage.Error)
                    menuReturnPending = true;
                // 这些有按钮的阶段把卡片召回一次；Countdown/Seeking 不因阶段变化自动挪卡片。
                nextUIUpdate = 0;
                // 归零让下方 UI 不等 0.1 秒间隔，马上显示新阶段文字。
            }

            // ===== 持续处理菜单位置，节流更新 UI =====
            UpdateMenuPosition(tracked, movingMenu);
            if (Time.unscaledTime < nextUIUpdate) return;
            // 时间还没到就只跳过下面的平面/文字刷新；上面的计时和菜单跟随已经完成。
            nextUIUpdate = Time.unscaledTime + 0.1f;
            // 把下次刷新时间设为当前时间后 0.1 秒，减少重复刷新文字和 Renderer。
            ApplyPlaneVisibility();
            ui.Refresh(this);
            // GameUI 读取本脚本状态后改文字/按钮；不会在 Refresh 里改变 Round.Stage。
            // UI refresh is limited to 10 times per second; the Handoff card still follows every frame.
        }

        // ===== 处理房间刷新召回、A 键定位和准备页跟随 =====
        // GameUI performs the move in LateUpdate; this method decides when to request it.
        void UpdateMenuPosition(bool tracked, bool movingMenu)
        {
            if (shownRoomRefresh != room.RefreshCount)
            {
                shownRoomRefresh = room.RefreshCount;
                menuReturnPending = true;
            }
            // 计数改变只表示组件刷新流程结束，不表示 Quest 已经返回任何平面。
            bool followReady = Round.Stage == Stage.Handoff;
            // 只有 Handoff 的 Ready to search 卡片持续跟着用户；其他页面只在需要时移动一次。
            bool repositioning = !ui.HasPosition || movingMenu || menuReturnPending;
            // 第一次、按 A、房间返回召回都要求重新定位；repositioning=false 表示没有一次性请求。
            if (!tracked || room.SettingUpRoom || (!repositioning && !followReady)) return;
            // 无追踪或正在系统设置就暂不移动；非 Handoff 且没有定位请求时也保持原位。
            ui.RequestRecenter();
            // 把请求交给 GameUI；这里不直接改 UI Transform 或正式模型的位置。
            if (repositioning) Round.Disarm();
            // 首次/按 A/召回后锁输入；Handoff 每帧跟随时不锁，否则扳机永远无法解锁 Start search。
            menuReturnPending = false;
            // 请求已交给 GameUI；如果前面的条件 return，待办标记不会被清除。
            // Only the Handoff card follows the wearer. The placed object's Anchor does not move.
        }

        void LateUpdate() { ApplyPlaneVisibility(); }
        // ARPlaneManager 可能在 Update 后重新绘制平面，所以这里再按当前阶段隐藏/显示。
        // This prevents plane visuals from reappearing during search.

        // ===== 只显示放置阶段可用的向上水平面网格 =====
        void ApplyPlaneVisibility()
        {
            bool showHorizontalPlanes = Round.Stage == Stage.Placing;
            // showHorizontalPlanes=true 只表示当前处于 Placing；下面仍会排除墙面等其他方向。
            foreach (var plane in room.planes.trackables)
                foreach (var renderer in plane.GetComponentsInChildren<Renderer>())
                    renderer.enabled = showHorizontalPlanes && plane.alignment == PlaneAlignment.HorizontalUp;
        }
        // foreach 会逐一查看 Quest 提供的平面和绘制它的 Renderer；仅 Placing + HorizontalUp 才显示。
        // 关 Renderer 只是隐藏平面网格，不删 Quest 房间数据，也不会让虚拟物体被现实墙面挡住。
        // Unity/Meta provides the planes; this code only controls their visible mesh.

        // ===== 主按钮：同一个按钮在不同阶段执行不同动作 =====
        // Inspector 的 Button.onClick 连接到这里；switch 根据 Round.Stage 选择当前页面的动作。
        // Primary action for Welcome, Review, Handoff, Found, and Error.
        public void Primary()
        {
            if (Round.Stage == Stage.Error) { ResetGame(); return; }
            // Error 页先允许重启，然后 return 避免继续执行下面普通的主按钮分支。
            if (!Tracked || room.SettingUpRoom) return;
            // 两种情况都会拒绝操作：头显/Anchor 追踪无效，或 Quest Space Setup 还没结束。
            switch (Round.Stage)
            {
                case Stage.Welcome:
                case Stage.Found:
                    if (!room.Granted) { room.Request(); return; }
                    // 权限未给时只请求权限并结束这次点击；授权后玩家还要再按 Start hiding。
                    if (placement.Available && placement.Clear()) Round.Start();
                    // 必须 Anchor 子系统正在运行，且旧物/Anchor 清理成功，才开始新一轮。
                    break;
                case Stage.Review: Round.Confirm(); break;
                // Hider 确认藏匿位置后转到 Handoff；不会在这里开始寻找倒数。
                case Stage.Handoff: Round.Ready(); break;
                // Seeker 在准备页明确按 Start search，RoundState 才开始三秒倒数。
            }
            Round.Disarm();
            // 按钮动作完成后锁住扳机，玩家必须松开再按，避免一次 Trigger 连续操作。
            // GameFlow routes the click; RoundState decides whether the stage change is allowed.
        }

        // ===== 次按钮：Review 时重新放置，Welcome/Placing 时换模型 =====
        // Secondary action for the hider's review and object-selection screens.
        public void Secondary()
        {
            if (Round.Stage == Stage.Review)
            {
                if (Tracked && placement.Clear()) Round.Move();
                // 只有追踪有效且 Clear 成功移除旧 Anchor，才回 Placing 重新选择位置。
            }
            else if ((Round.Stage == Stage.Welcome || Round.Stage == Stage.Placing) && Tracked && !room.SettingUpRoom)
                placement.SelectNext();
            // Welcome/Placing 次按钮切换模型；其他阶段不运行 SelectNext。
            Round.Disarm();
            // 完成本次点击后重新锁住 Trigger。
        }
        // PlaceOnPlane 还会拒绝 Busy 或已有 Anchor 的模型切换，避免修改已经藏好的物品。
        // The secondary button changes meaning with the current hider stage.

        // ===== 独立按钮请求 Quest Space Setup =====
        // This button opens Quest's room-setup flow; it does not perform a custom scan.
        public void SetUpRoom()
        {
            if (!Tracked || placement.Busy || room.SettingUpRoom) return;
            // 追踪无效、正在等 Anchor 保存或已有设置流程时，不再打开系统页面。
            if (Round.Stage != Stage.Welcome && Round.Stage != Stage.Placing) return;
            // 只有 Welcome/Placing 显示给 Hider 的页面可以发起房间设置。
            Round.Disarm();
            room.SetUpRoom();
        }
        // RoomAccess 再检查权限并请求 Quest 系统页面；空间平面由官方 XR 系统提供。
        // Opens Quest Space Setup through RoomAccess; it does not run a custom room scan.

        // ===== 色块按钮只在 Hider 选物和检查阶段生效 =====
        public void SelectColor(int index)
        {
            if (!Tracked) return;
            // 头显/Anchor 追踪不可用时不处理色块点击。
            if (Round.Stage != Stage.Welcome && Round.Stage != Stage.Placing && Round.Stage != Stage.Review) return;
            // 这三个条件以外的阶段都拒绝换色，避免 Handoff/Seeking 泄露藏匿物外观。
            placement.SelectColor(index);
            // PlaceOnPlane 检查 index 是否在颜色数组范围内，再更新模型/预览实例。
            Round.Disarm();
        }
        // index 是颜色编号；这里只转交选择，不直接改共享材质。
        // Color selection is limited to the hider's stages.

        public void ResetGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        // Restart 重新载入整个 Unity 场景；Play again 则在 Primary 中先清 Anchor 再重开回合。
        public void Fail(string message) { Error = message; Round.Fail(); }
        // 保存错误页要显示的文字，并让 RoundState 进入 Error；此处不直接改 UI。
        void OnApplicationPause(bool value) { paused = value; Round.Disarm(); }
        void OnApplicationFocus(bool value) { focused = value; Round.Disarm(); }
        // Unity 在 Quest 切应用/恢复焦点时调用；先锁住 Trigger，Update 下一帧再验证追踪。
    }
}
