using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Player;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Nivel 1: abecedario LSC. Abre la pantalla de aprendizaje (cámara dedicada a la mano 3D + interfaz),
    /// muestra la letra elegida en la mano riggeada y recuerda qué letras se han visto. "Vista" no significa
    /// "aprendida": todavía no hay evaluación.
    /// </summary>
    public class AlphabetLevelController : MonoBehaviour
    {
        [Header("Datos y mano")]
        [SerializeField] private LscAlphabet alphabet;
        [SerializeField] private HandPoseDriver hand;

        [Header("Interfaz")]
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private Button[] letterButtons;
        [SerializeField] private TMP_Text letterBig;
        [SerializeField] private TMP_Text instruction;
        [SerializeField] private TMP_Text validationBadge;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button backButton;

        [Header("Vistas de la mano (solo mueven la cámara; no tocan la pose)")]
        [SerializeField] private Button viewButton;
        [SerializeField] private TMP_Text viewLabel;
        [Tooltip("Giro de la cámara (grados) alrededor de la mano, en el eje vertical. 0 = vista frontal original.")]
        [SerializeField] private float[] viewYaws = { 0f, -90f, 180f };
        [SerializeField] private string[] viewNames = { "FRENTE", "PERFIL", "DORSO" };
        [Tooltip("Las vistas distintas de la frontal se acercan a la mano multiplicando la distancia por este valor.")]
        [SerializeField] private float sideViewDistanceScale = 0.75f;
        [Tooltip("Panel liso que se coloca detrás de la mano en las vistas laterales para que no se vea la habitación.")]
        [SerializeField] private Transform viewBackdrop;
        [SerializeField] private float backdropDistance = 0.9f;

        [Header("Cámaras y jugador")]
        [SerializeField] private Camera learningCamera;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private FirstPersonController player;
        [Tooltip("Indicadores u objetos que se ocultan mientras el nivel está abierto.")]
        [SerializeField] private GameObject[] hideWhileOpen;
        [Tooltip("Componentes que no deben reaccionar a la tecla E mientras el nivel está abierto.")]
        [SerializeField] private Behaviour[] disableWhileOpen;

        private static readonly Color Cream = new Color(1f, 0.976f, 0.925f);
        private static readonly Color Coral = new Color(1f, 0.369f, 0.302f);
        private static readonly Color Turquoise = new Color(0.18f, 0.769f, 0.714f);
        private static readonly Color TurquoiseSoft = new Color(0.56f, 0.89f, 0.85f);
        private static readonly Color Ink = new Color(0.059f, 0.227f, 0.267f);

        private Vector3 _frontPosition;
        private Quaternion _frontRotation;
        private Vector3 _viewPivot;
        private int _view;
        private bool[] _seen;
        private int _current;
        private bool _open;

        public bool IsOpen => _open;

        private void Awake()
        {
            if (alphabet == null || hand == null || uiRoot == null || learningCamera == null || player == null ||
                letterButtons == null || letterButtons.Length != alphabet.letters.Length)
            {
                Debug.LogError($"[AlphabetLevelController] Referencias incompletas en '{name}' " +
                               "(se esperan tantos botones como letras).", this);
                enabled = false;
                return;
            }

            _seen = new bool[alphabet.letters.Length];

            for (int i = 0; i < letterButtons.Length; i++)
            {
                int index = i;
                letterButtons[i].GetComponentInChildren<TMP_Text>().text = alphabet.letters[i].letter;
                letterButtons[i].onClick.AddListener(() => Select(index));

                // Con teclado o mando, moverse por la cuadrícula también cambia la letra mostrada.
                var trigger = letterButtons[i].gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                entry.callback.AddListener(_ => { if (_open && _current != index) Select(index); });
                trigger.triggers.Add(entry);
            }

            previousButton.onClick.AddListener(() => Select((_current + alphabet.letters.Length - 1) % alphabet.letters.Length));
            nextButton.onClick.AddListener(() => Select((_current + 1) % alphabet.letters.Length));
            replayButton.onClick.AddListener(hand.Replay);
            backButton.onClick.AddListener(Close);

            // La vista frontal es la posición original de la cámara; las demás la giran alrededor de la mano.
            _frontPosition = learningCamera.transform.position;
            _frontRotation = learningCamera.transform.rotation;
            // El pivote es la muñeca real de la mano (el objeto raíz de la mano no coincide con la malla visible).
            var wrist = System.Array.Find(hand.GetComponentsInChildren<Transform>(true), t => t.name == "J_Right_Hand");
            Vector3 center = wrist != null ? wrist.position : hand.transform.position;
            _viewPivot = new Vector3(center.x, _frontPosition.y, center.z);
            if (viewButton != null) viewButton.onClick.AddListener(NextView);

            uiRoot.SetActive(false);
            learningCamera.enabled = false;
        }

        public void Open()
        {
            if (_open) return;

            _open = true;
            uiRoot.SetActive(true);
            if (playerCamera != null) playerCamera.enabled = false;
            learningCamera.enabled = true;
            player.enabled = false;
            player.SetCursorLocked(false);
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(false);
            foreach (var b in disableWhileOpen) if (b != null) b.enabled = false;

            SetView(0);
            Select(_current);
        }

        /// <summary>Cambia a la siguiente vista (frente, perfil, dorso). La letra y la pose no cambian.</summary>
        public void NextView() => SetView((_view + 1) % Mathf.Min(viewYaws.Length, viewNames.Length));

        private void SetView(int index)
        {
            _view = index;
            var yaw = Quaternion.Euler(0f, viewYaws[index], 0f);
            float scale = index == 0 ? 1f : sideViewDistanceScale;
            learningCamera.transform.position = _viewPivot + yaw * ((_frontPosition - _viewPivot) * scale);
            learningCamera.transform.rotation = yaw * _frontRotation;
            if (viewBackdrop != null)
            {
                // En la vista frontal ya hay un telón; en las demás se coloca un panel liso detrás de la mano.
                viewBackdrop.gameObject.SetActive(index != 0);
                Vector3 away = learningCamera.transform.forward;
                viewBackdrop.position = _viewPivot + away * backdropDistance;
                viewBackdrop.rotation = Quaternion.LookRotation(away);
            }
            if (viewLabel != null) viewLabel.text = "VISTA: " + viewNames[index];
        }

        public void Close()
        {
            if (!_open) return;

            _open = false;
            SetView(0);
            uiRoot.SetActive(false);
            learningCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            player.enabled = true;
            player.SetCursorLocked(true);
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(true);
            foreach (var b in disableWhileOpen) if (b != null) b.enabled = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void Update()
        {
            if (!_open) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) Close();
            else if (keyboard.rKey.wasPressedThisFrame) hand.Replay();
            else if (keyboard.vKey.wasPressedThisFrame) NextView();
            else if (keyboard.pageUpKey.wasPressedThisFrame || keyboard.commaKey.wasPressedThisFrame) previousButton.onClick.Invoke();
            else if (keyboard.pageDownKey.wasPressedThisFrame || keyboard.periodKey.wasPressedThisFrame) nextButton.onClick.Invoke();
        }

        private void Select(int index)
        {
            _current = index;
            _seen[index] = true;
            var entry = alphabet.letters[index];

            hand.ShowLetter(entry);
            letterBig.text = entry.letter;
            instruction.text = entry.instruction;
            validationBadge.text = entry.validation == PoseValidation.ApproximateFromReference
                ? "Referencia aproximada · pendiente de validación"
                : "Pose provisional · pendiente de validación";

            int seen = 0;
            foreach (bool s in _seen) if (s) seen++;
            progressLabel.text = $"Letras vistas  {seen}/{_seen.Length}";

            RefreshButtons();

            // Mantiene el foco en la letra activa para navegar con teclado.
            var selected = letterButtons[index].gameObject;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != selected)
                EventSystem.current.SetSelectedGameObject(selected);
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < letterButtons.Length; i++)
            {
                Color baseColor = i == _current ? Coral : _seen[i] ? TurquoiseSoft : Cream;
                var colors = letterButtons[i].colors;
                colors.normalColor = baseColor;
                colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.3f);
                colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.12f);
                colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
                colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
                letterButtons[i].colors = colors;
                letterButtons[i].GetComponentInChildren<TMP_Text>().color = i == _current ? Cream : Ink;
            }
        }
    }
}
