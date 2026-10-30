using UnityEngine;
using UnityEngine.Events;

namespace Lumina.Interaction
{
    /// <summary>
    /// Punto de interacción simple sobre ProximityInteractable: muestra un indicador al acercarse y lanza
    /// un evento al pulsar E (una puerta, un libro...). Para diálogos usa SenaWelcomeInteraction.
    /// </summary>
    [RequireComponent(typeof(ProximityInteractable))]
    public class InteractionPoint : MonoBehaviour
    {
        [SerializeField] private Lumina.UI.InteractionPrompt prompt;
        public UnityEvent onActivated;

        private ProximityInteractable _interactable;
        private bool _suspended;

        private void Awake()
        {
            _interactable = GetComponent<ProximityInteractable>();
            if (prompt == null)
            {
                Debug.LogError($"[InteractionPoint] Falta la referencia 'prompt' en '{name}'.", this);
                enabled = false;
                return;
            }

            _interactable.onPlayerEnter.AddListener(OnEnter);
            _interactable.onPlayerExit.AddListener(prompt.Hide);
            _interactable.onInteract.AddListener(OnInteract);
        }

        private void OnDestroy()
        {
            if (_interactable == null) return;
            _interactable.onPlayerEnter.RemoveListener(OnEnter);
            _interactable.onInteract.RemoveListener(OnInteract);
            if (prompt != null) _interactable.onPlayerExit.RemoveListener(prompt.Hide);
        }

        /// <summary>Oculta el indicador y bloquea la interacción mientras otro sistema usa la pantalla.</summary>
        public void SetSuspended(bool suspended)
        {
            _suspended = suspended;
            if (suspended) prompt.Hide();
            else if (_interactable.PlayerInRange) prompt.Show();
        }

        private void OnEnter()
        {
            if (!_suspended) prompt.Show();
        }

        private void OnInteract()
        {
            if (!_suspended) onActivated.Invoke();
        }
    }
}
