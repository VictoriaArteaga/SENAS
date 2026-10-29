using UnityEngine;
using UnityEngine.InputSystem;
using Lumina.Interaction;
using Lumina.Player;
using Lumina.UI;

namespace Lumina.NPC
{
    /// <summary>
    /// Primera interacción con Sena: al pulsar E dentro del radio de proximidad abre una única
    /// bienvenida. No bloquea al jugador; la burbuja se cierra sola al alejarse.
    /// </summary>
    [RequireComponent(typeof(ProximityInteractable))]
    public class SenaWelcomeInteraction : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private DialogueBubble bubble;
        [SerializeField] private FirstPersonController player;

        [Header("Contenido")]
        [SerializeField] private string speakerName = "Sena";
        [SerializeField, TextArea(3, 6)] private string welcomeMessage =
            "¡Hola! Te doy la bienvenida a Lúmina. Aquí las manos comparten ideas, emociones e historias. " +
            "Soy Sena y te acompañaré mientras descubres la Lengua de Señas Colombiana. ¿Comenzamos?";

        private ProximityInteractable _interactable;
        private bool _cursorWasLocked;

        private void Awake()
        {
            _interactable = GetComponent<ProximityInteractable>();

            if (bubble == null)
            {
                Debug.LogError($"[SenaWelcomeInteraction] Falta la referencia 'bubble' en '{name}'.", this);
                enabled = false;
                return;
            }

            _interactable.onInteract.AddListener(OpenWelcome);
            _interactable.onPlayerExit.AddListener(bubble.Close);
            bubble.Closed += OnBubbleClosed;
        }

        private void OnDestroy()
        {
            if (bubble != null) bubble.Closed -= OnBubbleClosed;
            if (_interactable != null)
            {
                _interactable.onInteract.RemoveListener(OpenWelcome);
                if (bubble != null) _interactable.onPlayerExit.RemoveListener(bubble.Close);
            }
        }

        private void Update()
        {
            if (!bubble.IsOpen || player == null) return;

            // Con el cursor bloqueado, Esc lo libera (comportamiento del controlador). Esc cierra el
            // diálogo solo cuando el cursor ya estaba libre, para que una pulsación no haga ambas cosas.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !_cursorWasLocked)
                bubble.Close();
        }

        private void LateUpdate()
        {
            if (player != null) _cursorWasLocked = player.CursorLocked;
        }

        private void OpenWelcome()
        {
            bubble.Show(speakerName, welcomeMessage);
            _interactable.SetPromptSuppressed(true);
        }

        private void OnBubbleClosed()
        {
            _interactable.SetPromptSuppressed(false);
        }
    }
}
