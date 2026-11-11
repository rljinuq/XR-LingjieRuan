// ClassroomPreview.cs：在 Unity Editor 用普通相机模拟若干 Stage，并把任务卡截图保存为 PNG。
// 检查卡片边界、Text 高度和截图是否为空；不运行 Quest 权限、Tracking 或真实 Anchor。
// The output is a visual code check, not a real headset or participant test.
using System;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HideAR.Classroom.Editor
{
    public static class ClassroomPreview
    {
        const string Output = "../Builds/Week9/Review/ReadyFollow"; // 模拟截图保存位置，不是 APK 输出目录。

        // ===== 加载课堂 Scene，用模拟设备状态渲染各阶段画面 =====
        // Creates a temporary camera, changes in-memory state, captures screenshots, then reloads the saved scene.
        public static void Render()
        {
            // ===== 准备 Editor 截图环境，不启动 XR 设备 =====
            EditorSceneManager.OpenScene(ClassroomSetup.ScenePath);
            var game = Object.FindAnyObjectByType<GameFlow>();
            var ui = game.ui;
            var camera = new GameObject("Preview Camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.6f, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.22f, 0.21f);
            camera.fieldOfView = 34;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            ui.head = camera;
            ui.GetComponent<Canvas>().worldCamera = camera;
            ui.Recenter();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root != ui.gameObject && root != camera.gameObject && root.name != "Object Light") root.SetActive(false);
            // 截图时只保留卡片、临时相机和物品灯光；其他 XR/房间对象暂时隐藏。
            typeof(GameFlow).GetProperty(nameof(GameFlow.Tracked)).SetValue(game, true);
            // 反射把 Tracked 临时设为 true，让 UI 显示正常操作画面；这不是头显实际报告的追踪结果。
            var texture = new RenderTexture(1400, 1000, 24);
            texture.Create(); camera.targetTexture = texture;
            Directory.CreateDirectory(Output);
            // RenderTexture 是不显示在屏幕上的截图画布；这些更改只在当前 Editor 内存中生效。
            // The preview uses simulated tracking, not a Quest session.
            try
            {
                // ===== 检查 Welcome 权限提示，以及房间/换模型是否为两个按钮 =====
                Capture(game, camera, texture, "01-welcome");
                if (!ui.roomSetup.gameObject.activeSelf) throw new Exception("Room setup must remain available at welcome.");
                // Welcome 时房间设置按钮必须可见，不能被 Secondary 的 Change object 取代。
                typeof(RoomAccess).GetProperty(nameof(RoomAccess.Granted)).SetValue(game.room, true);
                // 只模拟权限已授予以检查 UI；不会弹 Android/Quest 权限窗口。
                Capture(game, camera, texture, "01b-room-authorized");
                if (!ui.roomSetup.gameObject.activeSelf || ui.secondary.GetComponentInChildren<Text>().text != "Change object")
                    throw new Exception("Room setup and object choices must remain separate.");
                // || 表示任一项出错就失败：房间设置按钮须显示，次按钮文字须仍为 Change object。

                // ===== 模拟头部移动：验证菜单定位、固定和房间返回召回 =====
                camera.transform.SetPositionAndRotation(new Vector3(2, 1.1f, 3), Quaternion.Euler(0, 30, 0));
                ui.RequestRecenter(); UpdateMenu(ui);
                Capture(game, camera, texture, "07-menu-seated");
                var pinnedPosition = ui.transform.position;
                var pinnedRotation = ui.transform.rotation;
                camera.transform.position += camera.transform.right * 0.12f;
                UpdateMenu(ui);
                Capture(game, camera, texture, "08-menu-pinned");
                if (ui.transform.position != pinnedPosition || ui.transform.rotation != pinnedRotation)
                    throw new Exception("The menu must stay pinned after releasing repositioning.");
                // 相机移动但没有新定位请求，卡片位置和旋转都应保持之前的值。
                camera.transform.SetPositionAndRotation(new Vector3(3, 1.6f, -2), Quaternion.Euler(0, 90, 0));
                typeof(RoomAccess).GetProperty(nameof(RoomAccess.RefreshCount)).SetValue(game.room, 1);
                typeof(GameFlow).GetMethod("UpdateMenuPosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true, false });
                UpdateMenu(ui);
                Capture(game, camera, texture, "09-room-return");
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
                ui.Recenter();
                // 这里改变的是模拟相机姿态；RefreshCount 改变后检查 GameFlow 是否召回卡片。
                // Camera poses are simulated; no Quest tracking is running.

                // ===== 逐个预览每个模型和颜色组合 =====
                for (int i = 0; i < game.placement.placeablePrefabs.Length; i++)
                {
                    for (int color = 0; color < PlaceOnPlane.Colors.Length; color++)
                    {
                        game.placement.SelectColor(color);
                        Capture(game, camera, texture, "choice-" + game.placement.SelectedName + "-" + PlaceOnPlane.ColorNames[color]);
                        if (!ui.previewRoot.gameObject.activeSelf || ui.previewRoot.childCount != 1)
                            throw new Exception("Every selected model must have exactly one menu preview.");
                        // Hider 选物时预览容器必须显示且只能有一个模型副本，避免旧/新模型叠在一起。
                    }
                    game.placement.SelectNext();
                }
                // i 是模型下标，color 是色块下标；SelectNext 后检查下一个 Prefab。
                // Each object and color is previewed in the Editor.

                // ===== 用 RoundState 模拟 Hider 放置、Review 和 Seeker Handoff =====
                game.placement.SelectColor(0);
                game.Round.Start(); Capture(game, camera, texture, "02-place");
                if (!ui.roomSetup.gameObject.activeSelf) throw new Exception("Room setup must remain available during placement.");
                // Placing 页面也必须显示独立的 Set up room 入口。
                game.Round.UpdateInput(false, 1); game.Round.BeginPlacement(); game.Round.PlacementFinished();
                Capture(game, camera, texture, "03-review");
                game.Round.Confirm(); Capture(game, camera, texture, "04-handoff");
                if (!ui.primary.gameObject.activeSelf || ui.primary.interactable) throw new Exception("Start search must wait for a fresh trigger release.");
                // Handoff 主按钮应该显示，但扳机尚未释放时不能点击。
                game.Round.UpdateInput(false, 1); game.Round.Tick(100, true);
                if (game.Round.Stage != Stage.Handoff) throw new Exception("Search must wait for the start button.");
                // 模拟等待 100 秒仍须停在 Handoff，证明页面不会自己开始倒数。
                Capture(game, camera, texture, "04b-seeker-ready");
                if (!ui.primary.interactable) throw new Exception("Start search must work without taking off the headset.");
                // 松开扳机后无需摘下头显，主按钮应可点击；这仍是 Editor 模拟，不是真机交接测试。
                // PlacementFinished was called directly; this preview does not create a real Anchor.

                // ===== 模拟 Seeker 移动：Ready 卡片跟随且 Start search 保持可用 =====
                camera.transform.SetPositionAndRotation(new Vector3(3, 1.6f, -4), Quaternion.Euler(0, 100, 0));
                typeof(GameFlow).GetMethod("UpdateMenuPosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true, false });
                UpdateMenu(ui); Capture(game, camera, texture, "10-ready-new-start");
                camera.transform.position += camera.transform.right * 0.2f;
                camera.transform.rotation = Quaternion.Euler(0, 110, 0);
                typeof(GameFlow).GetMethod("UpdateMenuPosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true, false });
                UpdateMenu(ui); Capture(game, camera, texture, "11-ready-following");
                var readyPosition = camera.transform.position + camera.transform.forward * 1.5f + Vector3.down * 0.1f;
                if (Vector3.Distance(ui.transform.position, readyPosition) > 0.0001f || !ui.primary.interactable)
                    throw new Exception("Readiness must follow movement without locking Start search.");
                // || 表示任一项失败就报错：卡片位置误差超过 0.1 毫米，或跟随期间按钮变成不可点。
                // 这是用相机坐标检查跟随公式，不是实际佩戴舒适度测试。

                // ===== 明确点击后倒数、隐藏 Hider 线索，再模拟找到物品 =====
                game.Round.Ready(); Capture(game, camera, texture, "04c-countdown");
                game.Round.Tick(3, true); Capture(game, camera, texture, "05-seeking");
                if (ui.previewRoot.gameObject.activeSelf || ui.colorButtons[0].gameObject.activeSelf)
                    throw new Exception("Object choices must not reveal the object during seeking.");
                // 预览或色块任一仍可见就失败，避免 Seeker 在寻找时看到藏匿线索。
                game.Round.Tick(24.6f, true); game.Round.UpdateInput(false, 1); game.Round.Find();
                Capture(game, camera, texture, "06-found");
                // 24.6 秒是测试直接传入的模拟值，不是真人寻找成绩或用户研究数据。
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = null;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
                EditorSceneManager.OpenScene(ClassroomSetup.ScenePath);
            }
            // finally 无论中间成功或抛错都会清理相机/纹理，并重新打开磁盘 Scene，丢弃内存模拟状态。
            // These captures validate simulated UI only, not headset behavior.
        }

        // ===== Helper：在 EditMode 手动调用 GameUI.LateUpdate =====
        static void UpdateMenu(GameUI ui) { typeof(GameUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null); }
        // 反射可按名称访问 private 方法；EditMode 没有正常游戏帧，需手动触发一次卡片定位。

        // ===== Helper：刷新 UI、检查边界/文字，再保存一张截图 =====
        // Throws an error if the card is clipped, text is too tall, or the rendered image is blank.
        static void Capture(GameFlow game, Camera camera, RenderTexture texture, string name)
        {
            game.ui.Refresh(game);
            Canvas.ForceUpdateCanvases();
            // 先让 Unity 完成这一个 Stage 的排版，再读取 RectTransform 和文字所需高度。

            // ===== 检查任务卡四个角都在相机画面内 =====
            var corners = new Vector3[4];
            ((RectTransform)game.ui.transform).GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = camera.WorldToViewportPoint(corner);
                if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1)
                    throw new Exception("Menu outside preview frame: " + name);
                // z<=0 表示角落在相机后方；x/y 不在 0~1 内表示落到画面外，任一角越界就中止这张预览。
            }
            // GetWorldCorners 取 RectTransform 的四角世界坐标；这里检查外框，不检查真实头显视野。

            // ===== 检查文字内容是否高过它自己的容器 =====
            foreach (var text in game.ui.GetComponentsInChildren<Text>())
                if (text.preferredHeight > text.rectTransform.rect.height + 1)
                    throw new Exception("Text overflow: " + text.name + " " + text.text);
                // preferredHeight 是文字排版需要的高度；比容器高 1 像素以上，说明文字可能被截断。
            // 这只检测高度，不会自动检查文字重叠、字号是否舒服或真机中的可读性。

            // ===== 用 URP 渲染到临时纹理，确认非空后写出 PNG =====
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = texture;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32();
            var first = colors[0]; int varied = 0;
            foreach (var color in colors) if (color.r != first.r || color.g != first.g || color.b != first.b) varied++;
            // 和第一像素 RGB 不同就把该像素算作画面内容；first 通常是背景色。
            if (varied < colors.Length / 20) throw new Exception("Blank UI preview: " + name);
            // 变化像素少于总量 1/20（5%）就按空白图处理；只是简单阈值，不是 UI 质量评分。
            File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            Debug.Log("Classroom preview passed: " + name);
            // URP 生成图片；ReadPixels 拷到 CPU，EncodeToPNG 编码后写入文件。
            // 通过只说明模拟图通过外框/文字高度/非空检查，不代表真机空间追踪或使用体验已验证。
        }
    }
}
