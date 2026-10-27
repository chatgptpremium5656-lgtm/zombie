using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zombie.Player;
using Zombie.UI;

namespace Zombie.EditorTools
{
    /// <summary>Builds the style-probe HUD (uGUI): health bar, 6-slot hotbar, crosshair.</summary>
    static class HudBuilder
    {
        const string FontPath = "Assets/Art/Fonts/LilitaOne-Regular.ttf";

        static readonly Color PanelColor = new Color(0.12f, 0.1f, 0.13f, 0.8f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.42f);
        static readonly Color Accent = new Color(1f, 0.82f, 0.35f);

        public struct Item
        {
            public Sprite Icon;
            public int Count;
        }

        public static GameObject Build(ViewModelAnimator viewModel, Item[] items)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var rounded = Sprite(TextureFactory.Rounded);
            var shadow = Sprite(TextureFactory.RoundedShadow);
            var outline = Sprite(TextureFactory.RoundedOutline);

            var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ---------------- health
            var health = Panel("Health", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 34f), new Vector2(480f, 96f));
            Shadowed(health, shadow, rounded, PanelColor);
            var heart = Img("Heart", health, Sprite(TextureFactory.Heart), Color.white, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(66f, 66f));
            var barBg = Img("BarBack", health, rounded, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, 0.5f), new Vector2(98f, -2f), new Vector2(268f, 30f));
            barBg.pivot = new Vector2(0f, 0.5f);
            Pill(barBg);
            var trail = Img("Trail", barBg, rounded, new Color(1f, 0.85f, 0.6f, 0.9f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(268f, 30f));
            trail.pivot = new Vector2(0f, 0.5f);
            Pill(trail);
            var fill = Img("Fill", barBg, rounded, new Color(0.96f, 0.33f, 0.36f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(268f, 30f));
            fill.pivot = new Vector2(0f, 0.5f);
            Pill(fill);
            var gloss = Img("Gloss", fill, rounded, new Color(1f, 1f, 1f, 0.28f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            gloss.anchorMin = new Vector2(0f, 0.55f);
            gloss.anchorMax = new Vector2(1f, 0.92f);
            gloss.offsetMin = new Vector2(8f, 0f);
            gloss.offsetMax = new Vector2(-8f, 0f);
            Pill(gloss);
            var healthText = Label("Value", health, font, "100", 38, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(80f, 60f));

            // ---------------- hotbar
            const int slotCount = 6;
            const float slotSize = 96f;
            const float gap = 12f;
            float trayWidth = slotCount * slotSize + (slotCount - 1) * gap + 32f;
            var hotbar = Panel("Hotbar", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(trayWidth, 128f));
            Shadowed(hotbar, shadow, rounded, new Color(0.1f, 0.09f, 0.11f, 0.5f));
            var slots = new RectTransform[slotCount];
            var highlights = new Graphic[slotCount];
            var backgrounds = new Image[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                float x = -((slotCount - 1) * 0.5f) * (slotSize + gap) + i * (slotSize + gap);
                var slot = Node("Slot" + (i + 1), hotbar, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(slotSize, slotSize));
                slots[i] = slot;
                var bg = Img("Back", slot, rounded, PanelColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(slotSize, slotSize));
                backgrounds[i] = bg.GetComponent<Image>();
                backgrounds[i].pixelsPerUnitMultiplier = 1.4f;
                var hl = Img("Highlight", slot, outline, Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(slotSize + 8f, slotSize + 8f));
                hl.GetComponent<Image>().pixelsPerUnitMultiplier = 1.25f;
                highlights[i] = hl.GetComponent<Image>();
                if (i < items.Length && items[i].Icon != null)
                {
                    var icon = Img("Icon", slot, items[i].Icon, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(82f, 82f));
                    var image = icon.GetComponent<Image>();
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    if (items[i].Count > 1)
                        Label("Count", slot, font, items[i].Count.ToString(), 26, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-9f, 4f), new Vector2(60f, 34f));
                }
                var key = Label("Key", slot, font, (i + 1).ToString(), 21, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(30f, 28f));
                key.color = new Color(1f, 1f, 1f, 0.7f);
            }

            // ---------------- crosshair
            var cross = Panel("Crosshair", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            var ringSprite = Sprite(TextureFactory.Ring);
            var dotSprite = Sprite(TextureFactory.Circle);
            Img("RingShadow", cross, ringSprite, new Color(0f, 0f, 0f, 0.3f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1.5f), new Vector2(32f, 32f));
            var ring = Img("Ring", cross, ringSprite, new Color(1f, 1f, 1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            Img("DotShadow", cross, dotSprite, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(9f, 9f));
            Img("Dot", cross, dotSprite, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));

            // ---------------- presenter
            var presenter = root.AddComponent<HudPresenter>();
            var so = new SerializedObject(presenter);
            var panels = so.FindProperty("_panels");
            panels.arraySize = 3;
            SetPanel(panels.GetArrayElementAtIndex(0), health, new Vector2(0f, -150f), 0.25f);
            SetPanel(panels.GetArrayElementAtIndex(1), hotbar, new Vector2(0f, -170f), 0.38f);
            SetPanel(panels.GetArrayElementAtIndex(2), cross, new Vector2(0f, 0f), 0.6f);
            so.FindProperty("_healthFill").objectReferenceValue = fill;
            so.FindProperty("_healthTrail").objectReferenceValue = trail;
            so.FindProperty("_healthBarWidth").floatValue = 268f;
            so.FindProperty("_healthText").objectReferenceValue = healthText;
            so.FindProperty("_heart").objectReferenceValue = heart;
            SetArray(so.FindProperty("_slots"), slots);
            SetArray(so.FindProperty("_slotHighlights"), highlights);
            SetArray(so.FindProperty("_slotBackgrounds"), backgrounds);
            so.FindProperty("_crosshairRing").objectReferenceValue = ring;
            so.FindProperty("_viewModel").objectReferenceValue = viewModel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void SetPanel(SerializedProperty p, RectTransform rect, Vector2 offset, float delay)
        {
            var group = rect.GetComponent<CanvasGroup>();
            p.FindPropertyRelative("Group").objectReferenceValue = group;
            p.FindPropertyRelative("Rect").objectReferenceValue = rect;
            p.FindPropertyRelative("Offset").vector2Value = offset;
            p.FindPropertyRelative("Delay").floatValue = delay;
        }

        static void SetArray(SerializedProperty p, Object[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = Node(name, parent, anchor, position, size);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        static void Shadowed(RectTransform panel, Sprite shadow, Sprite rounded, Color color)
        {
            var s = Img("Shadow", panel, shadow, ShadowColor, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            s.anchorMin = Vector2.zero;
            s.anchorMax = Vector2.one;
            s.offsetMin = new Vector2(-26f, -34f);
            s.offsetMax = new Vector2(26f, 18f);
            var bg = Img("Back", panel, rounded, color, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            var rim = Img("Rim", panel, Sprite(TextureFactory.RoundedOutline), new Color(1f, 1f, 1f, 0.07f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rim.anchorMin = Vector2.zero;
            rim.anchorMax = Vector2.one;
            rim.offsetMin = Vector2.zero;
            rim.offsetMax = Vector2.zero;
        }

        static void Pill(RectTransform rect)
        {
            // Corner radius = half the height (24 px corner in a 26 px border -> scale the slice down).
            var image = rect.GetComponent<Image>();
            image.pixelsPerUnitMultiplier = 26f / 15f;
        }

        static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform Img(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = Node(name, parent, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return rect;
        }

        static Text Label(string name, Transform parent, Font font, string value, int size, TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 box)
        {
            var rect = Node(name, parent, anchor, position, box);
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -2.5f);
            return text;
        }
    }
}
