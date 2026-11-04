using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;
using Lumina.Player;
using Lumina.Signs;
using Lumina.UI;
using Senas.AI.MediaPipe;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Nivel 1: abecedario LSC. Abre la pantalla de aprendizaje (cámara dedicada a la mano 3D + interfaz),
    /// muestra la letra elegida en la mano riggeada, recuerda letras vistas y permite practicar con evaluación
    /// geométrica inicial mediante webcam. Una coincidencia no equivale a validación lingüística.
    /// El estado de cada letra (bloqueada, disponible, superada) y la mejor precisión salen del BACKEND, y cada práctica
    /// reconocida se envía como POST /evaluations; aquí no se guarda ni se calcula progreso. Lo único local es qué letras se
    /// han visto en esta visita.
    /// </summary>
    public class AlphabetLevelController : MonoBehaviour, IPracticePanel
    {
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
        [SerializeField] private float[] viewYaws = { 0f, -90f };
        [SerializeField] private string[] viewNames = { "FRENTE", "PERFIL" };
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
        private bool[] _practiced;          // true = el backend dice que la letra está PASSED
        private string[] _statuses;         // LOCKED / AVAILABLE / PASSED por letra, según el backend
        private float[] _bestPrecision;     // 0–100, mejor precisión guardada en el backend
        private bool _statesLoaded;
        private bool _loadingStates;
        private bool _submitting;
        private float _practiceStartedAt;
        private CancellationTokenSource _cts;
        private int _current;
        private bool _open;
        private HandLandmarkerCapture _practiceCapture;
        private AlphabetLandmarkEvaluator _practiceEvaluator;
        private string _practiceStatus = "Practica la letra seleccionada con la webcam.";
        private float _practiceSimilarity;
        private bool _showPracticeFeedback;
        private bool _hasPracticeScore;
        private AlphabetPracticePanelView _practiceView;
        private PracticeBadgeTone _practiceBadgeTone;

        public bool IsOpen => _open;

        /// <summary>True si el panel ya se abrió al menos una vez en esta visita (para ofrecer «Continúa practicando»).</summary>
        public bool WasOpened { get; private set; }

        /// <summary>Abre la práctica en la primera letra sin superar (lo que ya hace <see cref="Open"/>).</summary>
        public void OpenPractice() => Open();

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
            _statuses = new string[alphabet.letters.Length];
            _bestPrecision = new float[alphabet.letters.Length];
            _cts = new CancellationTokenSource();

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

            // Paneles por proporciones de pantalla y con layouts automáticos (nada se sale ni se sobrepone).
            AlphabetResponsiveLayout.Apply(uiRoot);

            _practiceView = AlphabetPracticePanelView.Create(
                instruction, validationBadge, replayButton, Cream, Ink, Turquoise, TurquoiseSoft);

            uiRoot.SetActive(false);
            learningCamera.enabled = false;
            hand.enabled = false; // la mano 3D solo se mueve mientras el nivel está abierto
        }

        public void Open()
        {
            if (_open) return;

            _open = true;
            WasOpened = true;
            hand.Initialize();
            hand.enabled = true;
            uiRoot.SetActive(true);
            if (playerCamera != null) playerCamera.enabled = false;
            learningCamera.enabled = true;
            player.enabled = false;
            player.SetCursorLocked(false);
            _practiceView?.SetVisible(true);
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(false);
            foreach (var b in disableWhileOpen) if (b != null) b.enabled = false;

            SetView(0);
            _practiceStatus = "Practica la letra seleccionada con la webcam.";
            _practiceSimilarity = 0f;
            _showPracticeFeedback = false;
            _hasPracticeScore = false;
            // Con lo que haya en la caché (si ya se cargó) se muestra algo enseguida; después se pide el estado al backend.
            ApplyStates(ProgressStore.GetLevel(SignCodes.AlphabetLevel));
            SelectInitialLetter();
            if (!_statesLoaded)
            {
                _practiceStatus = Loc.T("alphabet.loading");
                _showPracticeFeedback = true;
                _practiceBadgeTone = PracticeBadgeTone.Info;
                RefreshPracticeUI();
            }
            LoadStates();
        }

        private void SelectInitialLetter()
        {
            var completed = _statesLoaded && CountPracticedLetters() == _practiced.Length;
            _current = completed ? _practiced.Length - 1 : _statesLoaded ? GetNextUnpracticedIndex() : 0;
            Select(_current);
            RefreshPracticeHint();
            if (completed)
            {
                _practiceStatus = "¡Nivel 1 completado! Terminaste el abecedario LSC.";
                _showPracticeFeedback = true;
                RefreshPracticeUI();
            }
        }

        /// <summary>Pide al backend el estado de las 27 letras (GET /levels/ALPHABET) y repinta con él.</summary>
        private async void LoadStates()
        {
            if (_loadingStates) return;
            _loadingStates = true;
            try
            {
                var detail = await ProgressStore.LoadLevelAsync(SignCodes.AlphabetLevel, _cts.Token);
                if (this == null) return;
                bool first = !_statesLoaded;
                ApplyStates(detail);
                if (first && _open) SelectInitialLetter();
                else if (_open) { RefreshButtons(); RefreshPracticeUI(); }
            }
            catch (OperationCanceledException) { }
            catch (ApiException e)
            {
                if (this == null) return;
                _practiceStatus = _statesLoaded ? ApiMessages.For(e) : Loc.T("alphabet.loadFailed") + " " + ApiMessages.For(e);
                _practiceBadgeTone = PracticeBadgeTone.Warning;
                _showPracticeFeedback = true;
                if (_open) { RefreshButtons(); RefreshPracticeUI(); }
                Toast.Show(ApiMessages.For(e));
            }
            finally
            {
                if (this != null) _loadingStates = false;
            }
        }

        /// <summary>Copia el estado del backend a los arreglos de la pantalla. Con null (sin datos) se marca como no cargado.</summary>
        private void ApplyStates(LevelDetailDto detail)
        {
            if (detail?.signs == null) { _statesLoaded = false; return; }

            for (int i = 0; i < alphabet.letters.Length; i++)
            {
                string code = SignCodes.ForLetter(alphabet.letters[i].letter);
                string status = SignStatusNames.Locked;
                float best = 0f;
                foreach (var sign in detail.signs)
                {
                    if (!string.Equals(sign.code, code, StringComparison.OrdinalIgnoreCase)) continue;
                    status = sign.status;
                    best = sign.bestPrecision;
                    break;
                }
                _statuses[i] = status;
                _bestPrecision[i] = best;
                _practiced[i] = status == SignStatusNames.Passed;
            }
            _statesLoaded = true;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
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
            if (viewLabel != null) viewLabel.text = Loc.T("alphabet.view", Loc.Translate(viewNames[index]));
        }

        public void Close()
        {
            if (!_open) return;

            _open = false;
            StopPractice();
            hand.enabled = false; // al salir de la práctica la mano 3D se detiene
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
            if (_practiceCapture != null)
                _practiceView?.UpdateFrame(_practiceCapture.CameraTexture);
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
                _practiceStatus = _statesLoaded && index > 0
                    ? Loc.T("alphabet.completeToUnlock", alphabet.letters[index - 1].letter, alphabet.letters[index].letter)
                    : Loc.T(_loadingStates ? "alphabet.loading" : "alphabet.loadFailed");
                _practiceSimilarity = 0f;
                _hasPracticeScore = false;
                _showPracticeFeedback = true;
                _practiceBadgeTone = PracticeBadgeTone.Warning;
                RefreshButtons();
                RefreshPracticeUI();
                return;
            }

            if (_current != index)
            {
                if (_practiceCapture != null) StopPractice();
                _practiceStatus = "Practica la letra seleccionada con la webcam.";
                _practiceSimilarity = 0f;
                _showPracticeFeedback = false;
                _hasPracticeScore = false;
                _practiceBadgeTone = PracticeBadgeTone.Neutral;
            }
            _current = index;
            RefreshPracticeHint();
            _seen[index] = true; // solo para esta visita: no se guarda en disco
            var entry = alphabet.letters[index];

            hand.ShowLetter(entry);
            letterBig.text = entry.letter;
            instruction.text = Loc.Translate(entry.instruction);
            validationBadge.text = Loc.Translate(entry.validation == PoseValidation.ApproximateFromReference
                ? "Referencia aproximada · pendiente de validación"
                : "Pose provisional · pendiente de validación");

            int seen = 0;
            foreach (bool s in _seen) if (s) seen++;
            progressLabel.text = Loc.T("alphabet.lettersSeen", seen, _seen.Length);

            RefreshButtons();
            RefreshPracticeUI();

            // Mantiene el foco en la letra activa para navegar con teclado.
            var selected = letterButtons[index].gameObject;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != selected)
                EventSystem.current.SetSelectedGameObject(selected);
        }

        private void StartPractice()
        {
            if (_submitting) return;
            if (!_statesLoaded)
            {
                _practiceStatus = Loc.T(_loadingStates ? "alphabet.loading" : "alphabet.loadFailed");
                _practiceBadgeTone = PracticeBadgeTone.Warning;
                _showPracticeFeedback = true;
                RefreshPracticeUI();
                LoadStates();
                return;
            }
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
            _practiceStartedAt = Time.realtimeSinceStartup;
            _practiceSimilarity = 0f;
            _hasPracticeScore = false;
            _practiceStatus = "Activando cámara… centra la mano en el recuadro.";
            _showPracticeFeedback = true;
            _practiceBadgeTone = PracticeBadgeTone.Info;
            _practiceView?.SetVisible(true);
            _practiceView?.SetHint("Conectando cámara…");
            RefreshPracticeUI();
        }

        private void TogglePractice()
        {
            if (_submitting) return;
            if (_practiceCapture != null)
            {
                StopPractice();
                _practiceStatus = "Práctica detenida.";
                _showPracticeFeedback = true;
                _practiceBadgeTone = PracticeBadgeTone.Neutral;
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
            _practiceView?.ClearFrame(GetPracticeHint(), _open);
        }

        private string GetPracticeHint()
        {
            if (!_open) return "La vista de tu mano aparecerá aquí.";
            if (_practiced != null && _current >= 0 && _current < _practiced.Length && _practiced[_current])
                return "Pulsa REPETIR para volver a practicar.";
            return "Pulsa PRACTICAR para activar la cámara.";
        }

        private void RefreshPracticeHint()
        {
            _practiceView?.SetHint(GetPracticeHint());
        }

        private void OnPracticeEvaluation(AlphabetLandmarkEvaluation evaluation)
        {
            if (evaluation == null) return;
            _practiceSimilarity = evaluation.Similarity;
            if (evaluation.Status == AlphabetEvaluationStatus.NoHand)
            {
                _hasPracticeScore = false;
                _practiceStatus = "No veo tu mano · centra y mejora la luz.";
                _practiceBadgeTone = PracticeBadgeTone.Warning;
                RefreshPracticeUI();
                return;
            }

            if (evaluation.Status == AlphabetEvaluationStatus.NoTemplates ||
                evaluation.Status == AlphabetEvaluationStatus.MotionTemplateNeedsSequence)
            {
                _hasPracticeScore = false;
                _practiceStatus = "Falta la referencia de esta letra.";
                _practiceBadgeTone = PracticeBadgeTone.Warning;
                RefreshPracticeUI();
                return;
            }

            if (evaluation.Status == AlphabetEvaluationStatus.MotionPending)
            {
                _hasPracticeScore = false;
                _practiceStatus = $"Haz el movimiento completo de {alphabet.letters[_current].letter}.";
                _practiceBadgeTone = PracticeBadgeTone.Info;
                RefreshPracticeUI();
                return;
            }

            _hasPracticeScore = evaluation.Status == AlphabetEvaluationStatus.Recognized ||
                                evaluation.Status == AlphabetEvaluationStatus.Uncertain;

            if (evaluation.Status == AlphabetEvaluationStatus.Recognized &&
                string.Equals(evaluation.CandidateLetter, alphabet.letters[_current].letter,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                if (evaluation.Similarity < minimumPracticeSimilarity)
                {
                    _practiceStatus = "Aún no alcanza el mínimo. Ajusta la forma y orientación.";
                    _practiceBadgeTone = PracticeBadgeTone.Warning;
                    RefreshPracticeUI();
                    return;
                }

                // Reconocida: precisión, estrellas y XP según la misma tabla que el reconocedor de números
                // (ScoreAdapter, solución temporal) y envío al backend, que decide si aprueba y qué desbloquea.
                var letterIndex = _current;
                var score = ScoreAdapter.FromSimilarity(evaluation.Similarity);
                StopPractice();
                SubmitPractice(letterIndex, score);
                return;
            }

            if (evaluation.Similarity < minimumPracticeSimilarity)
            {
                _practiceStatus = "Ajusta dedos y pulgar según el modelo e inténtalo de nuevo.";
                _practiceBadgeTone = PracticeBadgeTone.Warning;
            }
            else
            {
                _practiceStatus = "Vas bien. Mantén la seña quieta un momento para completar la evaluación.";
                _practiceBadgeTone = PracticeBadgeTone.Info;
            }
            RefreshPracticeUI();
        }

        /// <summary>Envía el resultado de la letra al backend y muestra lo que él responde (XP otorgada, desbloqueos).</summary>
        private async void SubmitPractice(int letterIndex, ScoreAdapter.Score score)
        {
            if (_submitting) return;
            _submitting = true;
            string letter = alphabet.letters[letterIndex].letter;
            _practiceStatus = Loc.T("alphabet.saving");
            _practiceBadgeTone = PracticeBadgeTone.Info;
            _showPracticeFeedback = true;
            RefreshPracticeUI();

            int durationMs = Mathf.RoundToInt((Time.realtimeSinceStartup - _practiceStartedAt) * 1000f);
            var submission = EvaluationService.Submit(SignCodes.ForLetter(letter), score.Precision, score.Stars, score.Xp, durationMs);
            try
            {
                await submission.Completion;
                if (this == null) return;

                if (submission.State == EvaluationSubmission.SubmissionState.Succeeded)
                {
                    await submission.ProgressRefreshed;
                    if (this == null) return;
                    var response = submission.Response;
                    ApplyStates(ProgressStore.GetLevel(SignCodes.AlphabetLevel));
                    if (!_statesLoaded) _statesLoaded = true;
                    // Si la recarga del progreso no llegó, se refleja al menos lo que el backend acaba de responder.
                    if (response.passed && _statuses[letterIndex] != SignStatusNames.Passed)
                    {
                        _statuses[letterIndex] = SignStatusNames.Passed;
                        _practiced[letterIndex] = true;
                        _bestPrecision[letterIndex] = Mathf.Max(_bestPrecision[letterIndex], response.bestPrecision);
                        int next = letterIndex + 1;
                        if (!string.IsNullOrEmpty(response.unlockedSign) && next < _statuses.Length && _statuses[next] == SignStatusNames.Locked)
                            _statuses[next] = SignStatusNames.Available;
                    }

                    _practiceSimilarity = response.precision / 100f;
                    _hasPracticeScore = true;
                    _practiceStatus = response.awardedXp > 0
                        ? Loc.T("alphabet.passed", letter, response.awardedXp, response.totalXp)
                        : Loc.T("alphabet.passedNoXp", letter);
                    if (!string.IsNullOrEmpty(response.unlockedSign))
                        _practiceStatus += " " + Loc.T("alphabet.nextUnlocked", SignCodes.DisplayName(response.unlockedSign));
                    _practiceBadgeTone = PracticeBadgeTone.Success;
                    if (response.unlockedLevels != null)
                        foreach (var level in response.unlockedLevels)
                            Toast.Show(Loc.T("results.unlockedLevel", Loc.Translate(SignCodes.SpanishLevelName(level))), 5f);
                }
                else
                {
                    // Sin conexión, sesión terminada, resultado inconsistente…: no se cambia nada en pantalla.
                    _hasPracticeScore = false;
                    _practiceStatus = ApiMessages.For(submission.Error) + " " + Loc.T("alphabet.notSaved");
                    _practiceBadgeTone = PracticeBadgeTone.Warning;
                }
            }
            finally
            {
                if (this != null) _submitting = false;
            }

            _showPracticeFeedback = true;
            RefreshButtons();
            RefreshPracticeUI();
        }

        private void RefreshPracticeUI()
        {
            if (alphabet == null || alphabet.letters == null || alphabet.letters.Length == 0) return;
            var entry = alphabet.letters[_current];
            if (entry == null) return;

            instruction.text = Loc.Translate(entry.instruction);
            var levelCompleted = CountPracticedLetters() == _practiced.Length;
            bool letterPassed = _practiced[_current];
            bool attemptRunning = _showPracticeFeedback && _practiceCapture != null;
            if (levelCompleted && !attemptRunning)
                _practiceView?.SetBadge("ABECEDARIO COMPLETADO", PracticeBadgeTone.Success);
            else if (attemptRunning && _hasPracticeScore)
                _practiceView?.SetBadge(Loc.T(_practiceBadgeTone == PracticeBadgeTone.Warning ? "alphabet.matchAdjust" : "alphabet.matchHold",
                    _practiceSimilarity.ToString("P0")), _practiceBadgeTone);
            else if (attemptRunning)
                _practiceView?.SetBadge(_practiceStatus, _practiceBadgeTone);
            else if (levelCompleted)
                _practiceView?.SetBadge("ABECEDARIO COMPLETADO", PracticeBadgeTone.Success);
            else if (letterPassed)
            {
                bool hasSavedScore = _hasPracticeScore || _bestPrecision[_current] > 0f;
                float displayedScore = _hasPracticeScore
                    ? _practiceSimilarity
                    : _bestPrecision[_current] / 100f;
                _practiceView?.SetBadge(hasSavedScore
                    ? Loc.T("alphabet.letterPassedScore", entry.letter, displayedScore.ToString("P0"))
                    : Loc.T("alphabet.letterPassed", entry.letter), PracticeBadgeTone.Success);
            }
            else if (_showPracticeFeedback && _hasPracticeScore)
                _practiceView?.SetBadge(Loc.T(_practiceBadgeTone == PracticeBadgeTone.Warning ? "alphabet.matchAdjust" : "alphabet.matchHold",
                    _practiceSimilarity.ToString("P0")), _practiceBadgeTone);
            else if (_showPracticeFeedback)
                _practiceView?.SetBadge(_practiceStatus, _practiceBadgeTone);
            else
                _practiceView?.SetBadge(entry.validation == PoseValidation.ApproximateFromReference
                    ? "Referencia aproximada · en revisión"
                    : "Pose provisional · en revisión", PracticeBadgeTone.Neutral); // el panel lo traduce

            var passed = CountPracticedLetters();
            int seen = 0;
            foreach (var value in _seen) if (value) seen++;
            progressLabel.text = Loc.T("alphabet.progress", seen, _seen.Length, passed, _practiced.Length);

            var buttonLabel = replayButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
                buttonLabel.text = Loc.T(_practiceCapture != null ? "alphabet.stop" : _practiced[_current] ? "alphabet.repeat" : "practice.go");
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

        /// <summary>
        /// Una letra se puede abrir si el backend no la marca LOCKED. Mientras no se conozca el estado (cargando, sin sesión o sin
        /// conexión) solo se puede VER la primera letra; no se calcula ningún desbloqueo en local.
        /// </summary>
        private bool IsUnlocked(int index)
        {
            if (index < 0 || index >= alphabet.letters.Length) return false;
            if (!_statesLoaded) return index == 0;
            return _statuses[index] != SignStatusNames.Locked;
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
