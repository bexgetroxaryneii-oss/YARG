using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YARG.Menu
{
    /// <summary>
    /// YARG iOS port: fits every screen-space menu canvas to the phone.
    /// <list type="bullet">
    /// <item>Content is pulled in horizontally to the safe area, clear of the Dynamic Island
    /// and the rounded corners. Full-screen backgrounds are left edge to edge, so the game
    /// still fills the whole screen.</item>
    /// <item>Menus are drawn at the "Menu Size" setting (bigger text and bigger touch targets).</item>
    /// </list>
    /// Canvases are found as they appear (scene loads, pause menu, dialogs), and refitted when
    /// the phone is turned around (the island swaps sides) or the setting changes.
    /// </summary>
    public sealed class IosMenuLayout : MonoBehaviour
    {
        public const float DEFAULT_SCALE = 1.1f;
        private const float MIN_SCALE = 1f, MAX_SCALE = 1.4f;
        private const float SCAN_INTERVAL = 0.5f;
        private const int MAX_CONTAINER_DEPTH = 3;
        private const string TOUCH_OVERLAY_NAME = "[Touch Guitar Overlay]";

        private static IosMenuLayout _instance;
        private static float _scale = DEFAULT_SCALE;

        private sealed class CanvasState
        {
            public CanvasScaler Scaler;
            public Vector2 ReferenceResolution;
            public float ScaleFactor;
            public readonly List<(RectTransform Rect, Vector2 Min, Vector2 Max)> Fitted = new();
            public Rect AppliedSafeArea;
            public float AppliedScale;
        }

        private readonly Dictionary<Canvas, CanvasState> _canvases = new();
        private readonly List<Canvas> _dead = new();
        private float _nextScan;

        /// <summary>Menu size as a multiplier (1 = YARG's desktop size).</summary>
        public static void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, MIN_SCALE, MAX_SCALE);
            if (_instance != null)
            {
                _instance.RefitAll();
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[iOS Menu Layout]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<IosMenuLayout>();
        }
#endif

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _nextScan = 0f;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + SCAN_INTERVAL;
                Scan();
            }

            var safeArea = Screen.safeArea;
            foreach (var (canvas, state) in _canvases)
            {
                if (canvas == null)
                {
                    _dead.Add(canvas);
                    continue;
                }

                if (state.AppliedSafeArea != safeArea || !Mathf.Approximately(state.AppliedScale, _scale))
                {
                    Apply(state, safeArea);
                }
            }

            foreach (var canvas in _dead)
            {
                _canvases.Remove(canvas);
            }
            _dead.Clear();
        }

        private void Scan()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (_canvases.ContainsKey(canvas) || !IsMenuCanvas(canvas))
                {
                    continue;
                }

                var state = new CanvasState { Scaler = canvas.GetComponent<CanvasScaler>() };
                if (state.Scaler != null)
                {
                    state.ReferenceResolution = state.Scaler.referenceResolution;
                    state.ScaleFactor = state.Scaler.scaleFactor;
                }

                CollectFitted((RectTransform) canvas.transform, state, 0);
                _canvases.Add(canvas, state);
                Apply(state, Screen.safeArea);
            }
        }

        private void RefitAll()
        {
            foreach (var (canvas, state) in _canvases)
            {
                if (canvas != null)
                {
                    Apply(state, Screen.safeArea);
                }
            }
        }

        private static bool IsMenuCanvas(Canvas canvas)
        {
            if (canvas.renderMode == RenderMode.WorldSpace || canvas.transform is not RectTransform)
            {
                return false;
            }

            // Only root canvases: nested ones move with their parent.
            var parent = canvas.transform.parent;
            if (parent != null && parent.GetComponentInParent<Canvas>(true) != null)
            {
                return false;
            }

            // The touch pads fit themselves to the safe area.
            return canvas.gameObject.name != TOUCH_OVERLAY_NAME;
        }

        /// <summary>
        /// Picks the rects to pull into the safe area: everything under the canvas, except
        /// full-screen backgrounds. Full-screen containers with no graphic of their own are
        /// looked through, so a background inside them stays full-screen too.
        /// </summary>
        private static void CollectFitted(RectTransform parent, CanvasState state, int depth)
        {
            foreach (Transform child in parent)
            {
                if (child is not RectTransform rect)
                {
                    continue;
                }

                if (IsFullScreen(rect))
                {
                    bool hasGraphic = rect.GetComponent<Graphic>() != null;
                    if (hasGraphic && IsBackground(rect))
                    {
                        continue;
                    }

                    if (!hasGraphic && depth < MAX_CONTAINER_DEPTH && rect.GetComponent<ScrollRect>() == null)
                    {
                        CollectFitted(rect, state, depth + 1);
                        continue;
                    }
                }

                state.Fitted.Add((rect, rect.anchorMin, rect.anchorMax));
            }
        }

        private static bool IsFullScreen(RectTransform rect)
        {
            return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                rect.offsetMin.sqrMagnitude < 1f && rect.offsetMax.sqrMagnitude < 1f;
        }

        private static bool IsBackground(RectTransform rect)
        {
            if (rect.childCount == 0)
            {
                return true;
            }

            string name = rect.name.ToLowerInvariant();
            return name.Contains("background") || name.Contains("blur") || name == "bg" || name.Contains("dim");
        }

        private static void Apply(CanvasState state, Rect safeArea)
        {
            state.AppliedSafeArea = safeArea;
            state.AppliedScale = _scale;

            if (state.Scaler != null)
            {
                if (state.Scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    state.Scaler.referenceResolution = state.ReferenceResolution / _scale;
                }
                else if (state.Scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                {
                    state.Scaler.scaleFactor = state.ScaleFactor * _scale;
                }
            }

            // Horizontal only: the island and the corners are on the sides in landscape.
            float left = 0f, right = 1f;
            if (Screen.width > 0)
            {
                left = safeArea.xMin / Screen.width;
                right = safeArea.xMax / Screen.width;
            }

            foreach (var (rect, min, max) in state.Fitted)
            {
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(Mathf.Lerp(left, right, min.x), min.y);
                rect.anchorMax = new Vector2(Mathf.Lerp(left, right, max.x), max.y);
            }
        }
    }
}
