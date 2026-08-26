#if UNITY_EDITOR
using HideAR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace HideAR.Editor
{
    [InitializeOnLoad]
    public static class HideARAutoSceneRepair
    {
        static HideARAutoSceneRepair()
        {
            EditorApplication.delayCall += RepairOpenArScene;
        }

        static void RepairOpenArScene()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>();
            HideARARPrototypeController controller = Object.FindFirstObjectByType<HideARARPrototypeController>();
            if (xrOrigin == null || controller == null)
            {
                return;
            }

            ARRaycastManager raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
            if (raycastManager == null)
            {
                raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
            }

            ARPlaneManager planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            if (planeManager == null)
            {
                planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
            }

            ARAnchorManager anchorManager = xrOrigin.GetComponent<ARAnchorManager>();
            if (anchorManager == null)
            {
                anchorManager = xrOrigin.gameObject.AddComponent<ARAnchorManager>();
            }

            raycastManager.enabled = true;
            planeManager.enabled = true;
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            anchorManager.enabled = true;

            Camera camera = xrOrigin.Camera != null ? xrOrigin.Camera : Camera.main;
            ARCameraBackground cameraBackground = camera != null ? camera.GetComponent<ARCameraBackground>() : null;
            if (camera != null && cameraBackground == null)
            {
                cameraBackground = camera.gameObject.AddComponent<ARCameraBackground>();
            }

            if (camera != null)
            {
                TrackedPoseDriver poseDriver = camera.GetComponent<TrackedPoseDriver>();
                if (poseDriver == null)
                {
                    poseDriver = camera.gameObject.AddComponent<TrackedPoseDriver>();
                }

                ConfigureTrackedPoseDriver(poseDriver);
                camera.transform.localPosition = Vector3.zero;
                camera.transform.localRotation = Quaternion.identity;
            }

            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("raycastManager").objectReferenceValue = raycastManager;
            serializedController.FindProperty("planeManager").objectReferenceValue = planeManager;
            serializedController.FindProperty("anchorManager").objectReferenceValue = anchorManager;
            serializedController.FindProperty("arSession").objectReferenceValue = Object.FindFirstObjectByType<ARSession>();
            serializedController.FindProperty("arCamera").objectReferenceValue = camera;
            serializedController.FindProperty("cameraBackground").objectReferenceValue = cameraBackground;
            serializedController.FindProperty("characterPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HideAR/Resources/LowPolyDino/dino.fbx");
            serializedController.FindProperty("placedScale").floatValue = 0.28f;
            serializedController.FindProperty("surfaceOffset").floatValue = 0.015f;
            serializedController.FindProperty("showDetectedPlanes").boolValue = true;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HideAR auto-repaired the AR scene: plane raycast, anchor placement, camera background, and character model are connected.");
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
