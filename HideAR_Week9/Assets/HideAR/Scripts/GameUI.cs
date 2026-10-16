using UnityEngine;
using UnityEngine.UI;

namespace HideAR.Classroom
{
    // 职责：显示回合状态、按钮、模型/颜色预览，并按请求移动任务卡。
    // Role: present round state, controls, model/color previews and requested task-card movement.
    // UI 不决定回合规则，也不移动真实放置物品的 Anchor。
    // UI does not own round rules or move the placed object's anchor.
    public sealed class GameUI : MonoBehaviour
    {
        public Camera head;
        public Text title;
        public Text body;
        public Text status;
        public Button primary;
        public Button secondary;
        public Button reset;
        public Button roomSetup;
        public GameObject mascot;
        public Transform previewRoot;
        public Button[] colorButtons;
        public bool HasPosition { get; private set; }
        bool recenterRequested;
        Vector3 menuForward;
        GameObject modelPreview;
        int previewIndex = -1;
        int previewColor = -1;

        // 请求与执行分开：LateUpdate 再读取相机位置，减少使用过早头部姿态的偏移。
        // Defer the request to LateUpdate to use a later camera pose for menu placement.
        public void RequestRecenter() { recenterRequested = true; }

        void LateUpdate()
        {
            if (recenterRequested) Recenter();
        }

        public void Recenter()
        {
            var forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            // 先检查水平投影的长度再归一化；几乎竖直看时保留旧方向，避免微小误差变成跳向。
            // Check length before normalizing; keep the previous heading near vertical to avoid flips.
            if (forward.sqrMagnitude > 0.04f) menuForward = forward.normalized;
            else if (!HasPosition) menuForward = Vector3.Cross(head.transform.right, Vector3.up).normalized;
            // 菜单直立放在水平方向前方 1.5 米、眼睛下方 10 厘米；不挂到 Camera 下。
            // Place the upright world-space menu 1.5 m ahead and 10 cm below the eyes, not under Camera.
            transform.SetPositionAndRotation(head.transform.position + menuForward * 1.5f + Vector3.down * 0.10f,
                Quaternion.LookRotation(menuForward, Vector3.up));
            HasPosition = true;
            recenterRequested = false;
        }

        // 从规则读取阶段来显示文字/按钮，不在显示函数中推进阶段或开始计时。
        // Read stage to display labels/buttons; rendering never advances the round or starts timing.
        public void Refresh(GameFlow game)
        {
            var round = game.Round;
            bool choosing = round.Stage == Stage.Welcome || round.Stage == Stage.Placing || round.Stage == Stage.Review;
            RefreshSelection(game.placement, choosing);
            string main = "", other = "";
            status.text = game.Tracked ? "" : "Tracking paused. Put on the headset and look around.";
            switch (round.Stage)
            {
                case Stage.Welcome:
                    title.text = "HideAR";
                    body.text = $"{game.placement.SelectedName} / {game.placement.SelectedColorName}\nChoose your object and color.";
                    main = game.room.Granted ? "Start hiding" : "Allow room access";
                    other = game.placement.HasChoices ? "Change object" : "";
                    if (!game.room.Granted) status.text = game.room.Message;
                    else if (!game.placement.Available) status.text = "Starting room tracking...";
                    else if (game.Tracked) status.text = game.room.Message;
                    break;
                case Stage.Placing:
                    title.text = "Pick a hiding spot";
                    body.text = $"{game.placement.SelectedName} / {game.placement.SelectedColorName}\nPoint at a table or floor.\nPress trigger to keep this spot.";
                    other = game.placement.HasChoices ? "Change object" : "";
                    if (game.Tracked) status.text = game.room.Message.Length > 0 ? game.room.Message :
                        game.room.planes.trackables.count == 0 ? "No surfaces yet. Select Set up room." : "";
                    break;
                case Stage.Saving:
                    title.text = "Saving your spot"; body.text = "Keep the hiding place in view for a moment."; break;
                case Stage.Review:
                    title.text = "Happy with this spot?";
                    body.text = $"{game.placement.SelectedName} / {game.placement.SelectedColorName}\nKeep this spot, change the color,\nor move the object.";
                    main = "Confirm hiding"; other = "Move object"; break;
                case Stage.Handoff:
                    // 交接只是等待玩家自己点击 Start search，不要求检测摘戴头显。
                    // Handoff waits for an explicit Start search click, not a headset removal cycle.
                    title.text = "Ready to search?";
                    body.text = "Let the seeker get comfortable and take the controller.\nPress Start search when ready.";
                    main = "Start search";
                    if (game.Tracked) status.text = round.CanReady ? "Your search starts after a 3-second countdown." : "Release the trigger before starting.";
                    break;
                case Stage.Countdown:
                    title.text = Mathf.CeilToInt(round.Countdown).ToString(); body.text = "Your search is about to begin."; break;
                case Stage.Seeking:
                    title.text = "Find the object"; body.text = "Explore your room. Point at the hidden object\nand press the right trigger when you find it.";
                    if (game.Tracked) status.text = $"Search time   {round.Seconds:0.0} s";
                    break;
                case Stage.Found:
                    title.text = "You found it!"; body.text = "A secret spot discovered.\nSwap roles and play again.";
                    status.text = $"Your time   {round.Seconds:0.0} s"; main = "Play again"; break;
                case Stage.Error:
                    title.text = "Let's try again"; body.text = game.Error; status.text = ""; main = "Restart game"; break;
            }
            SetButton(primary, main);
            SetButton(secondary, other);
            // 普通操作受追踪/忙碌/系统设置限制；错误重启例外，Start search 还需松扳机。
            // Gate ordinary controls on tracking/work/setup; error restart is exempt, search needs release.
            primary.interactable = round.Stage == Stage.Error || (game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom &&
                (round.Stage != Stage.Welcome || !game.room.Granted || game.placement.Available) &&
                (round.Stage != Stage.Handoff || round.CanReady));
            secondary.interactable = game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom;
            foreach (var button in colorButtons) button.interactable = game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom;
            SetButton(roomSetup, "Set up room");
            roomSetup.gameObject.SetActive(round.Stage == Stage.Welcome || round.Stage == Stage.Placing);
            roomSetup.interactable = game.Tracked && !game.placement.Busy && !game.room.SettingUpRoom;
            reset.gameObject.SetActive(round.Stage != Stage.Welcome);
            mascot.SetActive(false);
        }

        // 选物信息仅在藏匿/检查时显示；交接、倒数和寻找时隐藏以免泄露线索。
        // Show selection only while hiding/reviewing, not during handoff, countdown or searching.
        void RefreshSelection(PlaceOnPlane placement, bool visible)
        {
            body.rectTransform.anchoredPosition = visible ? new Vector2(-120, 56) : new Vector2(0, 36);
            body.rectTransform.sizeDelta = visible ? new Vector2(500, 108) : new Vector2(760, 108);
            status.rectTransform.anchoredPosition = visible ? new Vector2(-120, -36) : new Vector2(0, -62);
            status.rectTransform.sizeDelta = visible ? new Vector2(500, 50) : new Vector2(760, 58);
            previewRoot.gameObject.SetActive(visible);
            for (int i = 0; i < colorButtons.Length; i++)
            {
                colorButtons[i].gameObject.SetActive(visible);
                colorButtons[i].GetComponent<Outline>().enabled = i == placement.ColorIndex;
            }
            if (!visible) return;
            if (modelPreview == null || previewIndex != placement.SelectedIndex)
            {
                if (modelPreview != null)
                {
                    modelPreview.SetActive(false);
                    if (Application.isPlaying) Destroy(modelPreview); else DestroyImmediate(modelPreview);
                }
                modelPreview = placement.CreatePreview(previewRoot);
                modelPreview.transform.localPosition = Vector3.zero;
                modelPreview.transform.localRotation = Quaternion.Euler(0, placement.SelectedIndex == 0 ? -35 : 145, 0);
                var renderers = modelPreview.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                // 这里只缩放菜单预览以放入卡片，不改变实际物品 Prefab 的放置尺寸。
                // Fit only the menu preview into the card; never resize the placed-object prefab.
                modelPreview.transform.localScale *= Mathf.Min(0.16f / bounds.size.y, 0.18f / Mathf.Max(bounds.size.x, bounds.size.z));
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                modelPreview.transform.localPosition -= previewRoot.InverseTransformPoint(bounds.center);
                previewIndex = placement.SelectedIndex;
                previewColor = -1;
            }
            if (previewColor == placement.ColorIndex) return;
            placement.ApplyColor(modelPreview);
            previewColor = placement.ColorIndex;
        }

        static void SetButton(Button button, string label)
        {
            button.gameObject.SetActive(label.Length > 0);
            button.GetComponentInChildren<Text>().text = label;
        }
    }
}
