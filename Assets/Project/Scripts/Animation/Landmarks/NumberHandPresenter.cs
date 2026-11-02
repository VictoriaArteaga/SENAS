using TMPro;
using UnityEngine;
using Lumina.Alphabet;
using Lumina.UI;

namespace Lumina.Animation.Landmarks
{
    /// <summary>
    /// Muestra la mano 3D con la seña de un número (0 a 5) en la casa de los números. Se engancha al botón VER SEÑA del panel
    /// (<see cref="LearningLevelController.DemonstrationRequested"/>), que hoy no tiene oyentes: el número se lee del texto grande
    /// del propio panel, así no hace falta tocar ni leer miembros privados de <see cref="LearningLevelController"/>.
    /// Solo habilita VER SEÑA para los números 0 a 5 (el resto sigue mostrando el aviso que ya existía).
    /// La mano está oculta hasta que se pide; al pedirla se coloca delante de la cámara del jugador, en el lado libre del panel,
    /// con la palma hacia el jugador y los dedos hacia arriba (mano derecha, como en las imágenes de referencia), y se oculta
    /// al cambiar de número o al cerrar el panel. El 0 sigue provisional (ver <see cref="NumberPoseTestController.ShowNumber"/>).
    /// </summary>
    public sealed class NumberHandPresenter : MonoBehaviour
    {
        [Header("Escena")]
        [SerializeField] private LearningLevelController level;
        [Tooltip("Texto grande del panel con el número (el mismo que usa LearningLevelController).")]
        [SerializeField] private TMP_Text numberLabel;
        [Tooltip("Controlador de la mano 3D (en la raíz del prefab de la mano, con su HandPoseDriver).")]
        [SerializeField] private NumberPoseTestController hand;
        [Tooltip("Cámara del jugador. Vacío = Camera.main.")]
        [SerializeField] private Camera viewCamera;

        [Header("Encuadre (relativo a la cámara)")]
        [Tooltip("Posición en pantalla del centro de la mano (0 a 1). El panel ocupa el lado derecho; la mano va a la izquierda.")]
        [SerializeField, Range(0.05f, 0.95f)] private float viewportX = 0.27f;
        [SerializeField, Range(0.05f, 0.95f)] private float viewportY = 0.5f;
        [Tooltip("Distancia deseada a la cámara en metros.")]
        [SerializeField, Min(0.3f)] private float distance = 0.9f;
        [Tooltip("Distancia mínima si hay una pared u objeto delante; la mano se acerca y conserva su tamaño aparente.")]
        [SerializeField, Min(0.2f)] private float minDistance = 0.4f;
        [Tooltip("Margen respecto a lo que haya delante, en metros.")]
        [SerializeField, Min(0f)] private float clearance = 0.1f;
        [Tooltip("Altura de la mano (muñeca a punta del medio) como fracción del alto de pantalla.")]
        [SerializeField, Range(0.2f, 0.9f)] private float heightFraction = 0.5f;

        private Transform _root;
        private HandPoseDriver _driver;
        private bool _frameReady;
        private Quaternion _frameLocal;
        private Vector3 _centerLocal;
        private Vector3 _baseScale;
        private float _baseLength;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private bool _visible;
        private int _shownNumber = -1;

        /// <summary>Verdadero mientras la mano está a la vista.</summary>
        public bool IsVisible => _visible;

        private void Awake()
        {
            if (level == null || numberLabel == null || hand == null)
            {
                Debug.LogError("[NumberHand] Faltan referencias (panel, texto del número o controlador de la mano); el componente se desactiva.", this);
                enabled = false;
                return;
            }
            _root = hand.transform;
            _root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (level != null) level.DemonstrationRequested.AddListener(OnDemonstration);
        }

        private void OnDisable()
        {
            if (level != null) level.DemonstrationRequested.RemoveListener(OnDemonstration);
            if (_root != null) Hide();
        }

        private void Update()
        {
            bool open = level.IsOpen;
            int n = ReadNumber();
            if (open) level.DemonstrationAvailable = n >= 0 && n <= 5;
            if (_visible && (!open || n != _shownNumber)) Hide();
        }

        private void LateUpdate()
        {
            if (_visible) Place();
        }

        private int ReadNumber() => int.TryParse(numberLabel.text, out var n) ? n : -1;

        private void OnDemonstration()
        {
            int n = ReadNumber();
            if (n < 0 || n > 5) return;
            Present(n);
        }

        /// <summary>Muestra la mano con el número (0 a 5) delante de la cámara. Devuelve false si no se pudo.</summary>
        public bool Present(int n)
        {
            if (n < 0 || n > 5 || _root == null) return false;
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            if (!EnsureFrame())
            {
                Debug.LogError("[NumberHand] No se pudo medir el reposo de la mano (huesos J_Right_Hand… no encontrados).", this);
                Hide();
                return false;
            }
            if (!hand.ShowNumber(n))
            {
                Hide();
                return false;
            }
            _shownNumber = n;
            _visible = true;
            Place();
            Debug.Log($"[NumberHand] VER SEÑA → número {n}. {hand.Status}", this);
            return true;
        }

        /// <summary>Oculta la mano.</summary>
        public void Hide()
        {
            _visible = false;
            _shownNumber = -1;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // Mide una sola vez, con la mano en reposo, su marco (palma/dedos), su centro y su tamaño, en coordenadas locales de la raíz.
        private bool EnsureFrame()
        {
            if (_frameReady) return true;
            _driver = _root.GetComponent<HandPoseDriver>();
            if (!RigRest.TryCapture(_root, _driver, out var rest, FlexionMeasure.FlexionPlane)) return false;

            _baseScale = _root.localScale;
            _baseLength = Vector3.Distance(rest.Wrist.position, rest.Chain[2, 3].position);
            if (_baseLength < 1e-5f) return false;
            _frameLocal = Quaternion.Inverse(_root.rotation) * rest.FrameRotation;
            var center = rest.Wrist.position + rest.Frame.Finger * (_baseLength * 0.5f);
            _centerLocal = _root.InverseTransformPoint(center);
            _frameReady = true;
            return true;
        }

        private void Place()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null || !_frameReady) return;

            float d = distance;
            var origin = cam.transform.position;
            var dir = (cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, d)) - origin).normalized;
            int count = Physics.RaycastNonAlloc(origin, dir, _hits, d * 2f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(_root)) continue; // la propia mano no cuenta
                nearest = Mathf.Min(nearest, _hits[i].distance);
            }
            if (nearest < float.MaxValue)
            {
                float along = nearest * Vector3.Dot(dir, cam.transform.forward);
                d = Mathf.Clamp(along - clearance, minDistance, distance);
            }

            var target = cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, d));
            float visibleHeight = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float factor = heightFraction * visibleHeight / _baseLength;
            _root.localScale = _baseScale * factor;

            // Dedos hacia arriba y palma hacia el jugador (la palma sale hacia la cámara).
            var desired = Quaternion.LookRotation(cam.transform.up, -cam.transform.forward);
            _root.rotation = desired * Quaternion.Inverse(_frameLocal);
            _root.position = target - _root.rotation * Vector3.Scale(_centerLocal, _root.localScale);
        }
    }
}
