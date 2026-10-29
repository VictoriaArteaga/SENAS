using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>
    /// Burbuja de diálogo en el mundo 3D (Canvas World Space). Solo se encarga de la presentación:
    /// muestra un nombre y un mensaje, se orienta hacia la cámara, mantiene un tamaño legible según la
    /// distancia y entra/sale con una transición breve. La lógica de cuándo abrirse vive en quien la usa.
    /// </summary>
    public class DialogueBubble : MonoBehaviour
    {
        [Header("Contenido")]
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button closeButton;

        [Header("Presentación")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Transform viewCamera;

        [Header("Tamaño según distancia")]
        [Tooltip("Distancia a la que la burbuja tiene su escala nominal.")]
        [SerializeField] private float referenceDistance = 3f;
        [SerializeField] private float minDistanceScale = 0.5f;
        [SerializeField] private float maxDistanceScale = 1.25f;

        [Header("Transición")]
        [Tooltip("Desactívalo para que la burbuja aparezca y desaparezca sin animación.")]
        [SerializeField] private bool animate = true;
        [SerializeField] private float openSeconds = 0.16f;
        [SerializeField] private float closeSeconds = 0.10f;
        [SerializeField, Range(0.8f, 1f)] private float hiddenScale = 0.94f;

        // Progreso lineal 0 (oculta) → 1 (visible). La presentación se deriva de este único valor,
        // así que abrir y cerrar repetidamente retoma desde el estado actual sin procesos paralelos.
        private float _progress;
        private bool _wantsOpen;
        private Transform _visualTransform;
        private Vector3 _visualBaseScale;

        public bool IsOpen => _wantsOpen;

        /// <summary>Se emite una vez cada vez que la burbuja pasa de abierta a cerrada.</summary>
        public event Action Closed;

        private void Awake()
        {
            if (!Validate()) { enabled = false; return; }

            _visualTransform = visualRoot.transform;
            _visualBaseScale = _visualTransform.localScale;
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;

            closeButton.onClick.AddListener(Close);
            // El controlador del jugador recupera el cursor al pulsar el ratón; cerrar al presionar
            // (no al soltar) evita que el clic se pierda por ese cambio de estado del cursor.
            var trigger = closeButton.gameObject.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(_ => Close());
            trigger.triggers.Add(pointerDown);

            Apply(0f);
        }

        private bool Validate()
        {
            bool ok = true;
            ok &= Require(speakerLabel, nameof(speakerLabel));
            ok &= Require(messageLabel, nameof(messageLabel));
            ok &= Require(closeButton, nameof(closeButton));
            ok &= Require(visualRoot, nameof(visualRoot));
            ok &= Require(canvasGroup, nameof(canvasGroup));
            return ok;
        }

        private bool Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null) return true;
            Debug.LogError($"[DialogueBubble] Falta la referencia '{fieldName}' en '{name}'.", this);
            return false;
        }

        public void Show(string speaker, string message)
        {
            if (!enabled || _wantsOpen) return;

            speakerLabel.text = speaker;
            messageLabel.text = message;
            _wantsOpen = true;
            visualRoot.SetActive(true);
            canvasGroup.blocksRaycasts = true;
            if (!animate) Apply(1f);
        }

        public void Close()
        {
            if (!_wantsOpen) return;

            _wantsOpen = false;
            canvasGroup.blocksRaycasts = false;
            if (!animate) Apply(0f);
            Closed?.Invoke();
        }

        private void Update()
        {
            if (!animate) return;

            float target = _wantsOpen ? 1f : 0f;
            if (Mathf.Approximately(_progress, target)) return;

            float duration = _wantsOpen ? openSeconds : closeSeconds;
            float step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
            Apply(Mathf.MoveTowards(_progress, target, step));
        }

        private void LateUpdate()
        {
            if (!visualRoot.activeSelf || viewCamera == null) return;

            // Giro solo en vertical: el texto siempre queda recto y de cara al jugador.
            Vector3 toCamera = viewCamera.position - transform.position;
            float distance = toCamera.magnitude;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(-toCamera);

            float distanceScale = Mathf.Clamp(distance / referenceDistance, minDistanceScale, maxDistanceScale);
            transform.localScale = Vector3.one * distanceScale;
        }

        private void Apply(float progress)
        {
            _progress = progress;

            // Salida de ease-out fuerte: arranca rápido y asienta suave (solo opacidad y escala).
            float eased = 1f - Mathf.Pow(1f - progress, 4f);
            canvasGroup.alpha = eased;
            _visualTransform.localScale = _visualBaseScale * Mathf.Lerp(hiddenScale, 1f, eased);

            if (progress <= 0f && !_wantsOpen)
                visualRoot.SetActive(false);
        }
    }
}
