using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace HideAR
{
    public class HideARARPrototypeController : MonoBehaviour
    {
        enum Mode
        {
            Scanning,
            Placed
        }

        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] ARSession arSession;
        [SerializeField] Camera arCamera;
        [SerializeField] ARCameraBackground cameraBackground;
        [SerializeField] GameObject characterPrefab;
        [SerializeField] float placedScale = 0.28f;
        [SerializeField] float surfaceOffset = 0.015f;
        [SerializeField] bool showDetectedPlanes = true;

        static readonly List<ARRaycastHit> Hits = new();

        Mode mode = Mode.Scanning;
        GameObject placedRoot;
        GameObject placedCharacter;
        bool isPlacing;
        bool surfaceReady;
        int detectedPlaneCount;
        string statusMessage;
        Vector3 lastCameraPosition;
        float cameraTravelDistance;

        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        GUIStyle buttonStyle;
        GUIStyle panelStyle;
        Texture2D panelTexture;
        Material planeDebugMaterial;
        Material planeOutlineMaterial;

        const float UiTopHeight = 245f;
        const float UiBottomHeight = 135f;

        void Awake()
        {
            raycastManager ??= FindFirstObjectByType<ARRaycastManager>();
            planeManager ??= FindFirstObjectByType<ARPlaneManager>();
            anchorManager ??= FindFirstObjectByType<ARAnchorManager>();
            arSession ??= FindFirstObjectByType<ARSession>();
            arCamera ??= Camera.main;
            cameraBackground ??= arCamera != null ? arCamera.GetComponent<ARCameraBackground>() : null;
            ConfigureTrackedPoseDriver();

            if (raycastManager != null)
            {
                raycastManager.enabled = true;
            }

            if (planeManager != null)
            {
                planeManager.enabled = true;
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            }

            if (anchorManager != null)
            {
                anchorManager.enabled = true;
            }

            if (cameraBackground != null)
            {
                cameraBackground.enabled = true;
            }

            characterPrefab ??= Resources.Load<GameObject>("LowPolyDino/dino");
            lastCameraPosition = arCamera != null ? arCamera.transform.position : Vector3.zero;
            statusMessage = "Move slowly over a table or floor. Tap the real surface to place the character.";
        }

        void ConfigureTrackedPoseDriver()
        {
            if (arCamera == null)
            {
                return;
            }

            TrackedPoseDriver poseDriver = arCamera.GetComponent<TrackedPoseDriver>();
            if (poseDriver == null)
            {
                poseDriver = arCamera.gameObject.AddComponent<TrackedPoseDriver>();
            }

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

        void Update()
        {
            if (mode == Mode.Scanning)
            {
                UpdateSurfacePreview();
            }

            UpdateCameraTravelDebug();
            UpdatePlaneVisualizers();

            if (TryGetPressPosition(out Vector2 pressPosition))
            {
                if (mode == Mode.Scanning)
                {
                    TryPlaceCharacter(pressPosition);
                }
            }

        }

        void UpdateCameraTravelDebug()
        {
            if (arCamera == null)
            {
                return;
            }

            Vector3 cameraPosition = arCamera.transform.position;
            cameraTravelDistance += Vector3.Distance(lastCameraPosition, cameraPosition);
            lastCameraPosition = cameraPosition;
        }

        void UpdateSurfacePreview()
        {
            surfaceReady = false;
            detectedPlaneCount = 0;

            if (planeManager == null)
            {
                return;
            }

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null || plane.trackingState == TrackingState.None)
                {
                    continue;
                }

                detectedPlaneCount++;
            }

            surfaceReady = detectedPlaneCount > 0;
        }

        bool TryGetPressPosition(out Vector2 position)
        {
            position = default;

            if (Input.touchCount > 0)
            {
                UnityEngine.Touch touch = Input.GetTouch(0);
                if (touch.phase == UnityEngine.TouchPhase.Began)
                {
                    position = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }

            return false;
        }

        void TryPlaceCharacter(Vector2 screenPosition)
        {
            if (isPlacing)
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (!TryGetPlacementHit(screenPosition, out ARRaycastHit hit, out ARPlane hitPlane))
            {
                statusMessage = "Tap directly on a detected table or floor plane.";
                return;
            }

            ResetPlacedObject();

            isPlacing = true;
            Pose anchorPose = new(hit.pose.position, hit.pose.rotation);

            try
            {
                if (anchorManager.descriptor == null || !anchorManager.descriptor.supportsTrackableAttachments)
                {
                    statusMessage = "This AR provider is not ready for plane-attached anchors yet. Wait a moment and tap again.";
                    return;
                }

                ARAnchor anchor = anchorManager.AttachAnchor(hitPlane, anchorPose);

                if (anchor == null)
                {
                    statusMessage = "Could not create a plane anchor. Move slowly, then tap the plane again.";
                    return;
                }

                placedRoot = anchor.gameObject;
                placedRoot.name = "HideAR Attached Plane Anchor";

                placedCharacter = characterPrefab != null
                    ? Instantiate(characterPrefab, placedRoot.transform)
                    : CreateFallbackCharacter(placedRoot.transform);

                placedCharacter.name = "HideAR Placed Dinosaur";
                placedCharacter.transform.localPosition = Vector3.zero;
                placedCharacter.transform.localRotation = Quaternion.identity;
                placedCharacter.transform.localScale = Vector3.one;

                ApplyReadableMaterial(placedCharacter);
                NormalizeCharacterSize(placedCharacter, placedScale);
                placedCharacter.transform.localPosition += Vector3.up * surfaceOffset;

                mode = Mode.Placed;
                statusMessage = "Placed on an attached AR plane anchor. Move the phone; it should stay on the real spot.";
            }
            catch (System.Exception exception)
            {
                statusMessage = $"Anchor failed: {exception.GetType().Name}. Tap the surface again.";
                ResetPlacedObject();
            }
            finally
            {
                isPlacing = false;
            }
        }

        void ResetAll()
        {
            ResetPlacedObject();
            arSession?.Reset();
            cameraTravelDistance = 0f;
            lastCameraPosition = arCamera != null ? arCamera.transform.position : Vector3.zero;
            mode = Mode.Scanning;
            statusMessage = "Move slowly over a table or floor. Tap the real surface to place the character.";
        }

        void ResetPlacedObject()
        {
            if (placedRoot != null)
            {
                Destroy(placedRoot);
            }

            placedRoot = null;
            placedCharacter = null;
            surfaceReady = false;
            detectedPlaneCount = 0;
        }

        bool TryGetPlacementHit(Vector2 screenPosition, out ARRaycastHit hit, out ARPlane hitPlane)
        {
            hit = default;
            hitPlane = null;

            if (raycastManager == null || planeManager == null || anchorManager == null)
            {
                statusMessage = "Missing AR managers in the running scene.";
                return false;
            }

            if (!raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            {
                return false;
            }

            foreach (ARRaycastHit candidate in Hits)
            {
                ARPlane plane = planeManager.GetPlane(candidate.trackableId);
                if (plane == null || plane.trackingState == TrackingState.None)
                {
                    continue;
                }

                hit = candidate;
                hitPlane = plane;
                return true;
            }

            return false;
        }

        void UpdatePlaneVisualizers()
        {
            if (!showDetectedPlanes || planeManager == null)
            {
                return;
            }

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null)
                {
                    continue;
                }

                EnsurePlaneVisualizer(plane);
            }
        }

        void EnsurePlaneVisualizer(ARPlane plane)
        {
            GameObject planeObject = plane.gameObject;

            if (planeObject.GetComponent<MeshFilter>() == null)
            {
                planeObject.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = planeObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = planeObject.AddComponent<MeshRenderer>();
            }

            meshRenderer.sharedMaterial = PlaneDebugMaterial;

            LineRenderer lineRenderer = planeObject.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = planeObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.loop = true;
                lineRenderer.widthMultiplier = 0.018f;
            }

            lineRenderer.sharedMaterial = PlaneOutlineMaterial;
            lineRenderer.startColor = new Color(0.05f, 0.85f, 1f, 1f);
            lineRenderer.endColor = new Color(0.05f, 0.85f, 1f, 1f);

            ARPlaneMeshVisualizer visualizer = planeObject.GetComponent<ARPlaneMeshVisualizer>();
            if (visualizer == null)
            {
                visualizer = planeObject.AddComponent<ARPlaneMeshVisualizer>();
                visualizer.trackingStateVisibilityThreshold = TrackingState.Limited;
            }

            visualizer.hideSubsumed = true;
        }

        Material PlaneDebugMaterial
        {
            get
            {
                if (planeDebugMaterial != null)
                {
                    return planeDebugMaterial;
                }

                planeDebugMaterial = CreateDebugMaterial(new Color(0.05f, 0.85f, 1f, 0.18f));
                return planeDebugMaterial;
            }
        }

        Material PlaneOutlineMaterial
        {
            get
            {
                if (planeOutlineMaterial != null)
                {
                    return planeOutlineMaterial;
                }

                planeOutlineMaterial = CreateDebugMaterial(new Color(0.05f, 0.85f, 1f, 1f));
                return planeOutlineMaterial;
            }
        }

        static Material CreateDebugMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            Material material = new(shader)
            {
                color = color,
                renderQueue = 3000
            };
            return material;
        }

        static void ApplyReadableMaterial(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material fallbackMaterial = new(shader)
            {
                color = new Color(0.12f, 0.86f, 0.58f, 1f)
            };

            foreach (Renderer renderer in renderers)
            {
                if (renderer.sharedMaterial == null)
                {
                    renderer.sharedMaterial = fallbackMaterial;
                }
            }
        }

        static void NormalizeCharacterSize(GameObject root, float targetHeightMeters)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                root.transform.localScale = Vector3.one * targetHeightMeters;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float currentHeight = Mathf.Max(bounds.size.y, 0.001f);
            float scale = targetHeightMeters / currentHeight;
            root.transform.localScale *= scale;

            Bounds scaledBounds = root.GetComponentsInChildren<Renderer>()[0].bounds;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                scaledBounds.Encapsulate(renderer.bounds);
            }

            float bottomOffset = scaledBounds.min.y - root.transform.position.y;
            root.transform.position -= Vector3.up * bottomOffset;
        }

        static GameObject CreateFallbackCharacter(Transform parent)
        {
            GameObject root = new("Temporary Character");
            root.transform.SetParent(parent);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            head.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);

            return root;
        }

        bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (Input.touchCount > 0)
            {
                return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        void OnGUI()
        {
            EnsureStyles();

            float safeTop = Screen.safeArea.yMax < Screen.height ? Screen.height - Screen.safeArea.yMax : 0f;
            Rect panelRect = new(24f, Mathf.Max(24f, safeTop + 18f), Mathf.Min(Screen.width - 48f, 760f), UiTopHeight - 28f);
            GUI.Box(panelRect, GUIContent.none, panelStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 18f, panelRect.width - 48f, panelRect.height - 36f));
            GUILayout.Label("HideAR IP1", titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label(StatusLine(), bodyStyle);
            GUILayout.Space(8f);
            GUILayout.Label(statusMessage, bodyStyle);
            GUILayout.Space(8f);
            GUILayout.Label(DebugLine(), bodyStyle);
            GUILayout.EndArea();

            Rect resetRect = new(Screen.width - 220f, Screen.height - UiBottomHeight + 28f, 180f, 70f);
            if (GUI.Button(resetRect, "Reset", buttonStyle))
            {
                ResetAll();
            }
        }

        string StatusLine()
        {
            return mode switch
            {
                Mode.Scanning => surfaceReady ? $"Surface ready ({detectedPlaneCount}) -> tap inside the blue plane" : "Scanning surfaces...",
                Mode.Placed => "Placed on AR plane anchor",
                _ => string.Empty
            };
        }

        string DebugLine()
        {
            string requestedMode = planeManager != null ? planeManager.requestedDetectionMode.ToString() : "no plane mgr";
            string currentMode = planeManager != null ? planeManager.currentDetectionMode.ToString() : "no plane mgr";
            string planeSubsystem = planeManager != null && planeManager.subsystem != null ? "planeSub OK" : "planeSub NULL";
            string raySubsystem = raycastManager != null && raycastManager.subsystem != null ? "raySub OK" : "raySub NULL";
            string anchorSubsystem = anchorManager != null && anchorManager.subsystem != null ? "anchorSub OK" : "anchorSub NULL";
            string cameraMotion = arCamera != null ? $"camMove {cameraTravelDistance:0.00}m" : "no camera";

            return $"AR {ARSession.state} | planes {detectedPlaneCount} | req {requestedMode} cur {currentMode} | {planeSubsystem} {raySubsystem} {anchorSubsystem} | {cameraMotion}";
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            panelTexture = MakeTexture(new Color(0f, 0f, 0f, 0.68f));
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.055f, 30f, 52f)),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.036f, 22f, 34f)),
                normal = { textColor = Color.white },
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.04f, 24f, 38f)),
                fontStyle = FontStyle.Bold
            };

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTexture }
            };
        }

        static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
