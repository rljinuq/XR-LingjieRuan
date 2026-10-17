# HideAR 课堂版使用与讲解

## 这是哪一版

新项目：`/Users/lingjieruan/Desktop/XR-LingjieRuan/HideAR_Week9`

旧项目：`/Users/lingjieruan/Desktop/XR-LingjieRuan/Prototype1`，保留备用，没有被本次重建覆盖。

这是以课程第九周 Activity 1 为参考的独立项目，不是把旧项目改个名字。没有复制官方 Samples 的场景、脚本或菜单。底层 AR/XR 仍由 Unity 的包提供，这些包不能当作“无用代码”删除。

新 App 名为 **HideAR Classroom**，旧头显版名为 **HideAR IP2**，应用标识不同，可以同时安装。

## 先做最小真机检查

初版已有 16 项 Unity 自动测试通过，用户反馈基础功能可以运行。2026-10-05 最新菜单手动定位版已安装到 Quest 3S，ADB 返回 Success，启动检查显示课堂版 Unity Activity 且应用进程正在运行；当前 27 项自动测试通过。这不等于已完成游戏体验验收。实际换房间扫描、模型预览、Start search 流程和按住 A 调整菜单的体验仍需真机验收，具体记录见 VERIFICATION.md。

第一次只检查以下三件事，再走完整游戏：

1. 进入 App 后能看见现实透视、开场菜单和右手柄射线。
2. 完成头显的 Space Setup 并允许空间数据权限，点 Start hiding 后能看到绿色平面。
3. 放置一个所选小物品，转头、缓慢移动，再看回来。它应留在原来的现实位置附近，不应跟着视野移动。

以上任何一步失败，先记下画面或错误，再排查。编辑器预览和自动测试不能替代这些真机检查。

## 安装到 Quest 3S

先在配对手机的 Meta Horizon App 中开启头显开发者模式，使用数据线连接 Mac，并在头显里允许 USB 调试。只连接这一台设备。

Mac 按 Command + 空格，搜索“终端”，打开后执行：

```sh
"/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" devices
```

设备编号后应为 `device`。`unauthorized` 表示要在头显里允许调试；没有设备编号表示尚未连接成功。

安装课堂版（已有安装包时，不需要打开 Unity 或 Xcode）：

```sh
"/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" install -r "/Users/lingjieruan/Desktop/XR-LingjieRuan/Builds/Week9/HideAR_Classroom.apk"
```

显示 `Success` 后启动：

```sh
"/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" shell am start -n com.lingjieruan.hidear.classroom/com.unity3d.player.UnityPlayerGameActivity
```

以后在头显应用库的 Unknown Sources / 未知来源中找 **HideAR Classroom**。不要误开旧的 HideAR IP2。

## 平时怎么启动

- 不连电脑：在头显应用库中选择 **Unknown Sources / 未知来源 → HideAR Classroom**。系统版本不同，分类入口的位置可能不同；安装完成后不需要再装 APK。
- 连着 Mac：在 Finder 中打开本项目的 `Tools` 文件夹，双击 `Launch_Quest.command`。它会检查 USB 连接并打开已经安装的课堂版，无需复制终端命令。这个文件只负责启动，不会更新 APK。
- 安装最新版：连接 USB 并允许调试后，双击 `Tools/Install_Quest.command`，等待显示 `Success`。它安装 `Builds/Week9/HideAR_Classroom.apk` 并打开课堂版。
- 改完代码后：先重新打包、安装，才会运行新代码。拔掉数据线后，已经安装的应用仍可以运行。

## 操作一轮

仅用右手柄。食指扳机点击按钮、放置所选物品和确认找到。短按 A 把菜单移回面前；按住 A 时，菜单随转头和走动调整，松开后固定在该世界位置。每次换阶段后先松开扳机，再进行下一次操作。

菜单所在区域会拦截放置和寻找点击。若挡住目标，按住 A 并向旁边转头，松开 A 把菜单留在一侧，再回头指向目标。切换阶段、恢复追踪不再自动搬动卡片；交接后寻找者也可短按 A 找回菜单。菜单保持直立、略低于当前眼睛高度，接近垂直低头/抬头时沿用上一次水平方向。A 只移动菜单，不移动物品或 Anchor；调整时禁止放置/找到判定，松开 A 和扳机后再点击。

1. 允许房间权限。首页下方的 **Set up room** 是独立按钮，进入放置页后也一直可用，不会被 Change object 替换。换房间或需要重新设置时，点它打开 Quest 的系统房间设置，按系统提示扫描并包含当前房间的地面、桌面，完成后返回应用。
2. Start hiding：进入放置阶段，不会因为按开始按钮就自动放置。
3. Start hiding 前或进入放置阶段后，点 Change object 在 Dinosaur、Dog、Cat、Bunny 之间切换。菜单显示当前模型；Original 保留原色，四个色块分别选择苔绿、蓝、灰、白。
4. 指向绿色桌面或地面，会出现当前模型的实际大小预览。移动射线比较位置和颜色，再按一下扳机，只保存一个物品。
5. Happy with this spot：可以继续点色块，观察物品与现实背景的对比；Move object 重新选择位置，Confirm hiding 才确认。
6. Ready to search：物品、模型预览和颜色控件全部隐藏，等待明确点击开始。两个人玩时，把头显和右手柄交给寻找者；一个人测试时直接继续即可，应用不要求摘下再戴上。
7. 寻找者准备好，松开扳机，再自己点 **Start search**。只有这次点击才开始倒数三秒，随后显示物品并开始计时。
8. 追踪丢失或应用暂停时，倒数和寻找计时暂停；恢复后继续，不会因摘戴返回确认页。准备页即使一直等待或追踪恢复，也不会自动开始。
9. 指向物品，按一下扳机，出现 You found it 和寻找用时。寻找阶段不显示绿色平面、模型选择或颜色提示。
10. Play again 交换角色再玩。Reset round 重载本游戏场景，清除这一轮并回到开场；会重新初始化空间组件。

追踪丢失或应用暂停时不累计寻找用时。佩戴状态只用于判断追踪是否可用，不作为交接前置条件，不检测换人。换房间后先重新做当前房间的 Space Setup；这是 Quest 保存的空间数据，不是应用里持续扫描新平面。若系统没有打开，界面会提示从 Quest Settings 的 Space Setup 进入；设置完成返回应用时会重新读取平面。实际系统房间设置、平面重新加载和按钮响应仍需真机验收。

## 修改后重新打包

1. Unity Hub 的 Add / 从磁盘添加项目，选择上方的 **HideAR_Week9 文件夹**，不是 Prototype1，也不是某个 .unity 文件。
2. 使用 Unity **6000.3.21f1** 打开。此版本需要 Android Build Support、SDK、NDK、OpenJDK。
3. 在 Unity 的 Project 窗口进入 `Assets > HideAR > Scenes`，双击 `HideAR_Week9` 场景。
4. File > Build Profiles，选择 Android。尚未 Active 时先切换到 Android，等导入完成。
5. 点击顶部菜单 **HideAR > Classroom > Build APK**。
6. 完成后用上面的安装命令更新新 App。默认输出到 `XR-LingjieRuan/Builds/Week9/HideAR_Classroom.apk`。

场景和 Inspector 引用已由编辑器工具生成。正常使用不需要你逐项拖拽配置，也不要反复重建初始场景。先保留足够磁盘空间；不用为了新版本删除旧项目。

## 答辩时怎么解释

先看 [CODE_GUIDE_ZH.md：文件结构与答辩速查](CODE_GUIDE_ZH.md)。它逐个说明五个运行脚本、编辑器工具、测试、程序集和启动脚本的用途，并区分官方接口与项目自己的规则。代码已加简短中英文注释；本次只改说明，不改功能，不需要重新安装。

按这个顺序阅读 `Assets/HideAR/Scripts`：

1. **RoundState.cs**：记录游戏阶段和数字。确认藏好后先等待，玩家明确点 Start search 才倒数；松开扳机的短暂保护防止同一次点击连续跳阶段。不负责空间定位或换人检测。
2. **GameFlow.cs**：读取追踪状态，把 Primary、Secondary、SetUpRoom、SelectColor、ResetGame 这些普通按钮方法连接到游戏规则。SetUpRoom 独立打开房间设置；Secondary 只切换模型或重新放置。
3. **RoomAccess.cs**：取得房间数据权限后，启用平面与射线组件；SetUpRoom 调用 Unity 的官方系统房间设置接口，返回应用后重启平面与射线组件读取当前房间。这对应第九周 ScenePermission，再补房间设置入口。
4. **PlaceOnPlane.cs**：右手柄射线查找 PlaneWithinPolygon，用 TryAddAnchorAsync 保存定位点，并放置所选 prefab；射线指向平面时显示无碰撞盒的临时模型。颜色是一个数组，通过 MaterialPropertyBlock 改当前实例，不改下载资源。寻找时用 Physics.Raycast 检查物品碰撞盒。
5. **GameUI.cs**：根据阶段改文字、按钮和菜单位置；显示所选模型和颜色色块，交接与寻找时隐藏选物信息。不会移动已放置的物品。

`Editor` 是生成场景、配置 Android 和打包的工具代码，不在头显内作为游戏逻辑运行。`Tests` 是自动检查。你仍然应该知道它们的用途，但不要把这些工具代码与玩家交互混在一起讲。

## 必须说清楚的界限

- 课程代码是技术参考，不是整款游戏；阶段、按钮防误触、确认和找到判定属于项目自己的逻辑。
- 追踪、透视、空间平面、Anchor、UI 射线由 Unity 包提供，不是项目自行发明的算法。
- C++ 是 IL2CPP 打包生成的产物，本项目手写逻辑使用 C#。
- 不包含手势、联网、两台头显共享、退出后恢复藏匿点、GPS 或数据库。
- 未开启真实家具的深度遮挡。看到虚拟物品透过家具不代表 Anchor 跟随头部，两者是不同问题。
- 简化代码不等于自动满足 IP2b 与 IP2a 有显著交互差异的要求；本版先验证基础功能。

参考与辅助开发说明见 README.md。自动测试结果和头显测试结果必须分开记录，不把模拟数据当作真实用户测试。
