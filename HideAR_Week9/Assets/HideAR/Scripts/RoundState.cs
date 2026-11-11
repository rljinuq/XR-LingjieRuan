// RoundState.cs：管理 HideAR 目前进行到哪一步、倒数/寻找用了多久，以及扳机是否可再次操作。
// GameFlow 把设备输入交给这里；PlaceOnPlane 通知放置/找到结果；GameUI 根据这里的 Stage 显示页面。
// This class manages round rules. It does not read Quest tracking or create Anchors.
using System;

namespace HideAR.Classroom
{
    // Stage 是游戏流程的名称，不是头显或 Anchor 的追踪状态。
    // 玩家流程：Welcome 选物 -> Placing 放置 -> Saving 等待 -> Review 检查 -> Handoff 交接 -> Countdown 倒数 -> Seeking 寻找 -> Found。
    // Error 表示本轮无法继续；具体错误文字由 GameFlow 保存并显示。
    public enum Stage { Welcome, Placing, Saving, Review, Handoff, Countdown, Seeking, Found, Error }

    // 这是普通 C# 类，不挂在 Unity 场景的 GameObject 上；GameFlow 持有它并调用下方方法。
    public sealed class RoundState
    {
        // ===== 当前回合的数据 =====
        // get; private set; 表示其他脚本能读取，但只有 RoundState 能修改这些值。
        public Stage Stage { get; private set; } = Stage.Welcome; // 目前显示/允许操作的游戏阶段。
        public float Seconds { get; private set; } // 只累计 Seeking 寻找阶段的秒数，作为结果成绩。
        public float Countdown { get; private set; } // Start search 后剩余的倒数秒数。
        public bool Armed { get; private set; } // 扳机释放够久后为 true，下一次按下才可以触发动作。
        public bool CanReady => Stage == Stage.Handoff && Armed; // Handoff 等待页且扳机已解锁时，Start search 才能点击。
        float releasedFor; // 暂存扳机连续松开的秒数；按住就从 0 重新累计。
        // Other scripts can read these properties, but this class owns their values.

        // ===== 要求玩家松开扳机后才能做下一步 =====
        public void Disarm() { Armed = false; releasedFor = 0; }
        // 每次阶段改变、追踪恢复或卡片移动后调用：同一次按住的 Trigger 不会连着确认两步。
        // The player must release the trigger before the next action.

        // ===== 统计扳机松开时间，重新允许操作 =====
        public void UpdateInput(bool held, float delta)
        {
            if (Armed) return;
            // 已经解锁就维持 true，直到 Disarm 锁住；不然下一次按下会立即被本函数锁回去。
            releasedFor = held ? 0 : releasedFor + delta;
            // held 为 true 时清零；为 false 时加上本帧时间。松开必须连续达到 0.35 秒。
            Armed = releasedFor >= 0.35f;
        }
        // GameFlow 每帧传入“扳机当前是否按住”和“这一帧经过几秒”。
        // 松开 0.35 秒只是解锁；真正的放置/确认仍要再按一次扳机。
        // Release, then press again: one held press cannot trigger two actions.

        // ===== 从开场或结果页开始新一轮 =====
        public bool Start()
        {
            if (Stage != Stage.Welcome && Stage != Stage.Found) return false;
            // && 表示两个条件都成立才拒绝：除 Welcome 和 Found 外，其他阶段不能重开。
            Seconds = 0;
            // 新一轮清除上一轮成绩，接着进入 Placing；Enter 也会锁住扳机。
            Enter(Stage.Placing);
            return true;
        }
        // 返回 true 表示状态已切到 Placing；false 表示当前阶段不允许开始。
        // Starts hiding from Welcome or starts a replay from Found.

        // ===== 放置射线命中后，锁定本次 Anchor 请求 =====
        public bool BeginPlacement()
        {
            if (Stage != Stage.Placing || !Armed) return false;
            // || 表示任一条件成立就拒绝：不在 Placing，或玩家还没松开扳机，都不能保存位置。
            Enter(Stage.Saving);
            // 先切到 Saving；PlaceOnPlane 收到 true 后才调用 Anchor API，防止重复提交。
            return true;
        }
        // 返回 true 才允许 PlaceOnPlane 发起异步 Anchor 请求。
        // Saving is set before the asynchronous request starts.

        // ===== Anchor 创建成功后进入检查页 =====
        public void PlacementFinished()
        {
            if (Stage == Stage.Saving) Enter(Stage.Review);
            // 只在仍然等待保存时接受完成通知；如果已经 Error，迟到的回调不能把页面改回 Review。
        }
        // PlaceOnPlane 在正式模型已挂到 Anchor 后调用此方法。
        // A late anchor result cannot reopen Review after an error.

        // ===== Hider 检查藏匿位置后的两个选择 =====
        public void Move() { if (Stage == Stage.Review) Enter(Stage.Placing); }
        public void Confirm() { if (Stage == Stage.Review) Enter(Stage.Handoff); }
        // Move 回到放置阶段；GameFlow 会先删掉旧模型并移除旧 Anchor。
        // Confirm 只交给寻找者，不会自动倒数；准备好后由寻找者再点 Start search。
        // Move lets the hider try another spot. Confirm hands over the headset without starting the countdown.
        // Move returns to placement. Confirm waits for the seeker to start.

        // ===== Seeker 明确点击 Start search 后开始倒数 =====
        public void Ready()
        {
            if (!CanReady) return;
            // CanReady 为 false 时表示不在交接页，或扳机还没有松开够久；点击不产生任何变化。
            Countdown = 3;
            // 点击成功后设为 3 秒，再切到 Countdown；Enter 会锁扳机，避免这次点击继续触发。
            Enter(Stage.Countdown);
        }
        // 只有 Handoff 的 Start search 按钮会走到这里，准备页本身不会自动开始。
        // The seeker starts the countdown with an explicit action.

        // ===== 依据追踪状态推进倒数与寻找计时 =====
        public void Tick(float delta, bool tracked)
        {
            if (!tracked) { Disarm(); return; }
            // tracked=false 表示 GameFlow 检测到头显/Anchor 追踪不可用；锁输入并暂停计时。
            if (Stage == Stage.Countdown)
            {
                Countdown = Math.Max(0, Countdown - delta);
                // Math.Max(0, ...) 保证倒数不会显示负数。
                if (Countdown <= 0) Enter(Stage.Seeking);
                // 只有倒数到 0 才开放正式寻找；Handoff 等待页不会偷偷开始计时。
            }
            else if (Stage == Stage.Seeking) Seconds += delta;
            // 只在 Seeking 加寻找时间；Found 或其他阶段不会继续增加 Seconds。
        }
        // delta 是本帧经过的秒数；GameFlow 每帧调用，追踪无效时本轮时钟暂停。
        // The timer advances only during the countdown and the active search.

        // ===== PlaceOnPlane 命中正式物品后结束寻找 =====
        public bool Find()
        {
            if (Stage != Stage.Seeking || !Armed) return false;
            // 不在寻找阶段或扳机未解锁时忽略命中，防止提前结束/一次按压重复完成。
            Enter(Stage.Found);
            // Found 会停止后续计时；GameUI 接下来显示冻结的最终用时。
            return true;
        }
        // 这里不检测射线或 Collider；PlaceOnPlane 已确认命中的是正式放置模型。
        // PlaceOnPlane checks the hit. This method changes the round result.

        // ===== 统一进入错误页与切换阶段 =====
        public void Fail() { Enter(Stage.Error); }
        void Enter(Stage stage) { Stage = stage; Disarm(); }
        // 所有阶段切换都从 Enter 经过，因此切换后扳机必定重新锁定。
        // Fail 只改变阶段；错误说明文字由 GameFlow 提供给 GameUI。
        // Every stage change requires a fresh trigger release.
    }
}
