using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private readonly TMP_Text _badge;
        private Image _badgeBackground;
        private readonly Color _cream;
        private readonly Color _ink;
        private readonly Color _turquoise;
        private readonly Color _turquoiseSoft;

        private GameObject _root;
        private RawImage _image;
        private RectTransform _viewportRect;
        private TMP_Text _hint;
        private GameObject _successMark;
        private PracticeStarGraphic[] _stars;
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

        public void SetBadge(string message, PracticeBadgeTone tone, int stars = 0)
        {
            if (_badge == null) return;
            _badge.text = message;
            _badge.color = _ink;
            _badge.alignment = tone == PracticeBadgeTone.Success
                ? TextAlignmentOptions.MidlineLeft
                : TextAlignmentOptions.Center;
            _badge.fontSize = Mathf.Max(_badge.fontSize, 21f);
            if (_successMark != null) _successMark.SetActive(tone == PracticeBadgeTone.Success);
            var badgeRect = _badge.rectTransform;
            badgeRect.offsetMin = new Vector2(tone == PracticeBadgeTone.Success ? 42f : 12f, 0f);
            badgeRect.offsetMax = new Vector2(tone == PracticeBadgeTone.Success ? -82f : -12f, 0f);
            if (_stars != null)
            {
                for (int i = 0; i < _stars.Length; i++)
                {
                    bool showStars = tone == PracticeBadgeTone.Success && stars > 0;
                    _stars[i].gameObject.SetActive(showStars);
                    _stars[i].color = i < stars
                        ? new Color(1f, 0.69f, 0.19f)
                        : new Color(0.58f, 0.72f, 0.68f);
                }
            }
            if (_badgeBackground == null) return;

            _badgeBackground.color = tone switch
            {
                PracticeBadgeTone.Success => new Color(0.72f, 0.93f, 0.8f),
                PracticeBadgeTone.Warning => new Color(1f, 0.82f, 0.76f),
                PracticeBadgeTone.Info => _turquoiseSoft,
                _ => new Color(0.96f, 0.91f, 0.78f)
            };
        }

        private void Build(TMP_Text instruction, Button practiceButton)
        {
            var card = instruction.transform.parent as RectTransform;
            if (card == null) return;

            card.sizeDelta = new Vector2(card.sizeDelta.x, Mathf.Max(card.sizeDelta.y, 280f));
            var instructionRect = instruction.rectTransform;
            instructionRect.anchorMin = new Vector2(0f, 1f);
            instructionRect.anchorMax = new Vector2(1f, 1f);
            instructionRect.pivot = new Vector2(0f, 1f);
            instructionRect.anchoredPosition = new Vector2(24f, -22f);
            instructionRect.sizeDelta = new Vector2(-270f, 128f);

            if (_badge != null)
            {
                var badgeRect = _badge.rectTransform;
                var pillRect = _badgeBackground != null
                    ? _badgeBackground.rectTransform
                    : null;
                if (pillRect != null)
                {
                    pillRect.anchorMin = Vector2.zero;
                    pillRect.anchorMax = Vector2.zero;
                    pillRect.pivot = Vector2.zero;
                    pillRect.anchoredPosition = new Vector2(24f, 32f);
                    pillRect.sizeDelta = new Vector2(650f, 58f);
                }
                if (_badgeBackground != null)
                {
                    var pillOutline = _badgeBackground.GetComponent<Outline>();
                    if (pillOutline != null) Destroy(pillOutline);
                }
                badgeRect.SetParent(_badgeBackground != null ? _badgeBackground.transform : card, false);
                badgeRect.anchorMin = Vector2.zero;
                badgeRect.anchorMax = Vector2.one;
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                badgeRect.offsetMin = new Vector2(12f, 0f);
                badgeRect.offsetMax = new Vector2(-12f, 0f);
                _badge.alignment = TextAlignmentOptions.Center;
                _badge.fontSize = Mathf.Max(_badge.fontSize, 21f);
                if (_badgeBackground != null)
                {
                    _badgeBackground.color = _turquoiseSoft;
                    _badgeBackground.raycastTarget = false;
                    CreateSuccessMark(_badgeBackground.transform);
                    CreateStars(_badgeBackground.transform);
                }
            }

            var buttonRect = practiceButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.zero;
            buttonRect.pivot = Vector2.zero;
            buttonRect.anchoredPosition = new Vector2(700f, 30f);
            buttonRect.sizeDelta = new Vector2(260f, 62f);

            _root = new GameObject("PracticeCameraPreview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.transform.SetParent(card, false);
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.one;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = Vector2.one;
            rootRect.anchoredPosition = new Vector2(-40f, -18f);
            rootRect.sizeDelta = new Vector2(164f, 164f);

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
            _hint.text = "Centra tu mano";
            _hint.fontSize = 16f;
            _hint.color = _cream;
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.rectTransform.anchorMin = Vector2.zero;
            _hint.rectTransform.anchorMax = Vector2.one;
            _hint.rectTransform.offsetMin = Vector2.zero;
            _hint.rectTransform.offsetMax = Vector2.zero;

            SetBadge("Referencia provisional · en revisión", PracticeBadgeTone.Neutral);
            SetVisible(false);
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

        private void CreateStars(Transform parent)
        {
            _stars = new PracticeStarGraphic[3];
            for (int i = 0; i < _stars.Length; i++)
            {
                var starObject = new GameObject($"SuccessStar{i + 1}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(PracticeStarGraphic));
                starObject.transform.SetParent(parent, false);
                var rect = starObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-12f - i * 23f, 0f);
                rect.sizeDelta = new Vector2(20f, 20f);
                _stars[i] = starObject.GetComponent<PracticeStarGraphic>();
                _stars[i].color = new Color(1f, 0.69f, 0.19f);
                starObject.SetActive(false);
            }
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

    [AddComponentMenu("")]
    internal sealed class PracticeStarGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.48f;
            vertexHelper.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float pointRadius = i % 2 == 0 ? radius : radius * 0.46f;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius;
                vertexHelper.AddVert(point, color, Vector2.zero);
            }
            for (int i = 0; i < 10; i++)
                vertexHelper.AddTriangle(0, i + 1, (i + 1) % 10 + 1);
        }
    }
}
