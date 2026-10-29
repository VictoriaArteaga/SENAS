using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Lumina.Player;

namespace Lumina.Interaction
{
    /// <summary>
    /// Detecta cuando el jugador se acerca (por distancia horizontal, sin depender de la física)
    /// y emite eventos. No contiene lógica de lecciones: otros sistemas se suscriben a los eventos.
    /// </summary>
    public class ProximityInteractable : MonoBehaviour
    {
        [SerializeField] private float interactionRadius = 3f;
        [SerializeField] private string promptText = "Presiona E para hablar";
        [SerializeField] private bool showPrompt = true;

        public UnityEvent onPlayerEnter;
        public UnityEvent onPlayerExit;
        public UnityEvent onInteract;

        private Transform _player;
        private bool _promptSuppressed;

        public bool PlayerInRange { get; private set; }
        public Transform Player => _player;

        /// <summary>Oculta el aviso de interacción mientras otro sistema (p. ej. un diálogo) ya está en uso.</summary>
        public void SetPromptSuppressed(bool suppressed) => _promptSuppressed = suppressed;

        private void Start()
        {
            var controller = FindFirstObjectByType<FirstPersonController>();
            if (controller != null) _player = controller.transform;
        }

        private void Update()
        {
            if (_player == null) return;

            Vector3 offset = _player.position - transform.position;
            offset.y = 0f;
            bool inRange = offset.sqrMagnitude <= interactionRadius * interactionRadius;

            if (inRange != PlayerInRange)
            {
                PlayerInRange = inRange;
                (inRange ? onPlayerEnter : onPlayerExit).Invoke();
            }

            var keyboard = Keyboard.current;
            if (PlayerInRange && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                onInteract.Invoke();
        }

        private void OnGUI()
        {
            if (!showPrompt || _promptSuppressed || !PlayerInRange) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22
            };
            GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40), promptText, style);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
