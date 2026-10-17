using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace HideAR.Classroom
{
    // 职责：连接官方设备输入、项目回合规则、放置和 UI，不实现追踪算法。
    // Role: connect device input, round rules, placement and UI, without custom tracking.
    // 先于普通 Update 执行，让本帧的追踪/输入保护先更新，再处理放置射线。
    // Run before ordinary Updates so tracking/input guards precede placement processing.
    [DefaultExecutionOrder(-20)]
    public sealed class GameFlow : MonoBehaviour
    {
        public RoomAccess room;
        public PlaceOnPlane placement;
        public GameUI ui;
        public InputActionReference recenterAction;
        public RoundState Round { get; } = new();
        public bool Tracked { get; private set; }
        public string Error { get; private set; }
        Stage shownStage = (Stage)(-1);
        bool paused;
        bool focused = true;
        float nextUIUpdate;

        void Update()
        {
            // 读取 SDK 追踪结果；佩戴状态只用于暂停，不用来检测是否换了玩家。
            // Read SDK tracking; user presence pauses play but never identifies a new player.
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked = !paused && focused && head.TryGetFeatureValue(CommonUsages.isTracked, out bool valid) && valid;
            if (head.TryGetFeatureValue(CommonUsages.userPresence, out bool present) && !present) tracked = false;
            if (Round.Stage == Stage.Review || Round.Stage == Stage.Handoff || Round.Stage == Stage.Countdown || Round.Stage == Stage.Seeking)
            {
                if (!placement.Busy && placement.Anchor == null) Fail("The hiding spot was lost. Restart the game.");
                tracked &= placement.Tracking;
            }
            // 首次定位或按住 A 才搬菜单；换阶段和恢复追踪不重定位已固定的卡片。
            // Move the menu only on first placement or held A, not stage changes or tracking recovery.
            bool movingMenu = tracked && recenterAction.action.IsPressed();
            if (tracked && !Tracked) Round.Disarm();
            if (tracked && (!ui.HasPosition || movingMenu)) ui.RequestRecenter();
            Tracked = tracked;
            // 时间和防误触交给纯规则类；调整菜单期间不允许放置或找到。
            // Delegate timing/input gates to the rules; block placement/finding while moving the menu.
            Round.Tick(Time.unscaledDeltaTime, tracked);
            if (movingMenu) Round.Disarm();
            else if (tracked) Round.UpdateInput(placement.placeAction.action.IsPressed(), Time.unscaledDeltaTime);
            // 交接和倒数时隐藏已藏物品；这里只切换显示，不改变 Anchor 的位置。
            // Hide the object during handoff/countdown; visibility changes never move its anchor.
            if (shownStage != Round.Stage)
            {
                shownStage = Round.Stage;
                if (placement.Dinosaur != null)
                    placement.Dinosaur.SetActive(shownStage == Stage.Review || shownStage == Stage.Seeking || shownStage == Stage.Found);
                nextUIUpdate = 0;
            }
            if (Time.unscaledTime < nextUIUpdate) return;
            nextUIUpdate = Time.unscaledTime + 0.1f;
            ApplyPlaneVisibility();
            ui.Refresh(this);
        }

        // 平面组件更新后再次约束显示，避免寻找阶段偶尔闪出绿色平面提示。
        // Reapply visibility after updates so plane visuals do not reveal hints during searching.
        void LateUpdate() { ApplyPlaneVisibility(); }

        void ApplyPlaneVisibility()
        {
            bool showHorizontalPlanes = Round.Stage == Stage.Placing;
            foreach (var plane in room.planes.trackables)
                foreach (var renderer in plane.GetComponentsInChildren<Renderer>())
                    renderer.enabled = showHorizontalPlanes && plane.alignment == PlaneAlignment.HorizontalUp;
        }

        // 下列公开方法通过 Inspector 中普通 Button 的 onClick 连接，没有自定义事件总线。
        // Public handlers below are wired through ordinary Button.onClick events, not an event bus.
        // Primary 随阶段表示开始藏匿、确认位置、明确开始寻找或再玩一次。
        // Primary means start hiding, confirm, explicitly start searching, or replay by stage.
        public void Primary()
        {
            if (Round.Stage == Stage.Error) { ResetGame(); return; }
            if (!Tracked || room.SettingUpRoom) return;
            switch (Round.Stage)
            {
                case Stage.Welcome:
                case Stage.Found:
                    if (!room.Granted) { room.Request(); return; }
                    if (placement.Available && placement.Clear()) Round.Start();
                    break;
                case Stage.Review: Round.Confirm(); break;
                case Stage.Handoff: Round.Ready(); break;
            }
            Round.Disarm();
        }

        // 检查位置时表示重新放置，开场/放置时表示换模型，不再复用为房间设置。
        // Secondary repositions in review or changes the model before placement, never room setup.
        public void Secondary()
        {
            if (Round.Stage == Stage.Review)
            {
                if (Tracked && placement.Clear()) Round.Move();
            }
            else if ((Round.Stage == Stage.Welcome || Round.Stage == Stage.Placing) && Tracked && !room.SettingUpRoom)
                placement.SelectNext();
            Round.Disarm();
        }

        // 独立的系统房间设置入口；进入设置前先锁住下一次扳机操作。
        // Keep room setup separate and disarm input before opening the system flow.
        public void SetUpRoom()
        {
            if (!Tracked || placement.Busy || room.SettingUpRoom) return;
            if (Round.Stage != Stage.Welcome && Round.Stage != Stage.Placing) return;
            Round.Disarm();
            room.SetUpRoom();
        }

        public void SelectColor(int index)
        {
            if (!Tracked) return;
            if (Round.Stage != Stage.Welcome && Round.Stage != Stage.Placing && Round.Stage != Stage.Review) return;
            placement.SelectColor(index);
            Round.Disarm();
        }

        // Reset 重载课堂场景；Play again 则由 Primary 清理物品并开启新回合。
        // Reset reloads the classroom scene; Play again clears placement through Primary.
        public void ResetGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        public void Fail(string message) { Error = message; Round.Fail(); }
        void OnApplicationPause(bool value) { paused = value; Round.Disarm(); }
        void OnApplicationFocus(bool value) { focused = value; Round.Disarm(); }
    }
}
