using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;
using Lumina.Signs;

namespace Lumina.UI
{
    /// <summary>
    /// Resumen al terminar una práctica. Muestra lo que RESPONDIÓ EL BACKEND al envío del resultado (precisión, estrellas, XP
    /// otorgada, XP total y desbloqueos), no lo que reportó la IA. Mientras espera muestra un estado de carga y bloquea el
    /// botón para evitar dobles envíos; si el envío falla lo dice claramente y no se suma nada. Sin evaluación no inventa nota.
    /// </summary>
    public class ResultsController : MonoBehaviour
    {
        private const int MaxSends = 4; // igual que EvaluationService

        [SerializeField] private SceneHeaderUI header;
        [SerializeField] private TMP_Text signLabel;
        [SerializeField] private StatCardUI precisionCard;
        [SerializeField] private StatCardUI starsCard;
        [SerializeField] private StatCardUI xpCard;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button practiceAgainButton;

        private EvaluationSubmission _submission;
        private string _feedback; // texto de la IA (SignAttemptResult.feedback); la XP y las estrellas salen del backend

        private void Start()
        {
            header.SetTitle("title.results");
            practiceAgainButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Practice));

            var sign = PracticeSession.Sign;
            signLabel.text = sign != null ? sign.displayName.ToUpperInvariant() : Loc.T("title.practice");

            var result = PracticeSession.Result;
            _submission = PracticeSession.Submission;
            _feedback = result.HasValue ? result.Value.feedback : null;

            if (PracticeSession.ConsumeLocalPracticeOnly())
            {
                ShowLocalPracticeResult(result);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(practiceAgainButton.gameObject);
                return;
            }

            if (_submission != null)
            {
                ShowPending();
                _submission.Finished += OnFinished;
                if (!_submission.IsPending) OnFinished();
            }
            else if (result.HasValue && result.Value.evaluated)
            {
                // Hubo evaluación pero no se pudo enviar (por ejemplo, la IA no entregó estrellas o XP): no se muestra nada inventado.
                ShowNothing(Loc.T("results.notSaved"));
            }
            else
            {
                ShowNothing(Loc.T("results.none"));
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(practiceAgainButton.gameObject);
        }

        private void ShowLocalPracticeResult(SignAttemptResult? result)
        {
            practiceAgainButton.interactable = true;
            xpCard.gameObject.SetActive(false);
            if (!result.HasValue || !result.Value.evaluated)
            {
                starsCard.gameObject.SetActive(false);
                precisionCard.Set("—", Loc.T("results.notEvaluated"));
                messageLabel.text = Loc.T("results.localOnly");
                return;
            }

            var attempt = result.Value;
            precisionCard.Set(Mathf.RoundToInt(attempt.precision) + "%", Loc.T("results.similarity"));
            starsCard.gameObject.SetActive(attempt.stars >= 0);
            if (attempt.stars >= 0) starsCard.Set(attempt.stars.ToString(), Loc.T("progress.stars"));
            messageLabel.text = (string.IsNullOrWhiteSpace(_feedback) ? Loc.T("results.good") : _feedback) +
                "\n" + Loc.T("results.localOnly");
        }

        private void OnDestroy()
        {
            if (_submission != null) _submission.Finished -= OnFinished;
        }

        private void Update()
        {
            // Los reintentos de red usan el mismo attemptId; se le dice al jugador que se está reintentando.
            if (_submission != null && _submission.IsPending && _submission.Attempt > 1)
                messageLabel.text = Loc.T("results.retrying", _submission.Attempt, MaxSends);
        }

        private void ShowPending()
        {
            precisionCard.Set("…", Loc.T("results.similarity"));
            starsCard.gameObject.SetActive(false);
            xpCard.gameObject.SetActive(false);
            messageLabel.text = Loc.T("results.saving");
            practiceAgainButton.interactable = false; // evita el doble envío y salir antes de tener la respuesta
        }

        private void ShowNothing(string message)
        {
            precisionCard.Set("—", Loc.T("results.notEvaluated"));
            starsCard.gameObject.SetActive(false);
            xpCard.gameObject.SetActive(false);
            messageLabel.text = message;
        }

        private void OnFinished()
        {
            if (this == null) return;
            practiceAgainButton.interactable = true;

            if (_submission.State == EvaluationSubmission.SubmissionState.Failed)
            {
                ShowNothing(ApiMessages.For(_submission.Error) + "\n" + Loc.T("results.notSaved"));
                return;
            }

            var r = _submission.Response;
            precisionCard.Set(Mathf.RoundToInt(r.precision) + "%", Loc.T("results.similarity"));
            starsCard.gameObject.SetActive(r.stars >= 0); // como en su versión: oculta si no hay estrellas
            if (r.stars >= 0) starsCard.Set(r.stars.ToString(), Loc.T("progress.stars"));
            xpCard.gameObject.SetActive(true);
            xpCard.Set("+" + r.awardedXp, Loc.T("results.xp")); // XP otorgada por el backend, nunca la que reportó la IA

            var message = new StringBuilder();
            message.Append(!r.passed ? Loc.T("results.notPassed")
                : string.IsNullOrWhiteSpace(_feedback) ? Loc.T("results.good") : _feedback);
            if (r.passed && r.awardedXp == 0) message.Append('\n').Append(Loc.T("results.noXp"));
            message.Append('\n').Append(Loc.T("results.totalXp", r.totalXp.ToString("N0")));
            if (!string.IsNullOrEmpty(r.unlockedSign))
                message.Append('\n').Append(Loc.T("results.unlockedSign", SignCodes.DisplayName(r.unlockedSign)));
            if (r.unlockedLevels != null)
                foreach (var level in r.unlockedLevels)
                    message.Append('\n').Append(Loc.T("results.unlockedLevel", Loc.Translate(SignCodes.SpanishLevelName(level))));
            messageLabel.text = message.ToString();
        }
    }
}
