using System;
using System.Collections.Generic;
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
    public IReadOnlyList<AlphabetLandmarkTemplate> Templates => _templates;
    public IReadOnlyList<AlphabetMotionTemplate> MotionTemplates => _motionTemplates ?? (_motionTemplates = new List<AlphabetMotionTemplate>());

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
      if (!IsMotionLetter(letter)) throw new ArgumentException("La letra no pertenece a la lista dinamica LSC validada.", nameof(letter));
      if (frames == null || frames.Count < 6) throw new ArgumentException("La secuencia debe tener al menos 6 fotogramas.", nameof(frames));
      if (_motionTemplates == null) _motionTemplates = new List<AlphabetMotionTemplate>();

      var copy = new List<MotionLandmarkFrame>(frames.Count);
      foreach (var frame in frames)
      {
        if (frame == null || frame.NormalizedLandmarks == null || frame.NormalizedLandmarks.Count != 21)
          throw new ArgumentException("Cada fotograma debe contener exactamente 21 landmarks.", nameof(frames));
        copy.Add(new MotionLandmarkFrame(frame.Time, frame.WristPosition, frame.NormalizedLandmarks));
      }
      _motionTemplates.Add(new AlphabetMotionTemplate(letter, handedness, maximumAverageDistance, copy));
    }

    private static bool IsMotionLetter(string letter)
    {
      foreach (var motionLetter in MotionLetters)
        if (string.Equals(letter.Trim(), motionLetter, StringComparison.OrdinalIgnoreCase)) return true;
      return false;
    }
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
