using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Senas.AI.MediaPipe
{
  /// <summary>Referencias configurables de postura LSC, normalizadas respecto a muneca y palma.</summary>
  [CreateAssetMenu(menuName = "SENAS/IA/Plantillas de senas", fileName = "AlphabetLandmarkTemplates")]
  public sealed class AlphabetLandmarkTemplateSet : ScriptableObject
  {
    private static readonly string[] MotionLetters = { "G", "H", "J", "Ñ", "S", "Z" };
    [SerializeField] private List<AlphabetLandmarkTemplate> _templates = new List<AlphabetLandmarkTemplate>();
    [SerializeField] private List<AlphabetMotionTemplate> _motionTemplates = new List<AlphabetMotionTemplate>();
    [SerializeField] private List<GreetingMotionTemplate> _greetingMotionTemplates = new List<GreetingMotionTemplate>();
    public IReadOnlyList<AlphabetLandmarkTemplate> Templates => _templates;
    public IReadOnlyList<AlphabetMotionTemplate> MotionTemplates => _motionTemplates ?? (_motionTemplates = new List<AlphabetMotionTemplate>());
    public IReadOnlyList<GreetingMotionTemplate> GreetingMotionTemplates => _greetingMotionTemplates ?? (_greetingMotionTemplates = new List<GreetingMotionTemplate>());

    /// <summary>Agrega una muestra estatica capturada explícitamente desde el editor.</summary>
    public void AddStaticTemplate(string letter, LandmarkHandedness handedness, IReadOnlyList<Vector3> points, float maximumAverageDistance)
    {
      if (string.IsNullOrWhiteSpace(letter)) throw new ArgumentException("La letra no puede estar vacia.", nameof(letter));
      if (points == null || points.Count != 21) throw new ArgumentException("La muestra debe tener exactamente 21 landmarks.", nameof(points));

      var copy = new List<Vector3>(points.Count);
      for (var i = 0; i < points.Count; i++) copy.Add(points[i]);
      _templates.Add(new AlphabetLandmarkTemplate(letter, handedness, maximumAverageDistance, copy, IsMotionLetter(letter)));
    }

    public void AddMotionTemplate(string letter, LandmarkHandedness handedness, IReadOnlyList<MotionLandmarkFrame> frames, float maximumAverageDistance)
    {
      if (string.IsNullOrWhiteSpace(letter)) throw new ArgumentException("La letra no puede estar vacia.", nameof(letter));
      letter = letter.Trim();
      if (!IsSupportedMotionLabel(letter)) throw new ArgumentException("La etiqueta debe ser una letra dinámica, un número entre 0 y 30 o un saludo del catálogo.", nameof(letter));
      if (frames == null || frames.Count < 6) throw new ArgumentException("La secuencia debe tener al menos 6 fotogramas.", nameof(frames));
      if (_motionTemplates == null) _motionTemplates = new List<AlphabetMotionTemplate>();

      var copy = new List<MotionLandmarkFrame>(frames.Count);
      foreach (var frame in frames)
      {
        if (frame == null || frame.NormalizedLandmarks == null || frame.NormalizedLandmarks.Count != 21)
          throw new ArgumentException("Cada fotograma debe contener exactamente 21 landmarks.", nameof(frames));
        copy.Add(new MotionLandmarkFrame(frame.Time, frame.WristPosition, frame.NormalizedLandmarks));
      }
      foreach (var template in _templates)
        if (template != null && string.Equals(template.Letter, letter, StringComparison.OrdinalIgnoreCase))
          template.SetRequiresMotion(true);

      _motionTemplates.Add(new AlphabetMotionTemplate(letter, handedness, maximumAverageDistance, copy));
    }

    public void AddGreetingMotionTemplate(string id, GreetingMotionRequirement requirement, IReadOnlyList<GreetingMotionFrame> frames, float maximumAverageDistance)
    {
      if (string.IsNullOrWhiteSpace(id) ||
          !id.StartsWith("saludos.", StringComparison.OrdinalIgnoreCase) &&
          !id.StartsWith("meses.", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("La etiqueta debe pertenecer al catálogo temporal de saludos o meses.", nameof(id));
      if (frames == null || frames.Count < 6) throw new ArgumentException("La secuencia debe tener al menos 6 fotogramas.", nameof(frames));
      if (_greetingMotionTemplates == null) _greetingMotionTemplates = new List<GreetingMotionTemplate>();
      var copy = new List<GreetingMotionFrame>(frames.Count);
      foreach (var frame in frames)
      {
        if (frame == null || frame.Features == null || frame.Features.Count != GreetingMotionFrame.FeatureCount)
          throw new ArgumentException($"Cada fotograma debe contener {GreetingMotionFrame.FeatureCount} características de Holistic.", nameof(frames));
        copy.Add(new GreetingMotionFrame(frame.Time, frame.Features));
      }
      _greetingMotionTemplates.Add(new GreetingMotionTemplate(id, requirement, maximumAverageDistance, copy));
    }

    private static bool IsMotionLetter(string letter)
    {
      foreach (var motionLetter in MotionLetters)
        if (string.Equals(letter.Trim(), motionLetter, StringComparison.OrdinalIgnoreCase)) return true;
      return false;
    }

    private static bool IsSupportedMotionLabel(string label)
    {
      if (IsMotionLetter(label)) return true;
      if (label.StartsWith("saludos.", StringComparison.OrdinalIgnoreCase) && label.Length > "saludos.".Length)
        return true;
      return int.TryParse(label, NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
        number >= 0 && number <= 30 &&
        string.Equals(number.ToString(CultureInfo.InvariantCulture), label, StringComparison.Ordinal);
    }
  }

  [Flags]
  public enum GreetingMotionRequirement { OneHand = 1, TwoHands = 2, HeadReference = 4, HeadTouch = 8 }

  public static class GreetingMotionRules
  {
    public static GreetingMotionRequirement ForId(string id)
    {
      switch (id)
      {
        case "saludos.hola": return GreetingMotionRequirement.OneHand | GreetingMotionRequirement.HeadReference;
        case "saludos.buenos_dias": case "saludos.buenas_tardes": case "saludos.buenas_noches":
          return GreetingMotionRequirement.TwoHands | GreetingMotionRequirement.HeadReference;
        case "saludos.gracias": return GreetingMotionRequirement.TwoHands | GreetingMotionRequirement.HeadReference | GreetingMotionRequirement.HeadTouch;
        case "saludos.como_estas": case "saludos.bien": case "saludos.de_nada": return GreetingMotionRequirement.TwoHands;
        default: return GreetingMotionRequirement.OneHand;
      }
    }

    public static List<Vector3> Canonicalize(HolisticLandmarkObservation observation, GreetingMotionRequirement requirement)
    {
      var features = new List<Vector3>(GreetingMotionFrame.FeatureCount);
      for (var i = 0; i < GreetingMotionFrame.FeatureCount; i++) features.Add(observation.Features[i]);
      if ((requirement & GreetingMotionRequirement.OneHand) != 0 && (requirement & GreetingMotionRequirement.TwoHands) == 0)
      {
        var start = observation.HasRightHand ? 21 : 0;
        for (var i = 0; i < 21; i++) features[i] = observation.Features[start + i];
        for (var i = 21; i < 42; i++) features[i] = Vector3.zero;
      }
      if ((requirement & GreetingMotionRequirement.HeadReference) == 0)
        for (var i = 42; i < GreetingMotionFrame.FeatureCount; i++) features[i] = Vector3.zero;
      return features;
    }
  }

  [Serializable]
  public sealed class GreetingMotionTemplate
  {
    [SerializeField] private string _id;
    [SerializeField] private GreetingMotionRequirement _requirement;
    [SerializeField, Min(0f)] private float _maximumAverageDistance = 0.45f;
    [SerializeField] private List<GreetingMotionFrame> _frames = new List<GreetingMotionFrame>();
    public string Id => _id;
    public GreetingMotionRequirement Requirement => _requirement;
    public float MaximumAverageDistance => _maximumAverageDistance;
    public IReadOnlyList<GreetingMotionFrame> Frames => _frames;
    public GreetingMotionTemplate() { }
    public GreetingMotionTemplate(string id, GreetingMotionRequirement requirement, float threshold, List<GreetingMotionFrame> frames)
    { _id = id; _requirement = requirement; _maximumAverageDistance = Mathf.Max(0f, threshold); _frames = frames; }
  }

  [Serializable]
  public sealed class GreetingMotionFrame
  {
    public const int FeatureCount = 55; // 21 puntos por mano, 4 anclas faciales y 9 anclas de cabeza/parte superior del cuerpo.
    [SerializeField, Range(0f, 1f)] private float _time;
    [SerializeField] private List<Vector3> _features = new List<Vector3>();
    public float Time => _time;
    public IReadOnlyList<Vector3> Features => _features;
    public GreetingMotionFrame() { }
    public GreetingMotionFrame(float time, IReadOnlyList<Vector3> features)
    { _time = Mathf.Clamp01(time); _features = new List<Vector3>(features); }
  }

  public enum LandmarkHandedness { Any, Left, Right }

  [Serializable]
  public sealed class AlphabetLandmarkTemplate
  {
    [SerializeField] private string _letter;
    [SerializeField] private LandmarkHandedness _handedness = LandmarkHandedness.Any;
    [SerializeField] private bool _requiresMotion;
    [SerializeField, Min(0f)] private float _maximumAverageDistance = 0.2f;
    [SerializeField] private List<Vector3> _normalizedLandmarks = new List<Vector3>();

    public string Letter => _letter;
    public LandmarkHandedness Handedness => _handedness;
    public bool RequiresMotion => _requiresMotion;
    public float MaximumAverageDistance => _maximumAverageDistance;
    public IReadOnlyList<Vector3> NormalizedLandmarks => _normalizedLandmarks;

    public AlphabetLandmarkTemplate() { }

    internal void SetRequiresMotion(bool requiresMotion) => _requiresMotion = requiresMotion;

    public AlphabetLandmarkTemplate(string letter, LandmarkHandedness handedness, float maximumAverageDistance, List<Vector3> normalizedLandmarks, bool requiresMotion = false)
    {
      _letter = letter;
      _handedness = handedness;
      _maximumAverageDistance = Mathf.Max(0f, maximumAverageDistance);
      _normalizedLandmarks = normalizedLandmarks;
      _requiresMotion = requiresMotion;
    }
  }

  [Serializable]
  public sealed class AlphabetMotionTemplate
  {
    [SerializeField] private string _letter;
    [SerializeField] private LandmarkHandedness _handedness = LandmarkHandedness.Any;
    [SerializeField, Min(0f)] private float _maximumAverageDistance = 0.35f;
    [SerializeField] private List<MotionLandmarkFrame> _frames = new List<MotionLandmarkFrame>();

    public string Letter => _letter;
    public LandmarkHandedness Handedness => _handedness;
    public float MaximumAverageDistance => _maximumAverageDistance;
    public IReadOnlyList<MotionLandmarkFrame> Frames => _frames;

    public AlphabetMotionTemplate() { }

    public AlphabetMotionTemplate(string letter, LandmarkHandedness handedness, float threshold, List<MotionLandmarkFrame> frames)
    {
      _letter = letter;
      _handedness = handedness;
      _maximumAverageDistance = Mathf.Max(0f, threshold);
      _frames = frames;
    }
  }

  [Serializable]
  public sealed class MotionLandmarkFrame
  {
    [SerializeField, Range(0f, 1f)] private float _time;
    [SerializeField] private Vector2 _wristPosition;
    [SerializeField] private List<Vector3> _normalizedLandmarks = new List<Vector3>();

    public float Time => _time;
    public Vector2 WristPosition => _wristPosition;
    public IReadOnlyList<Vector3> NormalizedLandmarks => _normalizedLandmarks;

    public MotionLandmarkFrame() { }

    public MotionLandmarkFrame(float time, Vector2 wristPosition, IReadOnlyList<Vector3> landmarks)
    {
      _time = Mathf.Clamp01(time);
      _wristPosition = wristPosition;
      _normalizedLandmarks = new List<Vector3>(landmarks);
    }
  }
}
