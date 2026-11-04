using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;
using Lumina.Signs;
using Senas.AI.MediaPipe;

namespace Lumina.UI
{
    /// <summary>
    /// Práctica dentro de la casa de aprendizaje (sin cambiar de escena): cámara del jugador, cuenta atrás, reconocimiento de la seña
    /// elegida, calificación y estrellas en vivo, y envío del resultado al backend. La maneja <see cref="LearningLevelController"/>,
    /// que le dice qué seña se practica (<see cref="SetSign"/>) y cuándo se abre o cierra el panel (<see cref="Begin"/> / <see cref="End"/>).
    /// El reconocimiento es el mismo de Practice (MediaPipe + plantillas); no se simula nada: sin plantilla de la seña solo avisa.
    /// </summary>
    public class LearningPracticeView : MonoBehaviour
    {
        [SerializeField] private CameraFeedUI cameraFeed;
        [SerializeField] private RawImage cameraView;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text countdownLabel;
        [SerializeField] private StatCardUI scoreCard;
        [SerializeField] private StatCardUI starsCard;
        [Tooltip("Nivel de la casa: decide qué reconocedor se usa (Números: MediaPipeNumberSignRecognizer; Meses y Saludos: MediaPipeGreetingSignRecognizer).")]
        [SerializeField] private LevelKind kind = LevelKind.Months;
        [Tooltip("Capturas de las señas de este nivel (NumberLandmarkTemplates.asset o GreetingLandmarkTemplates.asset).")]
        [SerializeField] private AlphabetLandmarkTemplateSet templates;
        [SerializeField] private int countdownSeconds = 3;
        [Tooltip("Tiempo máximo de un intento, después de la cuenta atrás.")]
        [SerializeField] private float attemptSeconds = 25f;

        private ISignRecognizer _recognizer;
        private SignEntry _sign;
        private Coroutine _routine;
        private float _bestSimilarity;
        private float _attemptStartedAt;
        private bool _saving;

        /// <summary>True mientras corre la cuenta atrás o el reconocimiento.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Cambia cuando empieza o termina un intento (el botón pasa de EMPEZAR a CANCELAR).</summary>
        public event Action RunningChanged;

        /// <summary>El backend confirmó que la seña quedó superada; el panel avanza a la siguiente.</summary>
        public event Action SignPassed;

        private void Awake()
        {
            if (templates != null)
            {
                if (kind == LevelKind.Numbers)
                {
                    var numbers = gameObject.AddComponent<MediaPipeNumberSignRecognizer>();
                    numbers.Configure(templates, cameraFeed);
                    numbers.StatusUpdated += OnStatus;
                    _recognizer = numbers;
                }
                else
                {
                    var greetings = gameObject.AddComponent<MediaPipeGreetingSignRecognizer>();
                    greetings.Configure(templates, cameraFeed);
                    greetings.StatusUpdated += OnStatus;
                    _recognizer = greetings;
                }
                _recognizer.Completed += OnCompleted;
            }
            if (countdownLabel != null) countdownLabel.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (_recognizer == null) return;
            _recognizer.Completed -= OnCompleted;
            if (_recognizer is MediaPipeNumberSignRecognizer numbers) numbers.StatusUpdated -= OnStatus;
            else if (_recognizer is MediaPipeGreetingSignRecognizer greetings) greetings.StatusUpdated -= OnStatus;
        }

        /// <summary>El panel se abrió: la cámara del jugador sigue apagada hasta que pulse EMPEZAR.</summary>
        public void Begin()
        {
            if (cameraFeed != null) cameraFeed.Stop();
        }

        /// <summary>El panel se cerró: apaga la cámara y cancela cualquier intento.</summary>
        public void End()
        {
            Cancel();
            if (cameraFeed != null) cameraFeed.Stop();
        }

        /// <summary>Cambia la seña que se practica y deja la calificación en blanco.</summary>
        public void SetSign(SignEntry sign)
        {
            Cancel();
            _sign = sign;
            _bestSimilarity = 0f;
            ShowSavedScore();
            SetStatus(Loc.T("levels.watchTv"));
        }

        /// <summary>Muestra el mejor resultado ya guardado de la seña (si lo hay); si no, deja la calificación en blanco.</summary>
        private void ShowSavedScore()
        {
            var saved = _sign != null ? ProgressStore.SignOf(_sign.id) : null;
            bool hasResult = saved != null && (saved.status == SignStatusNames.Passed || saved.bestStars > 0 || saved.bestPrecision > 0f);
            if (!hasResult) { ShowScore(0f, false); return; }
            if (scoreCard != null) scoreCard.Set(Mathf.RoundToInt(saved.bestPrecision) + "%", Loc.T("levels.scoreCaption"));
            if (starsCard != null) starsCard.Set(saved.bestStars + " / 3", Loc.T("levels.starsCaption"));
        }

        // El detalle del nivel puede llegar del backend después de abrir la seña, o cambiar tras guardar un intento.
        private void OnEnable() => ProgressStore.Changed += OnProgressChanged;

        private void OnProgressChanged()
        {
            if (!IsRunning && !_saving && _sign != null) ShowSavedScore();
        }

        /// <summary>EMPEZAR / CANCELAR.</summary>
        public void Toggle()
        {
            if (IsRunning) { Cancel(); SetStatus(Loc.T("levels.watchTv")); return; }
            if (_sign == null || _saving) return;
            _bestSimilarity = 0f;
            ShowScore(0f, false);
            if (cameraFeed != null && !cameraFeed.IsRunning) cameraFeed.Show(); // EMPEZAR enciende la cámara del jugador
            _routine = StartCoroutine(Attempt());
        }

        private IEnumerator Attempt()
        {
            SetRunning(true);
            SetStatus(Loc.T("levels.getReady"));
            for (int n = countdownSeconds; n > 0; n--)
            {
                if (countdownLabel != null) countdownLabel.text = n.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
            if (countdownLabel != null) countdownLabel.text = string.Empty;

            if (_recognizer == null)
            {
                SetStatus(Loc.T("practice.noRecognition"));
                SetRunning(false);
                yield break;
            }

            // La cámara puede tardar en arrancar (o en cambiar a otra si la elegida no entrega imagen).
            float waited = 0f;
            while ((cameraFeed == null || !cameraFeed.IsRunning) && waited < 10f)
            {
                SetStatus(Loc.T("levels.cameraStarting"));
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _attemptStartedAt = Time.realtimeSinceStartup;
            SetStatus(Loc.T("practice.analyzing"));
            _recognizer.Begin(_sign, cameraView);

            yield return new WaitForSecondsRealtime(attemptSeconds);
            _recognizer.Stop();
            if (cameraFeed != null) cameraFeed.Stop(); // la cámara solo está encendida durante un intento
            SetStatus(Loc.T("levels.timeout"));
            SetRunning(false);
            _routine = null;
        }

        private void Cancel()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_recognizer != null) _recognizer.Stop();
            if (cameraFeed != null) cameraFeed.Stop(); // cambiar de seña, cancelar o cerrar apaga la cámara; solo EMPEZAR la enciende
            if (countdownLabel != null) countdownLabel.text = string.Empty;
            SetRunning(false);
        }

        private void SetRunning(bool running)
        {
            if (IsRunning == running) return;
            IsRunning = running;
            RunningChanged?.Invoke();
        }

        private void OnStatus(string message)
        {
            if (IsRunning) SetStatus(message);
        }

        private static int StarsFor(float similarity) => similarity >= 0.88f ? 3 : similarity >= 0.74f ? 2 : similarity >= 0.6f ? 1 : 0;

        private void ShowScore(float similarity, bool active)
        {
            if (scoreCard != null) scoreCard.Set(active ? Mathf.RoundToInt(similarity * 100f) + "%" : "—", Loc.T("levels.scoreCaption"));
            if (starsCard != null) starsCard.Set(active ? StarsFor(similarity) + " / 3" : "—", Loc.T("levels.starsCaption"));
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = Loc.Translate(message);
        }

        private async void OnCompleted(SignAttemptResult result)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _recognizer.Stop();
            if (cameraFeed != null) cameraFeed.Stop();
            _saving = true;
            SetRunning(false);

            ShowScore(result.precision / 100f, true);
            SetStatus(result.feedback);

            int durationMs = Mathf.RoundToInt((Time.realtimeSinceStartup - _attemptStartedAt) * 1000f);
            var submission = EvaluationService.Submit(result.signId, result.precision, result.stars, result.xp, durationMs, result.feedback);
            try
            {
                await submission.Completion;
                if (this == null) return;
                if (submission.State == EvaluationSubmission.SubmissionState.Succeeded)
                {
                    var r = submission.Response;
                    ShowScore(r.precision / 100f, true);
                    if (starsCard != null) starsCard.Set(r.stars + " / 3", Loc.T("levels.starsCaption"));
                    SetStatus(r.awardedXp > 0 ? Loc.T("levels.saved", r.awardedXp) : result.feedback);
                    if (r.passed) SignPassed?.Invoke();
                }
                else
                {
                    SetStatus(ApiMessages.For(submission.Error) + " " + Loc.T("results.notSaved"));
                }
            }
            finally
            {
                if (this != null) _saving = false;
            }
        }

        private void OnDisable()
        {
            ProgressStore.Changed -= OnProgressChanged;
            if (cameraFeed != null && IsRunning) Cancel();
        }
    }
}
