using UnityEngine;

namespace Lumina.UI
{
    /// <summary>
    /// Indicador de interacción en pantalla ("E — Hablar con Sena"). Solo aparece y desaparece con un
    /// fundido breve; quien lo usa decide cuándo mostrarlo.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private float fadeSeconds = 0.12f;

        private CanvasGroup _group;
        private float _target;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
        }

        public void Show() => _target = 1f;

        public void Hide() => _target = 0f;

        private void Update()
        {
            if (Mathf.Approximately(_group.alpha, _target)) return;

            float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _target, step);
        }
    }
}
