using System.Collections.Generic;
using Senas.AI.MediaPipe;
using UnityEditor;
using UnityEngine;

namespace Senas.AI.Editor
{
  /// <summary>Captura secuencias temporales de saludos y meses mediante landmarks Holistic.</summary>
  public sealed class GreetingMotionCaptureWindow : EditorWindow
  {
    private static readonly string[] Ids = { "saludos.hola", "saludos.buenos_dias", "saludos.buenas_tardes", "saludos.buenas_noches", "saludos.adios", "saludos.como_estas", "saludos.bien", "saludos.gracias", "saludos.de_nada", "saludos.si", "saludos.no" };
    private static readonly string[] Labels = { "Hola", "Buenos días", "Buenas tardes", "Buenas noches", "Adiós", "¿Cómo estás?", "Bien", "Gracias", "De nada", "Sí", "No" };
    private static readonly string[] MonthIds = { "meses.enero", "meses.febrero", "meses.marzo", "meses.abril", "meses.mayo", "meses.junio", "meses.julio", "meses.agosto", "meses.septiembre", "meses.octubre", "meses.noviembre", "meses.diciembre" };
    private static readonly string[] MonthLabels = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
    private const string AssetPath = "Assets/Project/Data/GreetingLandmarkTemplates.asset";
    [SerializeField] private AlphabetLandmarkTemplateSet _templates;
    [SerializeField] private int _selected;
    [SerializeField] private bool _monthsMode;
    [SerializeField] private bool _monthUsesTwoHands;
    [SerializeField, Range(0.05f, 1f)] private float _threshold = 0.45f;
    private HolisticLandmarkerCapture _capture;
    private GameObject _ownedCapture;
    private WebCamTexture _sharedTexture;
    private readonly List<GreetingMotionFrame> _frames = new List<GreetingMotionFrame>();
    private bool _recording;
    private long _firstTimestamp;
    private string _status = "Inicia Play en la escena de prueba y espera a que se conecte la cámara.";
    private string _detection = "Esperando resultados de Holistic…";
    private bool _requirementsVisible;
    private GreetingMotionRequirement _activeRequirement;

    [MenuItem("Window/SENAS/LSC Greeting Holistic Capture")]
    private static void Open()
    {
      var window = GetWindow<GreetingMotionCaptureWindow>("LSC Greeting Capture");
      window._monthsMode = false;
      window._selected = 0;
    }

    [MenuItem("Window/SENAS/LSC Month Holistic Capture")]
    private static void OpenMonths()
    {
      var window = GetWindow<GreetingMotionCaptureWindow>("LSC Month Capture");
      window._monthsMode = true;
      window._selected = 0;
    }

    private void OnEnable()
    {
      if (_templates == null) _templates = AssetDatabase.LoadAssetAtPath<AlphabetLandmarkTemplateSet>(AssetPath);
      EditorApplication.update += EditorUpdate;
      EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
      EditorApplication.update -= EditorUpdate;
      EditorApplication.playModeStateChanged -= OnPlayModeChanged;
      SetCapture(null);
      if (_ownedCapture != null) Destroy(_ownedCapture);
      _ownedCapture = null;
    }

    private void OnGUI()
    {
      EditorGUILayout.HelpBox(_monthsMode
        ? "Captura el movimiento del mes seleccionado. Indica si su referencia LSC requiere una o dos manos y mantén todas las manos requeridas dentro de la cámara. Guarda landmarks, no fotos ni video."
        : "Pulsa Iniciar captura con las manos libres; después coloca la seña y completa el movimiento dentro de la cámara. Guarda landmarks, no fotos ni video.", MessageType.Info);
      _templates = (AlphabetLandmarkTemplateSet)EditorGUILayout.ObjectField("Asset", _templates, typeof(AlphabetLandmarkTemplateSet), false);
      var labels = _monthsMode ? MonthLabels : Labels;
      _selected = Mathf.Clamp(_selected, 0, labels.Length - 1);
      _selected = EditorGUILayout.Popup(_monthsMode ? "Mes" : "Saludo", _selected, labels);
      if (_monthsMode) _monthUsesTwoHands = EditorGUILayout.Toggle("Requiere dos manos", _monthUsesTwoHands);
      var requirement = SelectedRequirement;
      EditorGUILayout.LabelField("Elementos requeridos", Describe(requirement));
      _threshold = EditorGUILayout.Slider("Umbral inicial", _threshold, 0.05f, 1f);
      EditorGUILayout.LabelField("Detección en vivo", _detection);
      EditorGUILayout.LabelField("Estado", _status);
      EditorGUILayout.LabelField("Fotogramas", _frames.Count.ToString());
      using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _capture == null || _templates == null))
      {
        if (!_recording)
        {
          if (GUILayout.Button("Iniciar captura")) BeginCapture();
        }
        else if (GUILayout.Button("Finalizar y guardar")) FinishCapture();
      }
    }

    private void EditorUpdate()
    {
      if (!EditorApplication.isPlaying) { SetCapture(null); return; }
      if (_capture != null && (_sharedTexture == null || !_sharedTexture.isPlaying))
      {
        SetCapture(null);
        if (_ownedCapture != null) Destroy(_ownedCapture);
        _ownedCapture = null;
        _sharedTexture = null;
        _requirementsVisible = false;
        _detection = "Webcam pausada; esperando que se reactive.";
        _status = "La cámara se detuvo. Espera a que reaparezca la vista y se conectará de nuevo.";
      }
      if (_capture == null) ConnectSharedCamera();
      Repaint();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
      if (state != PlayModeStateChange.EnteredPlayMode) { CancelCapture("Captura cancelada al salir de Play Mode."); SetCapture(null); }
    }

    private void ConnectSharedCamera()
    {
      foreach (var found in Resources.FindObjectsOfTypeAll<HolisticLandmarkerCapture>())
      {
        if (found == null || !found.isActiveAndEnabled || !found.gameObject.scene.IsValid()) continue;
        var texture = found.CameraTexture;
        if (texture == null || !texture.isPlaying) continue;
        _sharedTexture = texture;
        SetCapture(found);
        _status = "Holistic conectado a la webcam activa.";
        return;
      }
      foreach (var handCapture in Resources.FindObjectsOfTypeAll<HandLandmarkerCapture>())
      {
        if (handCapture == null || !handCapture.isActiveAndEnabled || !handCapture.gameObject.scene.IsValid()) continue;
        var texture = handCapture.CameraTexture;
        if (texture == null || !texture.isPlaying) continue;
        _sharedTexture = texture;
        _ownedCapture = new GameObject("SENAS Greeting Holistic Capture");
        _ownedCapture.hideFlags = HideFlags.DontSave;
        var capture = _ownedCapture.AddComponent<HolisticLandmarkerCapture>();
        capture.UseSharedCameraTexture(texture);
        SetCapture(capture);
        _status = "Holistic conectado a la webcam activa. Encuadra rostro, torso y ambas manos.";
        return;
      }
    }

    private void SetCapture(HolisticLandmarkerCapture capture)
    {
      if (_capture == capture) return;
      if (_capture != null) _capture.LandmarksUpdated -= OnLandmarks;
      _capture = capture;
      if (_capture != null) _capture.LandmarksUpdated += OnLandmarks;
      if (_recording) CancelCapture("Captura cancelada al cambiar la fuente.");
    }

    private void BeginCapture()
    {
      _activeRequirement = SelectedRequirement;
      _frames.Clear(); _firstTimestamp = 0; _recording = true;
      _status = $"Captura iniciada para {CurrentLabel}. Ahora encuadra los elementos requeridos y haz el movimiento completo.";
    }

    private void OnLandmarks(HolisticLandmarkObservation observation)
    {
      if (observation == null) return;
      var requirement = _recording ? _activeRequirement : SelectedRequirement;
      var handCount = (observation.HasLeftHand ? 1 : 0) + (observation.HasRightHand ? 1 : 0);
      _requirementsVisible = ((requirement & GreetingMotionRequirement.TwoHands) == 0 || handCount >= 2) &&
        ((requirement & GreetingMotionRequirement.OneHand) == 0 || handCount >= 1) &&
        ((requirement & GreetingMotionRequirement.HeadReference) == 0 || observation.HasFace && observation.HasHeadAndBody);
      _detection = $"Manos {handCount}/2 · Cara {(observation.HasFace ? "sí" : "no")} · Pose {observation.PoseLandmarkCount}/33";
      if (!_requirementsVisible)
      {
        _status = DescribeMissing(requirement, handCount, observation);
        return;
      }
      if (!_recording)
      {
        _status = "Seña lista para capturar. Pulsa Iniciar captura y haz el movimiento completo.";
        return;
      }
      if (_firstTimestamp == 0) _firstTimestamp = observation.TimestampMilliseconds;
      var time = Mathf.Clamp01((observation.TimestampMilliseconds - _firstTimestamp) / 4000f);
      _frames.Add(new GreetingMotionFrame(time, GreetingMotionRules.Canonicalize(observation, requirement)));
      if (_frames.Count >= 120) FinishCapture();
    }

    private void FinishCapture()
    {
      _recording = false;
      if (_frames.Count < 6) { _status = "Secuencia muy corta. Captura al menos 6 fotogramas y repite."; _frames.Clear(); return; }
      var id = CurrentId;
      Undo.RecordObject(_templates, $"Capturar seña LSC {CurrentLabel}");
      _templates.AddGreetingMotionTemplate(id, _activeRequirement, _frames, _threshold);
      EditorUtility.SetDirty(_templates);
      AssetDatabase.SaveAssets();
      _status = $"Secuencia de {CurrentLabel} guardada: {_frames.Count} fotogramas. No se guardaron imágenes.";
      _frames.Clear();
    }

    private void CancelCapture(string message) { if (_recording) _status = message; _recording = false; _frames.Clear(); }

    private string CurrentId => _monthsMode ? MonthIds[_selected] : Ids[_selected];
    private string CurrentLabel => _monthsMode ? MonthLabels[_selected] : Labels[_selected];
    private GreetingMotionRequirement SelectedRequirement => _monthsMode
      ? _monthUsesTwoHands ? GreetingMotionRequirement.TwoHands : GreetingMotionRequirement.OneHand
      : GreetingMotionRules.ForId(Ids[_selected]);

    private static string Describe(GreetingMotionRequirement requirement)
    {
      var text = (requirement & GreetingMotionRequirement.TwoHands) != 0 ? "dos manos" : "una mano";
      if ((requirement & GreetingMotionRequirement.HeadReference) != 0) text += ", cabeza visible";
      if ((requirement & GreetingMotionRequirement.HeadTouch) != 0) text += ", una mano debe tocar la cabeza";
      return text;
    }

    private static string DescribeMissing(GreetingMotionRequirement requirement, int handCount, HolisticLandmarkObservation observation)
    {
      if ((requirement & GreetingMotionRequirement.TwoHands) != 0 && handCount < 2) return "No detecto ambas manos. Aléjate un poco y mantenlas dentro de la imagen.";
      if ((requirement & GreetingMotionRequirement.OneHand) != 0 && handCount < 1) return "No detecto la mano. Muéstrala completa frente a la cámara.";
      if ((requirement & GreetingMotionRequirement.HeadReference) != 0 && !observation.HasFace) return "No detecto la cara; centra el rostro y mejora la iluminación.";
      return "No detecto hombros y brazos completos; aléjate un poco de la cámara.";
    }
  }
}
