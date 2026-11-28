using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu
{
    /// <summary>
    /// YARG iOS port: touch-friendly styling added at runtime.
    /// Menu entries (main menu, game modes) get a rounded card behind them that is the
    /// tap target for the whole row, and a chevron, so they read and work as buttons.
    /// </summary>
    public static class IosTouchStyle
    {
        private const string CARD_NAME = "[Touch Card]";
        private const float CARD_WIDTH = 600f;
        private const float CARD_LEFT = -40f;
        private const float CARD_EXTRA_HEIGHT = 22f;
        private const float ENTRY_SPACING = 18f;

        public static readonly Color CardColor = new(0.04f, 0.09f, 0.17f, 0.72f);
        public static readonly Color OutlineColor = new(0.35f, 0.85f, 1f, 0.9f);

        private static Sprite _rounded;

        /// <summary>A white rounded rectangle, sliced so it stretches to any size.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded != null)
                {
                    return _rounded;
                }

                const int size = 64, radius = 20;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Distance outside the rounded corner, antialiased over one pixel.
                        float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                        float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte) (alpha * 255));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);

                _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                    0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return _rounded;
            }
        }

        /// <summary>Makes an <see cref="Image"/> a rounded card of the given colour.</summary>
        public static Image AddRoundedImage(GameObject gameObject, Color color)
        {
            var image = gameObject.GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
            return image;
        }

        /// <summary>
        /// Puts a card behind every entry of each vertical list of menu entries under
        /// <paramref name="root"/> (entries are the children of a "Menu Options" object).
        /// Safe to call more than once.
        /// </summary>
        public static void DecorateMenuEntries(Transform root)
        {
#if UNITY_IOS
            foreach (var group in root.GetComponentsInChildren<VerticalLayoutGroup>(true))
            {
                if (group.gameObject.name != "Menu Options")
                {
                    continue;
                }

                group.spacing = Mathf.Max(group.spacing, ENTRY_SPACING);
                foreach (Transform entry in group.transform)
                {
                    if (entry is RectTransform rect && entry.Find(CARD_NAME) == null)
                    {
                        AddCard(rect);
                    }
                }
            }
#endif
        }

        private static void AddCard(RectTransform entry)
        {
            // Before the card exists, so its own chevron text is not picked up.
            var label = entry.GetComponentInChildren<TextMeshProUGUI>(true);

            var card = new GameObject(CARD_NAME, typeof(RectTransform));
            var rect = (RectTransform) card.transform;
            rect.SetParent(entry, false);
            rect.SetAsFirstSibling();

            // Out of the entry's layout, a fixed width from the entry's left edge, so every
            // card is the same size whatever the length of its label.
            card.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(CARD_WIDTH, CARD_EXTRA_HEIGHT);
            rect.anchoredPosition = new Vector2(CARD_LEFT, 0f);

            // The card's image is the tap target; taps go up to the entry's button.
            AddRoundedImage(card, CardColor).raycastTarget = true;

            var chevron = new GameObject("Chevron", typeof(RectTransform));
            var chevronRect = (RectTransform) chevron.transform;
            chevronRect.SetParent(rect, false);
            chevronRect.anchorMin = new Vector2(1f, 0f);
            chevronRect.anchorMax = new Vector2(1f, 1f);
            chevronRect.pivot = new Vector2(1f, 0.5f);
            chevronRect.sizeDelta = new Vector2(60f, 0f);
            chevronRect.anchoredPosition = new Vector2(-12f, 0f);

            var text = chevron.AddComponent<TextMeshProUGUI>();
            if (label != null)
            {
                text.font = label.font;
                text.fontSize = label.fontSize * 0.8f;
            }
            text.text = ">";
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 1f, 1f, 0.75f);
            text.raycastTarget = false;
        }
    }
}
