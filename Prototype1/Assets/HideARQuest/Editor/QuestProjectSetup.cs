using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.Samples;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Object = UnityEngine.Object;

namespace HideAR.Quest.Editor
{
    public static class QuestProjectSetup
    {
        public const string Root = "Assets/HideARQuest";
        public const string ScenePath = Root + "/Scenes/HideAR_IP2_Quest.unity";
        const string OfficialScene = "Assets/Scenes/Anchors/Anchors.unity";
        const string PipelinePath = Root + "/Settings/Quest Pipeline.asset";

        [MenuItem("HideAR/Quest/1. Open Quest Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        // Also usable in batch mode. Never replaces an existing Quest scene.
        public static void CreateScene()
        {
            foreach (var folder in new[] { "Scenes", "Settings", "Prefabs" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            if (!AssetDatabase.CopyAsset(OfficialScene, ScenePath)) throw new Exception("Could not copy the official Anchors scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Canvas Template" || root.name == "Persistent Anchors" || root.name == "DISABLE LANDSCAPE")
                    root.SetActive(false);

            var origin = Object.FindAnyObjectByType<XROrigin>();
            var anchorManager = Object.FindAnyObjectByType<ARAnchorManager>();
            var planes = Object.FindAnyObjectByType<ARPlaneManager>();
            var raycasts = Object.FindAnyObjectByType<ARRaycastManager>();
            var placement = Object.FindAnyObjectByType<ARPlaceAnchor>();
            var raycaster = Object.FindAnyObjectByType<RaycastEventController>();
            var session = Object.FindAnyObjectByType<ARSession>();
            if (origin == null || anchorManager == null || planes == null || placement == null || raycaster == null)
                throw new Exception("Official Anchors scene references are incomplete.");

            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0;
            origin.Camera.clearFlags = CameraClearFlags.SolidColor;
            origin.Camera.backgroundColor = Color.clear;
            origin.Camera.allowHDR = false;
            origin.Camera.nearClipPlane = 0.05f;
            var cameraData = origin.Camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            foreach (var check in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (check != null && check.GetType().FullName == "UnityEngine.XR.ARFoundation.Samples.PermissionsCheck") check.enabled = false;

            // A private anchor prefab contains ARAnchor only. The official creation component is unchanged.
            var anchorObject = new GameObject("HideAR Quest Anchor", typeof(ARAnchor));
            anchorManager.anchorPrefab = PrefabUtility.SaveAsPrefabAsset(anchorObject, Root + "/Prefabs/Quest Anchor.prefab");
            Object.DestroyImmediate(anchorObject);
            planes.planePrefab = MakePlane();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.enabled = false;

            var raw = MakeEvent("Quest Surface Hit");
            var approved = MakeEvent("Quest Approved Placement");
            raycaster.raycastHitEventAsset = raw;
            raycaster.trackableType = TrackableType.PlaneWithinPolygon;
            SetReference(raycaster, "m_GraphicRaycaster", null);
            SetReference(placement, "m_RaycastHitEvent", approved);
            placement.enabled = false;
            raycaster.enabled = false;

            var game = new GameObject("HideAR Quest Game");
            var room = game.AddComponent<QuestRoomAccess>();
            SetReference(room, "planeManager", planes);
            SetReference(room, "session", session);
            var panelObject = new GameObject("HideAR Quest Menu", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            var panel = panelObject.AddComponent<QuestPanel>();
            panel.Build(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Inter-Regular SDF.asset"));
            panel.GetComponent<Canvas>().worldCamera = origin.Camera;
            panel.Recenter(origin.Camera);

            var controller = game.AddComponent<QuestGameController>();
            SetReference(controller, "origin", origin);
            SetReference(controller, "anchors", anchorManager);
            SetReference(controller, "planes", planes);
            SetReference(controller, "raycasts", raycasts);
            SetReference(controller, "officialPlacement", placement);
            SetReference(controller, "officialRaycaster", raycaster);
            SetReference(controller, "rawHit", raw);
            SetReference(controller, "approvedHit", approved);
            SetReference(controller, "room", room);
            SetReference(controller, "panel", panel);
            SetReference(controller, "dinosaurPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_HideAR_IP2_Prototype/Resources/LowPolyDino/dino.fbx"));
            SetReference(controller, "reticle", MakeReticle());
            SetReference(controller, "questRenderPipeline", MakePipeline());
            AddMenuDinosaur();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ValidateScene();
            Debug.Log("HideAR Quest scene created: " + ScenePath);
        }

        static ARRaycastHitEventAsset MakeEvent(string name)
        {
            var asset = ScriptableObject.CreateInstance<ARRaycastHitEventAsset>();
            AssetDatabase.CreateAsset(asset, Root + "/Settings/" + name + ".asset");
            return asset;
        }

        static GameObject MakePlane()
        {
            var plane = new GameObject("HideAR Quest Surface", typeof(ARPlane), typeof(MeshFilter),
                typeof(MeshRenderer), typeof(LineRenderer), typeof(ARPlaneMeshVisualizer));
            plane.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Surface Fill", new Color(0.22f, 0.78f, 0.60f, 0.12f));
            var line = plane.GetComponent<LineRenderer>();
            line.sharedMaterial = MakeMaterial("Surface Outline", new Color(0.25f, 0.96f, 0.71f, 0.85f));
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.008f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(plane, Root + "/Prefabs/Quest Surface.prefab");
            Object.DestroyImmediate(plane);
            return prefab;
        }

        static Material MakeMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, Root + "/Settings/" + name + ".mat");
            return material;
        }

        static GameObject MakeReticle()
        {
            var reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "Placement Marker";
            reticle.transform.localScale = new Vector3(0.085f, 0.002f, 0.085f);
            Object.DestroyImmediate(reticle.GetComponent<Collider>());
            reticle.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Placement Marker", new Color(1, 0.83f, 0.28f, 0.9f));
            reticle.SetActive(false);
            return reticle;
        }

        static UniversalRenderPipelineAsset MakePipeline()
        {
            AssetDatabase.CopyAsset("Assets/URP/URP Render Pipeline Asset.asset", PipelinePath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 8;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_SupportsTerrainHoles").boolValue = false;
            var rendererProperty = serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0);
            var originalRenderer = rendererProperty.objectReferenceValue;
            var rendererPath = Root + "/Settings/Quest Renderer.asset";
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(originalRenderer), rendererPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            rendererProperty.objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        [MenuItem("HideAR/Quest/2. Configure Android for Quest")]
        public static void ConfigureAndroid()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || general.Manager == null) throw new Exception("XR Management Android settings are missing.");
            foreach (var loader in general.Manager.activeLoaders.ToArray())
                if (loader.GetType().FullName != "UnityEngine.XR.OpenXR.OpenXRLoader")
                    XRPackageMetadataStore.RemoveLoader(general.Manager, loader.GetType().FullName, BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                throw new Exception("Could not enable the OpenXR Android loader.");
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            OpenXRFeatureSetManager.InitializeFeatureSets();
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var feature in settings.GetFeatures())
            {
                string type = feature.GetType().FullName;
                feature.enabled = type == "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature" ||
                    type == "UnityEngine.XR.OpenXR.Features.Meta.OpenXRLifeCycleFeature" ||
                    type == "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile" ||
                    type == "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile" ||
                    type == "UnityEngine.XR.OpenXR.Features.CompositionLayers.OpenXRCompositionLayersFeature" ||
                    (type.StartsWith("UnityEngine.XR.OpenXR.Features.Meta.", StringComparison.Ordinal) &&
                     new[] { "ARSessionFeature", "ARCameraFeature", "ARAnchorFeature", "ARPlaneFeature", "ARRaycastFeature" }.Contains(feature.GetType().Name));
                if (feature.GetType().Name == "MetaQuestFeature") EnableQuestDevices(feature);
                EditorUtility.SetDirty(feature);
            }
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(settings);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lingjieruan.hidear.quest");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();
            Debug.Log("Quest Android settings ready. iOS settings and the shared scene list were not changed.");
        }

        static void EnableQuestDevices(OpenXRFeature feature)
        {
            var serialized = new SerializedObject(feature);
            var devices = serialized.FindProperty("targetDevices");
            bool quest3 = false, quest3s = false;
            for (int i = 0; i < devices.arraySize; i++)
            {
                var device = devices.GetArrayElementAtIndex(i);
                var name = device.FindPropertyRelative("manifestName").stringValue;
                bool enabled = name == "eureka" || name == "quest3s";
                device.FindPropertyRelative("enabled").boolValue = enabled;
                quest3 |= name == "eureka";
                quest3s |= name == "quest3s";
            }
            if (!quest3 || !quest3s) throw new Exception("Installed OpenXR settings do not list Quest 3 and Quest 3S.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("HideAR/Quest/3. Switch to Android")]
        public static void SwitchToAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Install Android Build Support", "Unity Hub > Installs > 6000.3.21f1 > Add modules > Android Build Support, including SDK & NDK Tools and OpenJDK. Then reopen this project.", "OK");
                return;
            }
            ConfigureAndroid();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        [MenuItem("HideAR/Quest/4. Build APK")]
        public static void BuildApk() => Build(false);
        [MenuItem("HideAR/Quest/5. Build and Run on Connected Quest")]
        public static void BuildAndRun() => Build(true);

        static void Build(bool run)
        {
            if (new DriveInfo(Path.GetPathRoot(Application.dataPath)).AvailableFreeSpace < 8L * 1024 * 1024 * 1024)
                throw new BuildFailedException("Less than 8 GB of free disk space. Free at least 10-15 GB before the first Android build, then try again.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("First choose HideAR > Quest > 3. Switch to Android. Android Build Support must be installed.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureAndroid();
            if (!File.Exists(ScenePath)) CreateScene();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var originalDefault = GraphicsSettings.defaultRenderPipeline;
            var originalQuality = Enumerable.Range(0, QualitySettings.names.Length).Select(QualitySettings.GetRenderPipelineAssetAt).ToArray();
            int originalAntialiasing = QualitySettings.antiAliasing;
            var originalOrientation = PlayerSettings.defaultInterfaceOrientation;
            var originalPreloadedAssets = PlayerSettings.GetPreloadedAssets();
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory("../Builds/Quest");
            try
            {
                if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                GraphicsSettings.defaultRenderPipeline = pipeline;
                SetQualityPipelines(Enumerable.Repeat<RenderPipelineAsset>(pipeline, originalQuality.Length).ToArray());
                ValidateOpenXR();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    target = BuildTarget.Android,
                    locationPathName = "../Builds/Quest/HideAR_IP2_Quest.apk",
                    options = BuildOptions.Development | (run ? BuildOptions.AutoRunPlayer : BuildOptions.None)
                });
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Quest APK build failed. See the first Console error.");
                Debug.Log("Quest APK: " + Path.GetFullPath("../Builds/Quest/HideAR_IP2_Quest.apk"));
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = originalDefault;
                SetQualityPipelines(originalQuality);
                QualitySettings.antiAliasing = originalAntialiasing;
                PlayerSettings.defaultInterfaceOrientation = originalOrientation;
                PlayerSettings.SetPreloadedAssets(originalPreloadedAssets);
                AssetDatabase.SaveAssets();
                // Batch mode can start without an open scene. Never mask a build error while restoring the editor.
                if (originalScenes.Any(scene => scene.isLoaded) && originalScenes.Count(scene => scene.isActive) == 1)
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
            }
        }

        static void SetQualityPipelines(RenderPipelineAsset[] pipelines)
        {
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < pipelines.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelines[i];
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        [MenuItem("HideAR/Quest/Validate Quest Scene")]
        public static void ValidateScene()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) throw new Exception("Open the Quest scene first.");
            var controllers = Object.FindObjectsByType<QuestGameController>(FindObjectsSortMode.None);
            if (controllers.Length != 1) throw new Exception("Expected exactly one QuestGameController.");
            var serialized = new SerializedObject(controllers[0]);
            foreach (var name in new[] { "origin", "anchors", "planes", "raycasts", "officialPlacement", "officialRaycaster", "rawHit", "approvedHit", "room", "panel", "dinosaurPrefab", "reticle", "questRenderPipeline" })
                if (serialized.FindProperty(name).objectReferenceValue == null) throw new Exception("Missing Quest reference: " + name);
            if (Object.FindObjectsByType<ARSession>(FindObjectsSortMode.None).Length != 1) throw new Exception("Expected one ARSession.");
            if (Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length != 1) throw new Exception("Expected one active EventSystem.");
            if (Object.FindAnyObjectByType<XRUIInputModule>() == null) throw new Exception("XR UI input module is missing.");
            Debug.Log("HideAR Quest scene validation passed.");
        }

        public static void PrepareForReview()
        {
            CreateScene();
            ConfigureAndroid();
            AddMenuDinosaur();
            ValidateScene();
        }

        public static void ValidateOpenXR()
        {
            var issues = new System.Collections.Generic.List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            foreach (var issue in issues)
                Debug.LogWarning("Quest OpenXR " + (issue.error ? "ERROR: " : "recommendation: ") + issue.message);
            if (issues.Any(issue => issue.error))
                throw new BuildFailedException("Quest OpenXR validation failed. See the reported rules.");
        }

        static void AddMenuDinosaur()
        {
            var controller = Object.FindAnyObjectByType<QuestGameController>();
            var serialized = new SerializedObject(controller);
            if (serialized.FindProperty("menuDinosaur").objectReferenceValue != null) return;
            var panel = Object.FindAnyObjectByType<QuestPanel>();
            var model = serialized.FindProperty("dinosaurPrefab").objectReferenceValue as GameObject;
            var mascot = QuestDinosaur.Create(model, panel.transform, 85, false);
            mascot.name = "Menu Dinosaur";
            mascot.transform.localPosition = new Vector3(376, 221, -45);
            mascot.transform.localRotation = Quaternion.Euler(0, -30, 0);
            SetReference(controller, "menuDinosaur", mascot);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        public static void CompileAndroidScripts()
        {
            string output = "Temp/HideARQuestAndroidScripts";
            Directory.CreateDirectory(output);
            var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(
                new UnityEditor.Build.Player.ScriptCompilationSettings
                {
                    target = BuildTarget.Android,
                    group = BuildTargetGroup.Android,
                    options = UnityEditor.Build.Player.ScriptCompilationOptions.DevelopmentBuild
                }, output);
            if (result.assemblies == null || result.assemblies.Count == 0) throw new Exception("Android script compilation failed.");
            Debug.Log("HideAR Quest Android script compilation passed: " + result.assemblies.Count + " assemblies.");
        }

        public static void VerifyForReview()
        {
            PrepareForReview();
            CompileAndroidScripts();
        }

        public static void RenderPreviews()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var panel = Object.FindAnyObjectByType<QuestPanel>();
            var originalPipeline = QualitySettings.renderPipeline;
            int originalAntialiasing = QualitySettings.antiAliasing;
            var cameraObject = new GameObject("Preview Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            var camera = cameraObject.GetComponent<Camera>();
            cameraObject.transform.position = new Vector3(0, 1.6f, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.20f, 0.19f);
            camera.fieldOfView = 34;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            panel.GetComponent<Canvas>().worldCamera = camera;
            panel.Recenter(camera);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root != panel.gameObject && root != cameraObject && root.name != "Directional Light") root.SetActive(false);
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var texture = new RenderTexture(1400, 1000, 24);
            texture.Create();
            camera.targetTexture = texture;
            Directory.CreateDirectory("../Builds/Quest/Review");
            try
            {
                CapturePanel(panel, camera, texture, "01-intro", "HideAR", "A little dinosaur. A real hiding place.\nTake turns hiding and finding it in your room.", "", "Start hiding", "Set up room");
                CapturePanel(panel, camera, texture, "02-review", "Happy with this spot?", "Look around your dinosaur.\nKeep this spot, or choose a different one.", "", "Confirm hiding", "Move dinosaur");
                CapturePanel(panel, camera, texture, "03-handoff", "Your turn to find it", "Pass this headset and the controllers to the seeker.\nWhen they are ready, begin the countdown.", "", "I'm ready", "");
                CapturePanel(panel, camera, texture, "04-found", "You found it!", "One hiding place discovered.\nSwap roles and play another round.", "Your time  42.3 s", "Play again", "");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
                QualitySettings.renderPipeline = originalPipeline;
                QualitySettings.antiAliasing = originalAntialiasing;
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        static void CapturePanel(QuestPanel panel, Camera camera, RenderTexture texture, string name,
            string title, string body, string status, string primary, string secondary)
        {
            panel.Show(title, body, status, primary, secondary, true, name != "01-intro");
            Canvas.ForceUpdateCanvases();
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                if (label.isTextOverflowing) throw new Exception("Quest UI text overflow: " + label.name);
            }
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = texture;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("../Builds/Quest/Review/" + name + ".png", pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            Debug.Log("Quest UI preview rendered: " + name);
        }

        static void SetReference(Object component, string property, Object value)
        {
            var serialized = new SerializedObject(component);
            var field = serialized.FindProperty(property) ?? throw new Exception("Missing serialized field: " + property);
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
