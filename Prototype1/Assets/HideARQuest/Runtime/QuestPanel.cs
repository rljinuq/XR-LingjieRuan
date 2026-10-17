using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HideAR.Quest
{
    public enum QuestCommand { Primary, Secondary, Reset }

    public sealed class QuestPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text status;
        [SerializeField] Button primary;
        [SerializeField] Button secondary;
        [SerializeField] Button reset;
        [SerializeField] TMP_Text primaryLabel;
        [SerializeField] TMP_Text secondaryLabel;
        public event Action<QuestCommand> Command;

        void Awake()
        {
            primary.onClick.AddListener(() => Command?.Invoke(QuestCommand.Primary));
            secondary.onClick.AddListener(() => Command?.Invoke(QuestCommand.Secondary));
            reset.onClick.AddListener(() => Command?.Invoke(QuestCommand.Reset));
        }

        public void Show(string heading, string description, string detail, string mainAction,
            string otherAction, bool mainEnabled, bool showReset)
        {
            title.text = heading;
            body.text = description;
            status.text = detail;
            primary.gameObject.SetActive(!string.IsNullOrEmpty(mainAction));
            secondary.gameObject.SetActive(!string.IsNullOrEmpty(otherAction));
            primaryLabel.text = mainAction;
            secondaryLabel.text = otherAction;
            primary.interactable = mainEnabled;
            reset.gameObject.SetActive(showReset);
        }

        public void Recenter(Camera camera)
        {
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            transform.SetPositionAndRotation(camera.transform.position + forward * 1.6f + Vector3.up * 0.12f,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        // Called by the editor scene builder. The resulting Canvas and controls are saved in the scene.
        public void Build(TMP_FontAsset font)
        {
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(960, 620);
            rect.localScale = Vector3.one * 0.001f;
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.055f, 0.075f, 0.073f, 0.98f);
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(960, 620, 2);
            gameObject.layer = LayerMask.NameToLayer("UI");

            Label("Brand", "HIDEAR  /  IP2", font, 22, new Vector2(42, -28), new Vector2(680, 32), new Color(0.44f, 0.9f, 0.71f));
            title = Label("Title", "HideAR", font, 52, new Vector2(42, -80), new Vector2(876, 78), Color.white);
            body = Label("Description", "A little dinosaur. A real hiding place. Your turn to explore.", font, 30,
                new Vector2(42, -178), new Vector2(876, 154), new Color(0.89f, 0.94f, 0.91f));
            status = Label("Status", "", font, 24, new Vector2(42, -342), new Vector2(876, 60), new Color(1f, 0.83f, 0.46f));
            primary = MakeButton("Primary", font, new Vector2(42, -424), new Vector2(426, 88),
                new Color(0.43f, 0.91f, 0.69f), out primaryLabel);
            secondary = MakeButton("Secondary", font, new Vector2(492, -424), new Vector2(426, 88),
                new Color(0.75f, 0.87f, 0.97f), out secondaryLabel);
            reset = MakeButton("Reset Round", font, new Vector2(42, -538), new Vector2(260, 62),
                new Color(0.94f, 0.75f, 0.53f), out var resetLabel);
            resetLabel.text = "Reset round";
            Label("Footer", "HIDE  /  HAND OVER  /  FIND", font, 18, new Vector2(356, -552), new Vector2(562, 36), new Color(0.65f, 0.72f, 0.69f));
            Show("HideAR", "A little dinosaur. A real hiding place.\nTake turns hiding and finding it in your room.",
                "", "Start hiding", "Set up room", true, false);
        }

        TMP_Text Label(string name, string value, TMP_FontAsset font, float size, Vector2 position, Vector2 dimensions, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Place((RectTransform)go.transform, position, dimensions);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size - 4;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        Button MakeButton(string name, TMP_FontAsset font, Vector2 position, Vector2 size, Color color, out TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(transform, false);
            Place((RectTransform)go.transform, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Label(name + " Label", name, font, 28, Vector2.zero, size - new Vector2(24, 8), new Color(0.06f, 0.10f, 0.08f));
            label.transform.SetParent(go.transform, false);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 dimensions)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
        }
    }
}
