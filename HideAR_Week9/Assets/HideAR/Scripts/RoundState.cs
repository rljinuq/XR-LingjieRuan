using System;

namespace HideAR.Classroom
{
    // 阶段只表示游戏流程，不表示设备追踪或房间扫描的状态。
    // Stages describe gameplay, not device tracking or room capture.
    public enum Stage { Welcome, Placing, Saving, Review, Handoff, Countdown, Seeking, Found, Error }

    // 职责：项目自己的回合规则；不依赖 Unity 场景、头显、UI 或空间锚点。
    // Role: project-specific round rules, independent of Unity scenes, headset, UI and anchors.
    public sealed class RoundState
    {
        public Stage Stage { get; private set; } = Stage.Welcome;
        public float Seconds { get; private set; }
        public float Countdown { get; private set; }
        // Armed 表示下一次操作已解锁，不是手柄是否被按住。
        // Armed means the next action is allowed, not that the trigger is held.
        public bool Armed { get; private set; }
        public bool CanReady => Stage == Stage.Handoff && Armed;
        float releasedFor;

        public void Disarm() { Armed = false; releasedFor = 0; }

        // 先连续松开扳机 0.35 秒，避免点击按钮的同一次按压又触发放置或开始。
        // Require a 0.35-second release so one button press cannot also place or start.
        public void UpdateInput(bool held, float delta)
        {
            if (Armed) return;
            releasedFor = held ? 0 : releasedFor + delta;
            Armed = releasedFor >= 0.35f;
        }

        public bool Start()
        {
            if (Stage != Stage.Welcome && Stage != Stage.Found) return false;
            Seconds = 0;
            Enter(Stage.Placing);
            return true;
        }

        // 立即进入 Saving，阻止等待异步锚点结果期间再次提交放置。
        // Enter Saving immediately to reject duplicate placement while awaiting the anchor.
        public bool BeginPlacement()
        {
            if (Stage != Stage.Placing || !Armed) return false;
            Enter(Stage.Saving);
            return true;
        }

        // 保存完成只进入检查阶段，必须再确认藏好；不能直接开始寻找。
        // A saved placement enters review; it must be confirmed before searching.
        public void PlacementFinished()
        {
            if (Stage == Stage.Saving) Enter(Stage.Review);
        }

        public void Move() { if (Stage == Stage.Review) Enter(Stage.Placing); }
        public void Confirm() { if (Stage == Stage.Review) Enter(Stage.Handoff); }

        // 只由明确的 Start search 操作调用；等待或摘戴头显不会自动开始倒数。
        // Only explicit Start search calls this; waiting or headset removal never starts it.
        public void Ready()
        {
            if (!CanReady) return;
            Countdown = 3;
            Enter(Stage.Countdown);
        }

        // 只有追踪有效才推进时间：Countdown 倒数，Seeking 累加用时，Found 停止。
        // Advance time only with valid tracking: count down, time the search, stop at Found.
        public void Tick(float delta, bool tracked)
        {
            if (!tracked) { Disarm(); return; }
            if (Stage == Stage.Countdown)
            {
                Countdown = Math.Max(0, Countdown - delta);
                if (Countdown <= 0) Enter(Stage.Seeking);
            }
            else if (Stage == Stage.Seeking) Seconds += delta;
        }

        // 外层已检查射线命中；这里仅判断当前阶段和输入是否允许找到。
        // The caller checks the ray hit; here only the stage and input gate are checked.
        public bool Find()
        {
            if (Stage != Stage.Seeking || !Armed) return false;
            Enter(Stage.Found);
            return true;
        }

        public void Fail() { Enter(Stage.Error); }
        // 统一在换阶段时重新锁定输入，下一次操作需要重新松开扳机。
        // Re-arm through a fresh trigger release after every stage transition.
        void Enter(Stage stage) { Stage = stage; Disarm(); }
    }
}
