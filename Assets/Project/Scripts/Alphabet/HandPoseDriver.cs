using UnityEngine;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Pone una mano riggeada (la derecha del pack "Free VR Hands") en la pose de una letra moviendo los
    /// huesos reales del rig. La mano izquierda se oculta. Pasa suavemente de una pose a otra y repite
    /// en bucle las letras con movimiento.
    /// </summary>
    public class HandPoseDriver : MonoBehaviour
    {
        private const string RightPrefix = "J_Right_Hand";

        [Header("Ejes del rig (calibrados con el modelo)")]
        [Tooltip("Signo de la flexión de dedos (eje local X). Cambia a -1 si los dedos se doblan hacia el dorso.")]
        [SerializeField] private float fingerCurlSign = 1f;
        [SerializeField] private float fingerSpreadSign = 1f;
        [SerializeField] private float thumbCurlSign = 1f;
        [SerializeField] private float thumbSpreadSign = 1f;

        [Header("Transición")]
        [SerializeField] private float blendSeconds = 0.35f;
        [SerializeField] private float loopPauseSeconds = 1.1f;

        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        private Transform[,] _bones;
        private Quaternion[,] _rest;
        private Transform _wrist;
        private Quaternion _wristRest;
        private Vector3 _wristRestPosition;

        private HandPose _from, _to, _current;
        private float _blend = 1f;
        private LetterEntry _entry;
        private float _motionTime;
        private bool _ready;

        public bool IsReady => _ready;

        private void Awake() => Initialize();

        /// <summary>Localiza los huesos del rig. Es seguro llamarlo varias veces.</summary>
        public void Initialize()
        {
            if (_ready) return;

            // El prefab trae un Animator que reaplica su pose base cada fotograma; los huesos se mueven desde aquí.
            foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            _bones = new Transform[5, 3];
            _rest = new Quaternion[5, 3];
            var all = GetComponentsInChildren<Transform>(true);

            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                {
                    string boneName = RightPrefix + FingerNames[f] + (j + 1);
                    _bones[f, j] = System.Array.Find(all, t => t.name == boneName);
                    if (_bones[f, j] == null)
                    {
                        Debug.LogError($"[HandPoseDriver] No se encontró el hueso '{boneName}' en '{name}'.", this);
                        enabled = false;
                        return;
                    }
                    _rest[f, j] = _bones[f, j].localRotation;
                }

            _wrist = System.Array.Find(all, t => t.name == "J_Right");
            var left = System.Array.Find(all, t => t.name == "J_Left");
            if (_wrist == null) { Debug.LogError("[HandPoseDriver] Falta el hueso 'J_Right'.", this); enabled = false; return; }
            if (left != null) left.localScale = Vector3.zero;   // solo se enseña una mano

            _wristRest = _wrist.localRotation;
            _wristRestPosition = _wrist.localPosition;
            _ready = true;
        }

        /// <summary>Muestra la letra: transición a su pose y, si tiene movimiento, repetición en bucle.</summary>
        public void ShowLetter(LetterEntry entry, bool instant = false)
        {
            Initialize();
            if (!_ready || entry == null) return;

            _entry = entry;
            _from = _current;
            _to = entry.pose;
            _blend = instant ? 1f : 0f;
            _motionTime = 0f;
            if (instant) { _current = _to; Apply(_current); }
        }

        /// <summary>Vuelve a reproducir la demostración desde el principio.</summary>
        public void Replay()
        {
            if (_entry == null) return;
            _from = _current;
            _to = _entry.pose;
            _blend = 0f;
            _motionTime = 0f;
        }

        private void Update()
        {
            if (!_ready || _entry == null) return;

            float dt = Time.unscaledDeltaTime;
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, blendSeconds));
                float eased = 1f - Mathf.Pow(1f - _blend, 3f);
                _current = HandPose.Lerp(_from, _to, eased);
                Apply(_current);
                return;
            }

            if (_entry.HasMotion)
            {
                _motionTime += dt;
                _current = EvaluateMotion(_entry.motion, _motionTime);
                Apply(_current);
            }
        }

        private HandPose EvaluateMotion(PoseKey[] keys, float time)
        {
            float duration = keys[keys.Length - 1].time;
            float cycle = duration + loopPauseSeconds;
            float t = time % cycle;
            if (t >= duration) return keys[keys.Length - 1].pose;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (t > keys[i + 1].time) continue;
                float span = Mathf.Max(0.0001f, keys[i + 1].time - keys[i].time);
                float k = Mathf.SmoothStep(0f, 1f, (t - keys[i].time) / span);
                return HandPose.Lerp(keys[i].pose, keys[i + 1].pose, k);
            }
            return keys[keys.Length - 1].pose;
        }

        private void Apply(HandPose pose)
        {
            ApplyFinger(0, pose.thumb, thumb: true);
            ApplyFinger(1, pose.index, thumb: false);
            ApplyFinger(2, pose.middle, thumb: false);
            ApplyFinger(3, pose.ring, thumb: false);
            ApplyFinger(4, pose.pinky, thumb: false);

            _wrist.localRotation = _wristRest * Quaternion.Euler(pose.wristEuler);
            _wrist.localPosition = _wristRestPosition + pose.wristOffset;
        }

        private void ApplyFinger(int f, FingerPose p, bool thumb)
        {
            float[] curl = { p.proximal, p.middle, p.distal };
            for (int j = 0; j < 3; j++)
            {
                Quaternion q;
                if (thumb)
                {
                    // Pulgar: flexión sobre Z local; la apertura se aplica solo en la primera articulación.
                    q = Quaternion.AngleAxis(thumbCurlSign * curl[j], Vector3.forward);
                    if (j == 0) q = Quaternion.AngleAxis(thumbSpreadSign * p.spread, Vector3.right) * q;
                }
                else
                {
                    // Dedos: flexión sobre X local; la separación se aplica solo en la primera articulación.
                    q = Quaternion.AngleAxis(fingerCurlSign * curl[j], Vector3.right);
                    if (j == 0) q = Quaternion.AngleAxis(fingerSpreadSign * p.spread, Vector3.forward) * q;
                }
                _bones[f, j].localRotation = _rest[f, j] * q;
            }
        }
    }
}
