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
    internal sealed class AlphabetPracticeCameraPreview
    {
        private readonly TMP_Text _badge;
        private readonly Image _badgeBackground;
        private readonly Color _cream;
        private readonly Color _ink;
        private readonly Color _turquoise;
        private readonly Color _turquoiseSoft;

        private GameObject _root;
        private RawImage _image;
        private RectTransform _viewportRect;
        private TMP_Text _hint;
        private Texture _configuredTexture;
        private int _configuredRotation = -1;
        private bool _configuredVerticalFlip;
        private int _configuredFrameWidth;
        private int _configuredFrameHeight;
        private Vector2 _configuredViewportSize;

        private AlphabetPracticeCameraPreview(
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

        public static AlphabetPracticeCameraPreview Create(
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

            return new AlphabetPracticeCameraPreview(
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

        public void SetBadge(string message, PracticeBadgeTone tone)
        {
            if (_badge == null) return;
            _badge.text = message;
            _badge.color = _ink;
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
            instructionRect.sizeDelta = new Vector2(-360f, 128f);

            if (_badge != null)
            {
                var badgeRect = _badge.rectTransform;
                badgeRect.anchorMin = Vector2.zero;
                badgeRect.anchorMax = Vector2.zero;
                badgeRect.pivot = Vector2.zero;
                badgeRect.anchoredPosition = new Vector2(24f, 18f);
                badgeRect.sizeDelta = new Vector2(650f, 54f);
            }

            var buttonRect = practiceButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.zero;
            buttonRect.pivot = Vector2.zero;
            buttonRect.anchoredPosition = new Vector2(700f, 14f);
            buttonRect.sizeDelta = new Vector2(260f, 62f);

            _root = new GameObject("PracticeCameraPreview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            _root.transform.SetParent(card, false);
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.one;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = Vector2.one;
            rootRect.anchoredPosition = new Vector2(-24f, -18f);
            rootRect.sizeDelta = new Vector2(284f, 164f);

            var background = _root.GetComponent<Image>();
            var cardBackground = card.GetComponent<Image>();
            if (cardBackground != null)
            {
                background.sprite = cardBackground.sprite;
                background.type = cardBackground.type;
            }
            background.color = _ink;
            background.raycastTarget = false;
            var outline = _root.GetComponent<Outline>();
            outline.effectColor = _turquoise;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            var title = CreateText(_root.transform, "CameraPreviewTitle", instruction.font);
            title.text = "TU MANO";
            title.fontSize = 16f;
            title.fontStyle = FontStyles.Bold;
            title.color = _cream;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            title.rectTransform.sizeDelta = new Vector2(-16f, 24f);

            var viewport = new GameObject("CameraViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(_root.transform, false);
            _viewportRect = viewport.GetComponent<RectTransform>();
            _viewportRect.anchorMin = Vector2.zero;
            _viewportRect.anchorMax = Vector2.one;
            _viewportRect.offsetMin = new Vector2(8f, 8f);
            _viewportRect.offsetMax = new Vector2(-8f, -30f);

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
            _hint.text = "Pulsa PRACTICAR";
            _hint.fontSize = 13f;
            _hint.color = _cream;
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.rectTransform.anchorMin = Vector2.zero;
            _hint.rectTransform.anchorMax = Vector2.one;
            _hint.rectTransform.offsetMin = Vector2.zero;
            _hint.rectTransform.offsetMax = Vector2.zero;

            SetBadge("Referencia provisional · en revisión", PracticeBadgeTone.Neutral);
            SetVisible(false);
        }

        private void ConfigureImageSize(int frameWidth, int frameHeight, int rotation, Vector2 viewportSize)
        {
            bool quarterTurn = rotation == 90 || rotation == 270;
            float sourceAspect = (float)frameWidth / frameHeight;
            float displayAspect = quarterTurn ? 1f / sourceAspect : sourceAspect;
            float viewportAspect = viewportSize.x / viewportSize.y;

            float displayWidth;
            float displayHeight;
            if (displayAspect >= viewportAspect)
            {
                displayWidth = viewportSize.x;
                displayHeight = displayWidth / displayAspect;
            }
            else
            {
                displayHeight = viewportSize.y;
                displayWidth = displayHeight * displayAspect;
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
            text.enableWordWrapping = true;
            return text;
        }
    }
}
