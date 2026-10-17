# HideAR Week9 文件结构与答辩速查

核对日期：2026-10-16。只针对独立课堂版 `HideAR_Week9`，不针对 `Prototype1`。

## 1. 解释量有多大

核心不是整个 Unity 工程，而是 `Assets/HideAR/Scripts` 下的 **5 个运行脚本**。本次加注释前共 640 行，现在共 740 行，均包含空行与注释；增加的是解释，不是新功能。它是一个需要理解流程的完整小原型，不是只有几十行，但也没有联网、数据库、手势识别或自行实现的定位算法。

| 运行文件 / Runtime file | 当前行数 | 需要讲清楚的职责 / Responsibility |
| --- | ---: | --- |
| `RoundState.cs` | 96 | 游戏阶段、防误触、明确开始、计时 / Round rules and timing |
| `GameFlow.cs` | 138 | 把设备状态、按钮、规则和 UI 接起来 / Input and component coordination |
| `RoomAccess.cs` | 125 | 权限、系统房间设置和返回后读取平面 / Permission and system room setup |
| `PlaceOnPlane.cs` | 210 | 射线、预览、单物品、官方 Anchor、颜色和找到 / Placement and finding |
| `GameUI.cs` | 171 | 文字按钮、选物预览、独立移动任务卡 / Presentation and menu positioning |

先读 `RoundState` 理解规则，再读 `GameFlow` 看谁调用谁；接着读 `RoomAccess` 和 `PlaceOnPlane`，最后看 `GameUI`。五个文件都应能说明用途和关键流程，不必背每个变量或每一行排版代码。这是阅读优先级，不是保证老师不会问其他部分。

## 2. 文件结构

以下路径相对于本项目根目录。

```text
HideAR_Week9/
  README.md                         项目概览、运行方式、技术和资源来源
  START_HERE_ZH.md                   中文安装、操作和打开 Unity 的步骤
  CODE_GUIDE_ZH.md                   本文：文件职责、流程和答辩问题
  VERIFICATION.md                    自动检查、安装记录与未验收的真机项目
  Tools/
    Launch_Quest.command            只启动已安装的课堂版
    Install_Quest.command           更新课堂 APK 后启动课堂版
  Assets/
    HideAR/
      Scripts/                      在 Quest 中运行的五个核心脚本
        RoundState.cs
        GameFlow.cs
        RoomAccess.cs
        PlaceOnPlane.cs
        GameUI.cs
        HideAR.Classroom.asmdef      核心代码的程序集和依赖定义
      Editor/                       仅在 Unity 编辑器使用的工具
        ClassroomSetup.cs
        ClassroomPreview.cs
        HideAR.Classroom.Editor.asmdef
      Tests/Editor/                 仅在编辑器运行的自动测试
        ClassroomTests.cs
        HideAR.Classroom.Tests.asmdef
      Scenes/
        HideAR_Week9.unity           保存好的课堂场景、组件引用和按钮事件
      Prefabs/                      可重复实例化的模型/平面对象模板
        Dinosaur.prefab
        Dog.prefab
        Cat.prefab
        Bunny.prefab
        Room Surface.prefab
      Settings/                     输入、XR、渲染、材质与宠物调色资源
    Art/
      Dinosaur/                     恐龙源模型与许可
      CandidateModels/              已下载的候选模型
        MODEL_SOURCES.md            模型来源和许可记录
  Packages/
    manifest.json                   本项目直接使用的 Unity 包
    packages-lock.json              包的具体版本与依赖关系
  ProjectSettings/                  Unity 工程设置，不是额外玩法系统
```

APK 不在这个项目目录内，而在 `../Builds/Week9/HideAR_Classroom.apk`。自动检查输出在 `../Builds/Week9/Review`。旧版的项目、场景和安装包不参与本版流程。

## 3. 五个核心文件怎么讲

### RoundState.cs：规则 / Rules

一句话：“这里管理一轮游戏何时能做什么，不处理空间定位。”

`Welcome → Placing → Saving → Review → Handoff → Countdown → Seeking → Found`。错误进入 `Error`；重新放置从 `Review` 回到 `Placing`，再玩从 `Found` 回到 `Placing`。

- `UpdateInput`：扳机连续松开约 0.35 秒后才解锁下一次操作，避免点 UI 的一次按压又触发放置或找到。
- `BeginPlacement` / `PlacementFinished`：先进入保存阶段阻止重复请求，保存成功后停在检查位置阶段。
- `Confirm` / `Ready`：确认藏好后先等待；明确点击 Start search 才开始三秒倒数，不检测摘戴交接。
- `Tick` / `Find`：追踪有效才推进倒数/寻找计时；找到后进入成功阶段，不再累计用时。

这是纯 C# 规则类，不继承 `MonoBehaviour`，因此可以不连接头显就测试阶段和时间规则。

### GameFlow.cs：协调 / Coordination

一句话：“把官方输入和追踪状态交给规则，再让放置与界面响应规则。”

- `Update` 读取头显追踪，并在相关阶段检查 Anchor 追踪；暂停或无追踪时暂停时间。佩戴状态不用于识别换人。
- 首次有效追踪或按住 A 才请求移动菜单；恢复追踪和换阶段不强制搬卡片。按住 A 调整时会锁住放置/找到操作。
- `Primary` 按阶段执行开始藏匿、确认位置、明确开始寻找、再玩或错误重启。
- `Secondary` 在藏匿前换模型，在检查阶段重新放置；`SetUpRoom` 始终是独立的房间设置按钮。
- 换阶段时只切换物品显示，不移动 Anchor。平面只在放置阶段显示，寻找阶段不提供绿色平面线索。
- `ResetGame` 重载课堂场景；这不同于 Play again 的清理物品并开始下一轮。

场景 Inspector 中普通 Button 的 `onClick` 保存了这些方法引用，没有额外的自定义事件系统。

### RoomAccess.cs：权限与系统入口 / Permission and system entry

一句话：“应用申请使用房间数据，再请官方组件读取；不是我自己写房间扫描。”

`Request → Allow`：使用 Android 权限回调，取得 Spatial Data 权限后才启用 `ARPlaneManager` 和 `ARRaycastManager`。

`SetUpRoom`：通过 `MetaOpenXRSessionSubsystem.TryRequestSceneCapture` 请求 Quest 系统房间设置；接口不可用时提示手动进入 Space Setup。返回应用后重新核对权限，并隔一帧重启两个管理器，重新查询房间数据。实际系统设置是否成功完成仍需真机验证。

本版查询系统保存的房间几何，不是持续发现每一件新家具的扫描算法。

### PlaceOnPlane.cs：放置与找到 / Placement and finding

一句话：“从右手柄射线命中合适的房间平面，再请求一个官方锚点保存物品位置。”

放置链路：`右手柄瞄准 → 拦截 UI 点击 → AR 平面射线 → 水平向上平面 → 实际大小预览 → 扳机 → 保存 Anchor → Review`。

- `Update` 同时检查追踪、权限、系统设置和忙碌状态。UI 整块卡片（含按钮空隙）优先拦截，不能点按钮时顺便放置。
- 平面使用 `ARRaycastManager.Raycast` 与 `PlaneWithinPolygon`；只接受水平向上的地面/桌面。
- `Place` 用 `ARAnchorManager.TryAddAnchorAsync(pose)`。这是 SDK 的异步接口，不是自己编写定位算法，也不是项目另写多线程系统。
- `Busy` 和 `Saving` 阶段防止重复提交。结果迟到且回合已经出错/退出时清掉多余锚点，不能突然恢复到检查页。
- 已放置模型是 Anchor 的子对象，不是 Camera 的子对象，不逐帧跟随头部。历史属性名 `Dinosaur` 实际也能指向 Dog、Cat、Bunny。
- `CreatePreview` 创建无碰撞的模型副本；`ApplyColor` 用 `MaterialPropertyBlock` 改实例外观，保留共享原材质和脸部细节。宠物使用编辑器预生成的调色贴图。
- 寻找时改用 `Physics.Raycast` 检查模型碰撞盒；当前处于 `Seeking` 且输入已解锁才能进入 `Found`。这不是靠距离自动找到，也不是识别现实中的动物。
- `Clear` 删除旧 Anchor/物品，成功后才允许重新放置。

AR 平面射线解决“在哪里放”，物理射线解决“点到了哪个虚拟碰撞盒”，两者不要混为一谈。

### GameUI.cs：显示与卡片 / Presentation and task card

一句话：“读取回合阶段来显示界面，不在绘制界面时推进游戏规则。”

- `Refresh` 更新文字、按钮和可交互状态；`RefreshSelection` 管理菜单里的模型与色块，交接和寻找时隐藏选物线索。
- `RequestRecenter → LateUpdate → Recenter`：在稍后的相机姿态上移动菜单。位置为水平方向前方 1.5 米、眼睛下方 10 厘米，并保持直立。
- 几乎垂直低头/抬头时先判断水平投影长度，再决定是否归一化，避免微小误差造成方向跳变。
- 松开 A 后没有持续移动请求，所以卡片留在世界位置；移动菜单不影响实际物品的 Anchor。
- 菜单预览会缩放以适应卡片。这不等于修改场景里实际放置 Prefab 的尺寸。

## 4. 其他代码文件为什么存在

| 文件 / File | 用途及答辩边界 / Purpose and boundary |
| --- | --- |
| `Editor/ClassroomSetup.cs` | 场景、输入绑定、预制体、普通按钮事件、Android/OpenXR 配置、APK 构建。它组装官方组件，不实现游戏里的追踪。`Prepare` 会配置 Android，但仅在缺少场景时生成初始场景；已有场景不会被它重新组装。`Apply...` 命令会显式修改已有资源，不应反复运行当作启动步骤。 |
| `Editor/ClassroomPreview.cs` | 在编辑器模拟阶段并截图，检查文字超框、卡片超出画面和空白画面。模拟的追踪状态与用时不是实验数据，也不能证明真机表现。 |
| `Tests/Editor/ClassroomTests.cs` | `RoundTests` 检查规则；`SceneTests` 检查引用、按钮、模型尺寸、颜色和菜单位置等。测试对象不等于真实空间锚点；不能用测试通过代替头显验收。 |
| `Scripts/HideAR.Classroom.asmdef` | 定义核心程序集和包引用，不是玩法逻辑。 |
| `Editor/HideAR.Classroom.Editor.asmdef` | 编辑器程序集，只在 Editor 平台使用。 |
| `Tests/Editor/HideAR.Classroom.Tests.asmdef` | 编辑器测试程序集及测试依赖，不进入头显游戏。 |
| `Tools/Launch_Quest.command` | Mac 脚本，调用 Unity 自带的 ADB，只打开已安装的课堂版，不安装更新。 |
| `Tools/Install_Quest.command` | Mac 脚本，检查设备，安装现成的 Week9 APK 并发起启动，不编译 Unity 项目。安装/启动命令成功不等于已完成游戏测试。 |

工具和测试也属于项目工作，应知道它们解决什么问题；不需要把其中每行布局和构建配置讲成运行时算法。

## 5. 非代码资源怎么看

- `.unity` 保存场景层级、组件和引用；`.prefab` 是可实例化对象模板。模型模板保存小尺寸和点击碰撞盒，`Room Surface` 模板显示系统提供的平面。
- `Settings/Controls.asset` 保存 Input System 绑定；`XR Settings.asset` 保存 XR 加载配置；`Pipeline.asset` 和 `Renderer.asset` 配置 URP 渲染。
- `.mat` 是材质；`Pet Color ... .asset` 是宠物调色贴图。这些资源要被脚本/场景引用，不是看到没有 C# 就可以删。
- `Assets/Art` 放下载的源模型和许可；模型来源见 `MODEL_SOURCES.md` 及 README，不能说成自己建模。
- `.meta` 保存 Unity 的资源标识，场景靠它维持引用，不要随意删除或重建。
- `Packages` 是所用官方包及版本记录；`Library` 是 Unity 生成的导入/编译缓存，不是自己写的玩法代码。
- Unity 生成的工程文件、IL2CPP 产生的 C++ 都不是另外手写了一套游戏系统。

## 6. 常见追问

**为什么不会无限放置？** 只在 Placing 且输入解锁时接受放置；提交后立即进入 Saving 并置 Busy，保存后停在 Review。要移动必须先清理旧对象。

**为什么换人后不会立刻计时？** Handoff 只等待；寻找者自己点击 Start search 才进入 Countdown，倒数完才开始寻找用时。不要求摘戴头显，一个人也能测试。

**物品位置是谁算的？** 官方 AR Foundation / OpenXR 提供平面命中和空间 Anchor；项目管理请求时机、模型实例及回合规则。移动头部不代表物品应该跟着头部走。

**为什么现实家具有时挡不住模型？** 本课堂版没有接入真实环境深度遮挡，按当前范围先不添加。不能声称能完全藏进家具后面；缩小、选色和改变位置不等于物理遮挡。

**你写了哪些、借用了哪些？** 手柄平面射线与权限启用思路参考 Week 09；定位、透视、输入和 UGUI 由官方包提供；阶段、确认、防误触、选物/颜色和找到流程是项目组织的逻辑，并有 Codex 辅助开发。出处记录在 README，不把现成功能说成原创定位算法。

**怎么知道没问题？** 自动测试检查规则和场景约束，编辑器图片检查布局；真实追踪、系统权限、房间切换、按钮体验必须用头显验证。安装成功不等于所有功能验收成功，具体已验证范围看 `VERIFICATION.md`。

## 7. 本次注释与版本范围

本次只给现有 C# 和两个 Mac 脚本添加/补全简短中英文注释，并增加本说明和文档入口。注释围绕职责、为什么需要保护、官方接口与项目规则的边界，没有给每行赋值写重复说明。

注释不表示代码永远冻结，以后确实改功能时需要同步更新注释。本次没有改变游戏语句或参数，没有新增遮挡、换人检测等系统，没有运行场景初始化/Apply 工具，也不需要重新打包或安装 APK。
