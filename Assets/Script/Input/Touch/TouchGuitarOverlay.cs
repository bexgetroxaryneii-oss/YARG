#if UNITY_IOS || YARG_TOUCH_OVERLAY
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YARG.Input.Touch
{
    /// <summary>
    /// YARG iOS port: an on-screen five-fret guitar for playing without a controller.
    /// <para>
    /// Every pad is an Input System <see cref="OnScreenButton"/>, so the overlay shows up
    /// as an ordinary virtual Gamepad. YARG's "Default Gamepad as 5F Guitar" bindings then
    /// apply as-is: frets = South/East/North/West/Left Shoulder, strum = D-pad Up/Down,
    /// star power = Select, pause = Start. Pressing any pad connects the profile bound to it.
    /// </para>
    /// <para>
    /// The pads show during gameplay and hide in the menus (which take taps directly).
    /// They are hidden rather than disabled: disabling the last on-screen control removes
    /// the virtual gamepad, which would disconnect its player. The small "PADS" button low on
    /// the left edge shows or hides them by hand.
    /// </para>
    /// </summary>
    public sealed class TouchGuitarOverlay : MonoBehaviour
    {
        private const string GAMEPLAY_SCENE = "Gameplay";
        private const int SORTING_ORDER = 30000;

        private static readonly (string Path, Color Color)[] FRETS =
        {
            ("<Gamepad>/buttonSouth",  new Color(0.15f, 0.75f, 0.25f)), // green
            ("<Gamepad>/buttonEast",   new Color(0.85f, 0.15f, 0.15f)), // red
            ("<Gamepad>/buttonNorth",  new Color(0.95f, 0.85f, 0.10f)), // yellow
            ("<Gamepad>/buttonWest",   new Color(0.15f, 0.40f, 0.90f)), // blue
            ("<Gamepad>/leftShoulder", new Color(0.95f, 0.55f, 0.10f)), // orange
        };

        private RectTransform _safeArea;
        private CanvasGroup _pads;
        private Text _toggleLabel;
        private Rect _lastSafeArea;
        private bool _visible;
        private bool _userOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[Touch Guitar Overlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<TouchGuitarOverlay>();
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            gameObject.AddComponent<GraphicRaycaster>();

            _safeArea = NewRect("Safe Area", transform);
            Stretch(_safeArea);

            BuildPads();
            BuildToggle();

            ApplySafeArea();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SetVisible(IsGameplayLoaded());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
            {
                ApplySafeArea();
            }
        }

        private void BuildPads()
        {
            var root = NewRect("Pads", _safeArea);
            Stretch(root);
            _pads = root.gameObject.AddComponent<CanvasGroup>();

            // Frets: a row along the bottom-left, held with the left hand.
            // "bottom" hugs the lower screen edge; "margin" keeps the pads off the side edges.
            const float fretSize = 150f, fretGap = 18f, margin = 30f, bottom = 8f;
            for (int i = 0; i < FRETS.Length; i++)
            {
                var pad = NewPad($"Fret {i + 1}", root, FRETS[i].Path, FRETS[i].Color);
                Anchor(pad, new Vector2(0f, 0f));
                pad.sizeDelta = new Vector2(fretSize, fretSize * 1.6f);
                pad.anchoredPosition = new Vector2(margin + i * (fretSize + fretGap), bottom);
            }

            // Strum bar: two halves on the bottom-right, tapped with the right hand.
            var strumColor = new Color(0.85f, 0.85f, 0.85f);
            var up = NewPad("Strum Up", root, "<Gamepad>/dpad/up", strumColor);
            Anchor(up, new Vector2(1f, 0f));
            up.sizeDelta = new Vector2(420f, 180f);
            up.anchoredPosition = new Vector2(-margin - 420f, bottom + 180f + 12f);

            var down = NewPad("Strum Down", root, "<Gamepad>/dpad/down", strumColor);
            Anchor(down, new Vector2(1f, 0f));
            down.sizeDelta = new Vector2(420f, 180f);
            down.anchoredPosition = new Vector2(-margin - 420f, bottom);

            // Star power above the strum bar; pause at the top centre.
            var star = NewPad("Star Power", root, "<Gamepad>/select", new Color(0.55f, 0.85f, 1f));
            Anchor(star, new Vector2(1f, 0f));
            star.sizeDelta = new Vector2(200f, 120f);
            star.anchoredPosition = new Vector2(-margin - 200f, bottom + 2 * 180f + 40f);
            AddLabel(star, "STAR");

            var pause = NewPad("Pause", root, "<Gamepad>/start", new Color(1f, 1f, 1f));
            Anchor(pause, new Vector2(0.5f, 1f));
            pause.sizeDelta = new Vector2(160f, 80f);
            pause.anchoredPosition = new Vector2(-80f, -margin - 80f);
            AddLabel(pause, "II");
        }

        private void BuildToggle()
        {
            // In the strip left of the safe area (menus are padded clear of it), so it never
            // covers a menu title or button.
            var toggle = NewRect("Pads Toggle", transform);
            // Low on the left edge, above the rounded corner.
            toggle.anchorMin = toggle.anchorMax = new Vector2(0f, 0.22f);
            toggle.pivot = new Vector2(0f, 0.5f);
            toggle.sizeDelta = new Vector2(110f, 90f);
            toggle.anchoredPosition = new Vector2(8f, 0f);

            var image = toggle.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            var button = toggle.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                _userOverride = true;
                SetVisible(!_visible);
            });
            _toggleLabel = AddLabel(toggle, "PADS");
        }

        private RectTransform NewPad(string name, Transform parent, string controlPath, Color color)
        {
            var rect = NewRect(name, parent);
            rect.gameObject.SetActive(false);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.35f);

            // Set the path while inactive so the control binds once, when it is enabled.
            var button = rect.gameObject.AddComponent<OnScreenButton>();
            button.controlPath = controlPath;

            var feedback = rect.gameObject.AddComponent<PadFeedback>();
            feedback.Image = image;
            feedback.Idle = image.color;
            feedback.Pressed = new Color(color.r, color.g, color.b, 0.8f);

            rect.gameObject.SetActive(true);
            return rect;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _pads.alpha = visible ? 1f : 0f;
            _pads.interactable = visible;
            _pads.blocksRaycasts = visible;
            _toggleLabel.text = visible ? "HIDE" : "PADS";
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GAMEPLAY_SCENE)
            {
                _userOverride = false;
                SetVisible(true);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == GAMEPLAY_SCENE && !_userOverride)
            {
                SetVisible(false);
            }
        }

        private static bool IsGameplayLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == GAMEPLAY_SCENE)
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplySafeArea()
        {
            _lastSafeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            // The bottom inset (home indicator) is ignored so the pads sit at the very bottom:
            // taps there are fine, only an upward swipe from the edge leaves the app.
            _safeArea.anchorMin = new Vector2(_lastSafeArea.xMin / Screen.width, 0f);
            _safeArea.anchorMax = new Vector2(_lastSafeArea.xMax / Screen.width, _lastSafeArea.yMax / Screen.height);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform) go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 corner)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = Vector2.zero;
        }

        private static Text AddLabel(RectTransform parent, string text)
        {
            var rect = NewRect("Label", parent);
            Stretch(rect);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        /// <summary>Brightens a pad while a finger is on it.</summary>
        private sealed class PadFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public Image Image;
            public Color Idle;
            public Color Pressed;

            public void OnPointerDown(PointerEventData eventData) => Image.color = Pressed;
            public void OnPointerUp(PointerEventData eventData) => Image.color = Idle;
        }
    }
}
#endif
