using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Lumina.Interaction;
using Lumina.Player;
using Lumina.UI;

namespace Lumina.NPC
{
    /// <summary>
    /// Primera interacción con Sena: al pulsar E dentro del radio de proximidad abre la bienvenida,
    /// que se avanza frase a frase (E o botón). No bloquea al jugador y se cierra sola al alejarse.
    /// Una vez completada no vuelve a iniciarse.
    /// </summary>
    [RequireComponent(typeof(ProximityInteractable))]
    public class SenaWelcomeInteraction : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private DialogueBubble bubble;
        [SerializeField] private InteractionPrompt prompt;
        [SerializeField] private FirstPersonController player;

        [Header("Contenido")]
        [SerializeField] private string speakerName = "Sena";
        [SerializeField] private string nextLabel = "SIGUIENTE";
        [SerializeField] private string finishLabel = "¡LISTO!";
        [SerializeField, TextArea(2, 4)] private string[] welcomeLines =
        {
            "¡Hola! Soy Sena.",
            "Te doy la bienvenida a Lúmina.",
            "Aquí aprenderás a comunicarte con tus manos.",
            "Yo te acompañaré durante tu aprendizaje.",
            "Cuando estés listo, comenzaremos con tu primera seña."
        };

        [Header("Eventos")]
        [Tooltip("Se emite una vez, al terminar la última frase de la bienvenida.")]
        public UnityEvent onWelcomeCompleted;

        private ProximityInteractable _interactable;
        private int _lineIndex;
        private bool _completed;
        private bool _cursorWasLocked;

        private void Awake()
        {
            _interactable = GetComponent<ProximityInteractable>();

            if (bubble == null || prompt == null || welcomeLines == null || welcomeLines.Length == 0)
            {
                Debug.LogError($"[SenaWelcomeInteraction] Falta 'bubble', 'prompt' o 'welcomeLines' en '{name}'.", this);
                enabled = false;
                return;
            }

            _interactable.onPlayerEnter.AddListener(OnPlayerEnter);
            _interactable.onPlayerExit.AddListener(OnPlayerExit);
            _interactable.onInteract.AddListener(OnInteract);
            bubble.ActionPressed += Advance;
            bubble.Closed += OnBubbleClosed;
        }

        private void OnDestroy()
        {
            if (_interactable != null)
            {
                _interactable.onPlayerEnter.RemoveListener(OnPlayerEnter);
                _interactable.onPlayerExit.RemoveListener(OnPlayerExit);
                _interactable.onInteract.RemoveListener(OnInteract);
            }
            if (bubble != null)
            {
                bubble.ActionPressed -= Advance;
                bubble.Closed -= OnBubbleClosed;
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

        private void OnPlayerEnter()
        {
            if (!_completed && !bubble.IsOpen) prompt.Show();
        }

        private void OnPlayerExit()
        {
            prompt.Hide();
            bubble.Close();
        }

        private void OnInteract()
        {
            if (_completed) return;

            if (bubble.IsOpen) Advance();
            else Open();
        }

        private void Open()
        {
            _lineIndex = 0;
            prompt.Hide();
            bubble.Show(speakerName, welcomeLines[0], ActionText(), ProgressText());
        }

        private void Advance()
        {
            if (!bubble.IsOpen) return;

            _lineIndex++;
            if (_lineIndex >= welcomeLines.Length)
            {
                _completed = true;
                bubble.Close();
                onWelcomeCompleted.Invoke();
                return;
            }

            bubble.SetMessage(welcomeLines[_lineIndex], ActionText(), ProgressText());
        }

        // Si se cierra sin terminar (Esc o alejarse), la bienvenida empieza de nuevo la próxima vez.
        private void OnBubbleClosed()
        {
            if (!_completed && _interactable.PlayerInRange) prompt.Show();
        }

        private string ActionText() => _lineIndex == welcomeLines.Length - 1 ? finishLabel : nextLabel;

        private string ProgressText() => $"{_lineIndex + 1}/{welcomeLines.Length}";
    }
}
