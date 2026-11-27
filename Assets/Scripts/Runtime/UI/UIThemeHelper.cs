using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.UI
{
    public static class UIThemeHelper
    {
        public static readonly Color PanelDark = new Color(0.08f, 0.10f, 0.16f, 0.94f);
        public static readonly Color PanelCard = new Color(0.13f, 0.16f, 0.24f, 0.96f);
        public static readonly Color PrimaryCyan = new Color(0.15f, 0.78f, 0.98f, 1f);
        public static readonly Color AccentGold = new Color(0.98f, 0.72f, 0.16f, 1f);
        public static readonly Color AccentOrange = new Color(0.96f, 0.42f, 0.14f, 1f);
        public static readonly Color AccentGreen = new Color(0.22f, 0.85f, 0.48f, 1f);
        public static readonly Color AccentRed = new Color(0.92f, 0.25f, 0.28f, 1f);
        public static readonly Color MutedSlate = new Color(0.28f, 0.33f, 0.44f, 1f);

        private static Font cachedFont;
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        public static Color GetRarityColor(Data.PartRarity rarity)
        {
            switch (rarity)
            {
                case Data.PartRarity.Common: return new Color(0.68f, 0.74f, 0.84f, 1f);
                case Data.PartRarity.Rare: return new Color(0.22f, 0.72f, 1.0f, 1f);
                case Data.PartRarity.Epic: return new Color(0.76f, 0.35f, 0.98f, 1f);
                case Data.PartRarity.Legendary: return new Color(1.0f, 0.72f, 0.15f, 1f);
                default: return Color.white;
            }
        }

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Image CreatePanel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = CreateUIObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null, FontStyle style = FontStyle.Bold)
        {
            var go = CreateUIObject(name, parent);
            var txt = go.AddComponent<Text>();
            txt.font = DefaultFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.fontStyle = style;
            txt.color = color ?? Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        public static Button CreateButton(string name, Transform parent, string label, Color bgColor, int fontSize = 24, Sprite sprite = null)
        {
            var img = CreatePanel(name, parent, bgColor, sprite);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            btn.colors = colors;

            var txt = CreateText("Label", img.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            StretchFull(txt.rectTransform, 8f);
            var outline = txt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.06f, 0.1f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return btn;
        }

        public static void StretchFull(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }
    }

    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastSafeArea = Rect.zero;
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea) ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
        }
    }
}
