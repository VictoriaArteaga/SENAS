using System;
using System.Collections.Generic;
using Senas.AI.MediaPipe;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Senas.AI.Editor
{
  /// <summary>Captura muestras estaticas promediadas. Solo guarda landmarks al pulsar el boton.</summary>
  public sealed class AlphabetLandmarkTemplateCaptureWindow : EditorWindow
  {
    private const int LandmarkCount = 21;
    private const int WristIndex = 0;
    private const int MiddleFingerMcpIndex = 9;

    [SerializeField] private AlphabetLandmarkTemplateSet _templateSet;
    [SerializeField] private string _letter = "A";
    [SerializeField] private LandmarkHandedness _handedness = LandmarkHandedness.Any;
    [SerializeField, Min(5)] private int _framesToAverage = 15;
    [SerializeField, Min(0.001f)] private float _maximumAverageDistance = 0.2f;

    private HandLandmarkerCapture _capture;
    private int _framesCaptured;
    private bool _isCapturing;
    private Vector3[] _sum;
    private string _status = "Crea o asigna un asset de plantillas y entra en Play Mode.";

    [MenuItem("Window/SENAS/LSC Template Capture")]
    private static void Open()
    {
      GetWindow<AlphabetLandmarkTemplateCaptureWindow>("LSC Template Capture");
    }

    private void OnEnable()
    {
      if (_templateSet == null)
      {
        _templateSet = AssetDatabase.LoadAssetAtPath<AlphabetLandmarkTemplateSet>(
          "Assets/IA/MediaPipe/AlphabetLandmarkTemplates.asset"
        );
      }
      EditorApplication.update += EditorUpdate;
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
      EditorApplication.update -= EditorUpdate;
      EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
      SetCapture(null);
    }

    private void OnGUI()
    {
      if (EditorApplication.isPlaying && _capture == null) ResolveCapture();
      EditorGUILayout.LabelField("Captura de plantillas estaticas", EditorStyles.boldLabel);
      EditorGUILayout.HelpBox(
        "Promedia landmarks de una mano sostenida. No guarda video ni imagenes. Las letras con movimiento necesitan otro tipo de plantilla.",
        MessageType.Info
      );

      _templateSet = (AlphabetLandmarkTemplateSet)EditorGUILayout.ObjectField(
        "Asset de plantillas", _templateSet, typeof(AlphabetLandmarkTemplateSet), false
      );

      if (_templateSet == null && GUILayout.Button("Crear asset de plantillas"))
      {
        CreateTemplateAsset();
      }

      using (new EditorGUI.DisabledScope(_isCapturing))
      {
        _letter = EditorGUILayout.TextField("Letra", _letter).Trim().ToUpperInvariant();
        _handedness = (LandmarkHandedness)EditorGUILayout.EnumPopup("Mano", _handedness);
        _framesToAverage = EditorGUILayout.IntSlider("Fotogramas a promediar", _framesToAverage, 5, 60);
        _maximumAverageDistance = EditorGUILayout.Slider("Umbral inicial", _maximumAverageDistance, 0.01f, 1f);
      }

      EditorGUILayout.Space();
      using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
      {
        var assignedCapture = (HandLandmarkerCapture)EditorGUILayout.ObjectField(
          "Fuente de landmarks", _capture, typeof(HandLandmarkerCapture), true
        );
        if (assignedCapture != _capture)
        {
          SetCapture(assignedCapture);
          _status = assignedCapture != null
            ? "Fuente de landmarks conectada."
            : "Buscando HandLandmarkerCapture activo en la escena...";
        }

        if (GUILayout.Button("Conectar MediaPipeCapture seleccionado"))
        {
          var selectedCapture = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<HandLandmarkerCapture>()
            : null;
          if (IsActiveCapture(selectedCapture))
          {
            SetCapture(selectedCapture);
            _status = "Fuente de landmarks conectada.";
          }
          else
          {
            _status = "Selecciona el objeto MediaPipeCapture de la Hierarchy mientras Play esta activo.";
          }
        }
      }
      EditorGUILayout.LabelField("Estado", _status);
      if (EditorApplication.isPlaying)
        EditorGUILayout.LabelField("Muestras recibidas", $"{_framesCaptured}/{_framesToAverage}");

      using (new EditorGUI.DisabledScope(
        !EditorApplication.isPlaying || _capture == null || _templateSet == null || _isCapturing || _letter.Length != 1))
      {
        if (GUILayout.Button("Capturar muestra estatica")) BeginCapture();
      }

      if (!EditorApplication.isPlaying)
      {
        EditorGUILayout.HelpBox(
          "Abre la escena de prueba y pulsa Play para que MediaPipe entregue landmarks. La muestra se agrega al asset al completar el promedio.",
          MessageType.None
        );
      }
      else if (_capture == null)
      {
        EditorGUILayout.HelpBox(
          "Esperando conexion con HandLandmarkerCapture en la escena. El boton se habilitara cuando la fuente este conectada.",
          MessageType.Warning
        );
      }
    }

    private void EditorUpdate()
    {
      if (!EditorApplication.isPlaying)
      {
        if (_capture != null) SetCapture(null);
        return;
      }

      ResolveCapture();
      if (_isCapturing) Repaint();
    }

    private void ResolveCapture()
    {
      var activeCapture = FindActiveCapture();
      if (activeCapture == _capture) return;

      SetCapture(activeCapture);
      _status = activeCapture != null
        ? "Fuente de landmarks conectada. Mantén la seña y captura cuando estés lista."
        : "Buscando HandLandmarkerCapture activo en la escena...";
      Repaint();
    }

    private static HandLandmarkerCapture FindActiveCapture()
    {
      // La selección de Unity suele ser la fuente más directa en una escena de prueba.
      if (Selection.activeGameObject != null)
      {
        var selectedCapture = Selection.activeGameObject.GetComponent<HandLandmarkerCapture>();
        if (IsActiveCapture(selectedCapture)) return selectedCapture;
      }

      // Buscar primero en la escena activa evita depender solo del caché global.
      var activeScene = SceneManager.GetActiveScene();
      if (activeScene.IsValid() && activeScene.isLoaded)
      {
        foreach (var root in activeScene.GetRootGameObjects())
        {
          foreach (var candidate in root.GetComponentsInChildren<HandLandmarkerCapture>(true))
          {
            if (IsActiveCapture(candidate)) return candidate;
          }
        }
      }

      // Respaldo para objetos persistentes o escenas aditivas.
      var captures = Resources.FindObjectsOfTypeAll<HandLandmarkerCapture>();
      foreach (var candidate in captures)
      {
        if (!IsActiveCapture(candidate)) continue;
        var scene = candidate.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded) return candidate;
      }
      return null;
    }

    private static bool IsActiveCapture(HandLandmarkerCapture candidate)
    {
      return candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
      if (state == PlayModeStateChange.EnteredPlayMode)
      {
        _status = "Buscando HandLandmarkerCapture activo en la escena...";
      }
      else
      {
        SetCapture(null);
      }
      Repaint();
    }

    private void SetCapture(HandLandmarkerCapture capture)
    {
      if (_capture == capture) return;
      if (_isCapturing)
      {
        _isCapturing = false;
        _framesCaptured = 0;
        _sum = null;
        _status = "Captura cancelada porque cambio el estado de la escena.";
      }
      if (_capture != null) _capture.LandmarksUpdated -= OnLandmarksUpdated;
      _capture = capture;
      if (_capture != null) _capture.LandmarksUpdated += OnLandmarksUpdated;
    }

    private void BeginCapture()
    {
      if (_capture == null)
      {
        _status = "No se encontro HandLandmarkerCapture activo. Comprueba la escena en Play Mode.";
        return;
      }

      _framesCaptured = 0;
      _isCapturing = true;
      _sum = new Vector3[LandmarkCount];
      _status = $"Sostén la seña '{_letter}' quieta frente a la camara.";
    }

    private void OnLandmarksUpdated(HandLandmarkFrame frame)
    {
      if (!_isCapturing || frame == null || frame.Hands == null) return;

      foreach (var hand in frame.Hands)
      {
        if (hand == null || !MatchesHandedness(_handedness, hand.Handedness)) continue;
        if (!TryNormalize(hand.WorldLandmarks, out var points) &&
            !TryNormalize(hand.NormalizedLandmarks, out points)) continue;

        for (var i = 0; i < LandmarkCount; i++) _sum[i] += points[i];
        _framesCaptured++;
        if (_framesCaptured >= _framesToAverage) SaveSample();
        return;
      }
    }

    private void SaveSample()
    {
      var average = new List<Vector3>(LandmarkCount);
      for (var i = 0; i < LandmarkCount; i++) average.Add(_sum[i] / _framesCaptured);

      Undo.RecordObject(_templateSet, $"Capturar seña {_letter}");
      _templateSet.AddStaticTemplate(_letter, _handedness, average, _maximumAverageDistance);
      EditorUtility.SetDirty(_templateSet);
      AssetDatabase.SaveAssets();

      _status = $"Muestra de '{_letter}' guardada ({_framesCaptured} fotogramas). Puedes capturar otra variacion.";
      _framesCaptured = 0;
      _isCapturing = false;
      _sum = null;
    }

    private void CreateTemplateAsset()
    {
      const string defaultPath = "Assets/IA/MediaPipe/AlphabetLandmarkTemplates.asset";
      var path = AssetDatabase.GenerateUniqueAssetPath(defaultPath);
      var asset = CreateInstance<AlphabetLandmarkTemplateSet>();
      AssetDatabase.CreateAsset(asset, path);
      AssetDatabase.SaveAssets();
      _templateSet = asset;
      EditorGUIUtility.PingObject(asset);
      _status = $"Asset creado: {path}";
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
      for (var i = 0; i < LandmarkCount; i++)
        result[i] = (ToVector3(points[i]) - wrist) / scale;
      return true;
    }

    private static Vector3 ToVector3(HandLandmarkPoint point)
    {
      return new Vector3(point.X, point.Y, point.Z);
    }
  }
}