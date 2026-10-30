using UnityEngine;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Pone una mano riggeada (la derecha del pack "Free VR Hands") en la pose de una letra moviendo los
    /// huesos reales del rig. La mano izquierda se oculta. Pasa suavemente de una pose a otra y repite
    /// en bucle las letras con movimiento. Los ejes y signos de HandPose están descritos en HandPose.cs.
    ///
    /// Decisiones técnicas:
    ///  · Animator: el prefab trae un Animator con un clip "Idle" que reescribiría los huesos cada fotograma.
    ///    Se le quita el controlador en la instancia (el componente sigue activo y el prefab no se modifica):
    ///    sin controlador no evalúa animación, así que no hay que desactivar el componente ni aplicar la pose
    ///    en LateUpdate (que además dejaría los huesos no controlados con el "idle").
    ///  · Mano izquierda: se lleva su hueso raíz a la muñeca derecha y se escala a 0, de modo que sus vértices
    ///    colapsan dentro de la mano visible. Así no amplía los límites de la malla.
    ///  · Límites de la malla: se calculan una vez a partir de la malla ya colapsada, con un margen para el
    ///    desplazamiento de la muñeca, y se fijan (sin updateWhenOffscreen).
    ///  · Aducción: el reposo del rig tiene los dedos en abanico. Al iniciar se calibra, para cada dedo, el giro
    ///    de "spread" que lo deja paralelo al dedo medio; HandPose.adduction mezcla entre 0 (reposo) y 1 (juntos).
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
        [SerializeField] private float thumbTwistSign = 1f;

        [Header("Pulgar: preset de oposición (se mezcla con FingerPose.opposition)")]
        [Tooltip("Ángulos que se suman al pulgar cuando opposition = 1. Calibrado numéricamente para acercar la yema del pulgar a las yemas de índice y medio semiflexionados (pinza); " +
                 "queda a ~1 cm. Combínalo con la flexión del pulgar para ajustar el contacto en cada letra. Sin giros axiales grandes para no deformar la malla.")]
        [SerializeField] private FingerPose oppositionPreset = new FingerPose { spread = 29.6f, twist = 29.6f, spreadMiddle = 40f };

        [Header("Límites de la malla")]
        [Tooltip("Margen (metros) añadido a los límites de la mano visible para cubrir el desplazamiento de la muñeca.")]
        [SerializeField] private float boundsMargin = 0.07f;

        [Header("Transición")]
        [SerializeField] private float blendSeconds = 0.35f;
        [SerializeField] private float loopPauseSeconds = 1.1f;

        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        private Transform[,] _bones;
        private Quaternion[,] _rest;
        private float[] _closeAngle;      // spread (en las unidades de FingerPose.spread) que deja cada dedo paralelo al medio
        private Transform _wrist;
        private Quaternion _wristRest;
        private Vector3 _wristRestPosition;

        private HandPose _from, _to, _current;
        private float _blend = 1f;
        private LetterEntry _entry;
        private float _motionTime;
        private bool _ready;

        public bool IsReady => _ready;

        /// <summary>Spread calibrado (grados) que lleva el dedo a quedar paralelo al medio. 0 para el medio y el pulgar.</summary>
        public float GetCloseAngle(int finger) => _closeAngle != null ? _closeAngle[finger] : 0f;

        private void Awake() => Initialize();

        /// <summary>Localiza los huesos del rig y prepara la mano. Es seguro llamarlo varias veces.</summary>
        public void Initialize()
        {
            if (_ready) return;

            // Ver "Animator" en el comentario de la clase.
            foreach (var animator in GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null) animator.runtimeAnimatorController = null;

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

            _wristRest = _wrist.localRotation;
            _wristRestPosition = _wrist.localPosition;

            CalibrateAdduction();

            if (left != null)
            {
                // Se oculta solo una mano: su raíz va a la muñeca derecha y se escala a 0 (sus vértices colapsan ahí).
                left.position = _wrist.position;
                left.localScale = Vector3.zero;
            }
            FitMeshBounds();

            _ready = true;
        }

        // Para cada dedo (salvo pulgar y medio): giro de spread que lo deja paralelo al dedo medio en la pose de reposo.
        // Se busca numéricamente el ángulo (en el plano de la palma) que minimiza la diferencia de dirección entre la
        // base y la punta del dedo y las del medio; así no depende de que el eje de giro sea exactamente perpendicular.
        private void CalibrateAdduction()
        {
            _closeAngle = new float[5];
            Vector3 middleDir = (_bones[2, 2].position - _bones[2, 0].position).normalized;
            foreach (int f in new[] { 1, 3, 4 })
            {
                float best = 0f, bestError = float.MaxValue;
                for (int pass = 0; pass < 3; pass++)
                {
                    float center = best, range = pass == 0 ? 40f : (pass == 1 ? 2f : 0.2f), step = pass == 0 ? 1f : (pass == 1 ? 0.1f : 0.01f);
                    for (float angle = center - range; angle <= center + range; angle += step)
                    {
                        _bones[f, 0].localRotation = _rest[f, 0] * Quaternion.AngleAxis(fingerSpreadSign * angle, Vector3.forward);
                        Vector3 dir = (_bones[f, 2].position - _bones[f, 0].position).normalized;
                        float error = Vector3.Angle(dir, middleDir);
                        if (error < bestError) { bestError = error; best = angle; }
                    }
                }
                _bones[f, 0].localRotation = _rest[f, 0];
                _closeAngle[f] = best;
            }
        }

        private void FitMeshBounds()
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.rootBone == null) return;

            // Se hornea la malla con la mano izquierda ya colapsada y se miden sus vértices (los "bounds" de la malla
            // horneada no se recalculan), pasados al espacio local del hueso raíz.
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var vertices = baked.vertices;
            if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
            if (vertices.Length == 0) return;

            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var v in vertices)
            {
                Vector3 p = smr.rootBone.InverseTransformPoint(smr.transform.TransformPoint(v));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }

            var fitted = new Bounds((min + max) * 0.5f, max - min);
            fitted.Expand(boundsMargin * 2f);
            smr.updateWhenOffscreen = false;
            smr.localBounds = fitted;
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

        /// <summary>Aplica una pose directamente (herramientas y pruebas).</summary>
        public void SetPose(HandPose pose)
        {
            Initialize();
            if (!_ready) return;
            _entry = null;
            _current = pose;
            Apply(pose);
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
            ApplyThumb(pose.thumb);
            ApplyFinger(1, pose.index, pose.adduction);
            ApplyFinger(2, pose.middle, pose.adduction);
            ApplyFinger(3, pose.ring, pose.adduction);
            ApplyFinger(4, pose.pinky, pose.adduction);

            _wrist.localRotation = _wristRest * Quaternion.Euler(pose.wristEuler);
            _wrist.localPosition = _wristRestPosition + pose.wristOffset;
        }

        private void ApplyFinger(int f, FingerPose p, float handAdduction)
        {
            // Aducción total: 0 = reposo, 1 = paralelo al medio. Acotada para evitar giros absurdos.
            float adduction = Mathf.Clamp(handAdduction + p.adduction, -2f, 1.25f);
            float spread = p.spread + adduction * _closeAngle[f];

            float[] curl = { p.proximal, p.middle, p.distal };
            for (int j = 0; j < 3; j++)
            {
                // Flexión sobre X local; la separación se aplica solo en la primera articulación.
                Quaternion q = Quaternion.AngleAxis(fingerCurlSign * curl[j], Vector3.right);
                if (j == 0) q = Quaternion.AngleAxis(fingerSpreadSign * spread, Vector3.forward) * q;
                _bones[f, j].localRotation = _rest[f, j] * q;
            }
        }

        private void ApplyThumb(FingerPose p)
        {
            float o = Mathf.Clamp01(p.opposition);
            float spread = p.spread + o * oppositionPreset.spread;
            float twist = p.twist + o * oppositionPreset.twist;
            float spreadMiddle = p.spreadMiddle + o * oppositionPreset.spreadMiddle;
            float c0 = p.proximal + o * oppositionPreset.proximal;
            float c1 = p.middle + o * oppositionPreset.middle;
            float c2 = p.distal + o * oppositionPreset.distal;

            // 1.ª articulación: apertura (X) · giro axial (Y) · flexión (Z). Con twist = 0 equivale a la versión anterior.
            Quaternion q0 = Quaternion.AngleAxis(thumbSpreadSign * spread, Vector3.right)
                          * Quaternion.AngleAxis(thumbTwistSign * twist, Vector3.up)
                          * Quaternion.AngleAxis(thumbCurlSign * c0, Vector3.forward);
            Quaternion q1 = Quaternion.AngleAxis(thumbSpreadSign * spreadMiddle, Vector3.right)
                          * Quaternion.AngleAxis(thumbCurlSign * c1, Vector3.forward);
            Quaternion q2 = Quaternion.AngleAxis(thumbCurlSign * c2, Vector3.forward);

            _bones[0, 0].localRotation = _rest[0, 0] * q0;
            _bones[0, 1].localRotation = _rest[0, 1] * q1;
            _bones[0, 2].localRotation = _rest[0, 2] * q2;
        }
    }
}
