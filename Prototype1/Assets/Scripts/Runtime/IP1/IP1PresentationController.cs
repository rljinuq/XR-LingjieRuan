using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HideAR.IP1
{
    public class IP1PresentationController : MonoBehaviour
    {
        [SerializeField] float dinosaurHeightMeters = 0.28f;
        [SerializeField] float surfaceOffsetMeters = 0.015f;

        ARAnchorManager anchorManager;
        UnityEngine.XR.ARFoundation.Samples.ARPlaceAnchor placeAnchor;
        GameObject dinosaurPrefab;
        readonly HashSet<ARAnchor> decoratedAnchors = new();

        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        GUIStyle smallStyle;
        GUIStyle buttonStyle;
        GUIStyle panelStyle;
        Texture2D panelTexture;

        void Awake()
        {
            anchorManager = FindAnyObjectByType<ARAnchorManager>();
            placeAnchor = FindAnyObjectByType<UnityEngine.XR.ARFoundation.Samples.ARPlaceAnchor>();
            dinosaurPrefab = Resources.Load<GameObject>("LowPolyDino/dino");
            HideTechnicalSampleUi();
        }

        void Update()
        {
            HideTechnicalSampleUi();

            if (anchorManager == null)
                return;

            foreach (var anchor in anchorManager.trackables)
            {
                if (anchor != null)
                    DecorateAnchor(anchor);
            }
        }

        void HideTechnicalSampleUi()
        {
            SetObjectsActive(false,
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
            if (!decoratedAnchors.Add(anchor))
                return;

            HideAnchorDebugVisuals(anchor.gameObject);

            if (dinosaurPrefab == null)
                return;

            GameObject dinosaur = Instantiate(dinosaurPrefab, anchor.transform);
            dinosaur.name = "HideAR IP1 Dinosaur";
            dinosaur.transform.localPosition = Vector3.up * surfaceOffsetMeters;
            dinosaur.transform.localRotation = Quaternion.identity;
            dinosaur.transform.localScale = Vector3.one;
            NormalizeHeight(dinosaur, dinosaurHeightMeters);
            LiftBottomToAnchorSurface(dinosaur, surfaceOffsetMeters);
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

        void OnGUI()
        {
            EnsureStyles();

            float safeTop = Screen.safeArea.yMax < Screen.height ? Screen.height - Screen.safeArea.yMax : 0f;
            Rect panelRect = new(24f, Mathf.Max(24f, safeTop + 18f), Mathf.Min(Screen.width - 48f, 820f), 255f);
            GUI.Box(panelRect, GUIContent.none, panelStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 24f, panelRect.y + 18f, panelRect.width - 48f, panelRect.height - 36f));
            GUILayout.Label("HideAR IP1", titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Place a tiny hidden dinosaur into the real room as a first test of shared AR hiding.", bodyStyle);
            GUILayout.Space(10f);
            GUILayout.Label(InstructionText(), bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label(anchorManager != null ? $"Placed anchors: {anchorManager.trackables.count}" : "Waiting for AR anchors...", smallStyle);
            GUILayout.EndArea();

            Rect resetRect = new(Screen.width - 220f, Screen.height - 112f, 190f, 72f);
            if (GUI.Button(resetRect, "Reset", buttonStyle))
            {
                placeAnchor?.RemoveAllAnchors();
                decoratedAnchors.Clear();
            }
        }

        string InstructionText()
        {
            if (anchorManager != null && anchorManager.trackables.count > 0)
                return "Dinosaur placed. Move your phone away and back to check that it stays in the real spot.";

            return "Move your phone to scan the surface. Tap the surface to place.";
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            panelTexture = MakeTexture(new Color(0f, 0f, 0f, 0.68f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.06f, 34f, 56f)),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.034f, 22f, 34f)),
                normal = { textColor = Color.white },
                wordWrap = true
            };

            smallStyle = new GUIStyle(bodyStyle)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.028f, 18f, 28f)),
                normal = { textColor = new Color(0.82f, 0.92f, 1f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.038f, 24f, 36f)),
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
