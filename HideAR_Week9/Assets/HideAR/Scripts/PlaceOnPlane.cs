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
    // 职责：改编 Week 09 的手柄射线到平面命中流程，加入预览、单物品和找到判定。
    // Role: adapt Week 09 controller-ray/plane hits with previews, one object and find detection.
    // 平面与 Anchor 定位由 Unity/Meta 提供；项目只决定何时请求和显示哪个模型。
    // Unity/Meta provide planes and anchor tracking; this project chooses when and what to place.
    public sealed class PlaceOnPlane : MonoBehaviour
    {
        public ARRaycastManager raycastManager;
        public ARAnchorManager anchorManager;
        public XRRayInteractor controllerRay;
        public InputActionReference placeAction;
        public GameObject prefabToPlace;
        public GameObject[] placeablePrefabs;
        public string[] placeableNames;
        public Texture2D[] colorTextures;
        public GameObject marker;
        public GameFlow game;
        public ARAnchor Anchor { get; private set; }
        // 保留历史名称 Dinosaur；此引用实际上指向当前选择的任意已放置物品。
        // Dinosaur is a legacy name for the placed object, including the other model choices.
        public GameObject Dinosaur { get; private set; }
        public bool Busy { get; private set; }
        public int SelectedIndex { get; private set; }
        public int ColorIndex { get; private set; }
        public static readonly string[] ColorNames = { "Original", "Moss", "Blue", "Gray", "White" };
        public static readonly Color[] Colors =
        {
            new Color(0.9f, 0.75f, 0.6f), new Color(0.32f, 0.48f, 0.27f),
            new Color(0.3f, 0.65f, 0.95f), new Color(0.45f, 0.46f, 0.48f), new Color(0.94f, 0.94f, 0.92f)
        };
        public string SelectedColorName => ColorNames[ColorIndex];
        readonly List<ARRaycastHit> hits = new();
        float startedAt;
        GameObject surfacePreview;
        int previewIndex = -1;

        public bool Available => anchorManager.subsystem != null && anchorManager.subsystem.running;
        public bool Tracking => Anchor != null && Anchor.trackingState == TrackingState.Tracking;
        public bool HasChoices => placeablePrefabs != null && placeablePrefabs.Length > 1;
        public string SelectedName => placeableNames != null && SelectedIndex < placeableNames.Length && !string.IsNullOrEmpty(placeableNames[SelectedIndex])
            ? placeableNames[SelectedIndex] : "Object";
        public GameObject SelectedPrefab => placeablePrefabs != null && SelectedIndex < placeablePrefabs.Length && placeablePrefabs[SelectedIndex] != null
            ? placeablePrefabs[SelectedIndex] : prefabToPlace;

        public void SelectNext()
        {
            if (!HasChoices || Busy || Anchor != null) return;
            SelectedIndex = (SelectedIndex + 1) % placeablePrefabs.Length;
        }

        public void SelectColor(int index)
        {
            if (Busy || index < 0 || index >= Colors.Length) return;
            ColorIndex = index;
            if (Dinosaur != null) ApplyColor(Dinosaur);
            if (surfacePreview != null) ApplyColor(surfacePreview);
        }

        // 属性块只覆盖当前实例，不修改共享材质或下载的原模型资源。
        // Property blocks override this instance without editing shared materials or source art.
        public void ApplyColor(GameObject obj)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    // 保留深色眼睛/嘴部；选 Original 时清除实例覆盖并恢复原色。
                    // Preserve dark eyes/mouth; Original clears instance overrides.
                    var material = materials[i];
                    bool detail = material != null && material.HasProperty("_BaseColor") && material.GetColor("_BaseColor").maxColorComponent < 0.2f;
                    properties.Clear();
                    if (ColorIndex != 0)
                    {
                        // 贴图模型使用预制调色贴图；纯色模型直接改 BaseColor。
                        // Textured pets use prepared palette textures; plain models use BaseColor.
                        bool textured = material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
                        properties.SetColor("_BaseColor", textured ? Color.white : Colors[ColorIndex]);
                        if (textured) properties.SetTexture("_BaseMap", colorTextures[ColorIndex]);
                    }
                    renderer.SetPropertyBlock(ColorIndex == 0 || detail ? null : properties, i);
                }
            }
        }

        // 预览也是模型副本，但关闭碰撞盒，避免挡住 UI/放置/找到的射线。
        // A preview is a model copy with disabled colliders so it cannot block interaction rays.
        public GameObject CreatePreview(Transform parent = null)
        {
            var preview = Instantiate(SelectedPrefab, parent);
            preview.name = SelectedName + " Preview";
            if (!Application.isPlaying) preview.hideFlags = HideFlags.DontSaveInEditor;
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            ApplyColor(preview);
            return preview;
        }

        void Update()
        {
            // 每帧先隐藏临时提示，只有本帧有效的放置命中才再次显示，寻找时不留线索。
            // Hide temporary hints first; only a valid placement hit shows them again this frame.
            marker.SetActive(false);
            if (surfacePreview != null) surfacePreview.SetActive(false);
            if (Busy && game.Round.Stage == Stage.Saving && Time.unscaledTime - startedAt > 12)
                game.Fail("The hiding spot could not be saved. Restart and try again.");
            if (!game.Tracked || !game.room.Granted || game.room.SettingUpRoom || Busy) return;
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!hand.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return;
            var origin = controllerRay.rayOriginTransform;
            var ray = new Ray(origin.position, origin.forward);
            // 菜单整块碰撞盒（含按钮空隙）优先拦截，防止点 UI 同时放置/找到。
            // Block the whole UI card, including gaps, before processing placement/finding.
            if (Physics.Raycast(ray, 20, 1 << 5, QueryTriggerInteraction.Collide)) return;
            if (controllerRay.TryGetCurrentUIRaycastResult(out _)) return;
            bool pressed = placeAction.action.WasPressedThisFrame() && game.Round.Armed;
            // 找到用 Unity 物理射线检查物品碰撞盒，不是距离接近或图像识别。
            // Finding uses a Unity physics ray against the object's collider, not proximity or vision.
            if (game.Round.Stage == Stage.Seeking && pressed && Dinosaur != null)
            {
                if (Physics.Raycast(ray, out var hit, 20, ~0, QueryTriggerInteraction.Ignore) &&
                    (hit.transform == Dinosaur.transform || hit.transform.IsChildOf(Dinosaur.transform)))
                    game.Round.Find();
            }
            if (game.Round.Stage != Stage.Placing || !Available) return;
            // AR 射线查询真实房间的已知平面，只接受多边形内、水平向上的命中。
            // AR raycasts query known room planes; accept only in-polygon upward horizontal hits.
            if (!raycastManager.Raycast(ray, hits, TrackableType.PlaneWithinPolygon)) return;
            if (hits[0].trackable is not ARPlane plane || plane.alignment != PlaneAlignment.HorizontalUp) return;
            var pose = hits[0].pose;
            marker.transform.SetPositionAndRotation(pose.position + Vector3.up * 0.005f, pose.rotation);
            marker.SetActive(true);
            if (previewIndex != SelectedIndex || surfacePreview == null)
            {
                if (surfacePreview != null) Destroy(surfacePreview);
                surfacePreview = CreatePreview();
                previewIndex = SelectedIndex;
            }
            surfacePreview.transform.SetPositionAndRotation(pose.position, pose.rotation);
            surfacePreview.SetActive(true);
            if (pressed && game.Round.BeginPlacement()) Place(pose);
        }

        // 异步请求期间 Busy 阻止重复操作；RoundState 的 Saving 阶段提供另一层保护。
        // Busy blocks duplicate work during the async request; the Saving stage also guards it.
        async void Place(Pose pose)
        {
            Busy = true;
            startedAt = Time.unscaledTime;
            var manager = anchorManager;
            try
            {
                // 本项目使用 Quest 支持的官方 Pose 锚点接口，不把锚点附着到 ARPlane。
                // Use the official pose-based anchor API supported by Quest, not plane attachment.
                var result = await manager.TryAddAnchorAsync(pose);
                // 超时/退出后可能才收到结果；清理迟到的锚点，不能把错误阶段变回检查页。
                // An async result may arrive after timeout/exit; remove it instead of reviving review.
                if (this == null || game.Round.Stage != Stage.Saving)
                {
                    if (manager != null && result.status.IsSuccess()) manager.TryRemoveAnchor(result.value);
                    return;
                }
                if (!result.status.IsSuccess()) { game.Fail("Could not save this spot. Restart and try again."); return; }
                Anchor = result.value;
                // 物品是世界空间 Anchor 的子对象，不是 Camera 的子对象，不逐帧追随头部。
                // Parent the object to the world-space anchor, not the camera or a head-follow loop.
                Dinosaur = Instantiate(SelectedPrefab, Anchor.transform);
                Dinosaur.transform.localPosition = Vector3.zero;
                Dinosaur.transform.localRotation = Quaternion.identity;
                ApplyColor(Dinosaur);
                game.Round.PlacementFinished();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (this != null) game.Fail("Placement failed. Restart the game.");
            }
            finally { if (this != null) Busy = false; }
        }

        // 成功清掉旧锚点和物品后才能重新放置；删除失败时不偷偷再创建一个。
        // Require successful anchor/object cleanup before replacing it; do not duplicate on failure.
        public bool Clear()
        {
            if (Busy) return false;
            if (Anchor != null && !anchorManager.TryRemoveAnchor(Anchor))
            {
                game.Fail("Could not clear this spot. Restart the game.");
                return false;
            }
            if (Dinosaur != null) Destroy(Dinosaur);
            Dinosaur = null;
            Anchor = null;
            return true;
        }

        void OnDestroy() { if (surfacePreview != null) Destroy(surfacePreview); }
    }
}
