using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>
    /// Animación de botones al estilo de Lúmina: crece un poco con el cursor o el foco de teclado (Hover)
    /// y se hunde sobre su sombra al pulsarse (Pressed). Todo con tiempo sin escalar, para que funcione
    /// también con la pantalla en pausa o durante transiciones.
    /// </summary>
    public class UIButtonAnimator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Elemento que se hunde al pulsar (la cara del botón). Si está vacío, el propio botón no se desplaza.")]
        [SerializeField] private RectTransform face;
        [Tooltip("Imagen que se aclara en Hover. Si está vacía solo se anima la escala.")]
        [SerializeField] private Image tint;
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressedScale = 0.97f;
        [SerializeField] private float pressedDrop = 6f;
        [SerializeField, Range(0f, 0.4f)] private float hoverLighten = 0.12f;
        [SerializeField] private float speed = 16f;

        private Selectable _selectable;
        private Color _baseColor;
        private ThemedGraphic _themed;
        private Vector2 _faceBase;
        private bool _hover, _selected, _pressed;
        private float _scale = 1f, _drop, _light, _dim;

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (tint != null) _baseColor = tint.color;
            if (face != null) _faceBase = face.anchoredPosition;
        }

        private void OnEnable()
        {
            _hover = _selected = _pressed = false;
            _scale = 1f; _drop = 0f; _light = 0f; _dim = Interactable ? 0f : 1f;
            Apply();
        }

        private void Update()
        {
            float targetScale = 1f, targetDrop = 0f, targetLight = 0f;
            if (Interactable)
            {
                bool highlighted = _hover || _selected;
                if (highlighted) { targetScale = hoverScale; targetLight = 1f; }
                if (_pressed) { targetScale = pressedScale; targetDrop = 1f; targetLight = 0.4f; }
            }

            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            _scale = Mathf.Lerp(_scale, targetScale, k);
            _drop = Mathf.Lerp(_drop, targetDrop, k);
            _light = Mathf.Lerp(_light, targetLight, k);
            _dim = Mathf.Lerp(_dim, Interactable ? 0f : 1f, k);
            Apply();
        }

        // El tema puede cambiar el color base del botón (p. ej. crema → petróleo); si no hay tema se usa el original.
        private Color BaseColor
        {
            get
            {
                if (_themed == null && tint != null) _themed = tint.GetComponent<ThemedGraphic>();
                return _themed != null ? _themed.Current : _baseColor;
            }
        }

        private void Apply()
        {
            var baseColor = BaseColor;
            transform.localScale = Vector3.one * _scale;
            if (face != null) face.anchoredPosition = _faceBase + Vector2.down * (pressedDrop * _drop);
            if (tint != null)
            {
                Color lit = Color.Lerp(baseColor, Color.white, hoverLighten);
                lit.a = baseColor.a;
                Color shown = Color.Lerp(baseColor, lit, _light);
                Color grey = Color.Lerp(baseColor, new Color(0.72f, 0.72f, 0.72f, baseColor.a), 0.55f);
                tint.color = Color.Lerp(shown, grey, _dim);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) { _hover = false; _pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnSelect(BaseEventData eventData) => _selected = true;
        public void OnDeselect(BaseEventData eventData) { _selected = false; _pressed = false; }
    }
}
