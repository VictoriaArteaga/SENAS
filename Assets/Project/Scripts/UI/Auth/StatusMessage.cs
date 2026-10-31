using TMPro;
using UnityEngine;

namespace Lumina.UI
{
    /// <summary>Línea de estado del formulario: informa, avisa de un error o confirma un éxito, con color según el caso.</summary>
    public class StatusMessage : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color infoColor = new Color(0.059f, 0.227f, 0.267f);
        [SerializeField] private Color errorColor = new Color(0.78f, 0.22f, 0.18f);
        [SerializeField] private Color successColor = new Color(0.09f, 0.53f, 0.498f);

        private float _alpha;
        private float _targetAlpha;

        private void Awake()
        {
            if (label == null) { Debug.LogError($"[StatusMessage] Falta 'label' en '{name}'.", this); enabled = false; return; }
            label.text = string.Empty;
            label.alpha = 0f;
        }

        public void Info(string message) => Show(message, infoColor);
        public void Error(string message) => Show(message, errorColor);
        public void Success(string message) => Show(message, successColor);

        public void Clear()
        {
            _targetAlpha = 0f;
        }

        private void Show(string message, Color color)
        {
            label.text = message;
            label.color = color;
            _targetAlpha = 1f;
            _alpha = 0.2f;
        }

        private void Update()
        {
            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, Time.unscaledDeltaTime * 4f);
            label.alpha = _alpha;
        }
    }
}
