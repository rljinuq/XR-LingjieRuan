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
    // 职责：编辑器里模拟回合阶段并截图，检查文字、边界和非空画面。
    // Role: simulate stages in the editor and capture text, boundary and nonblank-image checks.
    // 模拟追踪/用时仅为检查 UI，不是真机测试结果或真实用户实验数据。
    // Simulated tracking/times test UI only; they are not device results or participant data.
    public static class ClassroomPreview
    {
        public static void Render()
        {
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
            typeof(GameFlow).GetProperty(nameof(GameFlow.Tracked)).SetValue(game, true);
            var texture = new RenderTexture(1400, 1000, 24);
            texture.Create(); camera.targetTexture = texture;
            Directory.CreateDirectory("../Builds/Week9/Review");
            try
            {
                Capture(game, camera, texture, "01-welcome");
                if (!ui.roomSetup.gameObject.activeSelf) throw new Exception("Room setup must remain available at welcome.");
                typeof(RoomAccess).GetProperty(nameof(RoomAccess.Granted)).SetValue(game.room, true);
                Capture(game, camera, texture, "01b-room-authorized");
                if (!ui.roomSetup.gameObject.activeSelf || ui.secondary.GetComponentInChildren<Text>().text != "Change object")
                    throw new Exception("Room setup and object choices must remain separate.");
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
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
                ui.Recenter();
                for (int i = 0; i < game.placement.placeablePrefabs.Length; i++)
                {
                    for (int color = 0; color < PlaceOnPlane.Colors.Length; color++)
                    {
                        game.placement.SelectColor(color);
                        Capture(game, camera, texture, "choice-" + game.placement.SelectedName + "-" + PlaceOnPlane.ColorNames[color]);
                    }
                    game.placement.SelectNext();
                }
                game.placement.SelectColor(0);
                game.Round.Start(); Capture(game, camera, texture, "02-place");
                if (!ui.roomSetup.gameObject.activeSelf) throw new Exception("Room setup must remain available during placement.");
                game.Round.UpdateInput(false, 1); game.Round.BeginPlacement(); game.Round.PlacementFinished();
                Capture(game, camera, texture, "03-review");
                game.Round.Confirm(); Capture(game, camera, texture, "04-handoff");
                if (!ui.primary.gameObject.activeSelf || ui.primary.interactable) throw new Exception("Start search must wait for a fresh trigger release.");
                game.Round.UpdateInput(false, 1); game.Round.Tick(100, true);
                if (game.Round.Stage != Stage.Handoff) throw new Exception("Search must wait for the start button.");
                Capture(game, camera, texture, "04b-seeker-ready");
                if (!ui.primary.interactable) throw new Exception("Start search must work without taking off the headset.");
                game.Round.Ready(); Capture(game, camera, texture, "04c-countdown");
                game.Round.Tick(3, true); Capture(game, camera, texture, "05-seeking");
                if (ui.previewRoot.gameObject.activeSelf || ui.colorButtons[0].gameObject.activeSelf)
                    throw new Exception("Object choices must not reveal the object during seeking.");
                game.Round.Tick(24.6f, true); game.Round.UpdateInput(false, 1); game.Round.Find();
                Capture(game, camera, texture, "06-found");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = null;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
                EditorSceneManager.OpenScene(ClassroomSetup.ScenePath);
            }
        }

        // 仅在编辑器检查中手动执行菜单帧更新；游戏运行时由 Unity 自动调用。
        // Invoke the menu update manually only in editor checks; Unity calls it during gameplay.
        static void UpdateMenu(GameUI ui) { typeof(GameUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null); }

        // 超框或空白时抛出错误；通过也不能证明头显上的追踪、手柄或空间权限正常。
        // Fail on overflow/blank output; passing cannot prove headset tracking, input or permissions.
        static void Capture(GameFlow game, Camera camera, RenderTexture texture, string name)
        {
            game.ui.Refresh(game);
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)game.ui.transform).GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = camera.WorldToViewportPoint(corner);
                if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1)
                    throw new Exception("Menu outside preview frame: " + name);
            }
            foreach (var text in game.ui.GetComponentsInChildren<Text>())
                if (text.preferredHeight > text.rectTransform.rect.height + 1)
                    throw new Exception("Text overflow: " + text.name + " " + text.text);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = texture;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32();
            var first = colors[0]; int varied = 0;
            foreach (var color in colors) if (color.r != first.r || color.g != first.g || color.b != first.b) varied++;
            if (varied < colors.Length / 20) throw new Exception("Blank UI preview: " + name);
            File.WriteAllBytes("../Builds/Week9/Review/" + name + ".png", pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            Debug.Log("Classroom preview passed: " + name);
        }
    }
}
