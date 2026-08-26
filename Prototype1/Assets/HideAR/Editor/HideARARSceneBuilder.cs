#if UNITY_EDITOR
using HideAR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace HideAR.Editor
{
    public static class HideARARSceneBuilder
    {
        public const string ScenePath = "Assets/START_HERE_HideAR_AR.unity";

        [MenuItem("HideAR/Build Real AR IP1 Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "START_HERE_HideAR_AR";

            GameObject sessionObject = new("AR Session");
            sessionObject.AddComponent<ARSession>();
            sessionObject.AddComponent<ARInputManager>();

            GameObject originObject = new("XR Origin");
            XROrigin xrOrigin = originObject.AddComponent<XROrigin>();
            ARPlaneManager planeManager = originObject.AddComponent<ARPlaneManager>();
            ARRaycastManager raycastManager = originObject.AddComponent<ARRaycastManager>();
            ARAnchorManager anchorManager = originObject.AddComponent<ARAnchorManager>();
            originObject.AddComponent<ARPointCloudManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

            GameObject cameraOffset = new("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform);
            xrOrigin.CameraFloorOffsetObject = cameraOffset;

            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 40f;
            cameraObject.AddComponent<AudioListener>();
            ConfigureTrackedPoseDriver(cameraObject.AddComponent<TrackedPoseDriver>());
            cameraObject.AddComponent<ARCameraManager>();
            ARCameraBackground cameraBackground = cameraObject.AddComponent<ARCameraBackground>();
            xrOrigin.Camera = camera;

            GameObject previewSurface = CreatePreviewSurface();
            previewSurface.SetActive(true);

            GameObject controllerObject = new("HideAR Real AR Controller");
            HideARARPrototypeController controller = controllerObject.AddComponent<HideARARPrototypeController>();
            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("raycastManager").objectReferenceValue = raycastManager;
            serializedController.FindProperty("planeManager").objectReferenceValue = planeManager;
            serializedController.FindProperty("anchorManager").objectReferenceValue = anchorManager;
            serializedController.FindProperty("arSession").objectReferenceValue = sessionObject.GetComponent<ARSession>();
            serializedController.FindProperty("arCamera").objectReferenceValue = camera;
            serializedController.FindProperty("cameraBackground").objectReferenceValue = cameraBackground;
            serializedController.FindProperty("characterPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HideAR/Resources/LowPolyDino/dino.fbx");
            serializedController.FindProperty("placedScale").floatValue = 0.28f;
            serializedController.FindProperty("surfaceOffset").floatValue = 0.015f;
            serializedController.FindProperty("showDetectedPlanes").boolValue = true;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject light = new("AR Preview Directional Light");
            Light directionalLight = light.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            GameObject eventSystem = new("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeObject = controllerObject;
            Debug.Log($"HideAR real AR IP1 scene created: {ScenePath}");
        }

        static GameObject CreatePreviewSurface()
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "Editor Preview Surface - phone uses real detected planes";
            surface.transform.position = Vector3.zero;
            surface.transform.localScale = new Vector3(1.8f, 1f, 1.8f);
            surface.AddComponent<HideAREditorOnlyObject>();

            Renderer renderer = surface.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
                if (material.shader == null)
                {
                    material = new Material(Shader.Find("Standard"));
                }

                material.color = new Color(0.13f, 0.14f, 0.15f, 1f);
                renderer.sharedMaterial = material;
            }

            return surface;
        }

        static void ConfigureTrackedPoseDriver(TrackedPoseDriver poseDriver)
        {
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            poseDriver.ignoreTrackingState = false;
            poseDriver.positionInput = CreatePoseAction("Position", "Vector3", "<XRHMD>/centerEyePosition");
            poseDriver.rotationInput = CreatePoseAction("Rotation", "Quaternion", "<XRHMD>/centerEyeRotation");
            poseDriver.trackingStateInput = CreatePoseAction("Tracking State", "Integer", "<XRHMD>/trackingState");
        }

        static InputActionProperty CreatePoseAction(string name, string expectedControlType, string binding)
        {
            return new InputActionProperty(new InputAction(
                name,
                InputActionType.PassThrough,
                binding,
                expectedControlType: expectedControlType));
        }
    }
}
#endif
