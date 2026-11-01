using TMPro;
using UnityEngine;

namespace Lumina.UI
{
    /// <summary>Barra de progreso horizontal con relleno suave y texto opcional.</summary>
    public class ProgressBarUI : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float speed = 6f;

        private float _target, _shown;

        public void Set(float fraction, string text = null)
        {
            _target = Mathf.Clamp01(fraction);
            if (label != null && text != null) label.text = text;
        }

        private void Update()
        {
            _shown = Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            if (fill != null) fill.anchorMax = new Vector2(_shown, 1f);
        }
    }
}
