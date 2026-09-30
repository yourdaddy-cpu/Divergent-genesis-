using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DivergentGenesis.UI
{
    /// <summary>
    /// The whole UI is built from code in a factory. No prefabs, no GUIDs, no
    /// binary assets - which means the whole repository is reviewable text and a
    /// fresh clone cannot have a broken reference.
    /// </summary>
    public static class UIFactory
    {
        public static Font Font { get; private set; }
        private static Sprite _white;
        private static Sprite _circle;
        private static Sprite _rounded;

        public static readonly Color Panel = new Color(0.09f, 0.10f, 0.13f, 0.92f);
        public static readonly Color PanelLight = new Color(0.16f, 0.18f, 0.22f, 0.95f);
        public static readonly Color Accent = new Color(0.95f, 0.72f, 0.25f, 1f);
        public static readonly Color Text = new Color(0.94f, 0.95f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.66f, 0.69f, 0.74f, 1f);

        /// <summary>
        /// Loads the editor's built-in UI font.
        ///
        /// "Arial.ttf" has been an invalid built-in resource since Unity 2022.2 -
        /// GetBuiltinResource throws an ArgumentException on it rather than
        /// returning null, so it must never be requested, and there is no
        /// "try the old name" fallback to make. LegacyRuntime.ttf is the only
        /// name that works, and a failure here must not take the game down with
        /// it: the HUD would be unreadable but the world would still run.
        /// </summary>
        public static void EnsureFont()
        {
            if (Font != null) return;

            try
            {
                Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DivergentGenesis] built-in UI font unavailable: " + e.Message);
            }
        }

        public static Sprite Solid()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            tex.name = "DG_White";
            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
            _white.name = "DG_WhiteSprite";
            return _white;
        }

        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size * 0.5f - 2f;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01(r - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.name = "DG_Circle";
            _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _circle.name = "DG_CircleSprite";
            return _circle;
        }

        // ---------------------------------------------------------------- canvas
        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent)
        {
            EnsureFont();

            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        public static Image PanelRect(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid();
            img.color = color;
            img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Icon(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            EnsureFont();
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button TextButton(Transform parent, string name, string label, int fontSize, Color bg, Color fg)
        {
            EnsureFont();
            var img = PanelRect(parent, name, bg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            var text = Label(img.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, fg);
            Stretch((RectTransform)text.transform, 0f);
            return btn;
        }

        public static Button RoundButton(Transform parent, string name, string label, int fontSize, Color bg, Color fg)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.05f;
            btn.colors = colors;

            var text = Label(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, fg);
            Stretch((RectTransform)text.transform, 0f);
            return btn;
        }

        // ------------------------------------------------------------- anchoring
        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Corner(RectTransform rt, float x, float y, float w, float h)
        {
            // x,y measured from the bottom-left corner in reference pixels
            return Anchor(rt,
                Vector2.zero, Vector2.zero,
                new Vector2(0f, 0f),
                new Vector2(x, y),
                new Vector2(w, h));
        }

        public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            return Anchor(rt,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -y),
                new Vector2(w, h));
        }

        public static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- layout
        public static HorizontalLayoutGroup Row(RectTransform rt, int spacing, RectOffset padding, TextAnchor align)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.padding = padding ?? new RectOffset(0, 0, 0, 0);
            row.childAlignment = align;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childControlWidth = true;
            row.childControlHeight = true;
            return row;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, int spacing, RectOffset padding, TextAnchor align)
        {
            var col = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.padding = padding ?? new RectOffset(0, 0, 0, 0);
            col.childAlignment = align;
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            col.childControlWidth = true;
            col.childControlHeight = true;
            return col;
        }

        public static LayoutElement Size(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = w;
            le.minHeight = h;
            return le;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            // Object.FindObjectOfType is obsolete from Unity 6 onwards, and
            // FindFirstObjectByType is deprecated in 6.6 in favour of this one.
            // Any match will do - we only care that one already exists.
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }
    }
}
