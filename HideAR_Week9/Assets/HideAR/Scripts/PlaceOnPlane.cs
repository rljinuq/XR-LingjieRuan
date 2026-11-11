// PlaceOnPlane.cs：处理模型/颜色选择、手柄射线、临时预览、Anchor 和正式放置物。
// ARRaycastManager 检测 Quest 提供的现实平面；Physics.Raycast 检测 Unity 模型 Collider。
// GameFlow 管阶段；本类执行放置和命中；GameUI 自己生成菜单里的展示副本。
// Uses Unity/Meta XR room data; it does not implement room scanning. See README for SDK references.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace HideAR.Classroom
{
    public sealed class PlaceOnPlane : MonoBehaviour
    {
        // ===== Inspector 引用：XR 组件、输入和模型资源 =====
        public ARRaycastManager raycastManager; // AR Foundation：取得手柄射线打到现实平面的位置/方向。
        public ARAnchorManager anchorManager; // AR Foundation：请求创建或移除本次应用会话的 Anchor。
        public XRRayInteractor controllerRay; // 右手柄射线来源，也提供当前是否命中 UI 的信息。
        public InputActionReference placeAction; // Unity Input System 中绑定到右 Trigger 的输入动作。
        public GameObject prefabToPlace; // 默认/备用模型模板；Instantiate 后才是场景里的副本。
        public GameObject[] placeablePrefabs; // 可选模型 Prefab 数组；SelectedIndex 对应其中一个。
        public string[] placeableNames; // 与模型数组同编号的界面名称。
        public Texture2D[] colorTextures; // 彩色宠物模型换色用贴图；编号 0 表示原始贴图。
        public GameObject marker; // 平面命中处的小提示标记，不是实际模型或 Anchor。
        public GameFlow game; // 提供追踪/阶段状态，并接收放置和找到结果。

        // ===== 正式物品、当前选择与保存状态 =====
        public ARAnchor Anchor { get; private set; } // 正式模型所在的空间参考；不是模型，也不会跨应用重启永久保存。
        public GameObject Dinosaur { get; private set; } // 已正式放置的模型实例（也可为宠物）；不是任何预览副本。
        public bool Busy { get; private set; } // Anchor 请求尚未返回；此时不允许再放置、换模型或清理。
        public int SelectedIndex { get; private set; } // 当前选中的模型编号，对应 placeablePrefabs 的位置。
        public int ColorIndex { get; private set; } // 当前选中的颜色编号；0 表示恢复模型原本外观。
        // ColorNames、Colors 和 colorTextures 按编号对应；readonly 只防止替换数组本身。
        public static readonly string[] ColorNames =
        {
            "Original", "Moss", "Blue", "Gray", "White", "Red", "Yellow", "Orange", "Purple", "Pink", "Teal", "Black"
        };
        public static readonly Color[] Colors =
        {
            new Color(0.9f, 0.75f, 0.6f), new Color(0.32f, 0.48f, 0.27f),
            new Color(0.3f, 0.65f, 0.95f), new Color(0.45f, 0.46f, 0.48f), new Color(0.94f, 0.94f, 0.92f),
            new Color(0.85f, 0.22f, 0.2f), new Color(0.98f, 0.82f, 0.18f), new Color(0.97f, 0.48f, 0.12f),
            new Color(0.6f, 0.38f, 0.82f), new Color(0.96f, 0.48f, 0.67f), new Color(0.12f, 0.68f, 0.62f),
            new Color(0.1f, 0.11f, 0.13f)
        };
        // Colors 中的 RGB 数值用 0 到 1 表示；ColorIndex=0 会清除覆盖并恢复资源原样。
        public string SelectedColorName => ColorNames[ColorIndex]; // 供 GameUI 显示当前颜色名称。
        readonly List<ARRaycastHit> hits = new(); // 复用的 AR Raycast 命中列表，避免每帧重新创建。
        float startedAt; // 发起 Anchor 请求的时间，用来计算 12 秒超时。
        GameObject surfacePreview; // 地面/桌面上的临时模型副本；没有 Anchor，也不能作为找到目标。
        int previewIndex = -1; // 当前平面预览对应的模型编号；-1 表示尚未创建。

        // ===== 供 GameFlow/UI 查询的当前选择和 Anchor 状态 =====
        public bool Available => anchorManager.subsystem != null && anchorManager.subsystem.running; // Anchor 子系统存在且运行，才可发送创建请求。
        public bool Tracking => Anchor != null && Anchor.trackingState == TrackingState.Tracking; // 这个已创建的 Anchor 此刻正在被追踪。
        public bool HasChoices => placeablePrefabs != null && placeablePrefabs.Length > 1;
        public string SelectedName => placeableNames != null && SelectedIndex < placeableNames.Length && !string.IsNullOrEmpty(placeableNames[SelectedIndex])
            ? placeableNames[SelectedIndex] : "Object";
        public GameObject SelectedPrefab => placeablePrefabs != null && SelectedIndex < placeablePrefabs.Length && placeablePrefabs[SelectedIndex] != null
            ? placeablePrefabs[SelectedIndex] : prefabToPlace;
        // Available 只问“能不能创建 Anchor”；Tracking 问“已有 Anchor 还在不在正确位置”。
        // SelectedName/SelectedPrefab 的 ?: 会在数组缺项时选备用值；读取它们不会创建模型。

        // ===== 藏匿前循环切换模型 =====
        // Selects the next prefab; it never replaces an already placed object.
        public void SelectNext()
        {
            if (!HasChoices || Busy || Anchor != null) return;
            // 三种情况都会拒绝切换：只有一个模型、正在等保存，或已经有正式 Anchor。
            // 这样不会让已放置的模型和当前选择编号不一致。
            SelectedIndex = (SelectedIndex + 1) % placeablePrefabs.Length;
            // % 是取余：最后一个编号之后回到 0，循环选择数组里的 Prefab。
        }

        // ===== 选颜色：更新正式模型和平面预览 =====
        // Updates visible instances only; GameUI updates its separate menu preview.
        public void SelectColor(int index)
        {
            if (Busy || index < 0 || index >= Colors.Length) return;
            // 保存中不允许改选择；小于 0 或超过数组长度的编号也会被拒绝，避免访问不存在的颜色。
            ColorIndex = index;
            // 先存编号，后续创建的正式模型/预览才能沿用这次选择。
            if (Dinosaur != null) ApplyColor(Dinosaur);
            // 已放置物存在才改它的显示；不会因此创建、移动或重新保存 Anchor。
            if (surfacePreview != null) ApplyColor(surfacePreview);
            // 同步换平面上的临时预览；菜单里的预览由 GameUI 单独更新。
        }

        // ===== 给单个模型实例换色，不改 Prefab 共用材质 =====
        // MaterialPropertyBlock changes this copy, not the shared material asset.
        public void ApplyColor(GameObject obj)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    // 一些 Prefab 把眼睛/鼻子做成独立深色材质；这些槽保留原色，不跟身体一起变色。
                    bool detail = material != null && material.HasProperty("_BaseColor") && material.GetColor("_BaseColor").maxColorComponent < 0.2f;
                    properties.Clear();
                    if (ColorIndex != 0)
                    {
                        // 颜色 0 表示 Original，所以不写覆盖属性；下面清空覆盖后恢复模型原材质。
                        bool textured = material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
                        // 有 BaseMap 的宠物使用彩色贴图；纯色模型直接改 _BaseColor。
                        properties.SetColor("_BaseColor", textured ? Color.white : Colors[ColorIndex]);
                        // 贴图模型的底色保持白色，避免额外 RGB 乘色；纯色模型用所选 RGB。
                        if (textured) properties.SetTexture("_BaseMap", colorTextures[ColorIndex]);
                        // 只有原材质本来带贴图才替换贴图；模型自己的细节纹理得以保留。
                    }
                    renderer.SetPropertyBlock(ColorIndex == 0 || detail ? null : properties, i);
                    // Original 或深色细节槽传 null 清除覆盖；其他材质槽只改变这个 Renderer 的实例外观。
                }
            }
            // Renderer 是负责绘制模型的组件；一个模型可有多个子 Renderer 和材质槽，所以逐个处理。
            // ApplyColor 改的是正在显示的实例，不会写回 Prefab 或 Project 里的共享材质。
        }

        // ===== 创建菜单/放置用的临时模型副本 =====
        // Preview copies are visual only; they are not anchored and cannot be found by the seeker.
        public GameObject CreatePreview(Transform parent = null)
        {
            var preview = Instantiate(SelectedPrefab, parent);
            preview.name = SelectedName + " Preview";
            if (!Application.isPlaying) preview.hideFlags = HideFlags.DontSaveInEditor;
            // 编辑器检查时不把临时副本保存进 Scene。
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            // 关闭副本的 Collider，避免射线把预览误认为真实藏匿物或挡住 UI。
            ApplyColor(preview);
            return preview;
        }
        // Instantiate 用选择的 Prefab 复制一个 GameObject；parent 决定它归菜单还是放置预览管理。
        // GameUI 可以缩放自己的菜单副本；这不会改变 Prefab 源文件或 Anchor 下的正式物品。

        // ===== 每帧检查手柄射线：先挡 UI，再判断找物或放置 =====
        // Update runs after GameFlow has refreshed the current tracking state.
        void Update()
        {
            marker.SetActive(false);
            // 每帧先关掉上一帧标记；后面只有本帧命中合格平面才重新显示。
            if (surfacePreview != null) surfacePreview.SetActive(false);
            // 暂时隐藏而不销毁；命中有效平面时可重复使用这个预览对象。
            if (Busy && game.Round.Stage == Stage.Saving && Time.unscaledTime - startedAt > 12)
                game.Fail("The hiding spot could not be saved. Restart and try again.");
            // Busy 且仍在 Saving 超过 12 秒才进入错误页；这个超时不会取消 Unity 的异步 Anchor 请求。
            if (!game.Tracked || !game.room.Granted || game.room.SettingUpRoom || Busy) return;
            // 以上任一项成立就退出本帧：无追踪、没权限、Quest 设置中或 Anchor 请求未结束时都不使用射线。
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!hand.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return;
            // || 表示任一条件成立就退出：读不到右手柄状态，或手柄当前未追踪，都不能拿旧射线放置。
            // The headset can be tracked while the right controller is not.

            // ===== 构造右手柄射线；如果它打到 UI 就不做游戏射线 =====
            var origin = controllerRay.rayOriginTransform;
            var ray = new Ray(origin.position, origin.forward);
            if (Physics.Raycast(ray, 20, 1 << 5, QueryTriggerInteraction.Collide)) return;
            // 1 << 5 是 UI Layer 的遮罩；命中卡片的 Collider 就退出，空白区域也不会透过去放模型。
            if (controllerRay.TryGetCurrentUIRaycastResult(out _)) return;
            // XR UI 射线如果已指向按钮就退出，避免一次 Trigger 同时点击按钮和放置模型。
            bool pressed = placeAction.action.WasPressedThisFrame() && game.Round.Armed;
            // WasPressedThisFrame 只在刚按下的这一帧为 true；Armed 还要求前面先松开 Trigger。
            // A UI hit takes priority. One trigger press can perform only one action.

            // ===== Seeking 阶段：物理射线命中正式模型才算找到 =====
            if (game.Round.Stage == Stage.Seeking && pressed && Dinosaur != null)
            {
                // 三项都要成立：正在寻找、这是一次新按压、正式模型引用存在。
                if (Physics.Raycast(ray, out var hit, 20, ~0, QueryTriggerInteraction.Ignore) &&
                    (hit.transform == Dinosaur.transform || hit.transform.IsChildOf(Dinosaur.transform)))
                    game.Round.Find();
                // ~0 表示检查所有物理 Layer，Ignore 排除 Trigger Collider；命中必须属于正式模型或其子物体。
                // 命中墙、家具、菜单或临时预览都不会调用 Find；这里只依据 Unity Collider，不做现实物体识别。
            }

            // ===== Placing 阶段：AR Raycast 只选 Quest 提供的向上水平面 =====
            if (game.Round.Stage != Stage.Placing || !Available) return;
            // 非 Placing 阶段，或 ARAnchor 子系统未运行时，不查找可放置位置。
            if (!raycastManager.Raycast(ray, hits, TrackableType.PlaneWithinPolygon)) return;
            // 没打到 AR 平面多边形范围就退出；旧 marker/预览已在本帧开头隐藏。
            if (hits[0].trackable is not ARPlane plane || plane.alignment != PlaneAlignment.HorizontalUp) return;
            // is not 表示它不是 ARPlane 时退出；alignment 不是 HorizontalUp 也退出，所以墙面不支持。
            var pose = hits[0].pose;
            // pose 是 Quest 平面命中的位置和旋转；后续 marker、预览和 Anchor 都使用同一个姿态。
            // ARRaycastManager 查 AR Foundation 平面；它和下面找模型时用的 Physics.Raycast 不是同一种检测。

            // ===== 显示命中位置和临时模型；确认后才请求 Anchor =====
            marker.transform.SetPositionAndRotation(pose.position + Vector3.up * 0.005f, pose.rotation);
            marker.SetActive(true);
            // Marker 抬高 5 毫米以减少与平面重叠；它只是指示点，不是正式物品。
            if (previewIndex != SelectedIndex || surfacePreview == null)
            {
                // 首次预览，或选了不同模型时，先销毁旧副本，避免显示过时的模型。
                if (surfacePreview != null) Destroy(surfacePreview);
                surfacePreview = CreatePreview();
                previewIndex = SelectedIndex;
            }
            surfacePreview.transform.SetPositionAndRotation(pose.position, pose.rotation);
            surfacePreview.SetActive(true);
            if (pressed && game.Round.BeginPlacement()) Place(pose);
            // 新按下且 RoundState 接受 Placing->Saving 才调用 Place；否则只保留可见预览，不创建 Anchor。
            // The surface preview shows the candidate. Place creates the real anchor only after confirmation.
        }

        // ===== 等待 ARAnchorManager 创建 Anchor，再生成正式模型 =====
        // async/await waits for the XR system without freezing the whole Unity frame.
        async void Place(Pose pose)
        {
            Busy = true;
            startedAt = Time.unscaledTime;
            // Busy 防止等待期间再次放置/切模型；startedAt 供 Update 检查 12 秒超时。
            var manager = anchorManager;
            // 保存发起请求时的 Manager 引用，以便迟到的成功结果尝试移除 Anchor。
            try
            {
                var result = await manager.TryAddAnchorAsync(pose);
                // Meta/AR Foundation 按 Pose 请求当前会话 Anchor；它不附着到 ARPlane，也不是永久空间存档。
                // 等待结果期间 Unity 仍可运行其他帧，Busy 保持 true。

                // ===== 丢弃过期结果；失败时进入 Error =====
                if (this == null || game.Round.Stage != Stage.Saving)
                {
                    // this==null 表示组件已销毁；不在 Saving 表示游戏已离开保存阶段，这个结果不再属于当前操作。
                    if (manager != null && result.status.IsSuccess()) manager.TryRemoveAnchor(result.value);
                    // 迟到请求若已成功，就尝试移除它新建的 Anchor，免得留下无人管理的空间点。
                    return;
                }
                if (!result.status.IsSuccess()) { game.Fail("Could not save this spot. Restart and try again."); return; }
                // SDK 回报创建失败时进入 Error 并退出；不会在没有 Anchor 的情况下生成模型。

                // ===== 把正式模型放到 Anchor 下，通知 RoundState =====
                Anchor = result.value;
                Dinosaur = Instantiate(SelectedPrefab, Anchor.transform);
                Dinosaur.transform.localPosition = Vector3.zero;
                Dinosaur.transform.localRotation = Quaternion.identity;
                ApplyColor(Dinosaur);
                game.Round.PlacementFinished();
                // 模型成为 Anchor 的子对象，局部位置/旋转归零，所以跟随空间 Anchor 而非头显相机。
                // ApplyColor 改正式模型实例；PlacementFinished 只把阶段推进到 Review。
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (this != null) game.Fail("Placement failed. Restart the game.");
                // 若脚本对象仍存在，通知 GameFlow 显示 Error；已销毁时不再访问它。
            }
            finally { if (this != null) Busy = false; }
            // finally 不论成功、失败或 return 都会执行；组件还在时解除 Busy，允许后续清理。
        }

        // ===== 重新放置/再玩前移除旧 Anchor 和正式模型 =====
        // Returns false when cleanup is unsafe, so GameFlow must not start a new placement.
        public bool Clear()
        {
            if (Busy) return false;
            // Anchor 请求尚未完成时不清理，防止旧请求稍后又创建出一个模型/Anchor。
            if (Anchor != null && !anchorManager.TryRemoveAnchor(Anchor))
            {
                game.Fail("Could not clear this spot. Restart the game.");
                return false;
                // Anchor 移除失败就保留原引用并拒绝继续；GameFlow 因 false 不会开始新一轮。
            }
            if (Dinosaur != null) Destroy(Dinosaur);
            Dinosaur = null;
            Anchor = null;
            return true;
        }
        // Destroy 是删除正式模型；把引用设为 null 表示当前没有可用物品/Anchor。
        // 这不同于 SetActive(false)：后者只是隐藏，Clear 会结束旧物品的使用。

        // ===== 场景卸载时清理独立的平面预览 =====
        void OnDestroy() { if (surfacePreview != null) Destroy(surfacePreview); }
        // surfacePreview 不属于 Anchor，所以销毁本组件时要单独删除；菜单预览由 GameUI 清理。
    }
}
