using System.Collections.Generic;
using Senas.AI.MediaPipe;
using UnityEditor;
using UnityEngine;

namespace Senas.AI.Editor
{
  /// <summary>Captura landmarks temporales sin conservar video ni imagenes.</summary>
  public sealed class AlphabetMotionSequenceCaptureWindow : EditorWindow
  {
    private static readonly string[] Letters = { "G", "H", "J", "Ñ", "S", "Z" };
    private const int LandmarkCount = 21;
    private const int WristIndex = 0;
    private const int MiddleFingerMcpIndex = 9;

    [SerializeField] private AlphabetLandmarkTemplateSet _templateSet;
    [SerializeField] private int _letterIndex;
    [SerializeField] private LandmarkHandedness _handedness = LandmarkHandedness.Any;
    [SerializeField, Range(0.05f, 1f)] private float _distanceThreshold = 0.35f;
    private HandLandmarkerCapture _capture;
    private readonly List<RawFrame> _frames = new List<RawFrame>();
    private bool _recording;
    private bool _motionDetected;
    private int _stillFrameCount;
    private long _lastTimestamp;
    private string _status = "Abre la escena de webcam, inicia Play y conecta MediaPipe.";

    [MenuItem("Window/SENAS/LSC Motion Capture")]
    private static void Open() => GetWindow<AlphabetMotionSequenceCaptureWindow>("LSC Motion Capture");

    private void OnEnable()
    {
      if (_templateSet == null)
        _templateSet = AssetDatabase.LoadAssetAtPath<AlphabetLandmarkTemplateSet>("Assets/IA/MediaPipe/AlphabetLandmarkTemplates.asset");
      EditorApplication.update += EditorUpdate;
      EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
      EditorApplication.update -= EditorUpdate;
      EditorApplication.playModeStateChanged -= OnPlayModeChanged;
      SetCapture(null);
    }

    private void OnGUI()
    {
      EditorGUILayout.HelpBox("Captura secuencias de G, H, J, Ñ, S y Z. Solo guarda landmarks normalizados; no graba video ni imagenes. Se guarda al detectar el fin del movimiento o al pulsar el boton.", MessageType.Info);
      _templateSet = (AlphabetLandmarkTemplateSet)EditorGUILayout.ObjectField("Asset", _templateSet, typeof(AlphabetLandmarkTemplateSet), false);
      _letterIndex = EditorGUILayout.Popup("Letra dinamica", _letterIndex, Letters);
      _handedness = (LandmarkHandedness)EditorGUILayout.EnumPopup("Mano", _handedness);
      _distanceThreshold = EditorGUILayout.Slider("Umbral DTW inicial", _distanceThreshold, 0.05f, 1f);

      using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
      {
        var assigned = (HandLandmarkerCapture)EditorGUILayout.ObjectField("Fuente de landmarks", _capture, typeof(HandLandmarkerCapture), true);
        if (assigned != _capture) SetCapture(assigned);
        if (GUILayout.Button("Conectar captura activa")) SetCapture(FindCapture());
      }

      EditorGUILayout.LabelField("Estado", _status);
      EditorGUILayout.LabelField("Fotogramas", _frames.Count.ToString());
      using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _capture == null || _templateSet == null))
      {
        if (!_recording)
        {
          if (GUILayout.Button("Iniciar secuencia")) BeginRecording();
        }
        else if (GUILayout.Button("Finalizar y guardar")) FinishRecording();
      }
    }

    private void EditorUpdate()
    {
      if (!EditorApplication.isPlaying) { SetCapture(null); return; }
      if (_capture == null) SetCapture(FindCapture());
      if (_recording) Repaint();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
      if (state != PlayModeStateChange.EnteredPlayMode) SetCapture(null);
    }

    private static HandLandmarkerCapture FindCapture()
    {
      foreach (var capture in Resources.FindObjectsOfTypeAll<HandLandmarkerCapture>())
        if (capture != null && capture.isActiveAndEnabled && capture.gameObject.scene.IsValid()) return capture;
      return null;
    }

    private void SetCapture(HandLandmarkerCapture capture)
    {
      if (_capture == capture) return;
      if (_capture != null) _capture.LandmarksUpdated -= OnLandmarks;
      _capture = capture;
      if (_capture != null) _capture.LandmarksUpdated += OnLandmarks;
      if (_recording) CancelRecording("Captura cancelada al cambiar la fuente.");
      if (_capture != null) _status = "MediaPipe conectado. Encuadra la mano e inicia la secuencia.";
      Repaint();
    }

    private void BeginRecording()
    {
      _frames.Clear();
      _lastTimestamp = 0;
      _motionDetected = false;
      _stillFrameCount = 0;
      _recording = true;
      _status = $"Haz el movimiento completo de {Letters[_letterIndex]}. La captura se cierra al quedar la mano quieta.";
    }

    private void OnLandmarks(HandLandmarkFrame frame)
    {
      if (!_recording || frame == null || frame.Hands == null) return;
      if (_frames.Count >= 90) return;
      DetectedHandLandmarks selected = null;
      foreach (var hand in frame.Hands)
      {
        if (hand == null || !MatchesHandedness(_handedness, hand.Handedness)) continue;
        selected = hand;
        break;
      }
      if (selected == null || selected.NormalizedLandmarks == null || selected.NormalizedLandmarks.Count < LandmarkCount)
      {
        if (_lastTimestamp > 0 && frame.TimestampMilliseconds - _lastTimestamp > 250)
          CancelRecording("Tracking interrumpido; repite la secuencia en una sola toma.");
        return;
      }

      var points = selected.WorldLandmarks;
      if (!TryNormalize(points, out var normalized) && !TryNormalize(selected.NormalizedLandmarks, out normalized)) return;
      var image = selected.NormalizedLandmarks;
      var wrist = new Vector2(image[WristIndex].X, image[WristIndex].Y);
      var previous = _frames.Count > 0 ? _frames[_frames.Count - 1] : null;
      var motionDelta = previous != null ? MeasureFrameChange(previous, wrist, normalized) : 0f;
      _frames.Add(new RawFrame(frame.TimestampMilliseconds, wrist, normalized));
      _lastTimestamp = frame.TimestampMilliseconds;
      if (motionDelta >= 0.025f)
      {
        _motionDetected = true;
        _stillFrameCount = 0;
      }
      else if (_motionDetected) _stillFrameCount++;

      if (_motionDetected && _stillFrameCount >= 12)
      {
        FinishRecording();
        _status = "Movimiento completado automaticamente. " + _status;
      }
      else if (_frames.Count >= 90) FinishRecording();
    }

    private void FinishRecording()
    {
      _recording = false;
      if (_frames.Count < 6 || _frames[_frames.Count - 1].Timestamp - _frames[0].Timestamp < 150)
      {
        CancelRecording("Secuencia demasiado corta; captura al menos 6 fotogramas y 0,15 s.");
        return;
      }

      var start = _frames[0].Timestamp;
      var duration = _frames[_frames.Count - 1].Timestamp - start;
      var normalized = new List<MotionLandmarkFrame>(_frames.Count);
      foreach (var frame in _frames)
        normalized.Add(new MotionLandmarkFrame((frame.Timestamp - start) / (float)duration, frame.Wrist, frame.Points));

      var letter = Letters[_letterIndex];
      Undo.RecordObject(_templateSet, $"Capturar movimiento LSC {letter}");
      _templateSet.AddMotionTemplate(letter, _handedness, normalized, _distanceThreshold);
      EditorUtility.SetDirty(_templateSet);
      AssetDatabase.SaveAssets();
      _status = $"Secuencia {letter} guardada: {_frames.Count} fotogramas en {duration / 1000f:F2} s.";
      _frames.Clear();
    }

    private void CancelRecording(string reason)
    {
      _recording = false;
      _frames.Clear();
      _lastTimestamp = 0;
      _status = reason;
      Repaint();
    }

    private static bool MatchesHandedness(LandmarkHandedness expected, string detected) =>
      expected == LandmarkHandedness.Any || string.Equals(expected.ToString(), detected, System.StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalize(IReadOnlyList<HandLandmarkPoint> points, out Vector3[] result)
    {
      result = null;
      if (points == null || points.Count < LandmarkCount) return false;
      var wrist = new Vector3(points[WristIndex].X, points[WristIndex].Y, points[WristIndex].Z);
      var mcp = new Vector3(points[MiddleFingerMcpIndex].X, points[MiddleFingerMcpIndex].Y, points[MiddleFingerMcpIndex].Z);
      var scale = Vector3.Distance(wrist, mcp);
      if (scale <= 0.0001f) return false;
      result = new Vector3[LandmarkCount];
      for (var i = 0; i < LandmarkCount; i++)
        result[i] = (new Vector3(points[i].X, points[i].Y, points[i].Z) - wrist) / scale;
      return true;
    }

    private static float MeasureFrameChange(RawFrame previous, Vector2 wrist, IReadOnlyList<Vector3> points)
    {
      var shapeChange = 0f;
      for (var i = 0; i < LandmarkCount; i++) shapeChange += Vector3.Distance(previous.Points[i], points[i]);
      shapeChange /= LandmarkCount;
      return shapeChange + Vector2.Distance(previous.Wrist, wrist) * 0.5f;
    }

    private sealed class RawFrame
    {
      public long Timestamp { get; }
      public Vector2 Wrist { get; }
      public IReadOnlyList<Vector3> Points { get; }
      public RawFrame(long timestamp, Vector2 wrist, IReadOnlyList<Vector3> points) { Timestamp = timestamp; Wrist = wrist; Points = points; }
    }
  }
}
