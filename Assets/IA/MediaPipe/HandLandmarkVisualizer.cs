using UnityEngine;

namespace Senas.AI.MediaPipe
{
  /// <summary>
  /// Visualiza la webcam y los landmarks en la escena de prueba.
  /// Tambien muestra el resultado del evaluador de posturas estaticas.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(HandLandmarkerCapture))]
  public sealed class HandLandmarkVisualizer : MonoBehaviour
  {
    private static readonly int[,] Connections =
    {
      { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 },
      { 0, 5 }, { 5, 6 }, { 6, 7 }, { 7, 8 },
      { 5, 9 }, { 9, 10 }, { 10, 11 }, { 11, 12 },
      { 9, 13 }, { 13, 14 }, { 14, 15 }, { 15, 16 },
      { 13, 17 }, { 0, 17 }, { 17, 18 }, { 18, 19 }, { 19, 20 }
    };

    [SerializeField] private Color _lineColor = Color.green;
    [SerializeField] private Color _landmarkColor = Color.yellow;
    [SerializeField, Min(2f)] private float _landmarkSize = 8f;
    [SerializeField, Min(1f)] private float _lineWidth = 2f;
    [SerializeField] private bool _mirrorPreview = true;

    private HandLandmarkerCapture _capture;
    private AlphabetLandmarkEvaluator _evaluator;
    private HandLandmarkFrame _latestFrame;
    private AlphabetLandmarkEvaluation _latestEvaluation;
    private Texture2D _pixel;

    private void Awake()
    {
      _capture = GetComponent<HandLandmarkerCapture>();
      _evaluator = GetComponent<AlphabetLandmarkEvaluator>();
      _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
      _pixel.SetPixel(0, 0, Color.white);
      _pixel.Apply(false, true);
    }

    private void OnEnable()
    {
      if (_capture == null) _capture = GetComponent<HandLandmarkerCapture>();
      if (_evaluator == null) _evaluator = GetComponent<AlphabetLandmarkEvaluator>();
      if (_capture != null) _capture.LandmarksUpdated += OnLandmarksUpdated;
      if (_evaluator != null) _evaluator.EvaluationUpdated += OnEvaluationUpdated;
    }

    private void OnDisable()
    {
      if (_capture != null) _capture.LandmarksUpdated -= OnLandmarksUpdated;
      if (_evaluator != null) _evaluator.EvaluationUpdated -= OnEvaluationUpdated;
      _latestFrame = null;
      _latestEvaluation = null;
    }

    private void OnDestroy()
    {
      if (_pixel != null) Destroy(_pixel);
    }

    private void OnLandmarksUpdated(HandLandmarkFrame frame)
    {
      _latestFrame = frame;
    }

    private void OnEvaluationUpdated(AlphabetLandmarkEvaluation evaluation)
    {
      _latestEvaluation = evaluation;
    }

    private void OnGUI()
    {
      var cameraTexture = _capture != null ? _capture.CameraTexture : null;
      if (cameraTexture != null && cameraTexture.isPlaying)
      {
        var previousMatrix = GUI.matrix;
        if (_mirrorPreview)
        {
          GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        GUI.color = Color.white;
        GUI.DrawTexture(
          new Rect(0f, 0f, Screen.width, Screen.height),
          cameraTexture,
          ScaleMode.StretchToFill,
          false
        );
        GUI.matrix = previousMatrix;
      }

      var handCount = _latestFrame != null && _latestFrame.Hands != null
        ? _latestFrame.Hands.Count
        : -1;
      var status = handCount < 0
        ? "Esperando resultados de MediaPipe..."
        : $"Manos detectadas: {handCount}";
      var statusRect = new Rect(12f, 12f, 280f, 32f);
      GUI.color = new Color(0f, 0f, 0f, 0.7f);
      GUI.DrawTexture(statusRect, _pixel);
      GUI.color = Color.white;
      GUI.Label(new Rect(20f, 16f, 264f, 24f), status);

      if (_latestFrame != null && _latestFrame.Hands != null)
      {
        foreach (var hand in _latestFrame.Hands)
        {
          var landmarks = hand.NormalizedLandmarks;
          if (landmarks == null || landmarks.Count < 21) continue;

          for (var i = 0; i < Connections.GetLength(0); i++)
          {
            var start = ToScreenPoint(landmarks[Connections[i, 0]]);
            var end = ToScreenPoint(landmarks[Connections[i, 1]]);
            DrawLine(start, end);
          }

          for (var i = 0; i < 21; i++) DrawLandmark(ToScreenPoint(landmarks[i]));
        }
      }

      DrawEvaluationOverlay();
      GUI.color = Color.white;
    }

    private void DrawEvaluationOverlay()
    {
      if (_evaluator == null || !_evaluator.ShowDebugOverlay || _latestEvaluation == null) return;

      var message = _latestEvaluation.Status switch
      {
        AlphabetEvaluationStatus.NoHand => "Evaluador: muestra una mano",
        AlphabetEvaluationStatus.NoTemplates => "Evaluador: faltan plantillas estaticas",
        AlphabetEvaluationStatus.MotionTemplateNeedsSequence => "Evaluador: esta seña necesita una secuencia",
        AlphabetEvaluationStatus.MotionPending => "Evaluador: realiza el movimiento completo",
        AlphabetEvaluationStatus.Uncertain => $"Candidata: {_latestEvaluation.CandidateLetter}  similitud geometrica: {_latestEvaluation.Similarity:P0}",
        AlphabetEvaluationStatus.Recognized => $"Letra aceptada: {_latestEvaluation.CandidateLetter}  similitud geometrica: {_latestEvaluation.Similarity:P0}",
        _ => "Evaluador listo"
      };

      var rect = new Rect(12f, 50f, 420f, 42f);
      GUI.color = new Color(0f, 0f, 0f, 0.72f);
      GUI.DrawTexture(rect, _pixel);
      GUI.color = Color.white;
      GUI.Label(new Rect(rect.x + 8f, rect.y + 9f, rect.width - 16f, 24f), message);
    }

    private Vector2 ToScreenPoint(HandLandmarkPoint point)
    {
      var x = _mirrorPreview ? 1f - point.X : point.X;
      return new Vector2(x * Screen.width, (1f - point.Y) * Screen.height);
    }

    private void DrawLandmark(Vector2 point)
    {
      GUI.color = _landmarkColor;
      GUI.DrawTexture(
        new Rect(
          point.x - _landmarkSize * 0.5f,
          point.y - _landmarkSize * 0.5f,
          _landmarkSize,
          _landmarkSize
        ),
        _pixel
      );
    }

    private void DrawLine(Vector2 start, Vector2 end)
    {
      var delta = end - start;
      var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
      var previousMatrix = GUI.matrix;

      GUI.color = _lineColor;
      GUIUtility.RotateAroundPivot(angle, start);
      GUI.DrawTexture(
        new Rect(start.x, start.y - _lineWidth * 0.5f, delta.magnitude, _lineWidth),
        _pixel
      );
      GUI.matrix = previousMatrix;
    }
  }
}
