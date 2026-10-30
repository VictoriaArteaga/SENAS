using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Player;
using Senas.AI.MediaPipe;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Nivel 1: abecedario LSC. Abre la pantalla de aprendizaje (cámara dedicada a la mano 3D + interfaz),
    /// muestra la letra elegida en la mano riggeada, recuerda letras vistas y permite practicar con evaluación
    /// geométrica inicial mediante webcam. Una coincidencia no equivale a validación lingüística.
    /// </summary>
    public class AlphabetLevelController : MonoBehaviour
    {
        private const string SeenLetterPreferencePrefix = "Lumina.Alphabet.Seen.";
        private const string PracticedLetterPreferencePrefix = "Lumina.Alphabet.Practiced.Qualified.v1.";

        [Header("Datos y mano")]
        [SerializeField] private LscAlphabet alphabet;
        [SerializeField] private HandPoseDriver hand;
        [SerializeField] private AlphabetLandmarkTemplateSet landmarkTemplates;
        [SerializeField, Range(0f, 1f)] private float minimumPracticeSimilarity = 0.6f;

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
        private bool[] _practiced;
        private int _current;
        private bool _open;
        private HandLandmarkerCapture _practiceCapture;
        private AlphabetLandmarkEvaluator _practiceEvaluator;
        private string _practiceStatus = "Practica la letra seleccionada con la webcam.";
        private float _practiceSimilarity;
        private bool _showPracticeFeedback;

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
            _practiced = new bool[alphabet.letters.Length];
            LoadSeenLetters();

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
            replayButton.onClick.AddListener(TogglePractice);
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
            _practiceStatus = "Practica la letra seleccionada con la webcam.";
            _practiceSimilarity = 0f;
            _showPracticeFeedback = false;
            _current = GetNextUnpracticedIndex();
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
            StopPractice();
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
            if (!IsUnlocked(index))
            {
                _practiceStatus = $"Primero completa la letra {alphabet.letters[index - 1].letter} para desbloquear {alphabet.letters[index].letter}.";
                RefreshButtons();
                return;
            }

            if (_current != index)
            {
                if (_practiceCapture != null) StopPractice();
                _practiceStatus = "Practica la letra seleccionada con la webcam.";
                _practiceSimilarity = 0f;
                _showPracticeFeedback = false;
            }
            _current = index;
            if (!_seen[index])
            {
                _seen[index] = true;
                PlayerPrefs.SetInt(GetSeenPreferenceKey(alphabet.letters[index].letter), 1);
                PlayerPrefs.Save();
            }
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
            RefreshPracticeUI();

            // Mantiene el foco en la letra activa para navegar con teclado.
            var selected = letterButtons[index].gameObject;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != selected)
                EventSystem.current.SetSelectedGameObject(selected);
        }

        private void LoadSeenLetters()
        {
            for (var i = 0; i < alphabet.letters.Length; i++)
            {
                var entry = alphabet.letters[i];
                _seen[i] = entry != null && PlayerPrefs.GetInt(GetSeenPreferenceKey(entry.letter), 0) == 1;
                _practiced[i] = entry != null && PlayerPrefs.GetInt(GetPracticedPreferenceKey(entry.letter), 0) == 1;
            }
        }

        private static string GetSeenPreferenceKey(string letter)
        {
            return SeenLetterPreferencePrefix + letter;
        }

        private static string GetPracticedPreferenceKey(string letter)
        {
            return PracticedLetterPreferencePrefix + letter;
        }

        private void StartPractice()
        {
            if (landmarkTemplates == null)
            {
                _practiceStatus = "Falta asignar AlphabetLandmarkTemplates en la escena.";
                _showPracticeFeedback = true;
                RefreshPracticeUI();
                return;
            }

            StopPractice();
            var captureObject = new GameObject("SENAS Alphabet Practice Capture");
            captureObject.transform.SetParent(transform, false);
            _practiceCapture = captureObject.AddComponent<HandLandmarkerCapture>();
            _practiceEvaluator = captureObject.AddComponent<AlphabetLandmarkEvaluator>();
            _practiceEvaluator.Configure(landmarkTemplates, false, alphabet.letters[_current].letter);
            _practiceEvaluator.EvaluationUpdated += OnPracticeEvaluation;
            _practiceSimilarity = 0f;
            _practiceStatus = "Iniciando webcam… muestra la seña completa.";
            _showPracticeFeedback = true;
            RefreshPracticeUI();
        }

        private void TogglePractice()
        {
            if (_practiceCapture != null)
            {
                StopPractice();
                _practiceStatus = "Práctica detenida.";
                _showPracticeFeedback = true;
                RefreshPracticeUI();
                return;
            }

            StartPractice();
        }

        private void StopPractice()
        {
            if (_practiceEvaluator != null)
                _practiceEvaluator.EvaluationUpdated -= OnPracticeEvaluation;

            if (_practiceCapture != null)
                Destroy(_practiceCapture.gameObject);

            _practiceCapture = null;
            _practiceEvaluator = null;
        }

        private void OnPracticeEvaluation(AlphabetLandmarkEvaluation evaluation)
        {
            if (evaluation == null) return;
            _practiceSimilarity = evaluation.Similarity;
            if (evaluation.Status == AlphabetEvaluationStatus.NoHand)
            {
                _practiceStatus = "No detecto la mano. Centra la mano y mejora la iluminación.";
                RefreshPracticeUI();
                return;
            }

            if (evaluation.Status == AlphabetEvaluationStatus.NoTemplates ||
                evaluation.Status == AlphabetEvaluationStatus.MotionTemplateNeedsSequence)
            {
                _practiceStatus = $"No hay una secuencia de referencia suficiente para {alphabet.letters[_current].letter}. Captúrala antes de practicarla.";
                RefreshPracticeUI();
                return;
            }

            if (evaluation.Status == AlphabetEvaluationStatus.Recognized &&
                string.Equals(evaluation.CandidateLetter, alphabet.letters[_current].letter,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                if (evaluation.Similarity < minimumPracticeSimilarity)
                {
                    _practiceStatus = $"Aún no alcanza el mínimo de {minimumPracticeSimilarity:P0}. Ajusta la forma y orientación según el modelo y vuelve a intentar.";
                    RefreshPracticeUI();
                    return;
                }

                var letter = alphabet.letters[_current].letter;
                _practiced[_current] = true;
                PlayerPrefs.SetInt(GetPracticedPreferenceKey(letter), 1);
                PlayerPrefs.Save();
                _practiceStatus = $"¡Bien hecho! {letter} superó el mínimo. Ya puedes avanzar a la siguiente letra.";
                RefreshButtons();
                StopPractice();
                _showPracticeFeedback = true;
                RefreshPracticeUI();
                return;
            }

            if (evaluation.Similarity < minimumPracticeSimilarity)
                _practiceStatus = "La forma todavía no coincide. Compara dedos, pulgar y orientación con el modelo 3D; inténtalo de nuevo.";
            else
                _practiceStatus = "Vas bien. Mantén la seña quieta un momento para completar la evaluación.";
            RefreshPracticeUI();
        }

        private void RefreshPracticeUI()
        {
            if (alphabet == null || alphabet.letters == null || alphabet.letters.Length == 0) return;
            var entry = alphabet.letters[_current];
            if (entry == null) return;

            instruction.text = _showPracticeFeedback
                ? $"{entry.instruction}\n\n{_practiceStatus}"
                : entry.instruction;
            validationBadge.text = _showPracticeFeedback
                ? $"Precisión estimada {_practiceSimilarity:P0} · {GetPracticeStars(_practiceSimilarity)} estrellas · provisional"
                : entry.validation == PoseValidation.ApproximateFromReference
                    ? "Referencia aproximada · pendiente de validación"
                    : "Pose provisional · pendiente de validación";

            var passed = CountPracticedLetters();
            int seen = 0;
            foreach (var value in _seen) if (value) seen++;
            progressLabel.text = $"Vistas {seen}/{_seen.Length} · Superadas {passed}/{_practiced.Length}";

            var buttonLabel = replayButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null) buttonLabel.text = _practiceCapture != null ? "DETENER" : "PRACTICAR";
        }

        private int CountPracticedLetters()
        {
            var count = 0;
            foreach (var practiced in _practiced) if (practiced) count++;
            return count;
        }

        private int GetNextUnpracticedIndex()
        {
            for (var i = 0; i < _practiced.Length; i++)
                if (!_practiced[i]) return i;
            return 0;
        }

        private static int GetPracticeStars(float similarity)
        {
            if (similarity >= 0.9f) return 3;
            if (similarity >= 0.75f) return 2;
            if (similarity >= 0.6f) return 1;
            return 0;
        }

        private bool IsUnlocked(int index)
        {
            if (index < 0 || index >= alphabet.letters.Length) return false;
            for (var i = 0; i < index; i++)
                if (!_practiced[i]) return false;
            return true;
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < letterButtons.Length; i++)
            {
                bool unlocked = IsUnlocked(i);
                Color baseColor = i == _current ? Coral : !unlocked
                    ? new Color(0.78f, 0.78f, 0.78f)
                    : _seen[i] ? TurquoiseSoft : Cream;
                var colors = letterButtons[i].colors;
                colors.normalColor = baseColor;
                colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.3f);
                colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.12f);
                colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
                colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
                letterButtons[i].colors = colors;
                letterButtons[i].GetComponentInChildren<TMP_Text>().color = i == _current ? Cream : unlocked ? Ink : Color.gray;
            }
        }
    }
}
