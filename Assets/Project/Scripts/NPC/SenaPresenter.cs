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
        [Tooltip("Saluda con la animación «Wave» del controlador del avatar apenas carga la escena.")]
        [SerializeField] private bool waveOnStart;
        [SerializeField] private float waveDelay = 0.8f;
        [SerializeField] private string waveTrigger = "Wave";

        private ProximityInteractable _interactable;
        private Quaternion _homeRotation;
        private Animator _animator;
        private float _waveAt = -1f;

        /// <summary>Saluda ahora (si el avatar tiene el disparador «Wave»).</summary>
        public void Wave()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;
            foreach (var p in _animator.parameters)
                if (p.name == waveTrigger && p.type == AnimatorControllerParameterType.Trigger) { _animator.SetTrigger(waveTrigger); return; }
        }

        private void Awake()
        {
            _interactable = GetComponent<ProximityInteractable>();
            _homeRotation = transform.rotation;
            if (waveOnStart) _waveAt = Time.time + waveDelay;
        }

        private void Update()
        {
            if (_waveAt >= 0f && Time.time >= _waveAt) { _waveAt = -1f; Wave(); }

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
