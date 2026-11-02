using UnityEngine;
using Lumina.Api;
using Lumina.Signs;

namespace Lumina.Core
{
    /// <summary>
    /// Lo último que se practicó, para que Results sepa qué mostrar. Solo en memoria. Al terminar una práctica evaluada, el
    /// resultado se envía al backend (<see cref="Submission"/>); Results muestra lo que el backend respondió, no lo que dijo la IA.
    /// </summary>
    public static class PracticeSession
    {
        public static SignEntry Sign { get; private set; }
        public static SignAttemptResult? Result { get; private set; }
        /// <summary>Envío del último resultado al backend; null si la práctica no produjo un resultado enviable.</summary>
        public static EvaluationSubmission Submission { get; private set; }

        private static float _attemptStartedAt = -1f;

        /// <summary>True si la seña viene de una casa de aprendizaje: Practice abre directo en APRENDE con esa seña.</summary>
        public static bool StartAtLearning { get; set; }
        /// <summary>Escena de nivel a la que vuelve VOLVER desde APRENDE (null = flujo normal de Practice).</summary>
        public static string ReturnScene { get; set; }

        public static void Begin(SignEntry sign) { Sign = sign; Result = null; Submission = null; _attemptStartedAt = -1f; }

        /// <summary>Marca el comienzo del intento (cuando empieza el reconocimiento) para medir durationMs.</summary>
        public static void MarkAttemptStarted() => _attemptStartedAt = Time.realtimeSinceStartup;

        /// <summary>Practica una seña elegida en una casa de aprendizaje.</summary>
        public static void BeginFromLevel(SignEntry sign, string levelScene) { Begin(sign); StartAtLearning = true; ReturnScene = levelScene; }
        public static void Finish(SignAttemptResult? result)
        {
            Result = result;
            Submission = null;
            if (!result.HasValue || !result.Value.evaluated) return;

            var r = result.Value;
            // Precisión 0–100, estrellas y XP tal como los entrega la IA. Sin estrellas/XP (-1) o con un id que no es del
            // catálogo del backend no hay nada que enviar: Results lo dirá con claridad.
            if (r.stars < 0 || r.xp < 0 || SignCodes.LevelFor(r.signId) == null)
            {
                Debug.LogWarning($"[Practice] El resultado de '{r.signId}' no se puede enviar al backend (estrellas={r.stars}, xp={r.xp}).");
                return;
            }
            int durationMs = _attemptStartedAt >= 0f ? Mathf.RoundToInt((Time.realtimeSinceStartup - _attemptStartedAt) * 1000f) : -1;
            Submission = EvaluationService.Submit(r.signId, r.precision, r.stars, r.xp, durationMs, r.feedback);
        }
    }
}
