using System;
using System.Collections.Generic;
using UnityEngine;

namespace Senas.AI.MediaPipe
{
  public enum AlphabetEvaluationStatus { NoHand, NoTemplates, MotionTemplateNeedsSequence, Uncertain, Recognized }

  /// <summary>La similitud es proximidad geometrica, no una probabilidad calibrada.</summary>
  public sealed class AlphabetLandmarkEvaluation
  {
    public AlphabetEvaluationStatus Status { get; }
    public string CandidateLetter { get; }
    public float Similarity { get; }
    public int ConsecutiveFrames { get; }

    internal AlphabetLandmarkEvaluation(AlphabetEvaluationStatus status, string letter, float similarity, int consecutiveFrames)
    {
      Status = status;
      CandidateLetter = letter;
      Similarity = similarity;
      ConsecutiveFrames = consecutiveFrames;
    }
  }

  /// <summary>Evalua posturas estaticas y secuencias de movimiento del alfabeto LSC.</summary>
  [DefaultExecutionOrder(1000)]
  [DisallowMultipleComponent]
  [RequireComponent(typeof(HandLandmarkerCapture))]
  public sealed class AlphabetLandmarkEvaluator : MonoBehaviour
  {
    private const int LandmarkCount = 21;
    private const int WristIndex = 0;
    private const int MiddleFingerMcpIndex = 9;

    [SerializeField] private HandLandmarkerCapture _capture;
    [SerializeField] private AlphabetLandmarkTemplateSet _templateSet;
    [SerializeField, Min(1)] private int _stableFrameCount = 4;
    public event Action<AlphabetLandmarkEvaluation> EvaluationUpdated;
    [SerializeField] private bool _showDebugOverlay = true;
    [SerializeField, Min(6)] private int _minimumMotionFrames = 6;
    [SerializeField, Min(0.01f)] private float _maximumMotionGapSeconds = 0.25f;
    private AlphabetLandmarkEvaluation _latestEvaluation;
    private readonly List<MotionObservation> _motionHistory = new List<MotionObservation>();
    private long _lastMotionTimestamp;

    public bool ShowDebugOverlay => _showDebugOverlay;

    private string _candidateLetter;
    private int _consecutiveFrames;

    private void Awake()
    {
      if (_capture == null) _capture = GetComponent<HandLandmarkerCapture>();
    }

    private void OnEnable()
    {
      if (_capture == null) _capture = GetComponent<HandLandmarkerCapture>();
      if (_capture != null) _capture.LandmarksUpdated += OnLandmarksUpdated;
    }

    private void OnDisable()
    {
      if (_capture != null) _capture.LandmarksUpdated -= OnLandmarksUpdated;
      ResetStability();
      ResetMotionHistory();
    }

    private void OnLandmarksUpdated(HandLandmarkFrame frame)
    {
      if (frame == null || frame.Hands == null || frame.Hands.Count == 0)
      {
        ResetStability();
        ResetMotionHistory();
        Publish(AlphabetEvaluationStatus.NoHand, null, 0f);
        return;
      }

      var templates = _templateSet != null ? _templateSet.Templates : null;
      var motionTemplates = _templateSet != null ? _templateSet.MotionTemplates : null;
      if ((templates == null || templates.Count == 0) && (motionTemplates == null || motionTemplates.Count == 0))
      {
        ResetStability();
        Publish(AlphabetEvaluationStatus.NoTemplates, null, 0f);
        return;
      }

      var observations = new List<ObservedHand>(frame.Hands.Count);
      foreach (var hand in frame.Hands)
      {
        if (hand == null || !TryNormalize(hand.WorldLandmarks, out var points) &&
            !TryNormalize(hand.NormalizedLandmarks, out points))
          continue;
        var wrist = hand.NormalizedLandmarks != null && hand.NormalizedLandmarks.Count > WristIndex
          ? new Vector2(hand.NormalizedLandmarks[WristIndex].X, hand.NormalizedLandmarks[WristIndex].Y)
          : Vector2.zero;
        observations.Add(new ObservedHand(hand.Handedness, points, wrist));
      }

      if (observations.Count == 0)
      {
        ResetStability();
        ResetMotionHistory();
        Publish(AlphabetEvaluationStatus.NoHand, null, 0f);
        return;
      }

      AppendMotionObservation(frame.TimestampMilliseconds, observations[0]);
      if (TryEvaluateMotion(out var motionLetter, out var motionSimilarity))
      {
        if (string.Equals(motionLetter, _candidateLetter, StringComparison.Ordinal)) _consecutiveFrames++;
        else { _candidateLetter = motionLetter; _consecutiveFrames = 1; }
        var motionStatus = motionSimilarity > 0f && _consecutiveFrames >= Mathf.Max(1, _stableFrameCount)
          ? AlphabetEvaluationStatus.Recognized
          : AlphabetEvaluationStatus.Uncertain;
        Publish(motionStatus, motionLetter, motionSimilarity);
        return;
      }

      var hasStaticTemplate = false;
      var hasMotionTemplate = false;
      var bestDistance = float.PositiveInfinity;
      var bestLetter = string.Empty;
      var bestThreshold = 0f;

      foreach (var template in templates ?? Array.Empty<AlphabetLandmarkTemplate>())
      {
        if (template == null || string.IsNullOrWhiteSpace(template.Letter)) continue;
        if (template.RequiresMotion)
        {
          hasMotionTemplate = true;
          continue;
        }

        hasStaticTemplate = true;
        var reference = template.NormalizedLandmarks;
        if (reference == null || reference.Count < LandmarkCount) continue;

        foreach (var observation in observations)
        {
          if (!MatchesHandedness(template.Handedness, observation.Handedness)) continue;
          var distance = AverageDistance(observation.Points, reference);
          if (distance < bestDistance)
          {
            bestDistance = distance;
            bestLetter = template.Letter;
            bestThreshold = template.MaximumAverageDistance;
          }
        }
      }

      if (string.IsNullOrEmpty(bestLetter))
      {
        ResetStability();
        var hasMotionSamples = motionTemplates != null && motionTemplates.Count > 0;
        var status = !hasStaticTemplate && (hasMotionTemplate || hasMotionSamples)
          ? AlphabetEvaluationStatus.MotionTemplateNeedsSequence
          : AlphabetEvaluationStatus.NoTemplates;
        Publish(status, null, 0f);
        return;
      }

      if (string.Equals(bestLetter, _candidateLetter, StringComparison.Ordinal))
        _consecutiveFrames++;
      else
      {
        _candidateLetter = bestLetter;
        _consecutiveFrames = 1;
      }

      var accepted = bestThreshold > 0f && bestDistance <= bestThreshold;
      var stable = _consecutiveFrames >= Mathf.Max(1, _stableFrameCount);
      var resultStatus = accepted && stable
        ? AlphabetEvaluationStatus.Recognized
        : AlphabetEvaluationStatus.Uncertain;
      var similarity = bestThreshold > 0f ? Mathf.Clamp01(1f - bestDistance / bestThreshold) : 0f;
      Publish(resultStatus, bestLetter, similarity);
    }

    private void Publish(AlphabetEvaluationStatus status, string letter, float similarity)
    {
      _latestEvaluation = new AlphabetLandmarkEvaluation(status, letter, similarity, _consecutiveFrames);
      EvaluationUpdated?.Invoke(_latestEvaluation);
    }

    private void ResetStability()
    {
      _candidateLetter = null;
      _consecutiveFrames = 0;
    }

    private void AppendMotionObservation(long timestamp, ObservedHand hand)
    {
      if (_lastMotionTimestamp > 0 &&
          (timestamp <= _lastMotionTimestamp ||
           (timestamp - _lastMotionTimestamp) / 1000f > _maximumMotionGapSeconds))
        ResetMotionHistory();

      _lastMotionTimestamp = timestamp;
      _motionHistory.Add(new MotionObservation(timestamp, hand.Handedness, hand.WristPosition, hand.Points));
      while (_motionHistory.Count > 90) _motionHistory.RemoveAt(0);
    }

    private void ResetMotionHistory()
    {
      _motionHistory.Clear();
      _lastMotionTimestamp = 0;
    }

    private bool TryEvaluateMotion(out string letter, out float similarity)
    {
      letter = null;
      similarity = 0f;
      var templates = _templateSet != null ? _templateSet.MotionTemplates : null;
      if (templates == null || templates.Count == 0) return false;

      var bestDistance = float.PositiveInfinity;
      var bestThreshold = 0f;
      foreach (var template in templates)
      {
        if (template == null || string.IsNullOrWhiteSpace(template.Letter) || template.Frames == null ||
            template.Frames.Count < _minimumMotionFrames || _motionHistory.Count < _minimumMotionFrames)
          continue;

        var candidateCounts = new[]
        {
          Mathf.Max(_minimumMotionFrames, Mathf.RoundToInt(template.Frames.Count * 0.35f)),
          Mathf.Max(_minimumMotionFrames, Mathf.RoundToInt(template.Frames.Count * 0.5f)),
          Mathf.Max(_minimumMotionFrames, Mathf.RoundToInt(template.Frames.Count * 0.75f)),
          template.Frames.Count,
          Mathf.Max(_minimumMotionFrames, Mathf.RoundToInt(template.Frames.Count * 1.25f))
        };
        foreach (var candidateCount in candidateCounts)
        {
          if (candidateCount > _motionHistory.Count) continue;
          var observed = _motionHistory.GetRange(_motionHistory.Count - candidateCount, candidateCount);
          if (!MatchesHandedness(template.Handedness, observed[0].Handedness)) continue;
          var elapsed = (observed[observed.Count - 1].Timestamp - observed[0].Timestamp) / 1000f;
          if (elapsed < 0.15f || elapsed > 3f || !HasMotionEnergy(observed)) continue;

          var distance = DynamicTimeWarpingDistance(observed, template.Frames);
          if (distance < bestDistance)
          {
            bestDistance = distance;
            bestThreshold = template.MaximumAverageDistance;
            letter = template.Letter;
          }
        }
      }

      if (string.IsNullOrEmpty(letter)) return false;
      similarity = bestThreshold > 0f && bestDistance <= bestThreshold
        ? Mathf.Clamp01(1f - bestDistance / bestThreshold)
        : 0f;
      return true;
    }

    private static bool HasMotionEnergy(IReadOnlyList<MotionObservation> frames)
    {
      var wristTravel = 0f;
      var shapeChange = 0f;
      for (var i = 1; i < frames.Count; i++)
      {
        wristTravel += Vector2.Distance(frames[i - 1].WristPosition, frames[i].WristPosition);
        shapeChange += AverageDistance(frames[0].Points, frames[i].Points);
      }
      return wristTravel >= 0.05f || shapeChange / Mathf.Max(1, frames.Count - 1) >= 0.025f;
    }

    private static float DynamicTimeWarpingDistance(
      IReadOnlyList<MotionObservation> observed,
      IReadOnlyList<MotionLandmarkFrame> reference)
    {
      var rows = observed.Count;
      var columns = reference.Count;
      var previous = new float[columns + 1];
      var current = new float[columns + 1];
      for (var j = 0; j <= columns; j++) previous[j] = float.PositiveInfinity;
      previous[0] = 0f;
      var observedOrigin = observed[0].WristPosition;
      var referenceOrigin = reference[0].WristPosition;
      var band = Mathf.Max(Mathf.Abs(rows - columns), Mathf.CeilToInt(Mathf.Max(rows, columns) * 0.2f));

      for (var i = 1; i <= rows; i++)
      {
        current[0] = float.PositiveInfinity;
        var minColumn = Mathf.Max(1, i - band);
        var maxColumn = Mathf.Min(columns, i + band);
        for (var j = 1; j < minColumn; j++) current[j] = float.PositiveInfinity;
        for (var j = minColumn; j <= maxColumn; j++)
        {
          var shape = AverageDistance(observed[i - 1].Points, reference[j - 1].NormalizedLandmarks);
          var observedWrist = observed[i - 1].WristPosition - observedOrigin;
          var referenceWrist = reference[j - 1].WristPosition - referenceOrigin;
          var cost = shape + Vector2.Distance(observedWrist, referenceWrist) * 0.5f;
          current[j] = cost + Mathf.Min(previous[j], Mathf.Min(current[j - 1], previous[j - 1]));
        }
        for (var j = maxColumn + 1; j <= columns; j++) current[j] = float.PositiveInfinity;
        var swap = previous;
        previous = current;
        current = swap;
      }
      return previous[columns] / Mathf.Max(rows, columns);
    }

    private static bool MatchesHandedness(LandmarkHandedness expected, string detected)
    {
      return expected == LandmarkHandedness.Any ||
        string.Equals(expected.ToString(), detected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalize(IReadOnlyList<HandLandmarkPoint> points, out Vector3[] result)
    {
      result = null;
      if (points == null || points.Count < LandmarkCount) return false;
      var wrist = ToVector3(points[WristIndex]);
      var scale = Vector3.Distance(wrist, ToVector3(points[MiddleFingerMcpIndex]));
      if (scale <= 0.0001f) return false;

      result = new Vector3[LandmarkCount];
      for (var i = 0; i < LandmarkCount; i++) result[i] = (ToVector3(points[i]) - wrist) / scale;
      return true;
    }

    private static Vector3 ToVector3(HandLandmarkPoint point)
    {
      return new Vector3(point.X, point.Y, point.Z);
    }

    private static float AverageDistance(Vector3[] observed, IReadOnlyList<Vector3> reference)
    {
      var sum = 0f;
      for (var i = 0; i < LandmarkCount; i++) sum += Vector3.Distance(observed[i], reference[i]);
      return sum / LandmarkCount;
    }

    private sealed class ObservedHand
    {
      public string Handedness { get; }
      public Vector3[] Points { get; }
      public Vector2 WristPosition { get; }

      public ObservedHand(string handedness, Vector3[] points, Vector2 wristPosition)
      {
        Handedness = handedness;
        Points = points;
        WristPosition = wristPosition;
      }
    }

    private sealed class MotionObservation
    {
      public long Timestamp { get; }
      public string Handedness { get; }
      public Vector2 WristPosition { get; }
      public Vector3[] Points { get; }

      public MotionObservation(long timestamp, string handedness, Vector2 wristPosition, Vector3[] points)
      {
        Timestamp = timestamp;
        Handedness = handedness;
        WristPosition = wristPosition;
        Points = points;
      }
    }
  }
}
