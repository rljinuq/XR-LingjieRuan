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
    // 职责：编辑器里的场景组装、资源配置和 APK 构建工具；不在 Quest 游戏中运行。
    // Role: editor-only scene assembly, asset setup and APK building, not Quest runtime gameplay.
    // 已保存的场景可直接使用，不需要每次运行这些初始化工具。
    // Use the saved scene directly; these initialization tools are not needed each run.
    public static class ClassroomSetup
    {
        public const string ScenePath = "Assets/HideAR/Scenes/HideAR_Week9.unity";
        const string Settings = "Assets/HideAR/Settings/";
        const string Prefabs = "Assets/HideAR/Prefabs/";
        const string CubePetsFbx = "Assets/Art/CandidateModels/Sources/Kenney_CubePets/Models/FBX format/";

        [MenuItem("HideAR/Classroom/Open Game Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Prepare();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        // Apply 系列是显式修改现有资源/场景的工具，不是运行游戏时的必要步骤。
        // Apply commands explicitly modify saved assets/scenes; they are not play-time steps.
        [MenuItem("HideAR/Classroom/Apply Simple UI Style")]
        public static void ApplySimpleUiStyle()
        {
            if (!File.Exists(ScenePath)) Prepare();
            else EditorSceneManager.OpenScene(ScenePath);
            var ui = Object.FindAnyObjectByType<GameUI>();
            if (ui == null) throw new Exception("Game UI was not found.");
            ApplyUiStyle(ui);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Applied simple MR task-card UI style.");
        }

        [MenuItem("HideAR/Classroom/Apply Object Choices")]
        public static void ApplyObjectChoices()
        {
            if (!File.Exists(ScenePath)) Prepare();
            else EditorSceneManager.OpenScene(ScenePath);
            var placement = Object.FindAnyObjectByType<PlaceOnPlane>();
            if (placement == null) throw new Exception("PlaceOnPlane was not found.");
            var dinosaur = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Dinosaur.prefab") ?? MakeDinosaur();
            placement.prefabToPlace = dinosaur;
            placement.placeablePrefabs = MakePlaceableChoices(dinosaur);
            placement.placeableNames = new[] { "Dinosaur", "Dog", "Cat", "Bunny" };
            ApplyObjectAppearance(placement);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Applied simple object choices.");
        }

        [MenuItem("HideAR/Classroom/Apply Selection UI")]
        public static void ApplySelectionUi()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindAnyObjectByType<GameFlow>();
            ApplyUiStyle(game.ui);
            MakeSelectionControls(game.ui, game);
            ApplyObjectAppearance(game.placement);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Applied object preview and color swatches to the existing classroom scene.");
        }

        // 配置 Android 后，仅在缺少场景时组装初始场景；已有场景直接打开，不重建。
        // Configure Android, then build an initial scene only if missing; otherwise open it unchanged.
        public static void Prepare()
        {
            foreach (var path in new[] { Settings, Prefabs, "Assets/HideAR/Scenes" }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            ConfigureAndroid();
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
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

            // Input System 绑定头显姿态、右手柄瞄准、扳机和 A 键；不自行推算设备姿态。
            // Bind headset pose, right aim, trigger and A through Input System, not custom pose tracking.
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

            // 把官方 AR/XR 组件放进场景；权限通过前保持平面和 AR 射线管理器关闭。
            // Assemble official AR/XR components; keep plane/AR-raycast managers off until authorized.
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
            game.room = room; game.placement = placement;
            game.recenterAction = Reference(recenter);
            game.ui = MakeUI(camera, placement.prefabToPlace);
            ApplyObjectAppearance(placement);
            // 持久 Button 事件保存到场景 Inspector，运行时无需再手动绑定按钮。
            // Persist Button events in the scene Inspector instead of wiring them again at runtime.
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

        static GameObject Child(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj;
        }

        static InputActionReference Reference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            reference.name = action.name;
            AssetDatabase.AddObjectToAsset(reference, Settings + "Controls.asset");
            return reference;
        }

        static void AddPose(GameObject obj, InputAction position, InputAction rotation, InputAction tracking)
        {
            var driver = obj.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(Reference(position));
            driver.rotationInput = new InputActionProperty(Reference(rotation));
            driver.trackingStateInput = new InputActionProperty(Reference(tracking));
        }

        static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            if (color.a < 1)
            {
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

        // 把原模型按包围盒缩到 18.2 厘米高，并保存带可点击碰撞盒的 Prefab。
        // Fit the source model to 18.2 cm height and save a prefab with a clickable collider.
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
            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(old =>
                {
                    if (!materials.TryGetValue(old, out var converted))
                    {
                        converted = Material("Dinosaur " + materials.Count, old.HasProperty("_Color") ? old.color : new Color(0.95f, 0.45f, 0.62f));
                        materials.Add(old, converted);
                    }
                    return converted;
                }).ToArray();
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size + Vector3.one * 0.05f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Dinosaur.prefab");
            Object.DestroyImmediate(root); return prefab;
        }

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

        // 同一套资源处理方式用于不同小模型，不为每种动物建立独立游戏系统。
        // Reuse the same asset preparation for small models, not a separate game system per animal.
        static GameObject MakePlaceable(string sourceFile, string displayName, float targetHeight)
        {
            var sourcePath = CubePetsFbx + sourceFile;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new FileNotFoundException("Candidate model not found", sourcePath);
            var root = new GameObject(displayName);
            var model = Object.Instantiate(source, root.transform);
            model.name = displayName + " Model";
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception(displayName + " has no renderers.");
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

        static GameUI MakeUI(Camera camera, GameObject dinosaur)
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
            ui.mascot = Object.Instantiate(dinosaur, root.transform);
            Object.DestroyImmediate(ui.mascot.GetComponent<Collider>());
            ui.mascot.name = "Menu dinosaur";
            ui.mascot.transform.localScale = Vector3.one * 430;
            ui.mascot.transform.localPosition = new Vector3(350, 132, -80);
            ui.mascot.transform.localRotation = Quaternion.Euler(0, -35, 0);
            ApplyUiStyle(ui);
            MakeSelectionControls(ui, Object.FindAnyObjectByType<GameFlow>());
            return ui;
        }

        static void ApplyObjectAppearance(PlaceOnPlane placement)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CubePetsFbx + "Textures/colormap.png");
            if (texture == null) throw new Exception("Cube Pets color texture is missing.");
            placement.colorTextures = MakeColorTextures(texture);
            foreach (var prefab in placement.placeablePrefabs)
                foreach (var material in prefab.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
                {
                    material.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    material.SetFloat("_Smoothness", 0);
                    material.SetColor("_SpecColor", Color.black);
                    if (prefab != placement.prefabToPlace)
                    {
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

        // 编辑器预生成宠物调色贴图并保留灰阶脸部；运行时只选取对应贴图。
        // Precompute pet palettes while preserving grayscale faces; runtime only selects a texture.
        static Texture2D[] MakeColorTextures(Texture2D original)
        {
            var choices = new Texture2D[PlaceOnPlane.Colors.Length];
            choices[0] = original;
            var readable = new Texture2D(2, 2);
            readable.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(original)));
            var sourcePixels = readable.GetPixels();
            for (int i = 1; i < choices.Length; i++)
            {
                string path = Settings + "Pet Color " + PlaceOnPlane.ColorNames[i] + ".asset";
                var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                bool create = result == null;
                if (create) result = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, true);
                var pixels = sourcePixels.Select(pixel =>
                {
                    float min = Mathf.Min(pixel.r, pixel.g, pixel.b);
                    float max = Mathf.Max(pixel.r, pixel.g, pixel.b);
                    return max - min < 0.08f ? pixel : PlaceOnPlane.Colors[i] * Mathf.Lerp(0.75f, 1, max);
                }).ToArray();
                for (int p = 0; p < pixels.Length; p++) pixels[p].a = sourcePixels[p].a;
                result.SetPixels(pixels); result.Apply(); result.filterMode = FilterMode.Bilinear;
                if (create) AssetDatabase.CreateAsset(result, path); else EditorUtility.SetDirty(result);
                choices[i] = result;
            }
            Object.DestroyImmediate(readable);
            return choices;
        }

        // 把独立房间按钮、模型预览和色块引用保存到已有场景。
        // Save separate room setup, model preview and swatch references into the existing scene.
        static void MakeSelectionControls(GameUI ui, GameFlow game)
        {
            var existingRoom = ui.transform.Find("Room Setup");
            ui.roomSetup = existingRoom != null ? existingRoom.GetComponent<Button>() :
                Button(ui.transform, "Room Setup", Vector2.zero, Vector2.one, Color.white);
            StyleButton(ui.roomSetup, new Vector2(-240, -238), new Vector2(260, 58), new Color(0.72f, 0.82f, 0.83f));
            ui.roomSetup.GetComponent<Image>().color = Color.white;
            ui.roomSetup.GetComponentInChildren<Text>().text = "Set up room";
            ui.roomSetup.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(ui.roomSetup.onClick, game.SetUpRoom);
            var preview = ui.transform.Find("Object Preview");
            if (preview == null) preview = Child("Object Preview", ui.transform).transform;
            preview.localPosition = new Vector3(260, 40, -130);
            preview.localScale = Vector3.one;
            ui.previewRoot = preview;
            ui.mascot.SetActive(false);
            ui.colorButtons = new Button[PlaceOnPlane.Colors.Length];
            for (int i = 0; i < ui.colorButtons.Length; i++)
            {
                string name = "Color " + PlaceOnPlane.ColorNames[i];
                var existing = ui.transform.Find(name);
                var button = existing != null ? existing.GetComponent<Button>() : Button(ui.transform, name, Vector2.zero, Vector2.one, Color.white);
                var size = i == 0 ? new Vector2(110, 46) : new Vector2(46, 46);
                var position = new Vector2(i == 0 ? -310 : -205 + (i - 1) * 70, -96);
                StyleButton(button, position, size, PlaceOnPlane.Colors[i]);
                button.GetComponent<Image>().color = Color.white;
                var label = button.GetComponentInChildren<Text>();
                label.text = i == 0 ? "Original" : "";
                label.fontSize = 19;
                var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(3, -3);
                outline.enabled = i == 0;
                button.onClick = new Button.ButtonClickedEvent();
                UnityEventTools.AddIntPersistentListener(button.onClick, game.SelectColor, i);
                ui.colorButtons[i] = button;
            }
        }

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
            if (ui.mascot != null)
            {
                ui.mascot.transform.localScale = Vector3.one * 300;
                ui.mascot.transform.localPosition = new Vector3(302, 112, -80);
                ui.mascot.transform.localRotation = Quaternion.Euler(0, -35, 0);
            }
        }

        static void SetText(Text text, string content, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            if (text == null) return;
            var rect = (RectTransform)text.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.fontSize = fontSize;
            text.color = color;
            if (content != null) text.text = content;
        }

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
            label.fontSize = button.name == "Reset" ? 22 : 26;
            label.color = new Color(0.03f, 0.08f, 0.07f);
            label.rectTransform.sizeDelta = size - new Vector2(28, 8);
        }

        static Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.layer = 5;
            var rect = (RectTransform)obj.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = color; text.text = content; text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

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

        // Android/OpenXR 构建配置，不是玩法：独立包名使课堂版与旧版可以共存。
        // Android/OpenXR build configuration, not gameplay; a distinct package ID preserves the old app.
        static void ConfigureAndroid()
        {
            var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Settings + "XR Settings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, Settings + "XR Settings.asset");
                settings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            var general = settings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android)) throw new Exception("OpenXR loader setup failed.");
            general.InitManagerOnStart = true;
            OpenXRFeatureSetManager.InitializeFeatureSets();
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var feature in xr.GetFeatures())
            {
                string name = feature.GetType().Name;
                feature.enabled = new[] { "MetaQuestFeature", "OpenXRLifeCycleFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "OpenXRCompositionLayersFeature", "ARSessionFeature", "ARCameraFeature", "ARAnchorFeature", "ARPlaneFeature", "ARRaycastFeature" }.Contains(name);
                if (name == "MetaQuestFeature")
                {
                    var data = new SerializedObject(feature);
                    var devices = data.FindProperty("targetDevices");
                    for (int i = 0; i < devices.arraySize; i++)
                    {
                        var device = devices.GetArrayElementAtIndex(i);
                        string id = device.FindPropertyRelative("manifestName").stringValue;
                        device.FindPropertyRelative("enabled").boolValue = id == "eureka" || id == "quest3s";
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(feature);
            }
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(xr); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(settings);
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

        // 只构建课堂场景到 Builds/Week9；验证配置后调用 Unity BuildPipeline。
        // Validate settings and use Unity BuildPipeline for the classroom scene in Builds/Week9 only.
        [MenuItem("HideAR/Classroom/Build APK")]
        public static void BuildApk()
        {
            if (new DriveInfo(Path.GetPathRoot(Application.dataPath)).AvailableFreeSpace < 6L * 1024 * 1024 * 1024)
                throw new BuildFailedException("Free at least 8 GB before rebuilding. Existing APKs and projects have not been deleted.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to Android in File > Build Profiles first.");
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            foreach (var issue in issues) Debug.LogWarning(issue.message);
            if (issues.Any(issue => issue.error)) throw new BuildFailedException("Resolve OpenXR Project Validation errors first.");
            Directory.CreateDirectory("../Builds/Week9");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "../Builds/Week9/HideAR_Classroom.apk", options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("APK failed. Read the first build error.");
            Debug.Log("Classroom APK built successfully.");
        }
    }
}
