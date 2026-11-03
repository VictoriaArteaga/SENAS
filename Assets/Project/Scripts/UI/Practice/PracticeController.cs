using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;
using Lumina.Signs;
using Senas.AI.MediaPipe;

namespace Lumina.UI
{
    /// <summary>
    /// Flujo de PRÁCTICA: selección → aprendizaje → preparación → práctica → Results.
    /// El reconocimiento entra por <see cref="SetRecognizer"/>; los niveles de números y saludos configuran MediaPipe al volver desde sus casas.
    /// La demostración del avatar entra por <see cref="DemonstrationRequested"/>.
    /// </summary>
    public class PracticeController : MonoBehaviour
    {
        private enum Step { Selection, Learning, Preparation, Practice }

        [Header("Datos")]
        [SerializeField] private SignCatalog catalog;
        [SerializeField] private AlphabetLandmarkTemplateSet numberTemplates;
        [Tooltip("Secuencias capturadas de saludos y meses (GreetingLandmarkTemplates.asset).")]
        [SerializeField] private AlphabetLandmarkTemplateSet greetingTemplates;

        [Header("Cabecera")]
        [SerializeField] private SceneHeaderUI header;

        [Header("Selección")]
        [SerializeField] private GameObject selectionPanel;
        [Tooltip("Accesos a las casas de aprendizaje (Números, Meses, Saludos); solo se ven al elegir la seña.")]
        [SerializeField] private GameObject levelShortcuts;
        [SerializeField] private RectTransform categoryContainer;
        [SerializeField] private RectTransform signContainer;
        [SerializeField] private Button categoryTemplate;
        [SerializeField] private Button signTemplate;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button practiceButton;

        [Header("Aprendizaje")]
        [SerializeField] private GameObject learningPanel;
        [SerializeField] private GameObject demoStage;
        [SerializeField] private TMP_Text learningSignLabel;
        [SerializeField] private TMP_Text learningInstructionLabel;
        [SerializeField] private TMP_Text demoNotice;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button myTurnButton;

        [Header("Preparación y práctica")]
        [SerializeField] private GameObject sessionPanel;
        [SerializeField] private TMP_Text sessionSignLabel;
        [SerializeField] private TMP_Text sessionInstructionLabel;
        [SerializeField] private TMP_Text countdownLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private CameraFeedUI cameraFeed;
        [SerializeField] private RawImage cameraView;
        [SerializeField] private Button startButton;
        [SerializeField] private Button finishButton;

        [Header("Ajustes")]
        [SerializeField] private int countdownSeconds = 3;

        /// <summary>Se lanza al pulsar VER DE NUEVO. TODO(avatar): conectar la animación de la seña en el personaje.</summary>
        public UnityEvent DemonstrationRequested = new UnityEvent();

        /// <summary>Poner a true cuando haya una demostración animada; mientras tanto VER DE NUEVO avisa de que aún no existe.</summary>
        public bool DemonstrationAvailable { get; set; }

        private Step _step;
        private SignCategory _category;
        private SignEntry _sign;
        private ISignRecognizer _recognizer;
        private MediaPipeNumberSignRecognizer _numberRecognizer;
        private MediaPipeGreetingSignRecognizer _greetingRecognizer;
        private Coroutine _countdown;

        /// <summary>Conecta o sustituye el reconocedor activo de esta sesión.</summary>
        public void SetRecognizer(ISignRecognizer recognizer)
        {
            if (_recognizer != null) _recognizer.Completed -= OnAttemptCompleted;
            if (_recognizer is MediaPipeNumberSignRecognizer previous)
                previous.StatusUpdated -= OnRecognizerStatusUpdated;
            if (_recognizer is MediaPipeGreetingSignRecognizer previousGreeting)
                previousGreeting.StatusUpdated -= OnRecognizerStatusUpdated;
            _recognizer = recognizer;
            if (_recognizer != null) _recognizer.Completed += OnAttemptCompleted;
            if (_recognizer is MediaPipeNumberSignRecognizer current)
                current.StatusUpdated += OnRecognizerStatusUpdated;
            if (_recognizer is MediaPipeGreetingSignRecognizer currentGreeting)
                currentGreeting.StatusUpdated += OnRecognizerStatusUpdated;
        }

        private void OnRecognizerStatusUpdated(string message)
        {
            if (_step == Step.Practice && statusLabel != null) statusLabel.text = message;
        }

        private void Awake()
        {
            categoryTemplate.gameObject.SetActive(false);
            signTemplate.gameObject.SetActive(false);
            replayButton.onClick.AddListener(OnReplay);
            practiceButton.onClick.AddListener(() => Enter(Step.Learning));
            myTurnButton.onClick.AddListener(() => Enter(Step.Preparation));
            startButton.onClick.AddListener(StartCountdown);
            finishButton.onClick.AddListener(() => Finish(null));
        }

        private void OnReplay()
        {
            if (DemonstrationAvailable) DemonstrationRequested.Invoke();
            else demoNotice.gameObject.SetActive(true);
        }

        private void OnEnable() => GameSettings.LanguageChanged += RefreshTexts;
        private void OnDisable() => GameSettings.LanguageChanged -= RefreshTexts;

        /// <summary>Vuelve a escribir los textos del estado actual al cambiar el idioma, sin reiniciar cámara ni cuenta atrás.</summary>
        private void RefreshTexts()
        {
            if (_sign == null && _step != Step.Selection) return;
            switch (_step)
            {
                case Step.Selection: header.SetTitle("title.practice"); BuildSelection(); break;
                case Step.Learning: learningInstructionLabel.text = _sign.GetInstruction(); break;
                case Step.Preparation: sessionInstructionLabel.text = Loc.T("practice.prepare"); break;
                case Step.Practice:
                    PracticeSession.MarkAttemptStarted();
                    sessionInstructionLabel.text = Loc.T("practice.doSign");
                    statusLabel.text = Loc.T(_recognizer != null ? "practice.analyzing" : "practice.noRecognition");
                    break;
            }
        }

        private void Start()
        {
            // El reconocimiento de números existe para toda la sección (no solo al venir de la casa de números); se activa
            // solo cuando la seña elegida es un número (ver UseRecognizerFor).
            if (numberTemplates != null)
            {
                _numberRecognizer = gameObject.AddComponent<MediaPipeNumberSignRecognizer>();
                _numberRecognizer.Configure(numberTemplates, cameraFeed);
            }

            if (greetingTemplates != null)
            {
                _greetingRecognizer = gameObject.AddComponent<MediaPipeGreetingSignRecognizer>();
                _greetingRecognizer.Configure(greetingTemplates, cameraFeed);
            }

            _category = catalog != null && catalog.categories.Length > 0 ? catalog.categories[0] : null;
            if (PracticeSession.StartAtLearning && PracticeSession.Sign != null)
            {
                // Viene de una casa de aprendizaje: se abre directo en APRENDE con la seña elegida allí.
                PracticeSession.StartAtLearning = false;
                _sign = PracticeSession.Sign;
                Enter(Step.Learning);
                return;
            }
            PracticeSession.ReturnScene = null;
            Enter(Step.Selection);
        }

        private void OnDestroy() => SetRecognizer(null);

        private void UseRecognizerFor(SignEntry sign) =>
            SetRecognizer(IsGreetingOrMonth(sign?.id) ? _greetingRecognizer
                : SignCodes.IsNumber(sign?.id) ? _numberRecognizer
                : null);

        private static bool IsGreetingOrMonth(string id) =>
            !string.IsNullOrEmpty(id) && (id.StartsWith("saludos.") || id.StartsWith("meses."));

        private void Enter(Step step)
        {
            _step = step;
            StopCountdown();
            selectionPanel.SetActive(step == Step.Selection);
            learningPanel.SetActive(step == Step.Learning);
            demoStage.SetActive(step == Step.Selection || step == Step.Learning);
            if (levelShortcuts != null) levelShortcuts.SetActive(step == Step.Selection);
            sessionPanel.SetActive(step == Step.Preparation || step == Step.Practice);
            if (step != Step.Preparation && step != Step.Practice) cameraFeed.Stop();

            switch (step)
            {
                case Step.Selection:
                    header.SetTitle("title.practice");
                    header.ResetBackHandler();
                    BuildSelection();
                    break;

                case Step.Learning:
                    header.SetTitle("title.learn");
                    if (PracticeSession.ReturnScene != null) header.SetBackHandler(() => ScreenFader.Load(PracticeSession.ReturnScene));
                    else header.SetBackHandler(() => Enter(Step.Selection));
                    learningSignLabel.text = _sign.GetName().ToUpperInvariant();
                    learningInstructionLabel.text = _sign.GetInstruction();
                    demoNotice.gameObject.SetActive(false);
                    Focus(myTurnButton);
                    break;

                case Step.Preparation:
                    header.SetTitle("title.practice");
                    header.SetBackHandler(() => Enter(Step.Learning));
                    sessionSignLabel.text = _sign.GetName().ToUpperInvariant();
                    UseRecognizerFor(_sign);
                    sessionInstructionLabel.text = Loc.T("practice.prepare");
                    countdownLabel.text = string.Empty;
                    statusLabel.text = string.Empty;
                    startButton.gameObject.SetActive(true);
                    startButton.interactable = true;
                    finishButton.gameObject.SetActive(false);
                    cameraFeed.Show();
                    Focus(startButton);
                    break;

                case Step.Practice:
                    sessionInstructionLabel.text = Loc.T("practice.doSign");
                    countdownLabel.text = string.Empty;
                    startButton.gameObject.SetActive(false);
                    finishButton.gameObject.SetActive(true);
                    if (_recognizer != null)
                    {
                        statusLabel.text = Loc.T("practice.analyzing");
                        _recognizer.Begin(_sign, cameraView);
                    }
                    else
                    {
                        // TODO(MediaPipe): sin reconocedor no hay análisis; se dice la verdad en vez de simular uno.
                        statusLabel.text = Loc.T("practice.noRecognition");
                    }
                    Focus(finishButton);
                    break;
            }
        }

        // ---------- Selección ----------

        private void BuildSelection(bool focusSign = false)
        {
            Clear(categoryContainer, categoryTemplate);
            Clear(signContainer, signTemplate);

            if (catalog == null || catalog.categories.Length == 0)
            {
                emptyLabel.gameObject.SetActive(true);
                emptyLabel.text = Loc.T("practice.noSigns");
                return;
            }

            Button selectedTab = null;
            foreach (var category in catalog.categories)
            {
                var captured = category;
                bool selected = category == _category;
                var tab = Spawn(categoryTemplate, categoryContainer, category.LocalizedName.ToUpperInvariant(), selected);
                tab.onClick.AddListener(() => { _category = captured; _sign = null; BuildSelection(); });
                if (selected) selectedTab = tab;
            }

            bool hasSigns = _category != null && _category.signs.Length > 0;
            emptyLabel.gameObject.SetActive(!hasSigns);
            emptyLabel.text = Loc.T("practice.noSignsInCategory");
            if (hasSigns)
            {
                foreach (var sign in _category.signs)
                {
                    var captured = sign;
                    var button = Spawn(signTemplate, signContainer, ButtonLabel(sign), sign == _sign);
                    button.onClick.AddListener(() => { _sign = captured; PracticeSession.Begin(captured); BuildSelection(true); });
                }
            }

            practiceButton.interactable = _sign != null;
            practiceButton.GetComponentInChildren<TMP_Text>().text = Loc.T(_sign != null ? "practice.go" : "practice.choose");
            Focus(focusSign ? practiceButton : selectedTab);
        }

        /// <summary>Texto del botón de una seña: el nombre en mayúsculas; los números llevan además la cifra («7 · SIETE»).</summary>
        private static string ButtonLabel(SignEntry sign)
        {
            string name = sign.GetName().ToUpperInvariant();
            return SignCodes.IsNumber(sign.id) ? SignCodes.DisplayName(sign.id) + " · " + name : name; // los meses ya se llaman «Enero»…
        }

        /// <summary>Crea un botón desde la plantilla. La pestaña elegida se marca con subrayado además del color.</summary>
        internal static Button Spawn(Button template, RectTransform parent, string text, bool selected)
        {
            var button = Instantiate(template, parent);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            if (selected)
            {
                label.fontStyle |= FontStyles.Underline;
                var face = button.transform.Find("Face")?.GetComponent<Image>();
                if (face != null) ThemedGraphic.Set(face, new Color(0.059f, 0.227f, 0.267f));
                ThemedGraphic.Set(label, new Color(1f, 0.976f, 0.925f));
            }
            button.gameObject.SetActive(true);
            return button;
        }

        private static void Clear(RectTransform container, Button template)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                if (child != template.gameObject) Destroy(child);
            }
        }

        // ---------- Preparación y práctica ----------

        private void StartCountdown()
        {
            startButton.interactable = false;
            _countdown = StartCoroutine(Countdown());
        }

        private IEnumerator Countdown()
        {
            for (int n = countdownSeconds; n > 0; n--)
            {
                countdownLabel.text = n.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
            _countdown = null;
            Enter(Step.Practice);
        }

        private void StopCountdown()
        {
            if (_countdown != null) StopCoroutine(_countdown);
            _countdown = null;
            if (_recognizer != null) _recognizer.Stop();
        }

        private void OnAttemptCompleted(SignAttemptResult result) => Finish(result);

        private void Finish(SignAttemptResult? result)
        {
            StopCountdown();
            PracticeSession.Finish(result);
            PracticeSession.ReturnScene = null;
            ScreenFader.Load(SceneNames.Results);
        }

        private static void Focus(Selectable target)
        {
            if (target != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
