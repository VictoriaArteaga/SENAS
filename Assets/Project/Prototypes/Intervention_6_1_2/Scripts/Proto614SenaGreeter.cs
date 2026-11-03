using UnityEngine;
using Lumina.Player;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Presencia de Sena en el prototipo (6.1.4): sin diálogos ni interacciones. Se gira con suavidad hacia el jugador cuando está
    /// cerca y, al acercarse por primera vez, lo saluda con la animación «Wave» que ya existe en el controlador del avatar
    /// (AvatarAnimator_Locomotion). El resto del tiempo el controlador mantiene su estado por defecto (Locomotion → Idle_00).
    /// No modifica Sena.prefab: solo vive en la variante del prototipo.
    /// </summary>
    public class Proto614SenaGreeter : MonoBehaviour
    {
        [SerializeField] float lookRadius = 11f;
        [SerializeField] float waveRadius = 9f;
        [SerializeField] float turnSpeed = 2.5f;
        [SerializeField] float waveCooldown = 25f;
        [SerializeField] string waveTrigger = "Wave";
        [SerializeField] string idleState = "Locomotion";

        Animator _anim; Transform _player; Quaternion _home;
        float _nextWave; float _waveStarted = -99f; bool _hasWave;

        public bool HasAnimator => _anim != null;
        public string CurrentStateName => _anim != null && _anim.GetCurrentAnimatorStateInfo(0).IsName(idleState) ? idleState : (_anim != null && _anim.GetCurrentAnimatorStateInfo(0).IsName("Wave") ? "Wave" : "otro");

        void Start()
        {
            _anim = GetComponentInChildren<Animator>();           // solo los activos: el avatar masculino se desactiva
            var fpc = FindFirstObjectByType<FirstPersonController>(); if (fpc != null) _player = fpc.transform;
            _home = transform.rotation;
            if (_anim != null) foreach (var p in _anim.parameters) if (p.name == waveTrigger && p.type == AnimatorControllerParameterType.Trigger) _hasWave = true;
        }

        void Update()
        {
            if (_player == null) return;
            var to = _player.position - transform.position; to.y = 0f; float d = to.magnitude;
            var target = d < lookRadius && d > 0.5f ? Quaternion.LookRotation(to) : _home;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);

            if (_anim == null) return;
            if (_hasWave && d < waveRadius && Time.time >= _nextWave)
            {
                _anim.SetTrigger(waveTrigger); _nextWave = Time.time + waveCooldown; _waveStarted = Time.time;
            }
            // Seguro: si el controlador no vuelve solo a Idle tras el saludo, se le devuelve a su estado por defecto.
            if (_waveStarted > 0f && Time.time - _waveStarted > 4.6f) { _waveStarted = -99f; if (!_anim.GetCurrentAnimatorStateInfo(0).IsName(idleState)) _anim.CrossFade(idleState, 0.25f); }
        }
    }
}
