using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

namespace HideAR.IP1
{
    public class IP1PresentationController : MonoBehaviour
    {
        enum PrototypePhase
        {
            Intro,
            HidingPlacement,
            PlacementReview,
            HiddenHandoff,
            Countdown,
            Seeking,
            Found
        }

        [SerializeField] float dinosaurHeightMeters = 0.28f;
        [SerializeField] float surfaceOffsetMeters = 0.015f;
        [SerializeField] float placementInputDelaySeconds = 0.45f;
        [SerializeField] float countdownSeconds = 3f;

        ARAnchorManager anchorManager;
        UnityEngine.XR.ARFoundation.Samples.ARPlaceAnchor placeAnchor;
        UnityEngine.XR.ARFoundation.Samples.RaycastEventController raycastController;
        ARCameraBackground cameraBackground;
        Camera arCamera;
        GameObject dinosaurPrefab;
        GameObject placedDinosaur;
        ARAnchor placedAnchor;
        PrototypePhase phase = PrototypePhase.Intro;
        bool placementInputPending;
        float placementInputEnableAt;
        float countdownStartedAt;

        readonly HashSet<ARAnchor> decoratedAnchors = new();
        readonly List<ARAnchor> extraAnchors = new();

        GUIStyle heroTitleStyle;
        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        GUIStyle smallStyle;
        GUIStyle buttonStyle;
        GUIStyle secondaryButtonStyle;
        GUIStyle panelStyle;
        GUIStyle statusStyle;
        Texture2D backgroundTexture;
        Texture2D panelTexture;
        Texture2D buttonTexture;
        Texture2D buttonPressedTexture;
        Texture2D secondaryButtonTexture;
        Texture2D statusTexture;

        void Awake()
        {
            anchorManager = FindAnyObjectByType<ARAnchorManager>();
            placeAnchor = FindAnyObjectByType<UnityEngine.XR.ARFoundation.Samples.ARPlaceAnchor>();
            raycastController = FindAnyObjectByType<UnityEngine.XR.ARFoundation.Samples.RaycastEventController>();
            arCamera = Camera.main;
            cameraBackground = arCamera != null ? arCamera.GetComponent<ARCameraBackground>() : null;
            dinosaurPrefab = Resources.Load<GameObject>("LowPolyDino/dino");

            SetPlacementInput(false);
            SetCameraView(false);
            HideTechnicalSampleUi();
        }

        void Update()
        {
            HideTechnicalSampleUi();
            UpdatePlacementInputGate();
            UpdateCountdown();
            UpdateAnchors();

            if (phase == PrototypePhase.Seeking)
                TryMarkFoundFromTap();
        }

        void UpdateAnchors()
        {
            if (anchorManager == null)
                return;

            extraAnchors.Clear();
            foreach (ARAnchor anchor in anchorManager.trackables)
            {
                if (anchor == null)
                    continue;

                if (placedAnchor != null)
                {
                    if (anchor != placedAnchor)
                        extraAnchors.Add(anchor);
                    else
                        HideAnchorDebugVisuals(anchor.gameObject);

                    continue;
                }

                if (phase == PrototypePhase.HidingPlacement)
                    DecorateAnchor(anchor);
                else
                    extraAnchors.Add(anchor);
            }

            foreach (ARAnchor extraAnchor in extraAnchors)
                anchorManager.TryRemoveAnchor(extraAnchor);
        }

        void HideTechnicalSampleUi()
        {
            SetObjectsActive(false,
                "Canvas Template",
                "Persistent Anchors",
                "Persistent Anchors UI Toggle Button",
                "Remove All Anchors Button",
                "Unsupported Banner",
                "Erase Unsupported Banner",
                "Show Unsupported Banner Button",
                "Show Erase Unsupported Banner Button");
        }

        static void SetObjectsActive(bool active, params string[] names)
        {
            foreach (string objectName in names)
            {
                GameObject found = GameObject.Find(objectName);
                if (found != null && found.activeSelf != active)
                    found.SetActive(active);
            }
        }

        void DecorateAnchor(ARAnchor anchor)
        {
            if (phase != PrototypePhase.HidingPlacement || !decoratedAnchors.Add(anchor))
                return;

            placedAnchor = anchor;
            placementInputPending = false;
            SetPlacementInput(false);
            HideAnchorDebugVisuals(anchor.gameObject);

            if (dinosaurPrefab == null)
            {
                Debug.LogError("HideAR could not load the dinosaur prefab from Resources/LowPolyDino/dino.", this);
                return;
            }

            GameObject dinosaur = Instantiate(dinosaurPrefab, anchor.transform);
            dinosaur.name = "HideAR IP1 Dinosaur";
            dinosaur.transform.localPosition = Vector3.up * surfaceOffsetMeters;
            dinosaur.transform.localRotation = Quaternion.identity;
            dinosaur.transform.localScale = Vector3.one;
            NormalizeHeight(dinosaur, dinosaurHeightMeters);
            LiftBottomToAnchorSurface(dinosaur, surfaceOffsetMeters);
            AddEasyTapCollider(dinosaur);
            placedDinosaur = dinosaur;

            phase = PrototypePhase.PlacementReview;
            SetCameraView(true);
        }

        static void HideAnchorDebugVisuals(GameObject anchorObject)
        {
            foreach (Canvas canvas in anchorObject.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);

            foreach (LineRenderer lineRenderer in anchorObject.GetComponentsInChildren<LineRenderer>(true))
                lineRenderer.enabled = false;

            foreach (Transform child in anchorObject.GetComponentsInChildren<Transform>(true))
            {
                string childName = child.name.ToLowerInvariant();
                if (childName.Contains("axis") || childName.Contains("axes") || childName.Contains("debug"))
                    child.gameObject.SetActive(false);
            }
        }

        static void NormalizeHeight(GameObject root, float targetHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                root.transform.localScale = Vector3.one * targetHeight;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float height = Mathf.Max(bounds.size.y, 0.001f);
            root.transform.localScale *= targetHeight / height;
        }

        static void LiftBottomToAnchorSurface(GameObject root, float surfaceOffset)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float bottomOffset = bounds.min.y - root.transform.position.y;
            root.transform.position -= Vector3.up * bottomOffset;
            root.transform.position += Vector3.up * surfaceOffset;
        }

        static void AddEasyTapCollider(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null)
                collider = root.AddComponent<BoxCollider>();

            if (renderers.Length == 0)
            {
                collider.center = Vector3.up * 0.15f;
                collider.size = Vector3.one * 0.45f;
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            Bounds localBounds = WorldBoundsToLocalBounds(root.transform, worldBounds);
            collider.center = localBounds.center;
            collider.size = localBounds.size * 1.65f;
        }

        static Bounds WorldBoundsToLocalBounds(Transform root, Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new(min.x, min.y, min.z),
                new(min.x, min.y, max.z),
                new(min.x, max.y, min.z),
                new(min.x, max.y, max.z),
                new(max.x, min.y, min.z),
                new(max.x, min.y, max.z),
                new(max.x, max.y, min.z),
                new(max.x, max.y, max.z)
            };

            Bounds localBounds = new(root.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                localBounds.Encapsulate(root.InverseTransformPoint(corners[i]));

            return localBounds;
        }

        void StartGame()
        {
            ClearRound();
            phase = PrototypePhase.HidingPlacement;
            SetCameraView(true);
            SchedulePlacementInput();
        }

        void MoveDinosaur()
        {
            if (phase != PrototypePhase.PlacementReview)
                return;

            ClearRound();
            phase = PrototypePhase.HidingPlacement;
            SetCameraView(true);
            SchedulePlacementInput();
        }

        void ConfirmHide()
        {
            if (phase != PrototypePhase.PlacementReview || placedDinosaur == null)
                return;

            SetPlacementInput(false);
            phase = PrototypePhase.HiddenHandoff;
            SetCameraView(false);
        }

        void StartCountdown()
        {
            if (placedDinosaur == null)
                return;

            SetPlacementInput(false);
            SetCameraView(false);
            phase = PrototypePhase.Countdown;
            countdownStartedAt = Time.unscaledTime;
        }

        void UpdateCountdown()
        {
            if (phase != PrototypePhase.Countdown)
                return;

            if (Time.unscaledTime - countdownStartedAt < countdownSeconds)
                return;

            phase = PrototypePhase.Seeking;
            SetCameraView(true);
        }

        void ReturnToIntro()
        {
            ClearRound();
            phase = PrototypePhase.Intro;
            SetCameraView(false);
        }

        void ClearRound()
        {
            placementInputPending = false;
            SetPlacementInput(false);
            placeAnchor?.RemoveAllAnchors();
            decoratedAnchors.Clear();
            placedDinosaur = null;
            placedAnchor = null;
        }

        void SchedulePlacementInput()
        {
            SetPlacementInput(false);
            placementInputPending = true;
            placementInputEnableAt = Time.unscaledTime + placementInputDelaySeconds;
        }

        void UpdatePlacementInputGate()
        {
            if (!placementInputPending || phase != PrototypePhase.HidingPlacement)
                return;

            if (Time.unscaledTime < placementInputEnableAt || IsPointerPressed())
                return;

            placementInputPending = false;
            SetPlacementInput(true);
        }

        static bool IsPointerPressed()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;

            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        void SetPlacementInput(bool isEnabled)
        {
            if (raycastController != null && raycastController.enabled != isEnabled)
                raycastController.enabled = isEnabled;
        }

        void SetCameraView(bool isVisible)
        {
            if (cameraBackground != null && cameraBackground.enabled != isVisible)
                cameraBackground.enabled = isVisible;
        }

        void TryMarkFoundFromTap()
        {
            if (placedDinosaur == null || arCamera == null)
                return;

            if (!TryGetPointerDownPosition(out Vector2 screenPosition))
                return;

            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (hitTransform != placedDinosaur.transform && !hitTransform.IsChildOf(placedDinosaur.transform))
                    continue;

                phase = PrototypePhase.Found;
                SetCameraView(false);
                return;
            }
        }

        static bool TryGetPointerDownPosition(out Vector2 position)
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        void OnGUI()
        {
            GUI.depth = -1000;
            EnsureStyles();

            if (phase == PrototypePhase.HidingPlacement ||
                phase == PrototypePhase.PlacementReview ||
                phase == PrototypePhase.Seeking)
                DrawLiveCameraHud();
            else
                DrawFullScreenPhase();
        }

        void DrawFullScreenPhase()
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backgroundTexture, ScaleMode.StretchToFill);
            Rect safeArea = GuiSafeArea();
            float width = Mathf.Min(safeArea.width - 48f, 920f);
            float x = safeArea.x + (safeArea.width - width) * 0.5f;
            Rect contentRect = new(x, safeArea.y + 54f, width, safeArea.height - 108f);

            GUILayout.BeginArea(contentRect);
            GUILayout.Label(FullScreenEyebrow(), smallStyle);
            GUILayout.Space(18f);
            GUILayout.Label(FullScreenTitle(), heroTitleStyle);
            GUILayout.Space(22f);
            GUILayout.Label(FullScreenBody(), bodyStyle);
            GUILayout.FlexibleSpace();

            string buttonLabel = FullScreenButtonLabel();
            if (!string.IsNullOrEmpty(buttonLabel) && GUILayout.Button(buttonLabel, buttonStyle, GUILayout.Height(104f)))
            {
                if (phase == PrototypePhase.Intro)
                    StartGame();
                else if (phase == PrototypePhase.HiddenHandoff)
                    StartCountdown();
                else if (phase == PrototypePhase.Found)
                    ReturnToIntro();
            }

            GUILayout.EndArea();
        }

        void DrawLiveCameraHud()
        {
            Rect safeArea = GuiSafeArea();
            float panelWidth = Mathf.Min(safeArea.width - 48f, 920f);
            float panelX = safeArea.x + (safeArea.width - panelWidth) * 0.5f;
            Rect panelRect = new(panelX, safeArea.y + 22f, panelWidth, 230f);
            GUI.Box(panelRect, GUIContent.none, panelStyle);

            bool isPlacement = phase == PrototypePhase.HidingPlacement;
            bool isReview = phase == PrototypePhase.PlacementReview;

            GUILayout.BeginArea(new Rect(panelRect.x + 28f, panelRect.y + 22f, panelRect.width - 56f, panelRect.height - 44f));
            GUILayout.Label(phase == PrototypePhase.Seeking ? "SEEK  2 / 2" : "HIDE  1 / 2", smallStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                isPlacement ? "Choose a hiding spot" : isReview ? "Check the hiding spot" : "Find the dinosaur",
                titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                isPlacement
                    ? "Move your phone slowly, then tap a detected surface."
                    : isReview
                        ? "Walk around and check the dinosaur. Move it or confirm when you are happy."
                        : "Move around the room. Tap the dinosaur when you see it.",
                bodyStyle);
            GUILayout.EndArea();

            if (isPlacement)
            {
                Rect statusRect = new(panelX, safeArea.yMax - 104f, panelWidth, 82f);
                GUI.Box(statusRect, placementInputPending ? "Starting camera..." : "Tap the surface to hide", statusStyle);
            }
            else if (isReview)
            {
                const float gap = 16f;
                float buttonWidth = (panelWidth - gap) * 0.5f;
                float buttonY = safeArea.yMax - 116f;
                Rect moveRect = new(panelX, buttonY, buttonWidth, 94f);
                Rect confirmRect = new(panelX + buttonWidth + gap, buttonY, buttonWidth, 94f);

                if (GUI.Button(moveRect, "Move Dinosaur", secondaryButtonStyle))
                    MoveDinosaur();

                if (GUI.Button(confirmRect, "Confirm Hide", buttonStyle))
                    ConfirmHide();
            }
        }

        string FullScreenEyebrow()
        {
            return phase switch
            {
                PrototypePhase.Intro => "HIDEAR",
                PrototypePhase.HiddenHandoff => "HIDDEN",
                PrototypePhase.Countdown => "GET READY",
                PrototypePhase.Found => "FOUND",
                _ => string.Empty
            };
        }

        string FullScreenTitle()
        {
            if (phase == PrototypePhase.Countdown)
            {
                float remaining = countdownSeconds - (Time.unscaledTime - countdownStartedAt);
                return Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 3).ToString();
            }

            return phase switch
            {
                PrototypePhase.Intro => "Hide it. Then find it.",
                PrototypePhase.HiddenHandoff => "The dinosaur is hidden.",
                PrototypePhase.Found => "You found it!",
                _ => "HideAR"
            };
        }

        string FullScreenBody()
        {
            return phase switch
            {
                PrototypePhase.Intro => "One player hides a dinosaur in the real room. The next player searches for it through AR.",
                PrototypePhase.HiddenHandoff => "Pass the phone to the seeker without revealing the hiding place.",
                PrototypePhase.Countdown => "The camera will open when the search begins.",
                PrototypePhase.Found => "Great search. The hidden dinosaur has been found.",
                _ => string.Empty
            };
        }

        string FullScreenButtonLabel()
        {
            return phase switch
            {
                PrototypePhase.Intro => "Start Game",
                PrototypePhase.HiddenHandoff => "Ready to Seek",
                PrototypePhase.Found => "Play Again",
                _ => string.Empty
            };
        }

        static Rect GuiSafeArea()
        {
            Rect safeArea = Screen.safeArea;
            return new Rect(safeArea.x, Screen.height - safeArea.yMax, safeArea.width, safeArea.height);
        }

        void EnsureStyles()
        {
            if (heroTitleStyle != null)
                return;

            backgroundTexture = MakeTexture(new Color(0.035f, 0.045f, 0.065f, 1f));
            panelTexture = MakeTexture(new Color(0.035f, 0.045f, 0.065f, 0.88f));
            buttonTexture = MakeTexture(new Color(0.42f, 0.95f, 0.72f, 1f));
            buttonPressedTexture = MakeTexture(new Color(1f, 0.42f, 0.36f, 1f));
            secondaryButtonTexture = MakeTexture(new Color(0.95f, 0.96f, 0.98f, 0.96f));
            statusTexture = MakeTexture(new Color(0.95f, 0.96f, 0.98f, 0.94f));

            heroTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.09f, 54f, 88f)),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.052f, 34f, 52f)),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.033f, 22f, 34f)),
                normal = { textColor = new Color(0.9f, 0.93f, 0.97f, 1f) },
                wordWrap = true
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.025f, 18f, 26f)),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.42f, 0.95f, 0.72f, 1f) },
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.043f, 30f, 44f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = buttonTexture,
                    textColor = new Color(0.025f, 0.07f, 0.055f, 1f)
                },
                active =
                {
                    background = buttonPressedTexture,
                    textColor = Color.white
                },
                hover =
                {
                    background = buttonTexture,
                    textColor = new Color(0.025f, 0.07f, 0.055f, 1f)
                }
            };

            secondaryButtonStyle = new GUIStyle(buttonStyle)
            {
                normal =
                {
                    background = secondaryButtonTexture,
                    textColor = new Color(0.035f, 0.045f, 0.065f, 1f)
                },
                hover =
                {
                    background = secondaryButtonTexture,
                    textColor = new Color(0.035f, 0.045f, 0.065f, 1f)
                }
            };

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTexture }
            };

            statusStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.032f, 22f, 32f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = statusTexture,
                    textColor = new Color(0.035f, 0.045f, 0.065f, 1f)
                }
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
