using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.Alphabet
{
    internal enum PracticeBadgeTone
    {
        Neutral,
        Info,
        Warning,
        Success
    }

    /// <summary>Construye y actualiza el panel compacto de webcam y retroalimentación del abecedario.</summary>
    internal sealed class AlphabetPracticePanelView
    {
        private TMP_Text _badge;
        private Image _badgeBackground;
        private readonly Color _cream;
        private readonly Color _ink;
        private readonly Color _turquoise;
        private readonly Color _turquoiseSoft;

        private TMP_Text _scoreValue, _scoreCaption, _starsValue, _starsCaption;
        private GameObject _root;
        private RawImage _image;
        private RectTransform _viewportRect;
        private TMP_Text _hint;
        private GameObject _successMark;
        private Texture _configuredTexture;
        private int _configuredRotation = -1;
        private bool _configuredVerticalFlip;
        private int _configuredFrameWidth;
        private int _configuredFrameHeight;
        private Vector2 _configuredViewportSize;

        private AlphabetPracticePanelView(
            TMP_Text instruction,
            TMP_Text badge,
            Button practiceButton,
            Color cream,
            Color ink,
            Color turquoise,
            Color turquoiseSoft)
        {
            _badge = badge;
            _badgeBackground = badge != null ? badge.GetComponentInParent<Image>() : null;
            _cream = cream;
            _ink = ink;
            _turquoise = turquoise;
            _turquoiseSoft = turquoiseSoft;
            Build(instruction, practiceButton);
        }

        public static AlphabetPracticePanelView Create(
            TMP_Text instruction,
            TMP_Text badge,
            Button practiceButton,
            Color cream,
            Color ink,
            Color turquoise,
            Color turquoiseSoft)
        {
            if (instruction == null || practiceButton == null || instruction.transform.parent == null)
                return null;

            return new AlphabetPracticePanelView(
                instruction, badge, practiceButton, cream, ink, turquoise, turquoiseSoft);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        public void SetHint(string message)
        {
            if (_hint == null) return;
            message = Loc.Translate(message);
            if (_hint.text != message) _hint.text = message;
            _hint.gameObject.SetActive(true);
        }

        public void ClearFrame(string hint, bool visible)
        {
            if (_image != null) _image.texture = Texture2D.blackTexture;
            _configuredTexture = null;
            _configuredRotation = -1;
            SetHint(hint);
            SetVisible(visible);
        }

        public void UpdateFrame(WebCamTexture texture)
        {
            if (_image == null) return;
            if (texture == null || !texture.isPlaying || texture.width <= 16 || texture.height <= 16)
            {
                SetHint("Conectando cámara…");
                return;
            }

            int rotation = texture.videoRotationAngle;
            bool flipVertically = texture.videoVerticallyMirrored;
            if (_image.texture != texture) _image.texture = texture;

            var viewportSize = _viewportRect.rect.size;
            if (viewportSize.x > 1f && viewportSize.y > 1f &&
                (_configuredTexture != texture || _configuredRotation != rotation ||
                 _configuredVerticalFlip != flipVertically || _configuredFrameWidth != texture.width ||
                 _configuredFrameHeight != texture.height || _configuredViewportSize != viewportSize))
            {
                ConfigureImageSize(texture.width, texture.height, rotation, viewportSize);
                _image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotation);
                _image.uvRect = new Rect(1f, flipVertically ? 1f : 0f, -1f,
                    flipVertically ? -1f : 1f);
                _configuredTexture = texture;
                _configuredRotation = rotation;
                _configuredVerticalFlip = flipVertically;
                _configuredFrameWidth = texture.width;
                _configuredFrameHeight = texture.height;
                _configuredViewportSize = viewportSize;
            }

            if (_hint != null) _hint.gameObject.SetActive(false);
        }

        public void SetBadge(string message, PracticeBadgeTone tone)
        {
            if (_badge == null) return;

            _badge.text = Loc.Translate(message);
            _badge.color = _ink;
            bool success = tone == PracticeBadgeTone.Success;
            _badge.alignment = success ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;

            // El texto ocupa toda la píldora (que se estira con la tarjeta); con la marca de éxito deja sitio a la izquierda.
            var badgeRect = _badge.rectTransform;
            badgeRect.offsetMin = new Vector2(success ? 52f : 14f, 6f);
            badgeRect.offsetMax = new Vector2(-14f, -6f);

            if (_successMark != null) _successMark.SetActive(success);
            if (_badgeBackground != null)
            {
                _badgeBackground.color = tone switch
                {
                    PracticeBadgeTone.Success => new Color(0.72f, 0.93f, 0.8f),
                    PracticeBadgeTone.Warning => new Color(1f, 0.82f, 0.76f),
                    PracticeBadgeTone.Info => _turquoiseSoft,
                    _ => new Color(0.96f, 0.91f, 0.78f)
                };
            }
        }

        /// <summary>
        /// Acomoda la tarjeta inferior con layouts automáticos: a la izquierda el texto de la letra y, debajo, el estado y el botón de
        /// practicar; a la derecha la vista de la cámara (solo mientras se practica). Nada tiene posiciones en píxeles: todo se reparte
        /// el espacio de la tarjeta, que a su vez se ancla por proporciones de pantalla (<see cref="AlphabetResponsiveLayout"/>).
        /// </summary>
        private void Build(TMP_Text instruction, Button practiceButton)
        {
            var card = instruction.transform.parent as RectTransform;
            if (card == null) return;
            for (int i = card.childCount - 1; i >= 0; i--)
            {
                var child = card.GetChild(i);
                if (child.name == "PracticeStatusText" || child.name == "PracticeStatusPill" ||
                    child.name == "PracticeCameraPreview")
                    Object.Destroy(child.gameObject);
            }
            if (_badgeBackground != null)
            {
                for (int i = _badgeBackground.transform.childCount - 1; i >= 0; i--)
                {
                    var child = _badgeBackground.transform.GetChild(i);
                    if (child.name == "PracticeStatusText" || child.name == "SuccessCheck") Object.Destroy(child.gameObject);
                }
            }

            var cardLayout = card.GetComponent<HorizontalLayoutGroup>();
            if (cardLayout == null) cardLayout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardLayout.padding = new RectOffset(28, 28, 22, 22);
            cardLayout.spacing = 22f;
            cardLayout.childAlignment = TextAnchor.MiddleLeft;
            cardLayout.childControlWidth = cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = cardLayout.childForceExpandHeight = false;
            var ring = card.Find("Aro");
            if (ring != null) SetElement(ring, ignoreLayout: true);

            var column = new GameObject("PracticeColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            column.transform.SetParent(card, false);
            var columnLayout = column.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 12f;
            columnLayout.childControlWidth = columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true; columnLayout.childForceExpandHeight = false;
            SetElement(column.transform, minW: 200f, flexW: 1f, flexH: 1f);

            instruction.transform.SetParent(column.transform, false);
            AlphabetResponsiveLayout.Fit(instruction, 18f, true);
            instruction.alignment = TextAlignmentOptions.TopLeft;
            SetElement(instruction.transform, minH: 60f, prefH: 120f, flexH: 1f);

            // Tarjetas de coincidencia y estrellas, con el mismo aspecto que las de las casas de números, meses y saludos.
            var statsRow = new GameObject("PracticeStats", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            statsRow.transform.SetParent(column.transform, false);
            var statsLayout = statsRow.GetComponent<HorizontalLayoutGroup>();
            statsLayout.spacing = 14f; statsLayout.childAlignment = TextAnchor.MiddleCenter;
            statsLayout.childControlWidth = statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = true; statsLayout.childForceExpandHeight = true;
            SetElement(statsRow.transform, minH: 74f, prefH: 84f);
            var cardSprite = _badgeBackground != null ? _badgeBackground.sprite : null;
            _scoreValue = CreateStatCard(statsRow.transform, "ScoreCard", instruction.font, cardSprite, out _scoreCaption);
            _starsValue = CreateStatCard(statsRow.transform, "StarsCard", instruction.font, cardSprite, out _starsCaption);
            SetStats(false, 0f, 0);

            var row = new GameObject("PracticeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(column.transform, false);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f; rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false; rowLayout.childForceExpandHeight = true;
            SetElement(row.transform, minH: 56f, prefH: 62f);

            if (_badge != null)
            {
                var sourceBadge = _badge;
                Transform pill = _badgeBackground != null ? _badgeBackground.transform : null;
                if (pill != null)
                {
                    pill.SetParent(row.transform, false);
                    SetElement(pill, minW: 120f, minH: 56f, prefH: 62f, flexW: 1f);
                }
                sourceBadge.gameObject.SetActive(false);

                var badgeObject = new GameObject("PracticeStatusText",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                badgeObject.transform.SetParent(pill != null ? pill : row.transform, false);
                _badge = badgeObject.GetComponent<TextMeshProUGUI>();
                _badge.font = sourceBadge.font;
                _badge.fontSize = Mathf.Max(sourceBadge.fontSize, 22f);
                _badge.fontStyle = sourceBadge.fontStyle;
                _badge.color = _ink;
                _badge.raycastTarget = false;
                _badge.alignment = TextAlignmentOptions.Center;
                AlphabetResponsiveLayout.Fit(_badge, 13f, true);
                var badgeRect = _badge.rectTransform;
                badgeRect.anchorMin = Vector2.zero;
                badgeRect.anchorMax = Vector2.one;
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                if (pill == null) SetElement(badgeObject.transform, minW: 120f, minH: 56f, prefH: 62f, flexW: 1f);
                if (_badgeBackground != null)
                {
                    _badgeBackground.color = _turquoiseSoft;
                    _badgeBackground.raycastTarget = false;
                    CreateSuccessMark(_badgeBackground.transform);
                }
            }

            practiceButton.transform.SetParent(row.transform, false);
            SetElement(practiceButton.transform, minW: 150f, minH: 56f, prefW: 250f, prefH: 62f);
            var practiceLabel = practiceButton.GetComponentInChildren<TMP_Text>(true);
            if (practiceLabel != null) AlphabetResponsiveLayout.Fit(practiceLabel, 14f, false);

            _root = new GameObject("PracticeCameraPreview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.transform.SetParent(card, false);
            SetElement(_root.transform, minW: 110f, minH: 110f, prefW: 164f, prefH: 164f);

            var background = _root.GetComponent<Image>();
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = _ink;
            background.raycastTarget = false;

            var viewport = new GameObject("CameraViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(_root.transform, false);
            _viewportRect = viewport.GetComponent<RectTransform>();
            _viewportRect.anchorMin = Vector2.zero;
            _viewportRect.anchorMax = Vector2.one;
            _viewportRect.offsetMin = Vector2.zero;
            _viewportRect.offsetMax = Vector2.zero;

            var feed = new GameObject("CameraFeed",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            feed.transform.SetParent(viewport.transform, false);
            _image = feed.GetComponent<RawImage>();
            _image.texture = Texture2D.blackTexture;
            _image.raycastTarget = false;
            _image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _image.rectTransform.anchoredPosition = Vector2.zero;

            _hint = CreateText(viewport.transform, "CameraPreviewHint", instruction.font);
            _hint.text = Loc.Translate("Centra tu mano");
            _hint.fontSize = 16f;
            _hint.color = _cream;
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.rectTransform.anchorMin = Vector2.zero;
            _hint.rectTransform.anchorMax = Vector2.one;
            _hint.rectTransform.offsetMin = Vector2.zero;
            _hint.rectTransform.offsetMax = Vector2.zero;

            _hint.rectTransform.offsetMin = new Vector2(8f, 8f);
            _hint.rectTransform.offsetMax = new Vector2(-8f, -8f);

            SetBadge("Referencia provisional · en revisión", PracticeBadgeTone.Neutral);
            SetVisible(false);

            // Nada se sale de su caja: el texto del botón PRACTICAR, el de la píldora y el aviso de la cámara se reducen para caber.
            AlphabetResponsiveLayout.ContainTexts(card);
        }

        /// <summary>Muestra la coincidencia (0–1) y las estrellas (0–3); con <paramref name="active"/> falso deja «—».</summary>
        public void SetStats(bool active, float similarity, int stars)
        {
            if (_scoreValue == null || _starsValue == null) return;
            _scoreValue.text = active ? Mathf.RoundToInt(similarity * 100f) + "%" : "—";
            _starsValue.text = active ? stars + " / 3" : "—";
            _scoreCaption.text = Loc.T("levels.scoreCaption");
            _starsCaption.text = Loc.T("levels.starsCaption");
        }

        private TMP_Text CreateStatCard(Transform parent, string objectName, TMP_FontAsset font, Sprite sprite, out TMP_Text caption)
        {
            var outer = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outer.transform.SetParent(parent, false);
            var outerImage = outer.GetComponent<Image>();
            outerImage.sprite = sprite;
            outerImage.type = Image.Type.Sliced;
            outerImage.color = _turquoiseSoft;
            outerImage.raycastTarget = false;
            SetElement(outer.transform, minW: 90f, minH: 70f, flexW: 1f, flexH: 1f);

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inner.transform.SetParent(outer.transform, false);
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero; innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(5f, 5f); innerRect.offsetMax = new Vector2(-5f, -5f);
            var innerImage = inner.GetComponent<Image>();
            innerImage.sprite = sprite;
            innerImage.type = Image.Type.Sliced;
            innerImage.color = new Color(1f, 0.953f, 0.863f);
            innerImage.raycastTarget = false;

            var value = CreateText(inner.transform, "Value", font);
            value.fontSize = 36f; value.fontStyle = FontStyles.Bold;
            value.color = new Color(1f, 0.369f, 0.302f);
            value.alignment = TextAlignmentOptions.Center;
            AlphabetResponsiveLayout.Fit(value, 16f, false);
            var valueRect = value.rectTransform;
            valueRect.anchorMin = new Vector2(0f, 0.38f); valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = new Vector2(10f, 0f); valueRect.offsetMax = new Vector2(-10f, -4f);

            caption = CreateText(inner.transform, "Caption", font);
            caption.fontSize = 18f; caption.fontStyle = FontStyles.Bold;
            caption.color = _ink;
            caption.alignment = TextAlignmentOptions.Center;
            AlphabetResponsiveLayout.Fit(caption, 10f, false);
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = Vector2.zero; captionRect.anchorMax = new Vector2(1f, 0.38f);
            captionRect.offsetMin = new Vector2(8f, 3f); captionRect.offsetMax = new Vector2(-8f, 0f);
            return value;
        }

        private static void SetElement(Transform target, float minW = -1f, float minH = -1f, float prefW = -1f, float prefH = -1f,
            float flexW = -1f, float flexH = -1f, bool ignoreLayout = false)
        {
            var e = target.GetComponent<LayoutElement>();
            if (e == null) e = target.gameObject.AddComponent<LayoutElement>();
            e.ignoreLayout = ignoreLayout;
            e.minWidth = minW; e.minHeight = minH; e.preferredWidth = prefW; e.preferredHeight = prefH;
            e.flexibleWidth = flexW; e.flexibleHeight = flexH;
        }

        private void CreateSuccessMark(Transform parent)
        {
            _successMark = new GameObject("SuccessCheck", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _successMark.transform.SetParent(parent, false);
            var markRect = _successMark.GetComponent<RectTransform>();
            markRect.anchorMin = new Vector2(0f, 0.5f);
            markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = new Vector2(10f, 0f);
            markRect.sizeDelta = new Vector2(30f, 30f);
            var markBackground = _successMark.GetComponent<Image>();
            markBackground.sprite = _badgeBackground.sprite;
            markBackground.type = _badgeBackground.type;
            markBackground.color = _turquoise;
            markBackground.raycastTarget = false;
            CreateCheckStroke(_successMark.transform, new Vector2(-2f, -2f), 13f, 4f, 45f);
            CreateCheckStroke(_successMark.transform, new Vector2(4f, 1f), 18f, 4f, -48f);
            _successMark.SetActive(false);
        }

        private static void CreateCheckStroke(Transform parent, Vector2 position, float width, float height, float angle)
        {
            var stroke = new GameObject("CheckStroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stroke.transform.SetParent(parent, false);
            var rect = stroke.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
            rect.localEulerAngles = new Vector3(0f, 0f, angle);
            var image = stroke.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void ConfigureImageSize(int frameWidth, int frameHeight, int rotation, Vector2 viewportSize)
        {
            bool quarterTurn = rotation == 90 || rotation == 270;
            float sourceAspect = (float)frameWidth / frameHeight;
            float displayAspect = quarterTurn ? 1f / sourceAspect : sourceAspect;
            float viewportAspect = viewportSize.x / viewportSize.y;

            float displayWidth;
            float displayHeight;
            // Cubrir el recuadro y recortar los laterales para evitar bandas vacías.
            if (displayAspect >= viewportAspect)
            {
                displayHeight = viewportSize.y;
                displayWidth = displayHeight * displayAspect;
            }
            else
            {
                displayWidth = viewportSize.x;
                displayHeight = displayWidth / displayAspect;
            }

            _image.rectTransform.sizeDelta = quarterTurn
                ? new Vector2(displayHeight, displayWidth)
                : new Vector2(displayWidth, displayHeight);
        }

        private static TMP_Text CreateText(Transform parent, string objectName, TMP_FontAsset font)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }

}
