# HideAR Quest：代码阅读与答辩说明

## 先分清三层

1. **项目自己的 C#**：开场、藏匿、重新放置、确认、交接、倒数、寻找、计时、成功界面。
2. **Unity 官方组件**：头显/手柄追踪、现实透视、平面数据、Raycast 和 Anchor。项目使用这些组件，没有重写它们的算法。
3. **自动生成的 C++**：Unity 使用 IL2CPP 打包时生成的中间代码，再编译成头显运行的程序。不是额外手写的一套游戏逻辑，不要直接修改它。

Unity 的 [IL2CPP 官方说明](https://docs.unity3d.com/cn/6000.0/Manual/scripting-backends-il2cpp.html) 介绍了这条编译流程。答辩时重点解释项目自己的 C# 和如何调用官方组件，无需逐行解释自动生成的 C++。

## 按这个顺序看

文件都在 `Assets/HideARQuest/Runtime/`。

| 文件 | 主要职责 | 首先看什么 |
| --- | --- | --- |
| `QuestRound.cs` | 游戏规则与阶段，不操作现实空间 | `QuestPhase`、`StartHiding`、`RequestPlacement`、`Confirm`、`Ready`、`Found` |
| `QuestGameController.cs` | 把输入、官方 AR 组件、模型和界面连接起来 | `OnCommand`、`OnSurfaceHit`、`OnAnchorsChanged`、`TryFind` |
| `QuestPanel.cs` | 世界空间 Canvas、文字、按钮 | `Show` 更换内容；`Recenter` 移动菜单 |
| `QuestDinosaur.cs` | 把模型放到 Anchor 下，统一大小并加点击碰撞盒 | `Create` |
| `QuestRoomAccess.cs` | 请求房间权限，调用官方空间设置入口 | `RequestPermission`、`CaptureRoom` |

这 5 个文件目前约 716 行，包含空行。前两个核心文件约 450 行。
`Editor/QuestProjectSetup.cs` 是场景配置和打包工具，不是头显中每帧执行的游戏规则。
`Tests/Editor/` 是自动测试，不是玩家操作逻辑。

## 一轮游戏怎么走

`Intro → Placing → Saving → Review → Handoff → Countdown → Seeking → Found`

- `Intro`：玩家点开始，才进入放置阶段。
- `Placing`：指向有效平面，按扳机。
- `Saving`：等待官方组件完成 Anchor 创建，期间不接受第二次放置。
- `Review`：玩家检查位置；可以重新放置，或确认藏好。
- `Handoff`：隐藏恐龙，把同一台头显交给寻找者。不会自动开始倒数。
- `Countdown`：寻找者点准备，倒数三秒。
- `Seeking`：显示恐龙并计时；扳机只用于找到判定，不再创建物品。
- `Found`：显示成功和本轮用时。

## 几个需要讲清楚的问题

### 为什么恐龙不会跟着头移动？

官方 Raycast 给出平面上的位置；官方 `ARPlaceAnchor` 根据平台支持创建 Anchor。
恐龙是这个 Anchor 的子对象，不是 Camera 的子对象。游戏脚本不在 `Update` 中把恐龙移到镜头前。
实际稳定性仍依赖设备空间追踪，需要真机验证，不能承诺绝对无漂移。

### 为什么按按钮不应该同时放一个恐龙？

菜单有 UI 层的碰撞盒，挡住穿过菜单的放置射线。
切换阶段后，还必须先松开扳机约 0.35 秒，下一次按下才允许成为游戏操作。
只有 `Placing` 阶段的有效命中会转交官方放置组件。接受一次后立即进入 `Saving`，堵住重复放置。

### 为什么有两种 Raycast？

- 放置：官方 `ARRaycastManager` 查现实平面，限定 `PlaneWithinPolygon`。
- 寻找：Unity `Physics.Raycast` 查虚拟恐龙的碰撞盒，命中才进入 `Found`。

两者对象不同，寻找时不会再次调用放置逻辑。

### 计时从哪里开始？

倒数结束进入 `Seeking` 才累计时间；找到后停止。
追踪不可用、应用失去焦点或暂停时不继续累计。摘戴头显时的具体系统反馈需要真机验收。

### 为什么 Quest 需要先设置房间？

当前使用的官方 Meta OpenXR 平面功能读取头显 Space Setup 数据。
它不是 iPhone ARKit 那种进入 App 后持续扫描新平面的同一种流程。
安全边界也不等于桌面/地面数据；缺少房间数据或权限时，需要先完成设置。

## 当前范围

两位玩家轮流使用同一台正在运行的头显。没有两台设备共享 Anchor、退出后恢复藏匿点、GPS 或数据库。
当前未启用真实家具的深度遮挡，不能把这一版描述为“恐龙会自然被所有家具挡住”。

答辩时应区分“项目实现了游戏交互”和“调用官方库完成空间追踪”，不要把底层 Anchor 算法说成项目自行实现。
