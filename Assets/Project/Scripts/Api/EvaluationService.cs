using System;
using System.Threading.Tasks;
using UnityEngine;
using Lumina.Core;
using Lumina.Signs;

namespace Lumina.Api
{
    /// <summary>Un envío de resultado (POST /evaluations) en curso o terminado. Lo observan Results y el abecedario.</summary>
    public sealed class EvaluationSubmission
    {
        public enum SubmissionState { Pending, Succeeded, Failed }

        public string AttemptId { get; }
        public string SignCode { get; }
        public SubmissionState State { get; private set; } = SubmissionState.Pending;
        public EvaluationResponse Response { get; private set; }
        public ApiException Error { get; private set; }
        /// <summary>Intento actual (1 = primer envío); sube cuando se reintenta con el mismo attemptId.</summary>
        public int Attempt { get; internal set; } = 1;

        /// <summary>Termina cuando el backend respondió (bien o mal). No lanza excepciones: mira <see cref="State"/>.</summary>
        public Task Completion { get; internal set; } = Task.CompletedTask;
        /// <summary>Termina cuando, tras un éxito, la caché de progreso ya se volvió a cargar (o falló sin importar).</summary>
        public Task ProgressRefreshed { get; internal set; } = Task.CompletedTask;

        public bool IsPending => State == SubmissionState.Pending;

        public event Action Finished;

        internal EvaluationSubmission(string attemptId, string signCode)
        {
            AttemptId = attemptId;
            SignCode = signCode;
        }

        internal void Succeed(EvaluationResponse response) { Response = response; State = SubmissionState.Succeeded; Finished?.Invoke(); }
        internal void Fail(ApiException error) { Error = error; State = SubmissionState.Failed; Finished?.Invoke(); }
    }

    /// <summary>
    /// Envía los resultados de práctica al backend. Cada intento tiene su propio attemptId (Guid); si el envío falla por red,
    /// timeout, 429 o 5xx se reintenta con el MISMO attemptId, de modo que el backend nunca cuenta dos veces el mismo intento.
    /// </summary>
    public static class EvaluationService
    {
        private const int MaxSends = 4;
        private static readonly int[] RetryDelaysMs = { 1000, 2000, 4000 };

        /// <summary>
        /// Inicia el envío y devuelve de inmediato el objeto que lo representa. <paramref name="precision"/> es 0–100 tal como la
        /// entrega la IA (se trunca a 2 decimales); <paramref name="stars"/> y <paramref name="xp"/> también tal como vienen.
        /// </summary>
        public static EvaluationSubmission Submit(string signCode, double precision, int stars, int xp, int durationMs = -1, string feedback = null)
        {
            var submission = new EvaluationSubmission(Guid.NewGuid().ToString(), signCode);
            var body = new JsonObject()
                .Add("attemptId", submission.AttemptId)
                .Add("signCode", signCode)
                .Add("precision", ScoreAdapter.ToApiPrecision(precision))
                .Add("stars", stars)
                .Add("xp", xp);
            if (durationMs >= 0) body.Add("durationMs", durationMs);
            if (!string.IsNullOrWhiteSpace(feedback)) body.Add("feedback", feedback.Length > 500 ? feedback.Substring(0, 500) : feedback);

            string json = body.ToString();
            bool alreadyPassed = ProgressStore.StatusOf(signCode) == SignStatusNames.Passed;
            submission.Completion = RunAsync(submission, json, alreadyPassed);
            return submission;
        }

        private static async Task RunAsync(EvaluationSubmission submission, string json, bool alreadyPassed)
        {
            ApiException last = null;
            for (int send = 1; send <= MaxSends; send++)
            {
                submission.Attempt = send;
                try
                {
                    var response = await ApiClient.PostAsync<EvaluationResponse>("/evaluations", json);
                    if (response == null) throw new ApiException(ApiErrorKind.Server, 0, string.Empty, "Respuesta vacía.");
                    submission.ProgressRefreshed = RefreshProgressAsync(submission.SignCode);
                    submission.Succeed(response);
                    if (response.passed) _ = PlayPassedSoundAsync(submission, alreadyPassed);
                    return;
                }
                catch (ApiException e)
                {
                    last = e;
                    if (!e.IsRetryable || send == MaxSends) break;
                    await Task.Delay(RetryDelaysMs[send - 1]); // se reintenta con el mismo attemptId (mismo cuerpo)
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    last = new ApiException(ApiErrorKind.Server, 0, string.Empty, e.Message);
                    break;
                }
            }
            submission.Fail(last ?? new ApiException(ApiErrorKind.Server, 0, string.Empty, "No se pudo enviar el resultado."));
        }

        /// <summary>
        /// Seña superada: suena paso-sena; si con ella se completa el nivel (todas sus señas superadas, y esta era la última que faltaba),
        /// suena paso-nivel en su lugar. Aplica igual a los cuatro niveles porque todos envían sus resultados por aquí.
        /// </summary>
        private static async Task PlayPassedSoundAsync(EvaluationSubmission submission, bool alreadyPassed)
        {
            bool levelCompleted = false;
            if (!alreadyPassed)
            {
                try
                {
                    await submission.ProgressRefreshed;
                    var level = ProgressStore.GetLevel(SignCodes.LevelFor(submission.SignCode));
                    levelCompleted = level != null && level.totalSigns > 0 && level.passedSigns >= level.totalSigns;
                }
                catch (Exception e) { Debug.LogWarning("[Audio] No se pudo comprobar si el nivel se completó: " + e.Message); }
            }
            if (levelCompleted) GameAudio.PlayLevelCompleted();
            else GameAudio.PlaySignPassed();
        }

        private static async Task RefreshProgressAsync(string signCode)
        {
            try { await ProgressStore.RefreshAfterEvaluationAsync(SignCodes.LevelFor(signCode)); }
            catch (ApiException e) { Debug.LogWarning("[Api] No se pudo actualizar el progreso tras la evaluación: " + e.Message); }
        }
    }
}
