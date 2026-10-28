using UnityEngine;
using Lumina.Interaction;

namespace Lumina.NPC
{
    /// <summary>
    /// Presentación de Sena: se orienta hacia el jugador cuando está cerca y vuelve a su
    /// orientación original al alejarse. No incluye diálogos ni lógica de lecciones.
    /// </summary>
    [RequireComponent(typeof(ProximityInteractable))]
    public class SenaPresenter : MonoBehaviour
    {
        [SerializeField] private float turnSpeed = 4f;

        private ProximityInteractable _interactable;
        private Quaternion _homeRotation;

        private void Awake()
        {
            _interactable = GetComponent<ProximityInteractable>();
            _homeRotation = transform.rotation;
        }

        private void Update()
        {
            Quaternion target = _homeRotation;

            if (_interactable.PlayerInRange && _interactable.Player != null)
            {
                Vector3 toPlayer = _interactable.Player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                    target = Quaternion.LookRotation(toPlayer);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
