using System;
using Lumina.UI;
using Senas.AI.MediaPipe;
using UnityEngine;
using UnityEngine.UI;

namespace Lumina.Signs
{
    /// <summary>Adapta el evaluador de landmarks al flujo Practice para los números LSC 0–30.</summary>
    public sealed class MediaPipeNumberSignRecognizer : MonoBehaviour, ISignRecognizer
    {
        private const float MinimumSimilarity = 0.6f;

        private AlphabetLandmarkTemplateSet _templates;
        private CameraFeedUI _cameraFeed;
        private GameObject _captureRoot;
        private AlphabetLandmarkEvaluator _evaluator;
        private SignEntry _sign;
        private bool _completed;

        public event Action<SignAttemptResult> Completed;
        public event Action<string> StatusUpdated;

        public void Configure(AlphabetLandmarkTemplateSet templates, CameraFeedUI cameraFeed)
        {
            _templates = templates;
            _cameraFeed = cameraFeed;
        }

        public void Begin(SignEntry sign, RawImage cameraView)
        {
            Stop();
            _sign = sign;
            _completed = false;

            if (_templates == null || !TryGetNumber(sign, out var number))
            {
                StatusUpdated?.Invoke("No se encontró la referencia de este número.");
                return;
            }

            if (_cameraFeed == null || !(_cameraFeed.Texture is WebCamTexture texture) || !texture.isPlaying)
            {
                StatusUpdated?.Invoke("No está activa la cámara. Vuelve a preparación e inténtalo otra vez.");
                return;
            }

            _captureRoot = new GameObject("SENAS Number Practice Capture");
            var capture = _captureRoot.AddComponent<HandLandmarkerCapture>();
            capture.UseSharedCameraTexture(texture);
            _evaluator = _captureRoot.AddComponent<AlphabetLandmarkEvaluator>();
            _evaluator.Configure(_templates, false, number);
            _evaluator.EvaluationUpdated += OnEvaluationUpdated;
            StatusUpdated?.Invoke($"Evaluando el número {number}. Mantén la mano dentro del recuadro.");
        }

        public void Stop()
        {
            if (_evaluator != null) _evaluator.EvaluationUpdated -= OnEvaluationUpdated;
            _evaluator = null;
            if (_captureRoot != null) Destroy(_captureRoot);
            _captureRoot = null;
            _sign = null;
            _completed = false;
        }

        private void OnEvaluationUpdated(AlphabetLandmarkEvaluation evaluation)
        {
            if (_completed || evaluation == null) return;

            switch (evaluation.Status)
            {
                case AlphabetEvaluationStatus.NoHand:
                    StatusUpdated?.Invoke("No detecto la mano. Centra la seña y mejora la iluminación.");
                    return;
                case AlphabetEvaluationStatus.NoTemplates:
                    StatusUpdated?.Invoke("Falta una captura para este número.");
                    return;
                case AlphabetEvaluationStatus.MotionPending:
                    StatusUpdated?.Invoke($"Haz completo el movimiento del número {_sign.displayName}.");
                    return;
                case AlphabetEvaluationStatus.MotionTemplateNeedsSequence:
                    StatusUpdated?.Invoke("Este número requiere una captura de movimiento.");
                    return;
                case AlphabetEvaluationStatus.Uncertain:
                    StatusUpdated?.Invoke(evaluation.Similarity > 0f
                        ? "Vas bien. Mantén la seña un momento más."
                        : "Ajusta la forma de la mano e inténtalo otra vez.");
                    return;
                case AlphabetEvaluationStatus.Recognized:
                    if (!string.Equals(evaluation.CandidateLetter, NumberFromId(_sign.id), StringComparison.Ordinal)) return;
                    if (evaluation.Similarity < MinimumSimilarity)
                    {
                        StatusUpdated?.Invoke("Casi. Ajusta los dedos y vuelve a intentarlo.");
                        return;
                    }

                    _completed = true;
                    var stars = evaluation.Similarity >= 0.88f ? 3 : evaluation.Similarity >= 0.74f ? 2 : 1;
                    Completed?.Invoke(new SignAttemptResult(
                        _sign.id, true, evaluation.Similarity * 100f, stars, stars * 10,
                        "¡Muy bien! La seña coincide con el número."));
                    return;
            }
        }

        private static bool TryGetNumber(SignEntry sign, out string number)
        {
            number = NumberFromId(sign != null ? sign.id : null);
            return int.TryParse(number, out var value) && value >= 0 && value <= 30;
        }

        private static string NumberFromId(string id) =>
            !string.IsNullOrEmpty(id) && id.StartsWith("numeros.", StringComparison.Ordinal)
                ? id.Substring("numeros.".Length)
                : string.Empty;

        private void OnDisable() => Stop();
    }
}
