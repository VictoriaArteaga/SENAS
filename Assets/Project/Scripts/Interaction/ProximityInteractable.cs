using System.Collections.Generic;
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

        private static readonly List<ProximityInteractable> Active = new List<ProximityInteractable>();

        private Transform _player;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// Si hay varios a la vez en rango (p. ej. Sena y la puerta de salida), E solo responde al que el jugador tiene más de frente:
        /// así hablar con Sena no activa también la salida que queda a su espalda.
        /// </summary>
        private bool IsMostInFront()
        {
            var cam = Camera.main;
            Vector3 look = cam != null ? cam.transform.forward : _player.forward;
            look.y = 0f;
            ProximityInteractable winner = null;
            float best = float.MaxValue;
            foreach (var other in Active)
            {
                if (other == null || !other.PlayerInRange) continue;
                Vector3 to = other.transform.position - _player.position;
                to.y = 0f;
                float angle = to.sqrMagnitude < 0.01f ? 0f : Vector3.Angle(look, to);
                if (angle < best) { best = angle; winner = other; }
            }
            return winner == this;
        }

        public bool PlayerInRange { get; private set; }
        public Transform Player => _player;

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
            if (PlayerInRange && keyboard != null && keyboard.eKey.wasPressedThisFrame && IsMostInFront())
                onInteract.Invoke();
        }

        private void OnGUI()
        {
            if (!showPrompt || !PlayerInRange) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22
            };
            GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40), Lumina.Core.Loc.Translate(promptText), style);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
