// GameUI.cs：根据 GameFlow/RoundState 显示每个游戏阶段的任务卡、按钮和选物预览。
// 这里负责卡片位置/排版；GameFlow 决定何时召回；PlaceOnPlane 管正式模型和 Anchor。
// UI changes never move the hidden object or decide whether the round can advance.
using UnityEngine;
using UnityEngine.UI;

namespace HideAR.Classroom
{
    public sealed class GameUI : MonoBehaviour
    {
        // ===== Inspector 引用：相机、文字、按钮和菜单预览 =====
        public Camera head; // XR 头显相机；用于计算卡片在用户前方的位置，不是卡片的父对象。
        public Text title; // 任务卡标题；倒数阶段显示倒数数字。
        public Text body; // 当前阶段的操作说明；藏匿阶段还显示所选模型/颜色。
        public Text status; // 权限、追踪、扳机提示或寻找时间。
        public Button primary; // 主操作：开始藏匿、确认、开始寻找、再玩或重启。
        public Button secondary; // 次操作：藏匿前换模型，Review 时清除后重新放置。
        public Button reset; // 重新加载 Unity 场景；和清理旧 Anchor 后再玩的 Play again 不同。
        public Button roomSetup; // Welcome/Placing 的独立 Quest 房间设置入口。
        public Transform previewRoot; // UI 内模型副本的容器，不是正式模型的 Anchor。
        public Button[] colorButtons; // 颜色按钮；数组下标就是 PlaceOnPlane 的颜色编号。

        // ===== 卡片位置与菜单预览缓存 =====
        public bool HasPosition { get; private set; } // 是否已将这张卡片放到一个世界空间位置。
        bool recenterRequested; // 等 LateUpdate 用较新的头部姿态执行的一次定位请求。
        Vector3 menuForward; // 卡片面向的水平三维方向，低头时沿用上次有效方向。
        GameObject modelPreview; // 只显示在菜单中的模型副本；不是实际藏起来的物品。
        int previewIndex = -1; // 菜单预览对应的模型编号；变化时才销毁并重建。
        int previewColor = -1; // 菜单预览上次使用的颜色编号；变化时才重新换色。

        // ===== 记录一次菜单定位请求 =====
        // GameFlow asks for a move; this method does not move the card yet.
        public void RequestRecenter() { recenterRequested = true; }
        // GameFlow 根据阶段/A 键/房间返回决定何时调用；真正移动留到 LateUpdate。

        // ===== 在本帧稍后用最新头部姿态移动卡片 =====
        void LateUpdate()
        {
            if (recenterRequested) Recenter();
            // 没有请求就不重算 Transform，因此普通阶段卡片保持固定在现实空间中的原位置。
        }
        // Unity calls LateUpdate after Update, so the move can use this frame's updated camera pose.

        // ===== 把世界空间卡片放到眼前并保持直立 =====
        // Uses the headset pose for the card only; the placed model keeps its separate Anchor.
        public void Recenter()
        {
            var forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            // ProjectOnPlane 去掉上下分量，只留下头显朝向在地面上的投影。
            if (forward.sqrMagnitude > 0.04f) menuForward = forward.normalized;
            // 头显大致水平时更新方向；看得太直上/直下时投影接近零，保留上次方向防止卡片乱转。
            else if (!HasPosition) menuForward = Vector3.Cross(head.transform.right, Vector3.up).normalized;
            // 如果第一次定位就正好朝上/下，没有旧方向可用，才用相机右向量计算一个水平备用方向。
            transform.SetPositionAndRotation(head.transform.position + menuForward * 1.5f + Vector3.down * 0.10f,
                Quaternion.LookRotation(menuForward, Vector3.up));
            HasPosition = true;
            recenterRequested = false;
        }
        // 卡片放在头显前方 1.5 米、眼睛下方 0.1 米；LookRotation 使它朝用户且竖直。
        // HasPosition=true 记录已有位置；之后只有新请求才再次移动。只改变 UI 卡片，不碰正式模型。

        // ===== 根据 Stage 刷新任务文字、按钮含义和选物线索 =====
        // This displays the stage. GameFlow and RoundState still own the actual game actions.
        public void Refresh(GameFlow game)
        {
            var round = game.Round;
            bool choosing = round.Stage == Stage.Welcome || round.Stage == Stage.Placing || round.Stage == Stage.Review;
            // 只有 Hider 的开场、放置、检查页面显示物品名/颜色/预览；交接给 Seeker 后先隐藏这些线索。
            RefreshSelection(game.placement, choosing);
            RefreshReadyLayout(round.Stage == Stage.Handoff);
            string main = "", other = ""; // 本次主/次按钮标签；空字符串会让对应按钮隐藏。
            status.text = game.Tracked ? "" : "Tracking paused. Put on the headset and look around.";
            // 先用追踪暂停文字作为默认提示；各 Stage 分支可用权限/倒数/结果文字覆盖它。
            // status 只改变卡片文字，不会恢复 Tracking 或推进 Round.Stage。

            // ===== 每个 Stage 设置卡片内容 =====
            switch (round.Stage)
            {
                case Stage.Welcome:
                    // Welcome：Hider 选物/颜色；主按钮申请权限或开始藏匿，次按钮循环换模型。
                    title.text = "HideAR";
                    body.text = $"{game.placement.SelectedName} / {game.placement.SelectedColorName}\nChoose your object and color.";
                    main = game.room.Granted ? "Start hiding" : "Allow room access";
                    other = game.placement.HasChoices ? "Change object" : "";
                    if (!game.room.Granted) status.text = game.room.Message;
                    // 尚未授权就显示 RoomAccess 的权限说明；真正申请动作由 GameFlow.Primary 执行。
                    else if (!game.placement.Available) status.text = "Starting room tracking...";
                    // 已有权限但 Anchor 子系统未运行时提示等待；Available 不代表平面已加载。
                    else if (game.Tracked) status.text = game.room.Message;
                    // 正常追踪时显示 RoomAccess 提供的 Space Setup 提示（若为空则保持空白）。
                    break;
                case Stage.Placing:
                    // Placing：Hider 用右手柄瞄准地板/桌面并按 Trigger 放置；主按钮隐藏，次按钮可换模型。
                    title.text = "Pick a hiding spot";
                    body.text = $"{game.placement.SelectedName} / {game.placement.SelectedColorName}\nPoint at a table or floor.\nPress trigger to keep this spot.";
                    other = game.placement.HasChoices ? "Change object" : "";
                    if (game.Tracked) status.text = game.room.Message.Length > 0 ? game.room.Message :
                        game.room.planes.trackables.count == 0 ? "No surfaces yet. Select Set up room." : "";
                    // 有系统提示先显示它；否则平面数量为 0 时提醒设置房间。刷新计数本身不能证明平面已到达。
                    break;
                case Stage.Saving:
                    // Saving：等待 ARAnchorManager 返回结果；main/other 留空会隐藏两个操作按钮。
                    title.text = "Saving your spot"; body.text = "Keep the hiding place in view for a moment."; break;
                case Stage.Review:
                    // Review：Hider 看正式模型并可换颜色；主按钮确认，次按钮删除旧 Anchor 后重放。
                    title.text = "Happy with this spot?";
                    body.text = $"{game.placement.SelectedName} / {game.placement.SelectedColorName}\nKeep this spot, change the color,\nor move the object.";
                    main = "Confirm hiding"; other = "Move object"; break;
                case Stage.Handoff:
                    // Handoff：把头显交给 Seeker；只有按下 Start search 才会开始倒数，不检测摘戴来自动换人。
                    title.text = "Ready to search?";
                    body.text = "Let the seeker get comfortable and take the controller.\nPress Start search when ready.";
                    main = "Start search";
                    if (game.Tracked) status.text = round.CanReady ? "Your search starts after a 3-second countdown." : "Release the trigger before starting.";
                    // CanReady=true 显示倒数说明；false 提醒松开 Trigger。提示不会自行调用 RoundState.Ready。
                    break;
                case Stage.Countdown:
                    // Countdown：向上取整显示剩余秒数；RoundState.Tick 到 0 才进入 Seeking。
                    title.text = Mathf.CeilToInt(round.Countdown).ToString(); body.text = "Your search is about to begin."; break;
                case Stage.Seeking:
                    // Seeking：提示 Seeker 用射线找正式物品；选物预览/颜色区域已在 choosing=false 时隐藏。
                    title.text = "Find the object"; body.text = "Explore your room. Point at the hidden object\nand press the right trigger when you find it.";
                    if (game.Tracked) status.text = $"Search time   {round.Seconds:0.0} s";
                    // 只在追踪有效时显示 RoundState 的寻找时间；命中检查由 PlaceOnPlane 完成。
                    break;
                case Stage.Found:
                    // Found：显示冻结的成绩；主按钮 Play again 清理旧 Anchor 后开始新轮。
                    title.text = "You found it!"; body.text = "A secret spot discovered.\nSwap roles and play again.";
                    status.text = $"Your time   {round.Seconds:0.0} s"; main = "Play again"; break;
                case Stage.Error:
                    // Error：显示 GameFlow 保存的原因；主按钮 Restart game 会重新加载整张 Scene。
                    title.text = "Let's try again"; body.text = game.Error; status.text = ""; main = "Restart game"; break;
            }
            SetButton(primary, main);
            SetButton(secondary, other);
            // 空标签会由 SetButton 隐藏按钮；这里设置显示，不在 GameUI 内执行按钮对应的规则。
            // GameFlow handles clicks; RoundState decides whether a requested transition is allowed.

            // ===== 按追踪/保存/阶段状态控制按钮可点击性和显隐 =====
            primary.interactable = round.Stage == Stage.Error || (game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom &&
                (round.Stage != Stage.Welcome || !game.room.Granted || game.placement.Available) &&
                (round.Stage != Stage.Handoff || round.CanReady));
            // Error 的 Restart 不受 Tracking 限制；其他主按钮要求追踪正常、无 Anchor 保存/房间设置。
            // Welcome 已授权时还要 Anchor 子系统可用；Handoff 则必须先松开 Trigger 才可开始。
            secondary.interactable = game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom;
            // 次按钮可以显示，但追踪无效、正在保存或正在设置时不可点击。
            foreach (var button in colorButtons) button.interactable = game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom;
            // 每个色块都使用同一限制；显示当前颜色不等于此刻允许更改颜色。
            SetButton(roomSetup, "Set up room");
            roomSetup.gameObject.SetActive(round.Stage == Stage.Welcome || round.Stage == Stage.Placing);
            // SetActive(false) 把整个 Set up room 按钮隐藏；它只在开场和放置阶段出现。
            roomSetup.interactable = game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom;
            // interactable=false 保留按钮外观但不接收点击；它与上面的隐藏不是一回事。
            reset.gameObject.SetActive(round.Stage != Stage.Welcome);
            // Reset 只在 Welcome 隐藏；它重新载入 Scene，不等同于 Found 页的 Play again。
        }

        // ===== 调整选物 UI 布局，隐藏寻找者不应看到的线索 =====
        // This method changes the menu preview only, never the placed object.
        void RefreshSelection(PlaceOnPlane placement, bool visible)
        {
            body.rectTransform.anchoredPosition = new Vector2(visible ? -120 : 0, visible ? 56 : 36);
            body.rectTransform.sizeDelta = new Vector2(visible ? 500 : 760, 108);
            status.rectTransform.anchoredPosition = new Vector2(visible ? -120 : 0, visible ? -36 : -62);
            status.rectTransform.sizeDelta = new Vector2(visible ? 500 : 760, visible ? 50 : 58);
            previewRoot.gameObject.SetActive(visible);
            // false 会隐藏整个菜单预览容器，但不会删除里面的副本，更不会删除 Anchor 下的正式物品。
            for (int i = 0; i < colorButtons.Length; i++)
            {
                colorButtons[i].gameObject.SetActive(visible);
                colorButtons[i].GetComponent<Outline>().enabled = i == placement.ColorIndex;
            }
            if (!visible) return;
            // Handoff/Countdown/Seeking/Found/Error 立即退出：选色按钮和模型副本隐藏，不向 Seeker 暴露藏匿线索。
            // SetActive(false) 只是暂时不显示；RectTransform 的位置/尺寸只改卡片排版，不移动正式模型或 Anchor。

            // ===== 只有模型编号变化时才重建菜单模型副本 =====
            if (modelPreview == null || previewIndex != placement.SelectedIndex)
            {
                if (modelPreview != null)
                {
                    modelPreview.SetActive(false);
                    if (Application.isPlaying) Destroy(modelPreview); else DestroyImmediate(modelPreview);
                }
                modelPreview = placement.CreatePreview(previewRoot);
                // CreatePreview 给菜单生成无 Collider 副本；以下缩放只作用于菜单展示。
                modelPreview.transform.localPosition = Vector3.zero;
                modelPreview.transform.localRotation = Quaternion.Euler(0, placement.SelectedIndex == 0 ? -35 : 145, 0);
                var renderers = modelPreview.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                modelPreview.transform.localScale *= Mathf.Min(0.16f / bounds.size.y, 0.18f / Mathf.Max(bounds.size.x, bounds.size.z));
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                modelPreview.transform.localPosition -= previewRoot.InverseTransformPoint(bounds.center);
                previewIndex = placement.SelectedIndex;
                previewColor = -1;
            }
            // 先隐藏旧副本，再销毁它；运行时 Destroy 延迟到帧末，编辑器检查用 DestroyImmediate。
            // 合并 Renderer 的 bounds 后缩小/居中模型，让不同 Prefab 在卡片里看起来大小接近。
            // 这里仅缩放 modelPreview，不会改变正式放置模型的实际尺寸；-1 会强制重新应用颜色。

            // ===== 颜色改变时更新菜单副本 =====
            if (previewColor == placement.ColorIndex) return;
            // 编号未改变就跳过，避免每次 UI 刷新都重复写材质属性。
            placement.ApplyColor(modelPreview);
            previewColor = placement.ColorIndex;
            // 这里只给 UI 模型副本上色；正式物品和平面预览由 PlaceOnPlane 更新。
        }

        // ===== Handoff 用紧凑卡片；其他阶段恢复普通布局 =====
        // The collider follows the visible card size so rays do not hit an invisible oversized area.
        void RefreshReadyLayout(bool ready)
        {
            var rect = (RectTransform)transform;
            rect.sizeDelta = ready ? new Vector2(780, 420) : new Vector2(900, 560);
            GetComponent<BoxCollider>().size = new Vector3(rect.rect.width, rect.rect.height, 4);
            var edition = transform.Find("Edition");
            if (edition != null) edition.gameObject.SetActive(!ready);
            // 只有场景里找得到 Edition 才切换；Handoff 隐藏它给准备文字让空间。
            title.rectTransform.anchoredPosition = ready ? new Vector2(0, 142) : new Vector2(-25, 150);
            title.rectTransform.sizeDelta = ready ? new Vector2(660, 70) : new Vector2(760, 94);
            ((RectTransform)primary.transform).anchoredPosition = ready ? new Vector2(-160, -138) : new Vector2(-185, -166);
            ((RectTransform)reset.transform).anchoredPosition = ready ? new Vector2(190, -138) : new Vector2(270, -238);
            if (!ready) return;
            // false 时前面已经恢复普通尺寸和按钮位置，无需再改下面的紧凑内容排版。
            // ready=true 只在 Handoff；此时把文字放入较小卡片的中央区域。
            body.rectTransform.anchoredPosition = new Vector2(0, 47);
            body.rectTransform.sizeDelta = new Vector2(660, 108);
            status.rectTransform.anchoredPosition = new Vector2(0, -43);
            status.rectTransform.sizeDelta = new Vector2(660, 50);
        }
        // 这里只改 UI 尺寸、文字位置和装饰显隐，不改变触发规则、模型大小或 Anchor。

        // ===== 空标签时隐藏按钮，否则只更新按钮文字 =====
        // The saved Button.onClick event remains connected to GameFlow.
        static void SetButton(Button button, string label)
        {
            button.gameObject.SetActive(label.Length > 0);
            // 空字符串长度为 0，整颗按钮隐藏；非空时按钮显示。
            button.GetComponentInChildren<Text>().text = label;
            // 只替换看到的文字，不改 Inspector 中已经保存的点击事件绑定。
        }
    }
}
