// ClassroomTests.cs：用 NUnit 检查回合规则，以及已保存 Scene 的引用、按钮、模型和菜单行为。
// RoundTests 直接调用普通 C# 规则；SceneTests 在 Editor 中检查资源/模拟输入，不操作 Quest 硬件。
// These Editor tests catch project and logic errors; they are not real headset tests.
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using HideAR.Classroom.Editor;

namespace HideAR.Classroom.Tests
{
    // RoundState tests: pure game rules, with no Unity scene, permission prompt, or real Anchor.
    public class RoundTests
    {
        // ===== 测试辅助方法：直接把规则对象推进到 Review =====
        // Builds a fake review state without calling ARAnchorManager.
        static RoundState Review()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 0.36f);
            game.BeginPlacement(); game.PlacementFinished(); return game;
        }
        // 0.36 秒超过 0.35 秒解锁要求；BeginPlacement/PlacementFinished 只是直接改规则阶段。

        // ===== 测试辅助方法：从 Review 确认并开始三秒倒数 =====
        static RoundState Countdown()
        {
            var game = Review(); game.Confirm();
            game.UpdateInput(false, 0.36f); game.Ready(); return game;
        }
        // 后续计时测试共用这个初始状态，确保都从明确点击 Ready 后的 Countdown 开始。

        // ===== Welcome 不能跳过选择步骤直接放置 =====
        [Test] public void WelcomeNeverPlaces() { Assert.IsFalse(new RoundState().BeginPlacement()); }
        // 初始 Stage 是 Welcome；BeginPlacement 应拒绝请求并返回 false，避免直接生成 Anchor。
        // Placement cannot start before the hider enters Placing.

        // ===== Start hiding 的同一次 Trigger 按压不能又完成放置 =====
        [Test] public void StartPressDoesNotPlace()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(true, 2);
            Assert.IsFalse(game.BeginPlacement()); game.UpdateInput(false, 0.1f);
            Assert.IsFalse(game.BeginPlacement()); game.UpdateInput(false, 0.26f);
            Assert.IsTrue(game.BeginPlacement());
        }
        // 第一次按住 Trigger 仍锁定；松开 0.1 秒不够，累计到 0.36 秒后才允许放置。
        // The trigger must be released for 0.35 seconds before placement is accepted.

        // ===== 松开解锁后，下一次新的按压可以继续 =====
        [Test] public void InputStaysArmedForNextPress()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 1); game.UpdateInput(true, 0.02f);
            Assert.IsTrue(game.BeginPlacement());
        }
        // 先松开 1 秒解锁，再按下；Armed 必须保持到 BeginPlacement 读取它。

        // ===== 第一次请求进入 Saving 后拒绝第二次 Anchor 请求 =====
        [Test] public void OnlyOnePendingPlacement()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 1);
            Assert.IsTrue(game.BeginPlacement()); game.UpdateInput(false, 1);
            Assert.IsFalse(game.BeginPlacement());
        }
        // BeginPlacement 第一次返回 true 并转到 Saving；即使再次松开，Saving 仍会拒绝第二次请求。

        // ===== Anchor 创建后先停在 Review，不能跳过 Hider 确认 =====
        [Test] public void PlacementWaitsForConfirmation()
        {
            var game = Review(); game.Tick(100, true); Assert.AreEqual(Stage.Review, game.Stage);
            // Review 不累计寻找计时；Find 和 BeginPlacement 都不应在检查页面执行。
            Assert.IsFalse(game.Find()); Assert.IsFalse(game.BeginPlacement());
        }
        // 即使传入 100 秒也留在 Review；藏的人必须自己点 Confirm 才能交接。

        // ===== 点 Move object 后必须松开 Trigger 才能重新放置 =====
        [Test] public void RepositionRequiresRelease()
        {
            var game = Review(); game.Move(); Assert.AreEqual(Stage.Placing, game.Stage);
            // Move 切回 Placing 同时锁住输入；仍未松开时不能马上保存新位置。
            Assert.IsFalse(game.BeginPlacement()); game.UpdateInput(false, 1); Assert.IsTrue(game.BeginPlacement());
        }
        // 松开一秒后才接受下一次放置，避免同一次按钮按压完成 Move 和 Place 两步。

        // ===== Handoff 等待期间不自动开始倒数或计时 =====
        [Test] public void HandoffWaitsForSeeker()
        {
            var game = Review(); game.Confirm(); game.Tick(100, true);
            Assert.AreEqual(Stage.Handoff, game.Stage); Assert.AreEqual(0, game.Seconds);
        }
        // Confirm 只把阶段交给 Seeker；即使持续更新 100 秒，没点 Ready 仍在 Handoff，Seconds 仍为 0。

        // ===== 松开扳机并明确 Ready 即可开始，不需要摘下头显 =====
        [Test] public void CanStartCountdownWithoutRemovingHeadset()
        {
            var game = Review(); game.Confirm();
            game.UpdateInput(false, 1); game.Ready();
            Assert.AreEqual(Stage.Countdown, game.Stage); Assert.AreEqual(3, game.Countdown);
        }
        // 规则只要求新鲜扳机释放和 Ready 调用；这里没有检测 userPresence 或摘戴头显状态。

        // ===== Start search 同时需要扳机释放和明确点击 =====
        [Test] public void StartSearchNeedsFreshReleaseAndExplicitClick()
        {
            var game = Review(); game.Confirm(); game.UpdateInput(true, 1);
            game.Ready(); Assert.AreEqual(Stage.Handoff, game.Stage);
            game.UpdateInput(false, 0.36f); game.Tick(100, true);
            Assert.AreEqual(Stage.Handoff, game.Stage); Assert.IsTrue(game.CanReady);
            game.Ready(); Assert.AreEqual(Stage.Countdown, game.Stage); Assert.AreEqual(3, game.Countdown);
        }
        // 按住时 CanReady=false，Ready 不改变 Handoff；松开后 Tick 仍不开始，第二次 Ready 才进 Countdown。

        // ===== Tracking 恢复只恢复操作条件，不代替 Start search =====
        [Test] public void TrackingRecoveryDoesNotStartSearchAutomatically()
        {
            var game = Review(); game.Confirm();
            game.Tick(100, false); game.Tick(100, true);
            Assert.AreEqual(Stage.Handoff, game.Stage);
            game.UpdateInput(false, 1); game.Ready(); Assert.AreEqual(3, game.Countdown);
        }
        // 追踪丢失期间计时暂停并锁输入；恢复后依然停在 Handoff，需松开再明确 Ready。

        // ===== 倒数期间 Tracking 失效时暂停倒数 =====
        [Test] public void CountdownPausesWithoutTracking()
        {
            var game = Countdown(); game.Tick(100, false);
            Assert.AreEqual(3, game.Countdown); game.Tick(3, true); Assert.AreEqual(Stage.Seeking, game.Stage);
        }
        // tracked=false 时 100 秒不扣 Countdown；恢复后有效累计 3 秒才转到 Seeking。

        // ===== 寻找用时在失去追踪时暂停，在 Found 后冻结 =====
        [Test] public void SearchTimePausesAndStopsOnFound()
        {
            var game = Countdown(); game.Tick(3, true); game.UpdateInput(false, 1);
            game.Tick(5, true); game.Tick(100, false); Assert.AreEqual(5, game.Seconds);
            Assert.IsFalse(game.Find()); game.UpdateInput(false, 1); Assert.IsTrue(game.Find());
            game.Tick(100, true); Assert.AreEqual(5, game.Seconds);
        }
        // 追踪失效期间仍是 5 秒；重新解锁后 Find 进入 Found，之后再 Tick 100 秒成绩仍是 5 秒。

        // ===== Play again 开新回合时清除旧成绩 =====
        [Test] public void ReplayClearsTimer()
        {
            var game = Countdown(); game.Tick(3, true); game.UpdateInput(false, 1);
            game.Tick(5, true); game.Find(); Assert.IsTrue(game.Start()); Assert.AreEqual(0, game.Seconds);
        }
        // Start 返回 true 且 Seconds=0，说明上一轮用时不会沿用到新回合。

        // ===== 进入 Error 后忽略迟到的 PlacementFinished =====
        [Test] public void TimedOutResultCannotEnterReview()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 1); game.BeginPlacement();
            game.Fail(); game.PlacementFinished(); Assert.AreEqual(Stage.Error, game.Stage);
        }
        // 此测试只调用规则方法，不模拟 SDK 真实回调；它验证 Error 不会被迟到通知改回 Review。
    }

    // SceneTests: checks the saved scene and simulated Unity objects; no test communicates with Quest hardware.
    public class SceneTests
    {
        GameFlow game; // 当前测试所用的 GameFlow；SetUp 每次从保存的 Scene 重新取得它。
        // ===== Helper：在 EditMode 调用运行时的 private 菜单方法 =====
        static void UpdateMenu(GameUI ui) { typeof(GameUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null); }
        void PositionMenu(bool tracked, bool moving = false) { typeof(GameFlow).GetMethod("UpdateMenuPosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { tracked, moving }); }
        // Reflection 按方法名调用 private 方法；tracked/moving 是测试传入的假状态，不来自头显或 A 键。
        // These helpers call runtime code with simulated input values.

        // ===== NUnit 每项测试开始前重新加载课堂 Scene =====
        // A fresh scene prevents one test's changed transforms or stages from affecting another.
        [SetUp] public void OpenScene()
        {
            EditorSceneManager.OpenScene(ClassroomSetup.ScenePath);
            game = Object.FindAnyObjectByType<GameFlow>();
        }
        // [SetUp] 告诉 NUnit 每个测试先运行 OpenScene，避免共享前一项测试的内存改动。

        // ===== 检查 Scene 中项目脚本、Inspector 对象引用是否缺失 =====
        // This checks saved references, not runtime hardware behavior.
        [Test] public void NoMissingReferencesOrScripts()
        {
            Assert.NotNull(game);
            foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.NotNull(component);
                if (component.GetType().Namespace != "HideAR.Classroom") continue;
                // 不是本项目命名空间的 Unity/XR 组件就跳过；只检查 HideAR 自己的序列化引用。
                var fields = new SerializedObject(component).GetIterator();
                while (fields.NextVisible(true))
                    if (fields.propertyType == SerializedPropertyType.ObjectReference) Assert.NotNull(fields.objectReferenceValue, component.name + ": " + fields.name);
                // ObjectReference 字段若为空，Assert.NotNull 会用组件名/字段名报告错误，定位未连上的 Inspector 槽。
            }
        }
        // 这只证明 Scene 引用没有空槽；不证明真实手柄、权限、Anchor 或房间平面能运行。

        // ===== 检查 ARSession 数量、初始权限门控和 XR UI 组件 =====
        [Test] public void OneSessionAndPermissionGatedManagers()
        {
            Assert.AreEqual(1, Object.FindObjectsByType<ARSession>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(game.room.planes.enabled); Assert.IsFalse(game.room.raycasts.enabled);
            Assert.NotNull(game.ui.head.GetComponent<TrackedPoseDriver>());
            Assert.NotNull(Object.FindAnyObjectByType<XRUIInputModule>());
        }
        // Scene 初始时 ARPlaneManager/ARRaycastManager 都应关闭，等 RoomAccess 授权后再启动。
        // TrackedPoseDriver 和 XRUIInputModule 是 Unity XR 组件，不是项目自己计算头部姿态/按钮射线。

        // ===== 检查整张 UI 卡片可挡放置射线，所有按钮都连到 GameFlow =====
        [Test] public void MenuBlocksPlacementAndButtonsAreWired()
        {
            Assert.AreEqual(5, game.ui.gameObject.layer);
            var box = game.ui.GetComponent<BoxCollider>(); Assert.NotNull(box);
            Assert.AreEqual(((RectTransform)game.ui.transform).rect.width, box.size.x);
            foreach (var button in new[] { game.ui.primary, game.ui.secondary, game.ui.reset, game.ui.roomSetup }.Concat(game.ui.colorButtons))
            {
                Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
                Assert.AreEqual(game, button.onClick.GetPersistentTarget(0));
            }
        }
        // UI Layer 和整卡 BoxCollider 供 PlaceOnPlane 拦截射线；这里只检查配置，不模拟实际手柄点击。
        // Every button must have exactly one saved click target, the GameFlow component.

        // ===== 头显接近垂直朝上/下时，卡片仍保持之前的水平朝向 =====
        [Test] public void MenuKeepsItsHeadingWhenLookingAlmostVertically()
        {
            var ui = game.ui;
            ui.head.transform.rotation = Quaternion.Euler(0, 73, 0);
            ui.Recenter();
            var heading = ui.transform.forward;
            foreach (float pitch in new[] { 80f, 89.9f, 90f, -89.9f, -90f })
            {
                ui.head.transform.rotation = Quaternion.Euler(pitch, 210, 0);
                ui.Recenter();
                Assert.Less(Vector3.Angle(heading, ui.transform.forward), 0.01f);
                Assert.Less(Vector3.Angle(Vector3.up, ui.transform.up), 0.01f);
                Assert.That(Vector3.Distance(ui.transform.position, ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f), Is.LessThan(0.0001f));
            }
        }
        // 将模拟相机俯仰到接近 ±90 度；检查卡片朝向不突变、顶部仍朝世界上方、距离仍是 1.5 米。

        // ===== 第一次定位时若正低头，用头部左右转向算备用水平方向 =====
        [Test] public void FirstMenuPlacementWhileLookingDownUsesHeadYaw()
        {
            var ui = game.ui;
            Assert.IsFalse(ui.HasPosition);
            ui.head.transform.rotation = Quaternion.Euler(90, 125, 0);
            ui.Recenter();
            var expected = Quaternion.Euler(0, 125, 0) * Vector3.forward;
            Assert.Less(Vector3.Angle(expected, ui.transform.forward), 0.01f);
            Assert.Less(Vector3.Angle(Vector3.up, ui.transform.up), 0.01f);
        }
        // HasPosition=false 表示还没有上次方向；期望卡片仍采用相机的 yaw=125 度，不会朝任意方向。

        // ===== 用两种模拟眼高检查卡片都位于视线下方 10 厘米 =====
        [Test] public void MenuHeightAdaptsToSeatedAndStandingPlayers()
        {
            var ui = game.ui;
            ui.head.transform.rotation = Quaternion.Euler(20, 40, 30);
            var heading = Vector3.ProjectOnPlane(ui.head.transform.forward, Vector3.up).normalized;
            foreach (float height in new[] { 1.1f, 1.8f })
            {
                ui.head.transform.position = new Vector3(3, height, -4);
                ui.Recenter();
                Assert.That(ui.transform.position.y, Is.EqualTo(height - 0.1f).Within(0.0001f));
                Assert.That(Vector3.Distance(ui.transform.position, ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f), Is.LessThan(0.0001f));
                Assert.Less(Vector3.Angle(Vector3.up, ui.transform.up), 0.01f);
            }
        }
        // 这里仅把 Camera 高度设为 1.1/1.8 米并验证公式，不是 Quest 上的坐姿/站姿体验测试。

        // ===== RequestRecenter 延迟到 LateUpdate 使用最新姿态，之后卡片固定 =====
        [Test] public void MenuRequestUsesLatestPoseAndThenStaysPinned()
        {
            var ui = game.ui;
            ui.RequestRecenter();
            Assert.IsFalse(ui.HasPosition);
            ui.head.transform.SetPositionAndRotation(new Vector3(2, 1.2f, 3), Quaternion.Euler(0, 90, 0));
            UpdateMenu(ui);
            Assert.IsTrue(ui.HasPosition);
            Assert.That(Vector3.Distance(ui.transform.position, new Vector3(3.5f, 1.1f, 3)), Is.LessThan(0.0001f));
            var position = ui.transform.position;
            var rotation = ui.transform.rotation;
            ui.head.transform.SetPositionAndRotation(new Vector3(-3, 1.8f, 1), Quaternion.Euler(0, 220, 0));
            UpdateMenu(ui);
            Assert.AreEqual(position, ui.transform.position);
            Assert.AreEqual(rotation, ui.transform.rotation);
        }
        // 请求后先改变 Camera，再手动调用 LateUpdate；卡片应按新位置移到 1.5 米前方，此后头动不影响它。

        // ===== 每个 Stage 都检查卡片何时移动，并确认正式模型不随卡片移动 =====
        [Test] public void InteractiveStagesRecallAndOnlyReadinessFollowsWithoutMovingPlacedObject()
        {
            var anchorObject = new GameObject("Test Anchor");
            // 这是场景里的普通 GameObject，不是 ARAnchorManager 创建的 Quest 空间 Anchor。
            anchorObject.SetActive(false);
            var anchor = anchorObject.AddComponent<ARAnchor>();
            var placed = Object.Instantiate(game.placement.SelectedPrefab, anchor.transform);
            // 模拟 Anchor 子物体上的正式模型，记录世界位置用来确认 UI 移动不会带着它走。
            anchor.transform.position = new Vector3(-2, 0.8f, 4);
            typeof(PlaceOnPlane).GetProperty(nameof(PlaceOnPlane.Anchor)).SetValue(game.placement, anchor);
            typeof(PlaceOnPlane).GetProperty(nameof(PlaceOnPlane.Dinosaur)).SetValue(game.placement, placed);
            try
            {
                game.ui.Recenter();
                var objectPosition = placed.transform.position;
                // ===== Helper：对指定 Stage 检查卡片策略和正式物体位置 =====
                // This helper reuses the same position checks for every round stage.
                void Check(Stage expected)
                {
                    game.ui.head.transform.SetPositionAndRotation(new Vector3(2 + (int)expected * 0.3f, 1.2f, -3), Quaternion.Euler(20, (int)expected * 20, 0));
                    var position = game.ui.transform.position;
                    var rotation = game.ui.transform.rotation;
                    Assert.AreEqual(expected, game.Round.Stage);
                    typeof(GameFlow).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                    UpdateMenu(game.ui);
                    Assert.AreEqual(expected, game.Round.Stage);
                    Assert.AreEqual(position, game.ui.transform.position, expected.ToString());
                    Assert.AreEqual(rotation, game.ui.transform.rotation, expected.ToString());
                    PositionMenu(true); UpdateMenu(game.ui);
                    bool interactive = expected == Stage.Welcome || expected == Stage.Placing || expected == Stage.Review ||
                        expected == Stage.Handoff || expected == Stage.Found || expected == Stage.Error;
                    if (interactive)
                    {
                        // 交互阶段有按钮：首次进入/显式请求后应能把卡片放在当前模拟头显前方。
                        var heading = Vector3.ProjectOnPlane(game.ui.head.transform.forward, Vector3.up).normalized;
                        var inFront = game.ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f;
                        Assert.Less(Vector3.Distance(inFront, game.ui.transform.position), 0.0001f, expected.ToString());
                    }
                    else Assert.AreEqual(position, game.ui.transform.position, expected.ToString());
                    // Saving/Countdown/Seeking 没有自动召回请求，所以卡片应保持刚进入该检查步骤时的位置。
                    Assert.AreEqual(objectPosition, placed.transform.position, expected.ToString());
                    var pinned = game.ui.transform.position;
                    game.ui.head.transform.position += Vector3.one;
                    PositionMenu(true); UpdateMenu(game.ui);
                    if (expected == Stage.Handoff)
                    {
                        // 只有 Seeker 准备页每帧跟随当前头显姿态。
                        var heading = Vector3.ProjectOnPlane(game.ui.head.transform.forward, Vector3.up).normalized;
                        Assert.Less(Vector3.Distance(game.ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f, game.ui.transform.position), 0.0001f);
                    }
                    else Assert.AreEqual(pinned, game.ui.transform.position, expected + " must not follow every frame");
                    // 其他阶段在一次定位后应固定；比较正式模型坐标则确认只移动 UI，没有移动 Anchor 下模型。
                    Assert.AreEqual(objectPosition, placed.transform.position);
                }
                // 手动把 RoundState 依次推进所有 Stage，并向私有菜单方法传入模拟 tracked=true。
                // 这检查脚本坐标逻辑，不检查真实头显追踪，也不测试 Anchor 在房间里的定位稳定度。
                Check(Stage.Welcome);
                game.Round.Start(); Check(Stage.Placing);
                game.Round.UpdateInput(false, 1); game.Round.BeginPlacement(); Check(Stage.Saving);
                game.Round.PlacementFinished(); Check(Stage.Review);
                game.Round.Confirm(); Check(Stage.Handoff);
                game.Round.UpdateInput(false, 1); game.Round.Ready(); Check(Stage.Countdown);
                game.Round.Tick(3, true); Check(Stage.Seeking);
                game.Round.UpdateInput(false, 1); game.Round.Find(); Check(Stage.Found);
                game.Round.Fail(); Check(Stage.Error);
            }
            finally { Object.DestroyImmediate(anchorObject); }
            // 不论中间断言通过还是抛错，都删除此测试创建的普通 GameObject。
        }
        // 预期：交互页可召回、Handoff 跟随、Countdown/Seeking 固定；正式模型位置始终不变。

        // ===== 从 Space Setup 返回：追踪恢复/设置结束前不移动，之后只召回一次 =====
        // Simulates room refresh and headset tracking; it does not open Quest Settings.
        [Test] public void RoomRefreshRecallsMenuOnceAfterSetupAndTrackingRecover()
        {
            game.ui.Recenter();
            var oldPosition = game.ui.transform.position;
            typeof(RoomAccess).GetField("refreshing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game.room, true);
            typeof(RoomAccess).GetProperty(nameof(RoomAccess.RefreshCount)).SetValue(game.room, 1);
            game.ui.head.transform.SetPositionAndRotation(new Vector3(3, 1.6f, -2), Quaternion.Euler(0, 90, 0));
            PositionMenu(true); UpdateMenu(game.ui);
            Assert.AreEqual(oldPosition, game.ui.transform.position);
            typeof(RoomAccess).GetField("refreshing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game.room, false);
            PositionMenu(false); UpdateMenu(game.ui);
            Assert.AreEqual(oldPosition, game.ui.transform.position);
            PositionMenu(true); UpdateMenu(game.ui);
            Assert.Less(Vector3.Distance(new Vector3(4.5f, 1.5f, -2), game.ui.transform.position), 0.0001f);
            var recalled = game.ui.transform.position;
            game.ui.head.transform.position += Vector3.forward * 3;
            PositionMenu(false); PositionMenu(true); UpdateMenu(game.ui);
            Assert.AreEqual(recalled, game.ui.transform.position);
        }
        // 反射把 RoomAccess 设成 refreshing=true 并改变结束计数；前两次调用应保留请求但不移动卡片。
        // 等设置结束且 tracked=true 后才召回；之后再次调用时卡片固定，不会持续跟随。

        // ===== RefreshRoom 协程先等待一帧，再通知 GameFlow 刷新结束 =====
        // Manually advances the coroutine; no real room scan is performed.
        [Test] public void RoomRefreshSignalsMenuOnlyAfterTheRefreshFrame()
        {
            var refresh = (System.Collections.IEnumerator)typeof(RoomAccess).GetMethod("RefreshRoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.room, null);
            Assert.IsTrue(refresh.MoveNext());
            Assert.IsTrue(game.room.SettingUpRoom); Assert.AreEqual(0, game.room.RefreshCount);
            Assert.IsFalse(refresh.MoveNext());
            Assert.IsFalse(game.room.SettingUpRoom); Assert.AreEqual(1, game.room.RefreshCount);
        }
        // 第一次 MoveNext 执行到 yield return null，此时 SettingUpRoom=true、计数仍为 0。
        // 第二次继续到末尾，流程标记清除且 RefreshCount 变 1；这仍不表示 ARPlaneManager 已拿到平面。

        // ===== 模拟 A 键按住时移动卡片，释放后卡片不再跟随 =====
        [Test] public void HoldingAAdjustsMenuAndReleasePinsIt()
        {
            game.ui.Recenter();
            game.ui.head.transform.SetPositionAndRotation(new Vector3(2, 1.6f, 1), Quaternion.Euler(0, 90, 0));
            PositionMenu(true, true); UpdateMenu(game.ui);
            Assert.Less(Vector3.Distance(new Vector3(3.5f, 1.5f, 1), game.ui.transform.position), 0.0001f);
            game.ui.head.transform.position += Vector3.forward;
            PositionMenu(true, true); UpdateMenu(game.ui);
            Assert.Less(Vector3.Distance(new Vector3(3.5f, 1.5f, 2), game.ui.transform.position), 0.0001f);
            var pinned = game.ui.transform.position;
            game.ui.head.transform.position += Vector3.one;
            PositionMenu(true); UpdateMenu(game.ui);
            Assert.AreEqual(pinned, game.ui.transform.position);
        }
        // PositionMenu 的 moving=true/false 是测试传入的假值；没有读取 Input System 的真实 A 键。
        // 释放后头部再移动一段距离，卡片应保留最后一次 A 按住时的位置。

        // ===== 测试辅助方法：跳过真实 Anchor，直接构造 Handoff 阶段 =====
        // Advances only RoundState; no model placement or room anchor is created.
        void EnterReadiness()
        {
            game.Round.Start(); game.Round.UpdateInput(false, 1);
            game.Round.BeginPlacement(); game.Round.PlacementFinished(); game.Round.Confirm();
        }
        // 直接调用规则方法经过 Placing/Saving/Review，最终停在等待寻找者的 Handoff。
        // ===== Ready 卡片随 Seeker 移动；明确开始后停止跟随 =====
        // Tests the handoff card's follow behavior using simulated camera poses.
        [Test] public void ReadinessFollowsButStillAllowsExplicitStartAndStopsAtCountdown()
        {
            EnterReadiness();
            typeof(GameFlow).GetProperty(nameof(GameFlow.Tracked)).SetValue(game, true);
            game.ui.Recenter();
            // ===== 连续模拟 50 帧移动和转头 =====
            for (int frame = 0; frame < 50; frame++)
            {
                // frame 是测试帧编号；每轮给相机新的位置/朝向，模拟用户边走边转头。
                game.ui.head.transform.SetPositionAndRotation(new Vector3(frame * 0.05f, 1.6f, -2), Quaternion.Euler(0, frame * 2, 0));
                game.Round.Tick(0.1f, true); game.Round.UpdateInput(false, 0.1f);
                PositionMenu(true); UpdateMenu(game.ui);
                var heading = Vector3.ProjectOnPlane(game.ui.head.transform.forward, Vector3.up).normalized;
                Assert.Less(Vector3.Distance(game.ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f, game.ui.transform.position), 0.0001f);
                Assert.AreEqual(Stage.Handoff, game.Round.Stage);
                Assert.AreEqual(0, game.Round.Seconds);
            }
            // 每帧验证卡片在眼前 1.5 米、阶段仍为 Handoff、寻找用时仍为 0。

            // ===== Handoff 按钮可点；点击后进入倒数并停止卡片跟随 =====
            game.ui.Refresh(game);
            Assert.IsTrue(game.Round.CanReady); Assert.IsTrue(game.ui.primary.interactable);
            game.Primary();
            Assert.AreEqual(Stage.Countdown, game.Round.Stage); Assert.AreEqual(3, game.Round.Countdown);
            var pinned = game.ui.transform.position;
            game.ui.head.transform.SetPositionAndRotation(new Vector3(-3, 1.8f, 4), Quaternion.Euler(0, 230, 0));
            PositionMenu(true); UpdateMenu(game.ui);
            Assert.AreEqual(pinned, game.ui.transform.position);
            game.Round.Tick(3, true); PositionMenu(true); UpdateMenu(game.ui);
            Assert.AreEqual(Stage.Seeking, game.Round.Stage);
            Assert.AreEqual(pinned, game.ui.transform.position);
        }
        // Handoff 时松开足够久后 CanReady=true；Primary 进入 Countdown 后再移动 Camera，卡片应不动。
        // This checks code behavior, not whether the card feels comfortable in a headset.

        // ===== Handoff 追踪失效时暂停跟随，恢复后回到当前头显前方 =====
        [Test] public void ReadinessFollowPausesWithoutTrackingAndResumesAtCurrentPose()
        {
            EnterReadiness(); PositionMenu(true); UpdateMenu(game.ui);
            var position = game.ui.transform.position;
            game.ui.head.transform.SetPositionAndRotation(new Vector3(3, 1.2f, -2), Quaternion.Euler(0, 90, 0));
            PositionMenu(false); UpdateMenu(game.ui);
            Assert.AreEqual(position, game.ui.transform.position);
            PositionMenu(true); UpdateMenu(game.ui);
            Assert.Less(Vector3.Distance(new Vector3(4.5f, 1.1f, -2), game.ui.transform.position), 0.0001f);
            Assert.IsFalse(game.ui.transform.IsChildOf(game.ui.head.transform));
            Assert.AreEqual(Stage.Handoff, game.Round.Stage);
        }
        // tracked=false 时 PositionMenu 不移动；tracked=true 后用最新 Camera 位姿定位，但 Stage 仍是 Handoff。
        // IsChildOf=false 确认菜单不是 Camera 子物体，不会随每帧 Transform 自动跟随。

        // ===== Handoff 使用紧凑尺寸；进入 Countdown 后恢复普通任务卡 =====
        [Test] public void ReadinessCardIsCompactAndRestoresNormalLayoutAtCountdown()
        {
            EnterReadiness(); game.ui.Refresh(game);
            var root = (RectTransform)game.ui.transform;
            Assert.AreEqual(new Vector2(780, 420), root.sizeDelta);
            Assert.AreEqual(new Vector3(780, 420, 4), game.ui.GetComponent<BoxCollider>().size);
            Assert.IsFalse(game.ui.transform.Find("Edition").gameObject.activeSelf);
            foreach (var button in new[] { game.ui.primary, game.ui.reset })
            {
                var rect = (RectTransform)button.transform;
                Assert.IsTrue(root.rect.Contains(rect.anchoredPosition + rect.rect.min), button.name);
                Assert.IsTrue(root.rect.Contains(rect.anchoredPosition + rect.rect.max), button.name);
            }
            game.Round.UpdateInput(false, 1); game.Round.Ready(); game.ui.Refresh(game);
            Assert.AreEqual(new Vector2(900, 560), root.sizeDelta);
            Assert.AreEqual(new Vector3(900, 560, 4), game.ui.GetComponent<BoxCollider>().size);
            Assert.IsTrue(game.ui.transform.Find("Edition").gameObject.activeSelf);
            Assert.AreEqual(new Vector2(-185, -166), ((RectTransform)game.ui.primary.transform).anchoredPosition);
            Assert.AreEqual(new Vector2(270, -238), ((RectTransform)game.ui.reset.transform).anchoredPosition);
        }
        // 同时比较 RectTransform、BoxCollider 和按钮四角：可见卡片缩小后，射线拦截区也必须匹配。
        // Ready 后进入 Countdown，卡片恢复 900x560，Edition 标题重新显示。

        // ===== 检查恐龙可见尺寸、Collider 和 URP 材质 =====
        [Test] public void DinosaurFitsTableAndIsSelectable()
        {
            var dinosaur = Object.Instantiate(game.placement.prefabToPlace);
            var renderers = dinosaur.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.AreEqual(0.182f, bounds.size.y, 0.002f); Assert.GreaterOrEqual(bounds.min.y, 0);
            Assert.NotNull(dinosaur.GetComponent<BoxCollider>());
            Assert.IsFalse(dinosaur.transform.IsChildOf(game.ui.head.transform));
            foreach (var renderer in renderers) Assert.IsTrue(renderer.sharedMaterials.All(m => m.shader.name == "Universal Render Pipeline/Simple Lit"));
            Object.DestroyImmediate(dinosaur);
        }
        // bounds 合并子 Renderer 的实际可见范围；高度应约 18.2 厘米，误差容许 2 毫米。
        // 用 Prefab 副本检查，不改磁盘 Prefab；BoxCollider 用于 Seeker 后续的 Physics.Raycast 命中。

        // ===== Welcome/Placing 中 Set up room 与 Change object 保持不同用途 =====
        [Test] public void RoomSetupAndModelChoiceRemainIndependent()
        {
            typeof(GameFlow).GetProperty(nameof(GameFlow.Tracked)).SetValue(game, true);
            typeof(RoomAccess).GetProperty(nameof(RoomAccess.Granted)).SetValue(game.room, true);
            foreach (bool placing in new[] { false, true })
            {
                if (placing) game.Round.Start();
                // placing=false 保留 Welcome；true 时先进入 Placing，再重复检查按钮状态。
                game.ui.Refresh(game);
                Assert.IsTrue(game.ui.roomSetup.gameObject.activeSelf);
                Assert.IsTrue(game.ui.roomSetup.interactable);
                Assert.AreEqual("SetUpRoom", game.ui.roomSetup.onClick.GetPersistentMethodName(0));
                Assert.AreEqual("Change object", game.ui.secondary.GetComponentInChildren<Text>().text);
                int selected = game.placement.SelectedIndex;
                game.Secondary();
                Assert.AreEqual((selected + 1) % 4, game.placement.SelectedIndex);
            }
        }
        // 检查房间按钮持久绑定到 SetUpRoom；Secondary 只循环 SelectedIndex，不会打开系统设置。

        // ===== 检查四个 Prefab 的名称顺序、最大尺寸、Collider 和宠物贴图 =====
        [Test] public void ObjectChoicesAreSmallAndNamed()
        {
            Assert.AreEqual(4, game.placement.placeablePrefabs.Length);
            CollectionAssert.AreEqual(new[] { "Dinosaur", "Dog", "Cat", "Bunny" }, game.placement.placeableNames);
            foreach (var prefab in game.placement.placeablePrefabs)
            {
                var obj = Object.Instantiate(prefab);
                var renderers = obj.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.LessOrEqual(bounds.size.y, 0.19f, prefab.name);
                Assert.NotNull(obj.GetComponent<BoxCollider>(), prefab.name);
                if (prefab != game.placement.prefabToPlace)
                    // Dinosaur 用独立材质；Dog/Cat/Bunny 需要 Kenney 的 _BaseMap 贴图才能显示模型表面。
                    foreach (var renderer in renderers) Assert.NotNull(renderer.sharedMaterial.GetTexture("_BaseMap"), prefab.name + " texture");
                Object.DestroyImmediate(obj);
            }
        }
        // 每个模型副本都必须不高于 19 厘米且带 BoxCollider；测试结束销毁副本，不写回源 Prefab。

        // ===== 仅检查 Build Settings 指向课堂 Scene 和课堂 Android 包名 =====
        [Test] public void OnlyClassroomSceneBuilds()
        {
            Assert.AreEqual(1, EditorBuildSettings.scenes.Length);
            Assert.AreEqual(ClassroomSetup.ScenePath, EditorBuildSettings.scenes[0].path);
            Assert.AreEqual("com.lingjieruan.hidear.classroom", PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
        }
        // 读取 Build Settings，不会实际运行 BuildPipeline、生成 APK 或安装应用。

        // ===== 检查 12 个色块的编号、Outline 反馈、卡片边界和重叠 =====
        // The Editor test reads saved event arguments; it does not perform controller clicks.
        [Test] public void ExpandedSwatchesSelectTheirColorsAndStayClearOfOtherControls()
        {
            CollectionAssert.AreEqual(new[] { "Original", "Moss", "Blue", "Gray", "White", "Red", "Yellow", "Orange", "Purple", "Pink", "Teal", "Black" }, PlaceOnPlane.ColorNames);
            Assert.AreEqual(PlaceOnPlane.ColorNames.Length, PlaceOnPlane.Colors.Length);
            Assert.AreEqual(PlaceOnPlane.Colors.Length, game.ui.colorButtons.Length);
            typeof(GameFlow).GetProperty(nameof(GameFlow.Tracked)).SetValue(game, true);
            game.ui.Refresh(game);
            Rect Bounds(RectTransform rect) => new Rect(rect.anchoredPosition + rect.rect.min, rect.rect.size);
            var menu = ((RectTransform)game.ui.transform).rect;
            // Bounds 把按钮/文字的 RectTransform 转成卡片内的矩形，便于后面检查是否越界或相交。
            for (int i = 0; i < game.ui.colorButtons.Length; i++)
            {
                var button = game.ui.colorButtons[i];
                // ===== 读 Inspector 保存的颜色参数，并直接调用 GameFlow =====
                var argument = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_IntArgument");
                Assert.AreEqual("SelectColor", button.onClick.GetPersistentMethodName(0));
                Assert.AreEqual(i, argument.intValue);
                // 每颗色块都应连到 SelectColor，并保存自己的下标 i；直接读取事件参数，验证它传对颜色。
                game.SelectColor(argument.intValue); game.ui.Refresh(game);
                // EditMode 不会自动执行 RuntimeOnly 点击事件，所以这里手动调用对应方法，而不是假装点击了按钮。

                // ===== 检查选中后显示的颜色，并确认色块不越界/不重叠 =====
                Assert.AreEqual(i, game.placement.ColorIndex, button.name);
                Assert.AreEqual(PlaceOnPlane.ColorNames[i], game.placement.SelectedColorName);
                Assert.Less(Vector4.Distance(PlaceOnPlane.Colors[i], button.colors.normalColor), 0.00001f);
                // 前三项核对编号/名称/按钮普通态 RGB；距离阈值只容许浮点数的微小误差。
                Assert.IsTrue(button.GetComponent<Outline>().enabled);
                Assert.AreEqual(1, game.ui.colorButtons.Count(b => b.GetComponent<Outline>().enabled));
                // 当前按钮必须有选中边框，而且 12 个按钮里只能有一个 Outline 开启。
                var rect = Bounds((RectTransform)button.transform);
                Assert.IsTrue(menu.Contains(rect.min) && menu.Contains(rect.max), button.name);
                // 同时检查矩形左下和右上角都在卡片内；否则这个色块会越出卡片边界。
                foreach (var other in game.ui.colorButtons.Where(b => b != button))
                    Assert.IsFalse(rect.Overlaps(Bounds((RectTransform)other.transform)), button.name + "/" + other.name);
                // 当前色块与其他色块不能重叠；下一组循环还会检查它与主/次按钮、房间按钮和文字区不重叠。
                foreach (var other in new[] { game.ui.primary.transform, game.ui.secondary.transform, game.ui.roomSetup.transform,
                    game.ui.reset.transform, game.ui.body.transform, game.ui.status.transform })
                    Assert.IsFalse(rect.Overlaps(Bounds((RectTransform)other)), button.name + "/" + other.name);
            }
        }
        // 当前按钮颜色编号与名称应一致，只有一个 Outline；所有色块需留在卡片内并避开相邻控件。
        // Layout assertions do not measure real controller ray accuracy.

        // ===== 模型菜单预览只有一个且无 Collider；交给 Seeker 后隐藏 =====
        [Test] public void EveryModelIncludingDinosaurHasOneMenuAndPlacementPreview()
        {
            Assert.IsNull(game.ui.transform.Find("Menu dinosaur"));
            var preview = game.placement.CreatePreview();
            try
            {
                Assert.Greater(preview.GetComponentsInChildren<Renderer>().Length, 0);
                Assert.IsTrue(preview.GetComponentsInChildren<Collider>().All(c => !c.enabled));
            }
            finally { Object.DestroyImmediate(preview); }
            for (int i = 0; i < game.placement.placeablePrefabs.Length; i++)
            {
                game.ui.Refresh(game);
                Assert.IsTrue(game.ui.previewRoot.gameObject.activeSelf);
                Assert.AreEqual(1, game.ui.previewRoot.childCount);
                Assert.AreEqual(game.placement.SelectedName + " Preview", game.ui.previewRoot.GetChild(0).name);
                Assert.Greater(game.ui.previewRoot.GetComponentsInChildren<Renderer>().Length, 0);
                Assert.IsTrue(game.ui.previewRoot.GetComponentsInChildren<Collider>().All(c => !c.enabled));
                game.placement.SelectNext();
            }
            game.ui.Refresh(game);
            Assert.AreEqual("Dinosaur", game.placement.SelectedName);
            Assert.IsTrue(game.ui.previewRoot.gameObject.activeSelf);
            Assert.AreEqual(1, game.ui.previewRoot.childCount);
            game.Round.Start(); game.Round.UpdateInput(false, 1);
            game.Round.BeginPlacement(); game.Round.PlacementFinished(); game.Round.Confirm();
            game.ui.Refresh(game);
            Assert.IsFalse(game.ui.previewRoot.gameObject.activeSelf);
            Assert.IsTrue(game.ui.colorButtons.All(b => !b.gameObject.activeSelf));
        }
        // All 表示集合内每个 Collider 都必须 disabled；Handoff 时预览容器和色块应隐藏。
        // These are prefab/UI checks, not evidence that a real Seeker found the object.

        // ===== 逐色检查实例覆盖：恐龙改 BaseColor，宠物改 BaseMap，源材质不变 =====
        [Test] public void EveryColorAppliesToDinosaurAndTexturedPetsWithoutEditingSharedMaterials()
        {
            var placement = game.placement;
            var dinosaur = placement.CreatePreview();
            placement.SelectNext();
            var pet = placement.CreatePreview();
            try
            {
                Assert.AreEqual(PlaceOnPlane.Colors.Length, placement.colorTextures.Length);
                Assert.AreEqual(PlaceOnPlane.Colors.Length, placement.colorTextures.Distinct().Count());
                var properties = new MaterialPropertyBlock();
                var dinoRenderer = dinosaur.GetComponentInChildren<Renderer>();
                var petRenderer = pet.GetComponentInChildren<Renderer>();
                var originalDinoColor = dinoRenderer.sharedMaterial.GetColor("_BaseColor");
                var originalPetTexture = petRenderer.sharedMaterial.GetTexture("_BaseMap");
                // 保存共享材质原始 BaseColor/BaseMap；下面会逐个颜色比较实例覆盖与共享资源。
                for (int i = 0; i < PlaceOnPlane.Colors.Length; i++)
                {
                    Assert.NotNull(placement.colorTextures[i], PlaceOnPlane.ColorNames[i]);
                    placement.SelectColor(i); placement.ApplyColor(dinosaur); placement.ApplyColor(pet);
                    dinoRenderer.GetPropertyBlock(properties, 0);
                    if (i == 0) Assert.IsTrue(properties.isEmpty);
                    // 编号 0 是 Original，PropertyBlock 应为空，模型继续使用 Prefab 原材质。
                    else Assert.Less(Vector4.Distance(PlaceOnPlane.Colors[i], properties.GetColor("_BaseColor")), 0.00001f);
                    // 其他编号的恐龙颜色需接近对应 Colors[i]；小误差来自浮点数比较。
                    petRenderer.GetPropertyBlock(properties, 0);
                    if (i == 0) Assert.IsTrue(properties.isEmpty);
                    // 宠物选 Original 时清空覆盖，恢复原本的共享贴图。
                    else
                    {
                        Assert.AreEqual(Color.white, properties.GetColor("_BaseColor"));
                        Assert.AreEqual(placement.colorTextures[i], properties.GetTexture("_BaseMap"));
                    }
                    // 彩色宠物使用预生成贴图；白色底色避免所选 RGB 再次乘色而改变贴图颜色。
                    Assert.AreEqual(originalDinoColor, dinoRenderer.sharedMaterial.GetColor("_BaseColor"));
                    Assert.AreEqual(originalPetTexture, petRenderer.sharedMaterial.GetTexture("_BaseMap"));
                }
                // 每个颜色都再确认共享素材仍是原值，说明变化存于 Renderer 的 PropertyBlock，不是材质资产。
                placement.SelectColor(0); placement.ApplyColor(pet);
                petRenderer.GetPropertyBlock(properties, 0); Assert.IsTrue(properties.isEmpty);
            }
            finally { Object.DestroyImmediate(dinosaur); Object.DestroyImmediate(pet); }
        }
        // finally 销毁测试副本；只检查 API 属性值，不评价 Quest 房间里人眼看到的颜色效果。

        // ===== 检查无碰撞临时预览可换色/还原，且不修改 Prefab =====
        [Test] public void ColorPreviewDoesNotChangePrefabOrBlockPlacement()
        {
            var placement = game.placement;
            var material = placement.SelectedPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
            var original = material.GetColor("_BaseColor");
            placement.SelectColor(2);
            var preview = placement.CreatePreview();
            try
            {
                Assert.IsTrue(preview.GetComponentsInChildren<Collider>().All(c => !c.enabled));
                var renderer = preview.GetComponentInChildren<Renderer>();
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties, 0);
                var applied = properties.GetColor("_BaseColor");
                // 读取临时模型 Renderer 上的 PropertyBlock；随后与共享材质原色分别比较。
                Assert.That(applied.r, Is.EqualTo(PlaceOnPlane.Colors[2].r).Within(0.00001f));
                Assert.That(applied.g, Is.EqualTo(PlaceOnPlane.Colors[2].g).Within(0.00001f));
                Assert.That(applied.b, Is.EqualTo(PlaceOnPlane.Colors[2].b).Within(0.00001f));
                Assert.AreEqual(original, material.GetColor("_BaseColor"));
                placement.SelectColor(0); placement.ApplyColor(preview);
                renderer.GetPropertyBlock(properties, 0); Assert.IsTrue(properties.isEmpty);
                // 再选编号 0 后覆盖应清空；Collider 仍关闭，不会干扰放置射线。
                Assert.AreEqual(PlaceOnPlane.Colors.Length, game.ui.colorButtons.Length);
                Assert.NotNull(game.ui.previewRoot);
                placement.SelectNext(); placement.SelectColor(3);
                var petPreview = placement.CreatePreview();
                try
                {
                    petPreview.GetComponentInChildren<Renderer>().GetPropertyBlock(properties, 0);
                    Assert.AreEqual(placement.colorTextures[3], properties.GetTexture("_BaseMap"));
                }
                finally { Object.DestroyImmediate(petPreview); }
                // 无论宠物贴图断言通过与否，都立即清理该测试副本。
            }
            finally { Object.DestroyImmediate(preview); }
            // 外层 finally 同样清理恐龙预览；测试结束不把临时对象留在 Scene。
        }
        // PropertyBlock 与共享材质分开验证；换色和还原都只影响副本，不会修改源 Prefab。
    }
}
