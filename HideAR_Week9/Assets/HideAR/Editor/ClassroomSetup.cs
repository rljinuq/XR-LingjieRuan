// ClassroomSetup.cs：只在 Unity Editor 中运行的课堂项目设置工具，负责生成/更新 Scene、Prefab 和 Android 配置。
// 这里的菜单命令不是头显里的游戏流程；运行时由 Assets/HideAR/Scripts 下的脚本处理。
// Open 打开 Scene；Prepare 只在 Scene 不存在时组装；Apply 命令会写入编辑器资源；BuildApk 只负责打包。
// Editor commands prepare the project. The runtime scripts handle player input and gameplay.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Object = UnityEngine.Object;

namespace HideAR.Classroom.Editor
{
    public static class ClassroomSetup
    {
        // ===== 项目资源路径 =====
        public const string ScenePath = "Assets/HideAR/Scenes/HideAR_Week9.unity"; // 此课堂版运行、测试和打包使用的场景。
        const string Settings = "Assets/HideAR/Settings/"; // Controls、URP、材质和换色贴图保存位置。
        const string Prefabs = "Assets/HideAR/Prefabs/"; // 运行时 Instantiate 使用的已保存模型/平面模板。
        const string CubePetsFbx = "Assets/Art/CandidateModels/Sources/Kenney_CubePets/Models/FBX format/"; // Dog/Cat/Bunny 源 FBX 文件位置。

        // ===== Unity 菜单：打开课堂 Scene =====
        // Opens the saved classroom scene. It does not rebuild an existing scene.
        [MenuItem("HideAR/Classroom/Open Game Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Unity 询问是否保存当前 Scene；点 Cancel 时直接退出，不覆盖用户正在编辑的内容。
            if (!File.Exists(ScenePath)) Prepare();
            // 文件不存在才运行首次组装流程；文件存在则只打开它。
            else EditorSceneManager.OpenScene(ScenePath);
        }

        // ===== Unity 菜单：把普通尺寸/样式保存到当前 Scene =====
        // Applies editor-time card styling, then saves the scene and assets.
        [MenuItem("HideAR/Classroom/Apply Simple UI Style")]
        public static void ApplySimpleUiStyle()
        {
            if (!File.Exists(ScenePath)) Prepare();
            // 没有课堂 Scene 时先创建；存在时打开保存版本再修改。
            else EditorSceneManager.OpenScene(ScenePath);
            var ui = Object.FindAnyObjectByType<GameUI>();
            if (ui == null) throw new Exception("Game UI was not found.");
            // 找不到 GameUI 就报错停止；不能对不存在的卡片样式进行保存。
            ApplyUiStyle(ui);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Applied simple MR task-card UI style.");
        }
        // MarkSceneDirty 通知 Unity Scene 已改变；SaveScene/SaveAssets 才把变化写入项目文件。

        // ===== Unity 菜单：准备模型选项、Prefabs 和材质 =====
        // Creates editor assets; the in-game Change object button does not call this method.
        [MenuItem("HideAR/Classroom/Apply Object Choices")]
        public static void ApplyObjectChoices()
        {
            if (!File.Exists(ScenePath)) Prepare();
            // 确保目标 Scene 已存在并打开，之后才能找到 PlaceOnPlane 组件。
            else EditorSceneManager.OpenScene(ScenePath);
            var placement = Object.FindAnyObjectByType<PlaceOnPlane>();
            if (placement == null) throw new Exception("PlaceOnPlane was not found.");
            // 场景没有放置组件就停止，避免把模型引用写到错误对象。
            var dinosaur = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Dinosaur.prefab") ?? MakeDinosaur();
            // ?? 表示左边为 null 才执行右边：优先复用已有 Dinosaur.prefab，否则创建它。
            placement.prefabToPlace = dinosaur;
            placement.placeablePrefabs = MakePlaceableChoices(dinosaur);
            placement.placeableNames = new[] { "Dinosaur", "Dog", "Cat", "Bunny" };
            ApplyObjectAppearance(placement);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Applied simple object choices.");
        }
        // Prefab 顺序和 placeableNames 名称顺序必须一致，因为运行时按相同编号配对。

        // ===== Unity 菜单：更新选物预览、色块与房间设置按钮 =====
        // Saves the selection controls into the scene; GameUI drives their runtime visibility.
        [MenuItem("HideAR/Classroom/Apply Selection UI")]
        public static void ApplySelectionUi()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindAnyObjectByType<GameFlow>();
            // 这条 Editor 命令依赖已保存 Scene 内存在 GameFlow、GameUI 和 PlaceOnPlane；后面会改它们的序列化引用。
            ApplyUiStyle(game.ui);
            MakeSelectionControls(game.ui, game);
            ApplyObjectAppearance(game.placement);
            game.ui.Refresh(game);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Applied object preview and color swatches to the existing classroom scene.");
        }
        // 依次准备样式/选择控件/外观后刷新一次 UI，并保存 Scene 与资源；这里不运行真正游戏回合。

        // ===== 首次组装：准备 Android 配置；仅当 Scene 不存在时创建游戏对象 =====
        // Builds the editor scene and saved assets; it is not called for every play session.
        public static void Prepare()
        {
            foreach (var path in new[] { Settings, Prefabs, "Assets/HideAR/Scenes" }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            ConfigureAndroid();
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            // ConfigureAndroid 已在上面执行；如果场景已存在，这里只打开并结束，避免复制一套对象。

            // ===== 创建 URP 渲染资源并设为项目默认值 =====
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            AssetDatabase.CreateAsset(renderer, Settings + "Renderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 8;
            AssetDatabase.CreateAsset(pipeline, Settings + "Pipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            // 这里写入图形质量设置（URP、HDR、抗锯齿等）；它们不属于 HideAR 阶段逻辑，也不实现现实遮挡。

            // ===== 创建 XR Input Actions 资产：头显、右手柄、Trigger、A 键 =====
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("Headset");
            var headPosition = map.AddAction("Head position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlLayout: "Vector3");
            var headRotation = map.AddAction("Head rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlLayout: "Quaternion");
            var headTracking = map.AddAction("Head tracking", InputActionType.Value, "<XRHMD>/trackingState", expectedControlLayout: "Integer");
            var handPosition = map.AddAction("Right aim position", InputActionType.Value, "<XRController>{RightHand}/pointerPosition", expectedControlLayout: "Vector3");
            var handRotation = map.AddAction("Right aim rotation", InputActionType.Value, "<XRController>{RightHand}/pointerRotation", expectedControlLayout: "Quaternion");
            var handTracking = map.AddAction("Right tracking", InputActionType.Value, "<XRController>{RightHand}/trackingState", expectedControlLayout: "Integer");
            var trigger = map.AddAction("Right trigger", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            var recenter = map.AddAction("Bring menu here", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            AssetDatabase.CreateAsset(actions, Settings + "Controls.asset");
            // InputActionAsset 保存 Unity Input System 的动作绑定；TrackedPoseDriver 之后会读取头部/手柄姿态。
            // 这些 binding 来自 XR 设备，不是项目自己计算的头部或手柄追踪算法。

            // ===== 创建 ARSession、XR Origin、头显相机和平面/射线管理器 =====
            var session = new GameObject("AR Session", typeof(ARSession)).GetComponent<ARSession>();
            var rig = new GameObject("XR Origin", typeof(XROrigin));
            var origin = rig.GetComponent<XROrigin>();
            var offset = Child("Camera Offset", rig.transform);
            var camera = Child("Main Camera", offset.transform).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.05f;
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<ARCameraManager>();
            AddPose(camera.gameObject, headPosition, headRotation, headTracking);
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.Origin = rig;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0;
            var planes = rig.AddComponent<ARPlaneManager>();
            planes.planePrefab = MakePlane();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.enabled = false;
            var raycasts = rig.AddComponent<ARRaycastManager>();
            raycasts.enabled = false;
            var anchors = rig.AddComponent<ARAnchorManager>();
            // XROrigin 整理头显追踪坐标；TrackedPoseDriver 使用上面的 Input Actions 更新相机位置/方向。
            // ARPlaneManager 只请求水平面；两个查询组件初始关闭，RoomAccess 获得权限后才打开。

            // ===== 创建右手柄 UI 射线和点击输入 =====

            var interaction = new GameObject("XR Interaction Manager", typeof(XRInteractionManager), typeof(InputActionManager));
            interaction.GetComponent<InputActionManager>().actionAssets = new List<InputActionAsset> { actions };
            new GameObject("Event System", typeof(EventSystem), typeof(XRUIInputModule));
            var hand = Child("Right Controller Aim", offset.transform);
            AddPose(hand, handPosition, handRotation, handTracking);
            var ray = hand.AddComponent<XRRayInteractor>();
            ray.interactionManager = interaction.GetComponent<XRInteractionManager>();
            ray.rayOriginTransform = hand.transform;
            ray.enableUIInteraction = true;
            ray.maxRaycastDistance = 20;
            ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;
            ray.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;
            ray.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference;
            var triggerReference = Reference(trigger);
            ray.uiPressInput.inputActionReferencePerformed = triggerReference;
            ray.uiPressInput.inputActionReferenceValue = triggerReference;
            var line = hand.AddComponent<LineRenderer>();
            line.sharedMaterial = Material("Controller Ray", new Color(0.55f, 0.92f, 1));
            hand.AddComponent<XRInteractorLineVisual>().lineWidth = 0.004f;
            // XRUIInputModule 把手柄射线转换成按钮点击；Trigger 用于点 UI，LineRenderer 只负责画可见射线。
            // XRRayInteractor 的 select/activate 未绑定；放置/找到由 PlaceOnPlane 自己进行射线判断。

            // ===== 创建运行时的三个管理组件并连接 Inspector 引用 =====
            var logic = new GameObject("HideAR Game");
            var game = logic.AddComponent<GameFlow>();
            var placement = logic.AddComponent<PlaceOnPlane>();
            var room = logic.AddComponent<RoomAccess>();
            room.planes = planes; room.raycasts = raycasts; room.session = session;
            placement.raycastManager = raycasts;
            placement.anchorManager = anchors;
            placement.controllerRay = ray;
            placement.placeAction = triggerReference;
            placement.prefabToPlace = MakeDinosaur();
            placement.placeablePrefabs = MakePlaceableChoices(placement.prefabToPlace);
            placement.placeableNames = new[] { "Dinosaur", "Dog", "Cat", "Bunny" };
            placement.game = game;
            placement.marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placement.marker.name = "Placement marker";
            placement.marker.transform.localScale = new Vector3(0.08f, 0.002f, 0.08f);
            Object.DestroyImmediate(placement.marker.GetComponent<Collider>());
            placement.marker.GetComponent<Renderer>().sharedMaterial = Material("Marker", new Color(1, 0.85f, 0.2f));
            placement.marker.SetActive(false);
            // marker 是命中位置提示，移除 Collider 避免被物理射线误当目标；正式模型要等玩家放置后才创建。
            game.room = room; game.placement = placement;
            game.recenterAction = Reference(recenter);
            game.ui = MakeUI(camera);
            ApplyObjectAppearance(placement);

            // ===== 保存按钮点击绑定，并把课堂场景设为唯一构建场景 =====
            UnityEventTools.AddPersistentListener(game.ui.primary.onClick, game.Primary);
            UnityEventTools.AddPersistentListener(game.ui.secondary.onClick, game.Secondary);
            UnityEventTools.AddPersistentListener(game.ui.reset.onClick, game.ResetGame);
            game.ui.Refresh(game);
            game.ui.Recenter();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Classroom scene saved. Original project not used or modified.");
        }
        // AddPersistentListener 将按钮与 GameFlow 方法保存进 Scene；玩家运行时不执行 ClassroomSetup。
        // ScenePath 被加入 Build Settings，因此此 APK 只从课堂版场景启动。

        // ===== Helper：创建对象并放到指定 Transform 下 =====
        // Creates a child object while keeping its local transform at the parent origin.
        static GameObject Child(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj;
        }
        // worldPositionStays=false 表示按父对象的局部坐标组织层级，适合 XR Origin/相机/手柄结构。

        // ===== Helper：保存场景能引用的 InputActionReference =====
        // Stores a reference to an action; it does not create a second input binding.
        static InputActionReference Reference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            reference.name = action.name;
            AssetDatabase.AddObjectToAsset(reference, Settings + "Controls.asset");
            return reference;
        }
        // Scene 的 Inspector 字段引用同一个 Controls.asset 动作，避免重复复制输入设置。

        // ===== Helper：让 Unity 的 TrackedPoseDriver 使用对应 XR 输入 =====
        // Unity's component applies the headset/controller pose to the object's Transform.
        static void AddPose(GameObject obj, InputAction position, InputAction rotation, InputAction tracking)
        {
            var driver = obj.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(Reference(position));
            driver.rotationInput = new InputActionProperty(Reference(rotation));
            driver.trackingStateInput = new InputActionProperty(Reference(tracking));
        }
        // 这里只连接位置、旋转、追踪状态三个动作；实际姿态由 Unity XR 输入更新。

        // ===== Helper：创建供 UI/提示网格使用的 Unlit 材质 =====
        static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            if (color.a < 1)
            {
                // alpha<1 表示半透明；配置混合方式、关闭深度写入并让材质可双面显示。
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, Settings + name + ".mat");
            return material;
        }
        // 不透明材质跳过透明设置；模型的 Simple Lit 材质由 ApplyObjectAppearance 另行配置。

        // ===== Helper：创建 ARPlaneManager 使用的平面显示 Prefab =====
        // ARPlaneMeshVisualizer draws plane data supplied by AR Foundation; it does not detect surfaces.
        static GameObject MakePlane()
        {
            var obj = new GameObject("Room Surface", typeof(ARPlane), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer), typeof(ARPlaneMeshVisualizer));
            obj.GetComponent<MeshRenderer>().sharedMaterial = Material("Plane Fill", new Color(0.15f, 0.85f, 0.6f, 0.12f));
            var line = obj.GetComponent<LineRenderer>();
            line.sharedMaterial = Material("Plane Edge", new Color(0.2f, 1, 0.7f));
            line.useWorldSpace = false; line.loop = true; line.widthMultiplier = 0.007f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, Prefabs + "Room Surface.prefab");
            Object.DestroyImmediate(obj); return prefab;
        }
        // ARPlaneMeshVisualizer 把 AR Foundation 已提供的平面画出来；MeshRenderer/LineRenderer 负责填充和边线。
        // 保存成 Prefab 后销毁编辑器临时对象；运行时 GameFlow 只切换这些 Renderer 的显隐。

        // ===== Helper：缩小恐龙源模型并保存恐龙 Prefab =====
        // Measures the visible model, scales it, then adds a collider for the seeker's ray.
        static GameObject MakeDinosaur()
        {
            var root = new GameObject("Dinosaur");
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Dinosaur/dino.fbx"), root.transform);
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.localScale *= 0.182f / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y - 0.005f, bounds.center.z);
            // bounds 合并所有子 Renderer 的可见范围；用目标高度 0.182 米缩放，再把模型底部对齐到根节点附近。
            // 这样新生成的 Prefab 便于放在平面上；后来在 Inspector 手动改过的 Prefab 不受此处保证。

            // ===== 为源模型的每种材质建立 URP 材质副本 =====
            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(old =>
                {
                    if (!materials.TryGetValue(old, out var converted))
                    {
                        // 同一种源材质可能出现在多个 Renderer 上；字典里没有时才创建一次转换材质。
                        converted = Material("Dinosaur " + materials.Count, old.HasProperty("_Color") ? old.color : new Color(0.95f, 0.45f, 0.62f));
                        materials.Add(old, converted);
                    }
                    return converted;
                }).ToArray();
            // Select 将每个旧材质换成字典中的 URP 材质，ToArray 再组成 Renderer 需要的材质数组。
            // 这是编辑器制作 Prefab；运行时 PlaceOnPlane 只实例化保存后的模型，不重复转换 FBX。

            // ===== 添加正式模型的 Collider 并保存 Prefab =====
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size + Vector3.one * 0.05f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Dinosaur.prefab");
            Object.DestroyImmediate(root); return prefab;
        }
        // BoxCollider 比模型可见范围宽约 5 厘米，方便 Seeker 的 Physics.Raycast 命中；临时根对象保存后销毁。

        // ===== Helper：按固定顺序生成四个可放置模型 =====
        // The array order must match the names and SelectedIndex used at runtime.
        static GameObject[] MakePlaceableChoices(GameObject dinosaur)
        {
            return new[]
            {
                dinosaur,
                MakePlaceable("animal-dog.fbx", "Dog", 0.16f),
                MakePlaceable("animal-cat.fbx", "Cat", 0.15f),
                MakePlaceable("animal-bunny.fbx", "Bunny", 0.145f)
            };
        }
        // 返回数组的 0/1/2/3 依次是 Dinosaur/Dog/Cat/Bunny；每个宠物目标高度以米为单位。
        // GameFlow 的 SelectedIndex 直接使用这些下标，所以不能随意调换顺序。

        // ===== Helper：将 Kenney 源动物模型制成小型、可被射线命中的 Prefab =====
        // Reads a source FBX, sizes it, assigns its texture, adds a collider, and saves a prefab.
        static GameObject MakePlaceable(string sourceFile, string displayName, float targetHeight)
        {
            var sourcePath = CubePetsFbx + sourceFile;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new FileNotFoundException("Candidate model not found", sourcePath);
            // 源 FBX 不存在就抛出错误停止；不继续保存一个空的可选模型。
            var root = new GameObject(displayName);
            var model = Object.Instantiate(source, root.transform);
            model.name = displayName + " Model";
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception(displayName + " has no renderers.");
            // 找不到 Renderer 表示模型没有可绘制部分；此时停止，避免生成看不见的 Prefab。
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.localScale *= targetHeight / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y - 0.005f, bounds.center.z);
            var material = Material(displayName + " Choice", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CubePetsFbx + "Textures/colormap.png"));
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size + Vector3.one * 0.04f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + displayName + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
        // targetHeight 是编辑器制作 Prefab 时的目标高度；运行时不会重新测量 FBX。
        // Collider 稍大于可见模型，方便 Seeker 射线命中；保存后清除编辑器里的临时根对象。

        // ===== Helper：创建世界空间任务卡及基础文字/按钮 =====
        // Creates the saved menu canvas; GameUI fills it with stage-specific content at runtime.
        static GameUI MakeUI(Camera camera)
        {
            var root = new GameObject("Game Menu", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster), typeof(Image), typeof(BoxCollider), typeof(GameUI));
            root.layer = 5;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1000, 660); rect.localScale = Vector3.one * 0.001f;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            root.GetComponent<Image>().color = new Color(0.055f, 0.10f, 0.095f, 0.97f);
            root.GetComponent<BoxCollider>().size = new Vector3(1000, 660, 4);
            var ui = root.GetComponent<GameUI>(); ui.head = camera;
            Label(root.transform, "Edition", "HIDEAR  /  CLASSROOM EDITION", new Vector2(-10, 277), new Vector2(880, 32), 19, new Color(0.45f, 0.9f, 0.73f));
            ui.title = Label(root.transform, "Title", "HideAR", new Vector2(-55, 185), new Vector2(790, 116), 54, Color.white);
            ui.body = Label(root.transform, "Story", "", new Vector2(0, 60), new Vector2(880, 124), 30, new Color(0.87f, 0.94f, 0.92f));
            ui.status = Label(root.transform, "Status", "", new Vector2(0, -55), new Vector2(880, 78), 23, new Color(1, 0.81f, 0.4f));
            ui.primary = Button(root.transform, "Primary", new Vector2(-220, -167), new Vector2(420, 86), new Color(0.3f, 0.86f, 0.62f));
            ui.secondary = Button(root.transform, "Secondary", new Vector2(220, -167), new Vector2(420, 86), new Color(0.64f, 0.83f, 0.89f));
            ui.reset = Button(root.transform, "Reset", new Vector2(300, -274), new Vector2(260, 68), new Color(0.97f, 0.70f, 0.51f));
            ui.reset.GetComponentInChildren<Text>().text = "Reset round";
            ApplyUiStyle(ui);
            MakeSelectionControls(ui, Object.FindAnyObjectByType<GameFlow>());
            return ui;
        }
        // Canvas 是 WorldSpace，所以卡片是真实空间里的物体；0.001 把像素尺寸缩放成合适的世界尺寸。
        // TrackedDeviceGraphicRaycaster 接收 XR 射线 UI 点击；BoxCollider 让射线不能从卡片空白处穿过去。
        // 此处创建控件和默认样式；运行时的阶段文字、可见性由 GameUI.Refresh 决定。

        // ===== 编辑器处理模型共享材质、宠物调色贴图和补光 =====
        // This writes project assets. Runtime ApplyColor changes only each visible instance.
        static void ApplyObjectAppearance(PlaceOnPlane placement)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CubePetsFbx + "Textures/colormap.png");
            if (texture == null) throw new Exception("Cube Pets color texture is missing.");
            // 没有 Kenney 源贴图就停止；后续不能正确生成狗/猫/兔子的颜色选择。
            placement.colorTextures = MakeColorTextures(texture);
            foreach (var prefab in placement.placeablePrefabs)
                foreach (var material in prefab.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
                {
                    material.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    material.SetFloat("_Smoothness", 0);
                    material.SetColor("_SpecColor", Color.black);
                    if (prefab != placement.prefabToPlace)
                    {
                        // 恐龙保留它自己的材质；其他宠物模型使用 Cube Pets 的基础贴图。
                        material.SetTexture("_BaseMap", texture);
                        material.SetColor("_BaseColor", Color.white);
                    }
                    EditorUtility.SetDirty(material);
                }
            var lightObject = GameObject.Find("Object Light") ?? new GameObject("Object Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
        }
        // 这些是 Editor 中写入的共享资源；运行时选颜色走 PlaceOnPlane.ApplyColor 的实例覆盖，不再重写资源。
        // Directional Light 只照亮虚拟模型；不产生现实家具的深度遮挡或碰撞。

        // ===== Editor 预生成宠物颜色贴图，保留灰阶脸部细节 =====
        // Pixel recoloring happens while preparing assets, not every frame during gameplay.
        static Texture2D[] MakeColorTextures(Texture2D original)
        {
            var choices = new Texture2D[PlaceOnPlane.Colors.Length];
            choices[0] = original;
            var readable = new Texture2D(2, 2);
            readable.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(original)));
            var sourcePixels = readable.GetPixels();
            // 第 0 项是原图；另读临时副本来访问像素，避免直接改写 Kenney 的来源贴图。
            for (int i = 1; i < choices.Length; i++)
            {
                string path = Settings + "Pet Color " + PlaceOnPlane.ColorNames[i] + ".asset";
                var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                bool create = result == null;
                // 没有现成 .asset 就创建；已有同名颜色贴图则更新它，避免产生重复资源。
                if (create) result = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, true);
                // 新建贴图要能写入像素；已有贴图直接复用。
                var pixels = sourcePixels.Select(pixel =>
                {
                    float min = Mathf.Min(pixel.r, pixel.g, pixel.b);
                    float max = Mathf.Max(pixel.r, pixel.g, pixel.b);
                    return max - min < 0.08f ? pixel : PlaceOnPlane.Colors[i] * Mathf.Lerp(0.75f, 1, max);
                }).ToArray();
                // RGB 三个值相差很小时视作灰阶（常见于脸部细节），原样保留；其他像素换成选中颜色并保留明暗。
                for (int p = 0; p < pixels.Length; p++) pixels[p].a = sourcePixels[p].a;
                // 恢复来源图每个像素的透明度，避免换色时丢掉透明边缘。
                result.SetPixels(pixels); result.Apply(); result.filterMode = FilterMode.Bilinear;
                if (create) AssetDatabase.CreateAsset(result, path); else EditorUtility.SetDirty(result);
                // 新纹理登记为 Project 资源；旧纹理标为已修改，之后由 SaveAssets 写入磁盘。
                choices[i] = result;
            }
            Object.DestroyImmediate(readable);
            return choices;
        }
        // 最后销毁临时读取贴图；返回值包含原贴图及各颜色贴图，运行时按 ColorIndex 选择。

        // ===== Helper：创建 Set up room、模型预览容器和颜色按钮 =====
        // Wires each editor-created control to the existing GameFlow/GameUI runtime scripts.
        static void MakeSelectionControls(GameUI ui, GameFlow game)
        {
            var oldMascot = ui.transform.Find("Menu dinosaur");
            if (oldMascot != null) Object.DestroyImmediate(oldMascot.gameObject);
            // Scene 若残留旧的固定恐龙装饰就移除；菜单只保留跟随选择切换的预览。
            var existingRoom = ui.transform.Find("Room Setup");
            ui.roomSetup = existingRoom != null ? existingRoom.GetComponent<Button>() :
                Button(ui.transform, "Room Setup", Vector2.zero, Vector2.one, Color.white);
            StyleButton(ui.roomSetup, new Vector2(-240, -238), new Vector2(260, 58), new Color(0.72f, 0.82f, 0.83f));
            ui.roomSetup.GetComponent<Image>().color = Color.white;
            ui.roomSetup.GetComponentInChildren<Text>().text = "Set up room";
            ui.roomSetup.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(ui.roomSetup.onClick, game.SetUpRoom);
            // Set up room 始终调用 GameFlow.SetUpRoom；它不是 Secondary 换模型按钮。
            var preview = ui.transform.Find("Object Preview");
            if (preview == null) preview = Child("Object Preview", ui.transform).transform;
            // 重跑 Editor 命令时复用已有容器，避免 Scene 出现多个 Object Preview 根节点。
            preview.localPosition = new Vector3(260, 40, -130);
            preview.localScale = Vector3.one;
            ui.previewRoot = preview;
            ui.colorButtons = new Button[PlaceOnPlane.Colors.Length];
            // 每次运行都重建运行时使用的 Button 引用数组，数组下标与颜色编号一一对应。
            for (int i = 0; i < ui.colorButtons.Length; i++)
            {
                string name = "Color " + PlaceOnPlane.ColorNames[i];
                var existing = ui.transform.Find(name);
                // 同名色块已存在就复用；没有才新建，防止重复生成按钮。
                var button = existing != null ? existing.GetComponent<Button>() : Button(ui.transform, name, Vector2.zero, Vector2.one, Color.white);
                var size = i == 0 ? new Vector2(108, 48) : new Vector2(48, 48);
                // Original 要显示文字所以按钮较宽；其他项只显示颜色方块。
                var position = new Vector2(i == 0 ? -330 : -240 + (i - 1) * 60, -98);
                StyleButton(button, position, size, PlaceOnPlane.Colors[i]);
                button.GetComponent<Image>().color = Color.white;
                var label = button.GetComponentInChildren<Text>();
                label.text = i == 0 ? "Original" : "";
                label.fontSize = 19;
                var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
                // 已有 Outline 就复用；没有才添加。运行时 GameUI 用它标出所选颜色。
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(3, -3);
                outline.enabled = i == 0;
                button.onClick = new Button.ButtonClickedEvent();
                // 清除这个 Button 原有的持久点击列表，再只绑定一次 SelectColor(i)，避免重复点击回调。
                UnityEventTools.AddIntPersistentListener(button.onClick, game.SelectColor, i);
                // 每个色块都连到同一个 SelectColor 方法，只是保存不同的颜色编号 i。
                ui.colorButtons[i] = button;
            }
            // 这里负责把控件和引用保存进 Scene；菜单模型副本由 GameUI.RefreshSelection 创建/更换。
        }

        // ===== Helper：保存任务卡的普通尺寸与初始布局 =====
        // The runtime script switches to the smaller Handoff layout when needed.
        static void ApplyUiStyle(GameUI ui)
        {
            var root = (RectTransform)ui.transform;
            root.sizeDelta = new Vector2(900, 560);
            ui.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.052f, 0.91f);
            ui.GetComponent<BoxCollider>().size = new Vector3(root.rect.width, root.rect.height, 4);
            var outline = ui.GetComponent<Outline>() ?? ui.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.24f, 0.9f, 0.72f, 0.33f);
            outline.effectDistance = new Vector2(2, -2);

            SetText(ui.transform.Find("Edition")?.GetComponent<Text>(), "HIDEAR  /  MR TASK CARD",
                new Vector2(-10, 236), new Vector2(760, 30), 18, new Color(0.45f, 0.95f, 0.78f));
            SetText(ui.title, null, new Vector2(-25, 150), new Vector2(760, 94), 48, Color.white);
            SetText(ui.body, null, new Vector2(0, 36), new Vector2(760, 108), 27, new Color(0.87f, 0.94f, 0.92f));
            SetText(ui.status, null, new Vector2(0, -62), new Vector2(760, 58), 21, new Color(1, 0.82f, 0.42f));
            StyleButton(ui.primary, new Vector2(-185, -166), new Vector2(340, 68), new Color(0.27f, 0.86f, 0.62f));
            StyleButton(ui.secondary, new Vector2(185, -166), new Vector2(340, 68), new Color(0.62f, 0.82f, 0.9f));
            StyleButton(ui.reset, new Vector2(270, -238), new Vector2(220, 58), new Color(0.98f, 0.69f, 0.50f));
        }
        // 同步 BoxCollider 和卡片尺寸，让射线拦截范围与可见 UI 相同；Handoff 小卡片由 GameUI 再调整。

        // ===== Helper：设置已有 Text 的内容/位置/字号 =====
        // A null content keeps the stage text that GameUI will provide at runtime.
        static void SetText(Text text, string content, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            if (text == null) return;
            // Edition 等装饰文字可能不存在；没有 Text 时退出，避免后面访问 null 报错。
            var rect = (RectTransform)text.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.fontSize = fontSize;
            text.color = color;
            if (content != null) text.text = content;
            // content=null 时保留运行时阶段文字；有内容时才直接替换。
        }

        // ===== Helper：设置按钮 RectTransform 和普通/悬停/禁用颜色 =====
        // Styles a button only; its saved onClick event is left unchanged.
        static void StyleButton(Button button, Vector2 position, Vector2 size, Color color)
        {
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = button.GetComponent<Image>();
            image.color = color;
            button.colors = new ColorBlock
            {
                normalColor = color,
                highlightedColor = Color.Lerp(color, Color.white, 0.18f),
                pressedColor = Color.Lerp(color, Color.black, 0.15f),
                selectedColor = color,
                disabledColor = new Color(color.r, color.g, color.b, 0.35f),
                colorMultiplier = 1,
                fadeDuration = 0.08f
            };
            var label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            // 按钮没有文字子物体时仍保留按钮样式，只跳过下面的文字字号/颜色设置。
            label.fontSize = button.name == "Reset" ? 22 : 26;
            label.color = new Color(0.03f, 0.08f, 0.07f);
            label.rectTransform.sizeDelta = size - new Vector2(28, 8);
        }
        // ColorBlock 控制正常、悬停、按下和不可点时的外观，不改变按钮的点击功能。

        // ===== Helper：创建不遮挡 XR UI 射线的文字 =====
        // Creates a visual label; it is not itself a clickable control.
        static Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.layer = 5;
            var rect = (RectTransform)obj.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = color; text.text = content; text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        // raycastTarget=false 让文字区域把 XR UI 射线交给下方按钮；文本可换行但超高会被截断。

        // ===== Helper：创建按钮外观；点击事件由调用者另行绑定 =====
        // The caller adds the persistent GameFlow event after creating this control.
        static Button Button(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.layer = 5;
            var rect = (RectTransform)obj.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<Image>(); image.color = color;
            var button = obj.GetComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(obj.transform, "Label", name, Vector2.zero, size - new Vector2(32, 8), 28, new Color(0.04f, 0.1f, 0.08f)).alignment = TextAnchor.MiddleCenter;
            return button;
        }
        // 此函数只创建 Image/Button/Text；之后的 UnityEventTools 才会把点击连接到具体 GameFlow 方法。

        // ===== Editor 配置 Android/OpenXR Loader、Quest Features 和 PlayerSettings =====
        // This writes project build settings; it does not implement runtime tracking.
        static void ConfigureAndroid()
        {
            var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Settings + "XR Settings.asset");
            if (settings == null)
            {
                // 第一次没有 XR Settings.asset 时才新建；已有资产会在后面被复用。
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, Settings + "XR Settings.asset");
                settings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            var general = settings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android)) throw new Exception("OpenXR loader setup failed.");
            // Loader 分配失败就抛异常停止；没有 OpenXR Loader 的包无法按预期启动 XR。
            general.InitManagerOnStart = true;
            OpenXRFeatureSetManager.InitializeFeatureSets();
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            // 取得当前 Android OpenXR 配置；下面按功能名字启用所需的官方 Features。
            foreach (var feature in xr.GetFeatures())
            {
                string name = feature.GetType().Name;
                feature.enabled = new[] { "MetaQuestFeature", "OpenXRLifeCycleFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "OpenXRCompositionLayersFeature", "ARSessionFeature", "ARCameraFeature", "ARAnchorFeature", "ARPlaneFeature", "ARRaycastFeature" }.Contains(name);
                // 名称在白名单中才启用该 Feature；其余已安装 Features 会被关闭。
                if (name == "MetaQuestFeature")
                {
                    // MetaQuestFeature 还需要指定可用设备型号，下面只保留此配置认识的 Quest 3/3S ID。
                    var data = new SerializedObject(feature);
                    var devices = data.FindProperty("targetDevices");
                    for (int i = 0; i < devices.arraySize; i++)
                    {
                        var device = devices.GetArrayElementAtIndex(i);
                        string id = device.FindPropertyRelative("manifestName").stringValue;
                        device.FindPropertyRelative("enabled").boolValue = id == "eureka" || id == "quest3s";
                        // id 是 eureka 或 quest3s 才启用该条目；这是 SDK 配置，不是头显识别代码。
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(feature);
            }
            // SerializedObject 通过 Unity Inspector 序列化字段写入目标设备选项；追踪仍由 OpenXR/Meta 提供。
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(xr); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(settings);
            // 以下写入 Android 包名、IL2CPP、ARM64、最低系统版本、横屏和 Vulkan 等构建参数。
            PlayerSettings.companyName = "Lingjie Ruan";
            PlayerSettings.productName = "HideAR Classroom";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lingjieruan.hidear.classroom");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            player.FindProperty("activeInputHandler").intValue = 1;
            player.ApplyModifiedPropertiesWithoutUndo();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();
        }
        // 包名用于区分课堂 APK；IL2CPP/ARM64 决定 Android 程序后端与 CPU 架构，Vulkan 是图形 API，不是游戏功能。
        // 保存设置后由 Unity Editor 生成 Android 包；此方法不在头显运行。

        // ===== Unity 菜单：检查前置条件并构建课堂 APK =====
        // Builds the classroom scene only. A successful build does not install or test the APK.
        [MenuItem("HideAR/Classroom/Build APK")]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to Android in File > Build Profiles first.");
            // 当前平台不是 Android 就中止，并告诉用户手动切换；此命令不会更改构建平台。
            // ===== 平台与可用磁盘空间检查 =====
            bool hasAndroidCache = File.Exists("Library/Bee/artifacts/Android/il2cppOutput/build/arm64-v8a/libil2cpp.so") &&
                File.Exists("Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/outputs/apk/debug/launcher-debug.apk");
            int minimumGiB = hasAndroidCache ? 2 : 6;
            double freeGiB = new DriveInfo(Path.GetPathRoot(Application.dataPath)).AvailableFreeSpace / (1024d * 1024 * 1024);
            if (freeGiB < minimumGiB)
                throw new BuildFailedException($"Need at least {minimumGiB} GiB free for this Android build. Existing APKs and projects have not been deleted.");
            // 空间不足就中止，不自动删除 Library、缓存、源码或之前的 APK。
            Debug.Log($"Android build space: {freeGiB:0.0} GiB free; minimum {minimumGiB} GiB; prior Android outputs present: {hasAndroidCache}.");
            // 如果检测到之前的 Android 中间产物，门槛为 2 GiB；没有则按首次构建要求至少 6 GiB。
            // 这是开始前的保守检查，不保证构建实际只占用这些空间。

            // ===== 先检查 OpenXR，再让 Unity BuildPipeline 打包 =====
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            foreach (var issue in issues) Debug.LogWarning(issue.message);
            if (issues.Any(issue => issue.error)) throw new BuildFailedException("Resolve OpenXR Project Validation errors first.");
            // Any(error) 只要发现一条严重错误就停止；普通 warning 只记录，不在这里中止。
            Directory.CreateDirectory("../Builds/Week9");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "../Builds/Week9/HideAR_Classroom.apk", options = BuildOptions.Development
            });
            // BuildOptions.Development 表示生成开发构建；输出路径是项目上一级的 Builds/Week9，不是安装命令。
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("APK failed. Read the first build error.");
            // Unity 没有返回 Succeeded 就视为失败；只有成功后才打印完成日志。
            Debug.Log("Classroom APK built successfully.");
        }
        // BuildPlayer 只打包 ScenePath 到 ../Builds/Week9/HideAR_Classroom.apk；不会自动安装或验证 Quest 真机体验。
    }
}
