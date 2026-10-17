# HideAR IP2：Quest 3S / Quest 3 测试步骤

## 打开哪个项目

Unity 项目仍是：

`/Users/lingjieruan/Desktop/XR-LingjieRuan/Prototype1`

头显场景是：

`Assets/HideARQuest/Scenes/HideAR_IP2_Quest.unity`

iPhone 场景和脚本仍保留。不要用旧的 Xcode 项目安装头显版本。Quest 使用 Android APK。

## 第一次准备

1. 在 Quest 3S 上完成正常的初始设置，配对左右手柄。
2. 按照 [Meta 的开发者设备设置](https://developer.oculus.com/documentation/unity/unity-env-device-setup/) 开通开发者模式。如果账号要求验证或创建开发者组织，先完成该步骤。
3. 用支持数据传输的 USB-C 线连接 Quest 和 Mac。戴上头显，在出现 USB debugging / USB 调试授权时选择允许。只有充电功能的线无法安装 APK。
4. 在头显设置中完成 Space Setup / 空间设置，把本次测试的桌面或地面加入房间。仅画好安全边界不等于完成空间设置。也可以稍后在游戏中选择 `Set up room`。
5. Mac 的 Unity 版本为 `6000.3.21f1`。已检查到该版本带有 Android Build Support、SDK、NDK 和 OpenJDK，无需为此重新安装 Unity。
6. 第一次 Android 构建前建议留出 10–15 GB 空闲磁盘。自定义构建菜单在不足 8 GB 时会停止，避免临时文件耗尽磁盘。

Unity 的 [设备设置说明](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.3/manual/get-started/device-setup.html) 解释了开发者模式和 Space Setup。Quest 3S 和 Quest 3 各自需要设置当前房间。

## 现在直接安装已生成的 APK

2026-10-01 已生成安装包。首次测试可以直接安装它，无需打开 Unity 或 Xcode，也无需再次 Build。

1. 完成上面的开发者模式、USB 调试授权和空间设置，只连接这一台 Quest。
2. 在 Mac 按 `Command + 空格`，搜索并打开“终端 / Terminal”。执行：

```sh
"/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" devices
```

3. 设备编号后显示 `device` 才能安装。显示 `unauthorized` 时，戴上头显允许 USB 调试；没有设备行时，检查数据线、开发者模式和 USB 连接。
4. 执行安装命令，等到终端显示 `Success`：

```sh
"/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" install -r "/Users/lingjieruan/Desktop/XR-LingjieRuan/Builds/Quest/HideAR_IP2_Quest.apk"
```

5. 戴好头显。执行下面的命令打开游戏，然后允许游戏请求的空间数据权限：

```sh
"/Applications/Unity/Hub/Editor/6000.3.21f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb" shell am start -n com.lingjieruan.hidear.quest/com.unity3d.player.UnityPlayerGameActivity
```

安装或启动报错时，保留终端中的原始错误，不要先删除工程或重新构建。当前安装包尚未经过头显实测。

## 修改代码后重新构建

1. 打开 Unity Hub。在 Projects 中打开路径为上述 `Prototype1` 的项目，使用 `6000.3.21f1`。等待右下角导入/编译完成。
2. 在 Mac 顶部的 Unity 菜单栏选择 `HideAR > Quest > 1. Open Quest Scene`。
3. 选择 `HideAR > Quest > 2. Configure Android for Quest`。
4. 选择 `HideAR > Quest > 3. Switch to Android`。第一次切换会重新导入资源，请等编译结束。
5. 确认 Quest 已通过 USB 连接，并且你已经在头显里允许 USB 调试。
6. 选择 `HideAR > Quest > 5. Build and Run on Connected Quest`。
7. 等待 Unity 完成构建和安装。这个菜单只打包 `HideAR_IP2_Quest`，打开 App 会直接进入游戏。无需自己从几十个官方 Sample 场景中逐一取消勾选。

安装包的固定输出位置：

`/Users/lingjieruan/Desktop/XR-LingjieRuan/Builds/Quest/HideAR_IP2_Quest.apk`

只想生成 APK、不立即安装时，选择 `HideAR > Quest > 4. Build APK`。

如果有多个 Android/Quest 设备同时连接，先只保留这次要测试的那一个。构建失败时，在 Console 中看第一条红色错误；不要连续重复 Build。

请使用上述 `HideAR > Quest` 菜单，不要直接用默认 Build Profiles 的 Build 按钮。默认的共享场景列表仍为原来的 iPhone 版本保留。

## 在 Quest 3S 上玩一轮

1. 第一次打开，允许 Spatial Data / 空间数据权限。游戏保持现实透视画面。
2. 用手柄射线指向按钮，按食指扳机点击 `Start hiding`。
3. 有效桌面/地面会显示绿色边界。没有绿色区域时，选择 `Set up room`，按头显提示完成房间设置，返回游戏。
4. 用手柄指向标出的平面，按一下扳机。一次只允许一个恐龙；等待保存位置完成。
5. 在 `Happy with this spot?` 页面检查放置位置。选择 `Move dinosaur` 可重新放置，选择 `Confirm hiding` 才进入交接阶段。
6. 恐龙此时隐藏。把同一台头显和手柄交给寻找者，App 保持运行。寻找者戴好后选择 `I'm ready`。
7. 倒数 3、2、1，开始寻找。用手柄射线指向恐龙，按一下食指扳机。
8. 出现 `You found it!` 和寻找用时。`Play again` 开始下一轮，交换藏匿/寻找角色。

左手或右手柄都可以操作。按左手 X 或右手 A，可把游戏菜单移到当前面前。菜单保持在空间中，不会一直遮住视线。`Reset round` 清除本轮并回到开场。

摘下头显、应用失去焦点或空间跟踪丢失时，寻找计时暂停。交接期间保留现实透视，恐龙和放置平面隐藏。

## 带到学校的 Quest 3

使用同一个 APK，在 Quest 3 上允许 USB 调试并安装。Quest 3 必须在教室里重新完成 Space Setup，并授予应用空间数据权限。

两位玩家轮流使用同一台头显。该版本没有两台头显共享位置，也不保证退出 App 或重启后还能恢复上一轮恐龙位置。家中的 Quest 3S 房间数据不会转移到学校 Quest 3。

## 首轮真机验收

2026-10-01 已完成：16 项 Unity Edit Mode 自动测试全部通过，4 个主要界面完成编辑器渲染检查；Android APK 已成功打包并通过签名检查，已核对 ARM64、Quest 3/3S 设备声明和空间数据权限。它们不是头显实测，也不是用户测试数据。

以下是待执行检查，不代表已经在设备上通过：

- 连续按 `Start hiding`、`Move dinosaur`、`Confirm hiding`、`Reset round`，确认没有按钮误放恐龙。
- 同时按左右扳机，确认只出现一个恐龙。
- 放置后移动、转头再看回来，确认恐龙仍在同一桌面点附近。
- 放置后等待，确认不会自动进入寻找；必须先确认，再由寻找者点准备。
- 实际摘戴交接，检查位置是否恢复、菜单是否能看见、倒数是否正常。
- 点击恐龙后出现成功界面，用时停止；找不到时 Reset 可重新开始。
- 拒绝一次空间权限，检查提示；在系统设置允许后重新进入，能恢复平面显示。
- 在 Quest 3S、Quest 3 分别检查透视、左右手柄、文字清晰度和点击反馈。

当前版本没有开启深度遮挡，恐龙可能透过真实家具显示；这与“恐龙固定在现实位置”是两项不同的功能。

## 常见情况

| 情况 | 处理 |
| --- | --- |
| 只有安全边界，没有绿色平面 | 完成 Space Setup，并包含桌面/地面；允许空间数据权限 |
| 没看到菜单 | 戴好头显，按 X 或 A 把菜单移到面前 |
| 扳机刚松开不能立即放置 | 切换阶段后需要松开扳机约 0.35 秒，避免同一次按键误放 |
| 显示 Tracking paused | 戴好头显、回到 App，在有光线的房间环顾四周，等待定位恢复 |
| 显示保存失败 | 点 Restart game，重新开始；不会把迟到的 Anchor 当成下一轮物品 |
| Unity 提示磁盘空间不足 | 腾出空间后重试；保留工程 Assets、Packages、ProjectSettings |
| 点 Unity Play 没有真实房间 | 编辑器预览不能替代头显真机的房间设置和透视测试 |

## 官方实现来源

- [AR Foundation Samples 6.3](https://github.com/Unity-Technologies/arfoundation-samples/tree/6.3)
- [Meta OpenXR 2.3 项目配置](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.3/manual/get-started/project-settings.html)
- [Meta OpenXR 2.3 平面与 Space Setup](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.3/manual/features/planes.html)

官方 `ARPlaceAnchor.cs`、`RaycastEventController.cs`、`ARAnchorDebugVisualizer.cs`、`AnchorAddRemoveLogger.cs` 没有修改。Quest 使用官方的世界空间 Raycast；官方 Anchor 脚本会根据平台能力选择异步 Anchor 创建。
