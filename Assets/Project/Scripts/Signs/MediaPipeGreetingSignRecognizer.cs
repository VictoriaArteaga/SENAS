using System;
using System.Collections.Generic;
using Lumina.UI;
using Senas.AI.MediaPipe;
using UnityEngine;
using UnityEngine.UI;

namespace Lumina.Signs
{
  /// <summary>Evalúa saludos con secuencias Holistic de manos y referencias de cabeza/torso.</summary>
  public sealed class MediaPipeGreetingSignRecognizer : MonoBehaviour, ISignRecognizer
  {
    private const int MaximumFrames = 90;
    private const int QuietFramesToFinish = 8;
    // Holistic puede perder la mano durante unos fotogramas cuando pasa delante de la cara
    // o se mueve deprisa. No descartamos toda la seña por una pérdida breve.
    private const int MissingHandFramesBeforeReset = 8;
    private const float MotionStartDistance = 0.015f;
    private const float MinimumRecognizedScore = 0.60f;
    private static readonly string[] SupportedIds =
    {
      "saludos.hola", "saludos.buenos_dias", "saludos.buenas_tardes", "saludos.buenas_noches",
      "saludos.adios", "saludos.como_estas", "saludos.bien", "saludos.gracias", "saludos.de_nada", "saludos.si", "saludos.no",
      "meses.enero", "meses.febrero", "meses.marzo", "meses.abril", "meses.mayo", "meses.junio",
      "meses.julio", "meses.agosto", "meses.septiembre", "meses.octubre", "meses.noviembre", "meses.diciembre"
    };

    private AlphabetLandmarkTemplateSet _templates;
    private CameraFeedUI _cameraFeed;
    private GameObject _captureRoot;
    private HolisticLandmarkerCapture _capture;
    private HandLandmarkerCapture _handCapture;
    private AlphabetLandmarkEvaluator _handEvaluator;
    private SignEntry _sign;
    private readonly List<GreetingMotionFrame> _sequence = new List<GreetingMotionFrame>();
    private readonly Queue<IReadOnlyList<Vector3>> _preRoll = new Queue<IReadOnlyList<Vector3>>();
    private IReadOnlyList<Vector3> _previous;
    private long _sequenceStart;
    private int _quietFrames;
    private int _missingHandFrames;
    private bool _inMotion;
    private bool _completed;
    private string _lastStatus;

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
      if (_templates == null || !IsSupported(sign != null ? sign.id : null))
      { SetStatus("No se encontró la referencia de este saludo."); return; }
      if (_cameraFeed == null || !(_cameraFeed.Texture is WebCamTexture texture) || !texture.isPlaying)
      { SetStatus("No está activa la cámara. Vuelve a preparación e inténtalo otra vez."); return; }

      _captureRoot = new GameObject("SENAS Greeting Holistic Practice");
      if (UsesSingleHandEvaluator(_sign.id))
      {
        var hasHandTemplate = false;
        foreach (var template in _templates.MotionTemplates)
          if (template != null && string.Equals(template.Letter, _sign.id, StringComparison.OrdinalIgnoreCase) &&
              template.Frames != null && template.Frames.Count >= 6) { hasHandTemplate = true; break; }
        if (!hasHandTemplate)
        {
          Destroy(_captureRoot);
          _captureRoot = null;
          SetStatus("Falta capturar esta seña con el detector de una mano: Window > SENAS > LSC Single-Hand Greeting Capture.");
          return;
        }

        _captureRoot.name = "SENAS Single-Hand Greeting Practice";
        _handCapture = _captureRoot.AddComponent<HandLandmarkerCapture>();
        _handCapture.UseSharedCameraTexture(texture);
        _handEvaluator = _captureRoot.AddComponent<AlphabetLandmarkEvaluator>();
        _handEvaluator.Configure(_templates, false, _sign.id);
        _handEvaluator.EvaluationUpdated += OnHandEvaluationUpdated;
        SetStatus($"Evaluando «{_sign.displayName}» con el detector de una mano. Haz el movimiento completo.");
        return;
      }

      _capture = _captureRoot.AddComponent<HolisticLandmarkerCapture>();
      _capture.UseSharedCameraTexture(texture);
      _capture.LandmarksUpdated += OnLandmarks;
      var hasTemplate = false;
      foreach (var template in _templates.GreetingMotionTemplates)
        if (template != null && string.Equals(template.Id, _sign.id, StringComparison.OrdinalIgnoreCase)) { hasTemplate = true; break; }
      SetStatus(hasTemplate
        ? $"Haz el movimiento completo de «{_sign.displayName}». Mantén visibles los elementos indicados."
        : IsMonth(_sign.id)
          ? "Falta capturar este mes en Window > SENAS > LSC Month Holistic Capture."
          : "Falta capturar la secuencia Holistic de este saludo en Window > SENAS > LSC Greeting Holistic Capture.");
    }

    public void Stop()
    {
      if (_handEvaluator != null) _handEvaluator.EvaluationUpdated -= OnHandEvaluationUpdated;
      _handEvaluator = null;
      _handCapture = null;
      if (_capture != null) _capture.LandmarksUpdated -= OnLandmarks;
      _capture = null;
      if (_captureRoot != null)
      {
        if (Application.isPlaying) Destroy(_captureRoot);
        else DestroyImmediate(_captureRoot);
      }
      _captureRoot = null;
      _sign = null;
      ResetSequence();
      _completed = false;
      _lastStatus = null;
    }

    private void OnLandmarks(HolisticLandmarkObservation observation)
    {
      if (_completed || observation == null || _sign == null) return;
      var requirement = RequirementForSign(_sign.id);
      var handCount = (observation.HasLeftHand ? 1 : 0) + (observation.HasRightHand ? 1 : 0);
      if ((requirement & GreetingMotionRequirement.TwoHands) != 0 && handCount < 2 ||
          (requirement & GreetingMotionRequirement.OneHand) != 0 && handCount < 1)
      {
        if (_inMotion && ++_missingHandFrames <= MissingHandFramesBeforeReset)
          return;
        if (_inMotion) ResetSequence();
        SetStatus((requirement & GreetingMotionRequirement.TwoHands) != 0 ? "Encuadra las dos manos para hacer esta seña." : "No detecto la mano. Centra la seña e inténtalo otra vez.");
        return;
      }
      _missingHandFrames = 0;
      if ((requirement & GreetingMotionRequirement.HeadReference) != 0 && (!observation.HasFace || !observation.HasHeadAndBody))
      { SetStatus("Asegúrate de que tu cara, hombros y brazos estén visibles."); return; }

      var features = GreetingMotionRules.Canonicalize(observation, requirement);
      var change = _previous == null ? 0f : MeasureChange(_previous, features, requirement);
      _previous = features;
      if (!_inMotion)
      {
        _preRoll.Enqueue(features);
        while (_preRoll.Count > 4) _preRoll.Dequeue();
        if (change < MotionStartDistance) return;
        _inMotion = true;
        _sequenceStart = observation.TimestampMilliseconds;
        foreach (var previous in _preRoll) _sequence.Add(new GreetingMotionFrame(0f, previous));
        _preRoll.Clear();
      }
      _sequence.Add(new GreetingMotionFrame(Mathf.Clamp01((observation.TimestampMilliseconds - _sequenceStart) / 3000f), features));
      if (change < MotionStartDistance * 0.3f) _quietFrames++; else _quietFrames = 0;
      SetStatus("Movimiento detectado. Completa la seña y pausa un instante para evaluarla.");
      if (_quietFrames >= QuietFramesToFinish || _sequence.Count >= MaximumFrames) EvaluateAndReset(requirement);
    }

    private void OnHandEvaluationUpdated(AlphabetLandmarkEvaluation evaluation)
    {
      if (_completed || evaluation == null || _sign == null) return;
      switch (evaluation.Status)
      {
        case AlphabetEvaluationStatus.NoHand:
          SetStatus("No detecto la mano. Muéstrala completa y céntrala frente a la cámara.");
          return;
        case AlphabetEvaluationStatus.NoTemplates:
          SetStatus("Falta la captura de movimiento de esta seña.");
          return;
        case AlphabetEvaluationStatus.MotionPending:
        case AlphabetEvaluationStatus.MotionTemplateNeedsSequence:
          SetStatus($"Haz el movimiento completo de «{_sign.displayName}» y mantén la mano visible.");
          return;
        case AlphabetEvaluationStatus.Uncertain:
          SetStatus(evaluation.Similarity > 0f
            ? $"Movimiento en curso · coincidencia {evaluation.Similarity:P0}. Completa la seña."
            : "Detecto la mano. Haz el movimiento de la seña con claridad.");
          return;
        case AlphabetEvaluationStatus.Recognized:
          if (!string.Equals(evaluation.CandidateLetter, _sign.id, StringComparison.OrdinalIgnoreCase)) return;
          // The evaluator has already matched the sequence inside the template's configured DTW tolerance.
          // Its raw geometric similarity is relative to that tolerance (0 at the boundary), not a probability.
          // Calibrate that accepted range onto the backend score band [60%, 100%] instead of applying a second,
          // stricter 60% geometric cutoff that rejects valid within-tolerance matches.
          var geometricSimilarity = Mathf.Clamp01(evaluation.Similarity);
          var similarity = Mathf.Lerp(MinimumRecognizedScore, 1f, geometricSimilarity);
          _completed = true;
          var stars = similarity >= 0.88f ? 3 : similarity >= 0.74f ? 2 : 1;
          var feedback = "¡Muy bien! El movimiento de la seña coincide con la referencia.";
          SetStatus(feedback);
          Completed?.Invoke(new SignAttemptResult(_sign.id, true, similarity * 100f, stars, stars * 10, feedback));
          return;
      }
    }

    private void EvaluateAndReset(GreetingMotionRequirement requirement)
    {
      if (_sequence.Count < 6) { ResetSequence(); SetStatus("Haz el movimiento completo e inténtalo de nuevo."); return; }
      if ((requirement & GreetingMotionRequirement.HeadTouch) != 0 && !TouchesHead(_sequence))
      { ResetSequence(); SetStatus("En «Gracias», lleva una mano hasta tocar la cabeza durante el movimiento."); return; }

      var best = float.PositiveInfinity;
      foreach (var template in _templates.GreetingMotionTemplates)
      {
        if (template == null || !string.Equals(template.Id, _sign.id, StringComparison.OrdinalIgnoreCase) || template.Frames == null || template.Frames.Count < 6) continue;
        var distance = DynamicTimeWarping(_sequence, template.Frames, requirement);
        if (distance < best) best = distance;
      }
      var threshold = 0.45f;
      foreach (var template in _templates.GreetingMotionTemplates)
        if (template != null && string.Equals(template.Id, _sign.id, StringComparison.OrdinalIgnoreCase)) { threshold = template.MaximumAverageDistance; break; }
      ResetSequence();
      if (float.IsPositiveInfinity(best)) { SetStatus("Falta una secuencia de referencia para este saludo."); return; }
      // MaximumAverageDistance ya representa el límite de distancia aceptable. Antes se
      // volvía a exigir un 45 % de similitud sobre ese límite, reduciendo el umbral efectivo
      // casi a la mitad y rechazando movimientos que estaban dentro de la tolerancia capturada.
      if (best > threshold)
      {
        var rejectedConfidence = Mathf.Clamp01(1f - best / Mathf.Max(0.01f, threshold * 2f));
        SetStatus($"Todavía no coincide con la referencia ({rejectedConfidence:P0}). Repite el movimiento completo.");
        return;
      }

      // Mapea el límite geométrico a la precisión mínima oficial (60 %) y una coincidencia exacta a 100 %.
      var similarity = Mathf.Clamp(1f - 0.4f * best / Mathf.Max(0.01f, threshold), MinimumRecognizedScore, 1f);

      _completed = true;
      var stars = similarity >= 0.88f ? 3 : similarity >= 0.74f ? 2 : 1;
      SetStatus($"¡Seña reconocida! Precisión: {similarity:P0}.");
      Completed?.Invoke(new SignAttemptResult(_sign.id, true, similarity * 100f, stars, stars * 10,
        IsMonth(_sign.id)
          ? "¡Muy bien! El movimiento del mes coincide con la referencia."
          : "¡Muy bien! El movimiento del saludo coincide con la referencia."));
    }

    private static float DynamicTimeWarping(IReadOnlyList<GreetingMotionFrame> live, IReadOnlyList<GreetingMotionFrame> reference, GreetingMotionRequirement requirement)
    {
      var rows = live.Count; var columns = reference.Count;
      var previous = new float[columns + 1]; var current = new float[columns + 1];
      for (var j = 0; j <= columns; j++) previous[j] = float.PositiveInfinity;
      previous[0] = 0f;
      for (var i = 1; i <= rows; i++)
      {
        current[0] = float.PositiveInfinity;
        var low = Mathf.Max(1, i - Mathf.Max(8, Mathf.Abs(rows - columns)));
        var high = Mathf.Min(columns, i + Mathf.Max(8, Mathf.Abs(rows - columns)));
        for (var j = 1; j < low; j++) current[j] = float.PositiveInfinity;
        for (var j = low; j <= high; j++)
        {
          var cost = FrameDistance(live[i - 1].Features, reference[j - 1].Features, requirement);
          current[j] = cost + Mathf.Min(previous[j], Mathf.Min(current[j - 1], previous[j - 1]));
        }
        for (var j = high + 1; j <= columns; j++) current[j] = float.PositiveInfinity;
        var swap = previous; previous = current; current = swap;
      }
      return previous[columns] / Mathf.Max(rows, columns);
    }

    private static float FrameDistance(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b, GreetingMotionRequirement requirement)
    {
      var sum = 0f; var count = 0;
      var handEnd = (requirement & GreetingMotionRequirement.TwoHands) != 0 ? 42 : 21;
      for (var i = 0; i < handEnd; i++) { sum += Vector3.Distance(a[i], b[i]); count++; }
      if ((requirement & GreetingMotionRequirement.HeadReference) != 0)
        for (var i = 42; i < GreetingMotionFrame.FeatureCount; i++) { sum += Vector3.Distance(a[i], b[i]) * 0.8f; count++; }
      return count == 0 ? 0f : sum / count;
    }

    private static float MeasureChange(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b, GreetingMotionRequirement requirement)
    {
      var count = (requirement & GreetingMotionRequirement.TwoHands) != 0 ? 42 : 21;
      var total = 0f;
      for (var i = 0; i < count; i++) total += Vector3.Distance(a[i], b[i]);
      return total / count;
    }

    private static bool TouchesHead(IReadOnlyList<GreetingMotionFrame> frames)
    {
      foreach (var frame in frames)
      {
        var points = frame.Features;
        var min = float.PositiveInfinity;
        for (var hand = 0; hand < 2; hand++)
        for (var point = 0; point < 21; point++)
        for (var head = 42; head <= 45; head++)
          min = Mathf.Min(min, Vector3.Distance(points[hand * 21 + point], points[head]));
        if (min <= 0.20f) return true;
      }
      return false;
    }

    private void ResetSequence()
    {
      _sequence.Clear(); _preRoll.Clear(); _previous = null; _sequenceStart = 0; _quietFrames = 0; _missingHandFrames = 0; _inMotion = false;
    }

    private void SetStatus(string message)
    {
      if (string.Equals(_lastStatus, message, StringComparison.Ordinal)) return;
      _lastStatus = message;
      StatusUpdated?.Invoke(message);
    }

    private static bool IsSupported(string id)
    {
      if (string.IsNullOrWhiteSpace(id)) return false;
      foreach (var supportedId in SupportedIds) if (string.Equals(id, supportedId, StringComparison.OrdinalIgnoreCase)) return true;
      return false;
    }

    private GreetingMotionRequirement RequirementForSign(string id)
    {
      if (!IsMonth(id)) return GreetingMotionRules.ForId(id);
      foreach (var template in _templates.GreetingMotionTemplates)
        if (template != null && string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase))
          return template.Requirement;
      return GreetingMotionRequirement.OneHand;
    }

    private static bool IsMonth(string id) =>
      !string.IsNullOrEmpty(id) && id.StartsWith("meses.", StringComparison.OrdinalIgnoreCase);

    private static bool UsesSingleHandEvaluator(string id) =>
      string.Equals(id, "saludos.si", StringComparison.OrdinalIgnoreCase) ||
      string.Equals(id, "saludos.no", StringComparison.OrdinalIgnoreCase) ||
      string.Equals(id, "saludos.adios", StringComparison.OrdinalIgnoreCase);

    private void OnDisable() => Stop();
  }
}
