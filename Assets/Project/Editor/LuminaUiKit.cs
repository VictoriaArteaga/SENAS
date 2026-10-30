using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lumina.UI;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Piezas de interfaz con la identidad de Lúmina (mismos sprites, colores y fuente que el menú principal),
    /// usadas por los constructores de LoginScene, RegisterScene y LogoutModal.
    /// </summary>
    internal static class LuminaUiKit
    {
        // Paleta tomada del menú principal.
        public static readonly Color Cream = new Color(1f, 0.976f, 0.925f);
        public static readonly Color CreamWarm = new Color(1f, 0.953f, 0.863f);
        public static readonly Color Coral = new Color(1f, 0.369f, 0.302f);
        public static readonly Color CoralDark = new Color(0.812f, 0.29f, 0.247f);
        public static readonly Color Turquoise = new Color(0.18f, 0.769f, 0.714f);
        public static readonly Color TurquoiseDark = new Color(0.09f, 0.529f, 0.498f);
        public static readonly Color TurquoiseSoft = new Color(0.56f, 0.89f, 0.85f);
        public static readonly Color Ink = new Color(0.059f, 0.227f, 0.267f);
        public static readonly Color Sand = new Color(0.98f, 0.89f, 0.74f);
        public static readonly Color SandDark = new Color(0.851f, 0.745f, 0.549f);
        public static readonly Color Dim = new Color(0.05f, 0.19f, 0.23f, 0.72f);

        private const string SpriteDir = "Assets/Project/UI/Sprites/";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static Sprite _round, _circle;
        private static TMP_FontAsset _font;

        public static Sprite Round => _round != null ? _round : _round = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "RoundedRect.png");
        public static Sprite Circle => _circle != null ? _circle : _circle = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "Circle.png");
        public static TMP_FontAsset Font => _font != null ? _font : _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // ---------- Rectángulos ----------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Coloca un rectángulo con anclas, pivote, posición y tamaño explícitos.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Elemento anclado arriba al centro (pivote arriba); y se mide hacia abajo desde el borde superior.</summary>
        public static RectTransform TopCenter(RectTransform rt, float yFromTop, float width, float height, float x = 0f)
            => Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -yFromTop), new Vector2(width, height));

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = true)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite != null ? sprite : (sliced ? Round : null);
            image.type = sprite == Circle ? Image.Type.Simple : (sliced ? Image.Type.Sliced : Image.Type.Simple);
            image.raycastTarget = false;
            return image;
        }

        // ---------- Texto ----------

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            FontStyles style = FontStyles.Bold, TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 0f)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Texto con sombra desplazada, como el título del menú principal.</summary>
        public static TextMeshProUGUI ShadowedTitle(Transform parent, string name, string text, float size, Color face, Color shadow,
            Vector2 shadowOffset, float yFromTop, float width, float height, float spacing = 4f)
        {
            var shadowLabel = Label(parent, name + "Shadow", text, size, shadow, FontStyles.Bold, TextAlignmentOptions.Center, spacing);
            TopCenter(shadowLabel.rectTransform, yFromTop, width, height).anchoredPosition += new Vector2(shadowOffset.x, -shadowOffset.y);
            var faceLabel = Label(parent, name, text, size, face, FontStyles.Bold, TextAlignmentOptions.Center, spacing);
            TopCenter(faceLabel.rectTransform, yFromTop, width, height);
            return faceLabel;
        }

        // ---------- Tarjeta ----------

        /// <summary>Tarjeta del menú: marco turquesa y fondo crema. Devuelve el interior (crema).</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 size, out RectTransform outer)
        {
            var frame = Panel(parent, name, Turquoise);
            frame.pixelsPerUnitMultiplier = 1f;
            outer = frame.rectTransform;
            outer.sizeDelta = size;
            var inner = Panel(outer, name + "Inner", Cream);
            Stretch(inner.rectTransform, 9f, 9f, 9f, 9f);
            return inner.rectTransform;
        }

        public static Image Pill(Transform parent, string name, string text, float fontSize, Color fill, Color textColor,
            float yFromTop, float width, float height, float spacing = 6f)
        {
            var pill = Panel(parent, name, fill);
            TopCenter(pill.rectTransform, yFromTop, width, height);
            var label = Label(pill.transform, "Label", text, fontSize, textColor, FontStyles.Bold, TextAlignmentOptions.Center, spacing);
            Stretch(label.rectTransform);
            return pill;
        }

        // ---------- Botón ----------

        public static Button MakeButton(Transform parent, string name, string text, Color face, Color shadow, Color textColor,
            Vector2 size, float fontSize = 34f, float drop = 8f, float spacing = 3f)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            var shadowImage = root.gameObject.AddComponent<Image>();
            shadowImage.sprite = Round; shadowImage.type = Image.Type.Sliced; shadowImage.color = shadow;

            var faceRect = Rect("Face", root);
            faceRect.anchorMin = Vector2.zero; faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(0f, drop); faceRect.offsetMax = Vector2.zero;
            var faceImage = faceRect.gameObject.AddComponent<Image>();
            faceImage.sprite = Round; faceImage.type = Image.Type.Sliced; faceImage.color = face; faceImage.raycastTarget = false;

            var label = Label(faceRect, "Label", text, fontSize, textColor, FontStyles.Bold, TextAlignmentOptions.Center, spacing);
            Stretch(label.rectTransform);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            button.transition = Selectable.Transition.None;

            var anim = root.gameObject.AddComponent<UIButtonAnimator>();
            Set(anim, "face", faceRect);
            Set(anim, "tint", faceImage);
            return button;
        }

        /// <summary>Botón de texto sin caja (enlace). Solo escala al pasar el cursor.</summary>
        public static Button MakeLink(Transform parent, string name, string text, float fontSize, Color color, Vector2 size,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var label = Label(root, "Label", text, fontSize, color, FontStyles.Bold | FontStyles.Underline, align);
            Stretch(label.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UIButtonAnimator>();
            return button;
        }

        // ---------- Campo de formulario ----------

        /// <summary>
        /// Contenedor vertical de un formulario: coloca los campos uno bajo otro y hace sitio a los mensajes de error
        /// (los campos usan layout, no coordenadas fijas), de modo que nada se monta sobre lo siguiente.
        /// </summary>
        public static RectTransform MakeForm(Transform parent, string name, float yFromTop, float width, float spacing)
        {
            var form = Rect(name, parent);
            TopCenter(form, yFromTop, width, 0f);
            var layout = form.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = form.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return form;
        }

        /// <summary>Fila de altura fija dentro de un formulario (para casilla + enlace, etc.).</summary>
        public static RectTransform LayoutRow(Transform parent, string name, float height)
        {
            var row = Rect(name, parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height; element.minHeight = height;
            return row;
        }

        public static LayoutElement FixHeight(Component target, float height)
        {
            var element = target.gameObject.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height; element.minHeight = height;
            return element;
        }

        /// <summary>
        /// Campo de formulario: etiqueta, cuadro de texto y mensaje de error apilados con un VerticalLayoutGroup. El mensaje está
        /// desactivado mientras no hay error (no ocupa espacio) y es de una sola línea (se reduce para caber, nunca desborda).
        /// Con <paramref name="inLayout"/> el campo lo coloca un layout padre; si no, se ancla a <paramref name="yFromTop"/>.
        /// </summary>
        public static AuthField MakeField(Transform parent, string name, string labelText, string placeholder, bool password,
            float yFromTop, float width, float labelHeight, float inputHeight, float errorHeight, bool withReveal, float fontSize = 28f,
            bool inLayout = false)
        {
            var root = Rect(name, parent);
            if (!inLayout) TopCenter(root, yFromTop, width, labelHeight + inputHeight + errorHeight + 6f);

            var stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 2f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true; stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;

            var label = Label(root, "Label", labelText, labelHeight * 0.85f, Ink, FontStyles.Bold, TextAlignmentOptions.BottomLeft, 1f);
            label.margin = new Vector4(8f, 0f, 8f, 0f);
            FixHeight(label, labelHeight);

            // Contorno (cambia de color con el foco o el error) y relleno claro.
            var outline = Panel(root, "Outline", TurquoiseSoft);
            FixHeight(outline, inputHeight);
            var fill = Panel(outline.transform, "Fill", Color.white);
            Stretch(fill.rectTransform, 4f, 4f, 4f, 4f);
            fill.raycastTarget = true;

            float rightPad = withReveal ? 150f : 22f;
            var viewport = Rect("Text Area", fill.transform);
            Stretch(viewport, 24f, 4f, rightPad, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var placeholderText = Label(viewport, "Placeholder", placeholder, fontSize, Ink, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            Stretch(placeholderText.rectTransform);
            placeholderText.color = new Color(Ink.r, Ink.g, Ink.b, 0.42f);
            placeholderText.enableWordWrapping = false;

            var text = Label(viewport, "Text", string.Empty, fontSize, Ink, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.enableWordWrapping = false;

            var input = fill.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fill;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.fontAsset = Font;
            input.pointSize = fontSize;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = Ink;
            input.caretWidth = 3;
            input.selectionColor = new Color(Turquoise.r, Turquoise.g, Turquoise.b, 0.35f);
            input.onFocusSelectAll = false;
            input.transition = Selectable.Transition.None;
            input.characterLimit = 64;

            var error = Label(root, "Error", string.Empty, errorHeight * 0.8f, new Color(0.78f, 0.22f, 0.18f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            error.margin = new Vector4(8f, 2f, 8f, 0f);
            error.enableAutoSizing = true;
            error.fontSizeMin = 12f;
            error.fontSizeMax = errorHeight * 0.8f;
            error.overflowMode = TextOverflowModes.Ellipsis;
            FixHeight(error, errorHeight);

            var field = root.gameObject.AddComponent<AuthField>();
            Set(field, "input", input);
            Set(field, "outline", outline);
            Set(field, "errorLabel", error);

            if (withReveal)
            {
                var reveal = MakeButton(fill.transform, "RevealButton", "VER", TurquoiseSoft, new Color(0.35f, 0.74f, 0.7f), Ink,
                    new Vector2(112f, inputHeight - 22f), 20f, 4f, 2f);
                Place((RectTransform)reveal.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(112f, inputHeight - 24f));
                Set(field, "revealButton", reveal);
                Set(field, "revealLabel", reveal.GetComponentInChildren<TMP_Text>());
            }
            return field;
        }

        // ---------- Casilla ----------

        public static Toggle MakeToggle(Transform parent, string name, string text, float fontSize, Vector2 size)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            // Superficie invisible que recibe el clic: así toda la fila (casilla y texto) alterna la opción.
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var box = Panel(root, "Box", TurquoiseSoft);
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            box.raycastTarget = true;
            var inside = Panel(box.transform, "Inside", Color.white);
            Stretch(inside.rectTransform, 4f, 4f, 4f, 4f);
            var check = Panel(box.transform, "Check", Turquoise);
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            var label = Label(root, "Label", text, fontSize, Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, 52f, 0f, 0f, 0f);

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            toggle.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UIButtonAnimator>();
            return toggle;
        }

        // ---------- Indicador de carga ----------

        public static LoadingDots MakeLoadingDots(Transform parent, Color color)
        {
            var root = Rect("Loading", parent);
            Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 40f));
            var dots = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var dot = Panel(root, "Dot" + (i + 1), color, Circle);
                Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 42f, 0f), new Vector2(26f, 26f));
                dots[i] = dot.rectTransform;
            }
            var loading = root.gameObject.AddComponent<LoadingDots>();
            SetArray(loading, "dots", dots);
            root.gameObject.SetActive(false);
            return loading;
        }

        // ---------- Sistema de eventos ----------

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            // En el editor, añadir el módulo asigna las acciones de UI del proyecto (igual que el menú de Unity).
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ---------- Serialización ----------

        public static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) throw new System.InvalidOperationException($"No existe la propiedad '{property}' en {target.GetType().Name}.");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) throw new System.InvalidOperationException($"No existe la propiedad '{property}' en {target.GetType().Name}.");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property) ?? throw new System.InvalidOperationException($"No existe '{property}'.");
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property) ?? throw new System.InvalidOperationException($"No existe '{property}'.");
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
