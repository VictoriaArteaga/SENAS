using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lumina.Alphabet;
using Senas.AI.MediaPipe;
using UnityEngine;

namespace Lumina.Animation.Landmarks
{
    /// <summary>
    /// Prueba de concepto: aplica una plantilla estática de número (solo lectura de NumberLandmarkTemplates.asset)
    /// a la mano 3D mediante HandPoseDriver.SetPose y registra los errores angulares por articulación.
    /// En Play Mode, las teclas 0 a 5 muestran cada número con su plantilla recomendada y V la variante (si existe).
    /// </summary>
    public sealed class NumberPoseTestController : MonoBehaviour
    {
        [SerializeField] private AlphabetLandmarkTemplateSet numberTemplates;
        [SerializeField] private HandPoseDriver driver;
        [SerializeField] private string number = "0";
        [SerializeField, Min(0)] private int templateIndex = 0;
        [SerializeField, Min(1f)] private float toleranceDegrees = 15f;
        [SerializeField] private SeparationMethod separationMethod = SeparationMethod.RigCalibrated;
        [Tooltip("FlexionPlane (por defecto) = flexiones proyectadas al plano de flexión. Legacy = medida original, con banda muerta cerca de 0°; reproduce la línea base antigua del 0. Tecla M para alternar.")]
        [SerializeField] private FlexionMeasure flexionMeasure = FlexionMeasure.FlexionPlane;
        [Tooltip("Resuelve el pulgar articulación por articulación; cada articulación solo se aplica si su error de reconstrucción está dentro de la tolerancia.")]
        [SerializeField] private bool applyThumb = true;
        [Tooltip("Registra también el resultado con el método de separación anterior (RestOffset) para comparar antes y después.")]
        [SerializeField] private bool logPreviousMethod = true;
        [Tooltip("Atajos de teclado 0 a 5 (recomendada) y V (variante) en Play Mode.")]
        [SerializeField] private bool enableKeys = true;
        [Tooltip("Texto de depuración en pantalla (número, plantilla, medida, residuo). Apagado por defecto.")]
        [SerializeField] private bool showOverlay = false;
        [Tooltip("Aplica el número serializado al iniciar. Apágalo cuando otro componente decide cuándo mostrar la mano.")]
        [SerializeField] private bool applyOnStart = true;

        private static readonly string[] FingerNames = { "pulgar", "índice", "medio", "anular", "meñique" };
        private static readonly string[] JointNames = { "separación", "proximal", "media", "distal" };
        private static readonly string[] ThumbJointNames = { "Thumb1", "Thumb2", "Thumb3" };

        // Plantilla recomendada por número (índice 0-based dentro de las plantillas estáticas de ese número).
        // Elegida en el análisis previo por el medoide de las plantillas de cada número; no es una validación LSC.
        private static readonly Dictionary<string, int> RecommendedTemplate = new Dictionary<string, int>
        {
            { "0", 0 }, { "1", 2 }, { "2", 4 }, { "3", 2 }, { "4", 5 }, { "5", 0 },
        };

        // Variante solo para comparar visualmente (tecla V).
        private static readonly Dictionary<string, int> VariantTemplate = new Dictionary<string, int>
        {
            { "5", 3 },
        };

        private string _status = "";

        /// <summary>Medida de flexión usada en la siguiente aplicación.</summary>
        public FlexionMeasure Measure { get => flexionMeasure; set => flexionMeasure = value; }

        /// <summary>Plantilla recomendada (índice 0-based) para un número, o −1 si no hay.</summary>
        public static int GetRecommendedTemplate(string number)
            => RecommendedTemplate.TryGetValue(number ?? "", out var index) ? index : -1;

        private void Start()
        {
            if (applyOnStart) ApplyNumber();
        }

        /// <summary>Texto de estado de la última aplicación (convergencia y pulgar); vacío si falló.</summary>
        public string Status => _status;

        /// <summary>
        /// Muestra un número (0 a 5) con su plantilla recomendada (ver la tabla) y la medida de flexión activa.
        /// Devuelve false, sin tocar la mano, si el número no está en 0–5 o la aplicación falla.
        /// El 0 sigue provisional: Thumb3 pendiente, anomalía 7/8, lateralidad y validación LSC sin cerrar.
        /// </summary>
        public bool ShowNumber(int n)
        {
            if (n < 0 || n > 5)
            {
                Debug.LogWarning($"[NumberPoseTest] ShowNumber({n}): solo hay plantillas para 0 a 5.", this);
                return false;
            }
            string key = n.ToString();
            int index = GetRecommendedTemplate(key);
            Debug.Log($"[NumberPoseTest] ShowNumber({n}): plantilla recomendada T{index + 1} (índice {index}), medida {flexionMeasure}.", this);
            number = key;
            templateIndex = index;
            return Apply();
        }

        [ContextMenu("Aplicar número")]
        public void ApplyNumber() => Apply();

        /// <summary>Muestra el número con la plantilla indicada (0-based) y deja ambos valores como activos.</summary>
        public void ApplyNumber(string number, int templateIndex)
        {
            this.number = number;
            this.templateIndex = Mathf.Max(0, templateIndex);
            Apply();
        }

        private void Update()
        {
            if (!enableKeys) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int n = 0; n <= 5; n++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + n) || Input.GetKeyDown(KeyCode.Keypad0 + n))
                {
                    string key = n.ToString();
                    ApplyNumber(key, GetRecommendedTemplate(key));
                    return;
                }
            }
            if (Input.GetKeyDown(KeyCode.V) && VariantTemplate.TryGetValue(number, out var variant))
                ApplyNumber(number, variant);
            if (Input.GetKeyDown(KeyCode.M))
            {
                flexionMeasure = flexionMeasure == FlexionMeasure.Legacy ? FlexionMeasure.FlexionPlane : FlexionMeasure.Legacy;
                Apply();
            }
#endif
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            // El texto se escala con la altura de pantalla para que se lea también en resoluciones altas.
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(1f, Screen.height / 720f));
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            GUI.Label(new Rect(12, 8, 900, 28), $"Número {number} · plantilla T{templateIndex + 1} (índice {templateIndex})", style);
            GUI.Label(new Rect(12, 36, 900, 22), "Referencia visual, pendiente de validación LSC", small);
            GUI.Label(new Rect(12, 58, 900, 22), $"Teclas: 0-5 número (plantilla recomendada) · V variante · M medida de flexión ({flexionMeasure})", small);
            if (!string.IsNullOrEmpty(_status)) GUI.Label(new Rect(12, 80, 900, 22), _status, small);
        }

        private bool Apply()
        {
            _status = "";
            if (numberTemplates == null || driver == null)
            {
                Debug.LogError("[NumberPoseTest] Falta asignar la plantilla de números o el HandPoseDriver.", this);
                return false;
            }

            var candidates = numberTemplates.Templates
                .Where(t => t.Letter == number && t.NormalizedLandmarks.Count == LandmarkToHandPoseConverter.LandmarkCount)
                .ToList();
            if (templateIndex >= candidates.Count)
            {
                _status = $"El número {number} no tiene la plantilla {templateIndex}.";
                Debug.LogError($"[NumberPoseTest] El número {number} no tiene la plantilla {templateIndex}.", this);
                return false;
            }
            var template = candidates[templateIndex];
            var landmarks = template.NormalizedLandmarks;

            Debug.Log($"[NumberPoseTest] Número {number}: {candidates.Count} plantillas estáticas; se usa T{templateIndex + 1} (índice {templateIndex}). " +
                      $"_handedness = {template.Handedness} (la plantilla no indica lateralidad: que sea mano derecha es una suposición).", this);
            if (number == "0")
                Debug.Log("[NumberPoseTest] Aviso: el 0 es provisional (Thumb3 pendiente, anomalía 7/8 del índice, lateralidad y validación LSC sin cerrar).", this);

            if (!RigRest.TryCapture(driver.transform, driver, out var rest, flexionMeasure))
            {
                Debug.LogError("[NumberPoseTest] No se pudo capturar el reposo del rig (huesos J_Right_Hand… no encontrados).", this);
                return false;
            }

            if (logPreviousMethod)
                RunPass("ANTES (separación respecto al reposo, sin pulgar)", landmarks, rest, SeparationMethod.RestOffset, false, false);

            return RunPass($"DESPUÉS ({separationMethod}{(applyThumb ? ", pulgar por articulación" : ", sin pulgar")})",
                landmarks, rest, separationMethod, applyThumb, true);
        }

        private bool RunPass(string label, IReadOnlyList<Vector3> landmarks, RigRest rest, SeparationMethod method, bool solveThumb, bool final)
        {
            if (!LandmarkToHandPoseConverter.TryConvert(landmarks, rest, driver, method, solveThumb, toleranceDegrees, out var result))
            {
                if (final) _status = "La conversión produjo valores no finitos o datos inválidos.";
                Debug.LogError($"[NumberPoseTest] {label}: la conversión produjo valores no finitos o datos inválidos.", this);
                return false;
            }

            driver.SetPose(result.Pose);
            var applied = rest.MeasureCurrent();
            var errors = LandmarkToHandPoseConverter.Errors(result.Target, applied);

            var sb = new StringBuilder();
            sb.AppendLine($"[NumberPoseTest] {label} · número {number}, plantilla {templateIndex + 1}. Medida de flexión: {rest.Mode}. Mano derecha provisional, muñeca y desplazamiento en cero.");
            if (method == SeparationMethod.RigCalibrated)
                sb.AppendLine($"  Calibración contra el rig: {result.CalibrationIterations} iteraciones, error máximo {result.CalibrationResidual:F2}° → " +
                              (result.CalibrationConverged ? "CONVERGE (< 0,25°)" : "NO CONVERGE (se conserva la mejor iteración medida)") +
                              ". La convergencia no demuestra fidelidad.");
            if (method == SeparationMethod.RigCalibrated && !result.CalibrationConverged)
                sb.AppendLine($"  Objetivo no alcanzable con la medida {rest.Mode}: se aplica la mejor pose medida; falta como máximo {MaxError(errors):F1}° ({WorstJoint(errors)}). " +
                              (rest.Mode == FlexionMeasure.Legacy ? "En el diagnóstico el rig sí alcanza ese rango; la limitación es la banda muerta de la medida Legacy (prueba la tecla M)." : ""));
            AppendPose(sb, result.Pose);
            if (method == SeparationMethod.RigCalibrated) AppendCalibrationShift(sb, result);
            AppendErrors(sb, errors, solveThumb);
            if (solveThumb) AppendThumb(sb, result, rest);
            if (final) AppendGeometry(sb, landmarks, rest);
            if (final && method == SeparationMethod.RigCalibrated && solveThumb && number == "0" && templateIndex == 0)
                AppendBaseline(sb, result, rest.Mode);

            if (!AllFinite(errors)) sb.AppendLine("  ATENCIÓN: hay valores no finitos en los errores.");
            Debug.Log(sb.ToString(), this);

            if (final)
                _status = $"Calibración: {result.CalibrationIterations} it., residuo {result.CalibrationResidual:F2}° " +
                          $"({(result.CalibrationConverged ? "converge" : "no converge")}) · {ThumbSummary(result)}" +
                          (number == "0" ? " · 0 provisional (Thumb3 pendiente)" : "");
            return true;
        }

        // Líneas base del 0 (T1), separación / proximal / media / distal de índice, medio, anular y meñique.
        // Legacy: valores de la línea base antigua (6 iteraciones, 0,15°). FlexionPlane: nueva línea base, tomada al
        // activar FlexionPlane por defecto (5 iteraciones, 0,23°). En ambas, Thumb1 y Thumb2 se aplican y Thumb3 no.
        private static readonly float[,] BaselineLegacy =
        {
            { 15.3f, 41.3f, 32.9f, 41.4f }, { 0.0f, 40.1f, 52.5f, 38.2f }, { -22.6f, 47.3f, 39.9f, 40.9f }, { -28.1f, 36.3f, 21.5f, 56.6f },
        };

        private static readonly float[,] BaselinePlane =
        {
            { 12.8f, 45.8f, 31.2f, 43.7f }, { 0.0f, 40.5f, 51.6f, 35.2f }, { -22.0f, 46.2f, 38.8f, 39.8f }, { -27.2f, 33.9f, 20.1f, 58.0f },
        };

        private static void AppendBaseline(StringBuilder sb, ConversionResult result, FlexionMeasure mode)
        {
            var expected = mode == FlexionMeasure.Legacy ? BaselineLegacy : BaselinePlane;
            var fingers = new[] { result.Pose.index, result.Pose.middle, result.Pose.ring, result.Pose.pinky };
            int expectedIterations = mode == FlexionMeasure.Legacy ? 6 : 5;
            bool ok = result.CalibrationConverged && result.CalibrationIterations == expectedIterations;
            float worst = 0f;
            for (int f = 0; f < 4; f++)
            {
                var p = fingers[f];
                var actual = new[] { p.spread, p.proximal, p.middle, p.distal };
                for (int j = 0; j < 4; j++) worst = Mathf.Max(worst, Mathf.Abs(actual[j] - expected[f, j]));
            }
            ok &= worst <= 0.06f;
            bool thumbOk = result.Thumb.Length == 3 && result.Thumb[0].Stable && result.Thumb[1].Stable && !result.Thumb[2].Stable;
            sb.AppendLine($"  Regresión del 0 (T1), línea base {(mode == FlexionMeasure.Legacy ? "Legacy (antigua)" : "FlexionPlane (nueva)")}: " +
                          $"{expectedIterations} iteraciones, desviación máxima {worst:F2}° frente a los valores registrados, " +
                          $"pulgar Thumb1/Thumb2 aplicados y Thumb3 no → {(ok && thumbOk ? "COINCIDE" : "DIFIERE")}.");
        }

        private static float MaxError(float[,] errors)
        {
            float worst = 0f;
            for (int f = 1; f < 5; f++)
                for (int j = 0; j < 4; j++) worst = Mathf.Max(worst, Mathf.Abs(errors[f, j]));
            return worst;
        }

        private static string WorstJoint(float[,] errors)
        {
            float worst = -1f; string label = "";
            for (int f = 1; f < 5; f++)
                for (int j = 0; j < 4; j++)
                    if (Mathf.Abs(errors[f, j]) > worst) { worst = Mathf.Abs(errors[f, j]); label = $"{FingerNames[f]} {JointNames[j]}"; }
            return label;
        }

        private static bool AllFinite(float[,] values)
        {
            foreach (var v in values) if (!float.IsFinite(v)) return false;
            return true;
        }

        private static string ThumbSummary(ConversionResult result)
        {
            if (result.Thumb == null || result.Thumb.Length == 0) return "pulgar no resuelto";
            return "pulgar " + string.Join(", ", result.Thumb.Select((t, j) => $"{ThumbJointNames[j]} {(t.Stable ? "aplicada" : "pendiente")}"));
        }

        private void AppendPose(StringBuilder sb, HandPose pose)
        {
            var fingers = new[] { pose.thumb, pose.index, pose.middle, pose.ring, pose.pinky };
            for (int f = 0; f < 5; f++)
            {
                var p = fingers[f];
                sb.AppendLine($"  {FingerNames[f]}: separación {p.spread:F1}°, proximal {p.proximal:F1}°, media {p.middle:F1}°, distal {p.distal:F1}°"
                    + (f == 0 ? $", spreadMiddle {p.spreadMiddle:F1}°" : ""));
            }
        }

        // Valor geométrico original del landmark (relativo al reposo) → valor tras calibrar, y cuánto movió la calibración.
        private void AppendCalibrationShift(StringBuilder sb, ConversionResult result)
        {
            if (result.PreCalibration == null) return;
            var after = new[] { result.Pose.thumb, result.Pose.index, result.Pose.middle, result.Pose.ring, result.Pose.pinky };
            sb.AppendLine("  Calibración por articulación (antes → después, Δ = lo que movió la calibración):");
            for (int f = 1; f < 5; f++)
            {
                var b = result.PreCalibration[f];
                var a = after[f];
                sb.AppendLine($"    {FingerNames[f]}: " +
                              $"separación {b.spread:F1}→{a.spread:F1} (Δ {a.spread - b.spread:+0.0;-0.0}); " +
                              $"proximal {b.proximal:F1}→{a.proximal:F1} (Δ {a.proximal - b.proximal:+0.0;-0.0}); " +
                              $"media {b.middle:F1}→{a.middle:F1} (Δ {a.middle - b.middle:+0.0;-0.0}); " +
                              $"distal {b.distal:F1}→{a.distal:F1} (Δ {a.distal - b.distal:+0.0;-0.0})");
            }
        }

        private void AppendErrors(StringBuilder sb, float[,] errors, bool includeThumb)
        {
            sb.AppendLine($"  Errores de los dedos (aplicado − landmark), umbral {toleranceDegrees:F0}°:");
            for (int f = includeThumb ? 0 : 1; f < 5; f++)
            {
                if (f == 0) continue; // el pulgar se evalúa por dirección de segmento, no por estos ángulos
                var line = $"    {FingerNames[f]}:";
                for (int j = 0; j < 4; j++)
                {
                    float e = errors[f, j];
                    string flag = Mathf.Abs(e) > toleranceDegrees ? " [FUERA DE UMBRAL]" : "";
                    line += $" {JointNames[j]} {e:+0.0;-0.0}°{flag};";
                }
                sb.AppendLine(line);
            }
        }

        private void AppendThumb(StringBuilder sb, ConversionResult result, RigRest rest)
        {
            var applied = rest.MeasureThumbDirections();
            var thumbErrors = LandmarkToHandPoseConverter.ThumbErrors(result.ThumbTargetWorld, applied);
            sb.AppendLine("  Pulgar (ejes del driver: Thumb1 = Rx·Ry·Rz, Thumb2 = Rx·Rz, Thumb3 = Rz; twist no determinable = 0):");
            for (int j = 0; j < result.Thumb.Length; j++)
            {
                var t = result.Thumb[j];
                sb.AppendLine($"    {ThumbJointNames[j]}: apertura {t.Spread:F1}°, flexión {t.Curl:F1}°, " +
                              $"error de reconstrucción {t.ReconstructionError:F1}°, " +
                              $"error de dirección aplicada {thumbErrors[j]:F1}°, " +
                              (t.Stable ? "APLICADA" : "NO APLICADA (inestable)"));
            }
        }

        private static void AppendGeometry(StringBuilder sb, IReadOnlyList<Vector3> landmarks, RigRest rest)
        {
            if (GeometricFidelityReport.TryCompute(landmarks, rest, out var report))
                sb.Append(GeometricFidelityReport.Format(report));
            else
                sb.AppendLine("  Fidelidad geométrica: no se pudo calcular (datos inválidos o valores no finitos).");
        }
    }
}
