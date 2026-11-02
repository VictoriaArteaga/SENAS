using System;
using System.Collections.Generic;
using Lumina.Alphabet;
using UnityEngine;

namespace Lumina.Animation.Landmarks
{
    /// <summary>Un fotograma de un clip: tiempo normalizado (0 a 1) y la pose de la mano.</summary>
    [Serializable]
    public struct NumberSignClipFrame
    {
        [Range(0f, 1f)] public float time;
        public HandPose pose;
    }

    /// <summary>
    /// Secuencia de poses de un número (6 en adelante), horneada fuera de línea desde una secuencia de NumberLandmarkTemplates.asset
    /// con <c>NumberSignClipBaker</c>. No guarda landmarks, solo la pose ya convertida y suavizada. La muñeca (wristEuler, wristOffset)
    /// queda en cero: la rotación y traslación globales del dato no se aplican. La duración es PROVISIONAL: el asset de origen
    /// solo tiene tiempo normalizado, no fps ni segundos reales.
    /// </summary>
    [CreateAssetMenu(menuName = "SENAS/Animación/Clip de número", fileName = "Number_00")]
    public sealed class NumberSignClip : ScriptableObject
    {
        [Header("Origen")]
        [SerializeField] private int number;
        [Tooltip("Índice de la secuencia dentro de NumberLandmarkTemplates.asset (_motionTemplates).")]
        [SerializeField] private int sourceSequenceId;
        [SerializeField] private int sourceFrameCount;
        [Tooltip("La secuencia llegó al tope de 90 fotogramas de la captura: puede estar truncada.")]
        [SerializeField] private bool sourceMayBeTruncated;

        [Header("Reproducción")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 2f;
        [Tooltip("Verdadero mientras la duración no venga de una medición real (hoy: fotogramas medianos del número / 30).")]
        [SerializeField] private bool durationProvisional = true;
        [SerializeField] private List<NumberSignClipFrame> frames = new List<NumberSignClipFrame>();

        [Header("Suavizado de los ángulos de salida")]
        [SerializeField] private int medianWindow = 3;
        [SerializeField] private int averageWindow = 5;

        [Header("Convergencia (definición nueva: flexiones ≤ 0,25°)")]
        [SerializeField] private int convergedFrames;
        [SerializeField] private int spreadUndeterminableFrames;
        [SerializeField] private int failedFrames;
        [Tooltip("Mediana y p95 del error de posición de la punta (fidelidad geométrica; no decide la convergencia).")]
        [SerializeField] private float tipErrorMedian;
        [SerializeField] private float tipErrorP95;

        [Header("Pulgar (Thumb3: se mantiene el último valor aplicado 15 fotogramas y vuelve al reposo en 5)")]
        [SerializeField] private float thumb3AffectedPercent;

        [Header("Muñeca (medida, NO aplicada)")]
        [Tooltip("Giro del marco de la mano respecto al primer fotograma, en grados: mediana y máximo. Solo informativo.")]
        [SerializeField] private float wristRotationMedianDegrees;
        [SerializeField] private float wristRotationMaxDegrees;

        [Header("Validación")]
        [Tooltip("Verdadero solo cuando un hablante o intérprete de LSC haya validado la seña. Por defecto falso: ningún clip está validado.")]
        [SerializeField] private bool lscValidated;

        [TextArea(2, 6)] [SerializeField] private string notes;

        public int Number => number;
        public int SourceSequenceId => sourceSequenceId;
        public int SourceFrameCount => sourceFrameCount;
        public bool SourceMayBeTruncated => sourceMayBeTruncated;
        public float DurationSeconds => durationSeconds;
        public bool DurationProvisional => durationProvisional;
        public IReadOnlyList<NumberSignClipFrame> Frames => frames;
        public int ConvergedFrames => convergedFrames;
        public int SpreadUndeterminableFrames => spreadUndeterminableFrames;
        public int FailedFrames => failedFrames;
        public float Thumb3AffectedPercent => thumb3AffectedPercent;
        public float WristRotationMedianDegrees => wristRotationMedianDegrees;
        public string Notes => notes;

        /// <summary>Validado por un hablante o intérprete de LSC. Falso por defecto.</summary>
        public bool LscValidated => lscValidated;

        /// <summary>Un clip es reproducible si tiene al menos dos fotogramas y una duración positiva.</summary>
        public bool IsPlayable => frames != null && frames.Count >= 2 && durationSeconds > 0f;

        /// <summary>Pose en el tiempo normalizado t (0 a 1), interpolando linealmente entre fotogramas. Fuera del rango se mantiene la primera o la última.</summary>
        public HandPose Evaluate(float t)
        {
            if (frames == null || frames.Count == 0) return new HandPose();
            if (frames.Count == 1 || t <= frames[0].time) return frames[0].pose;
            var last = frames[frames.Count - 1];
            if (t >= last.time) return last.pose;

            int lo = 0, hi = frames.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (frames[mid].time <= t) lo = mid; else hi = mid;
            }
            float span = Mathf.Max(1e-6f, frames[hi].time - frames[lo].time);
            return HandPose.Lerp(frames[lo].pose, frames[hi].pose, (t - frames[lo].time) / span);
        }

        /// <summary>Solo para el baker de Editor: rellena el clip. No se usa en ejecución.</summary>
        public void Configure(int number, int sourceSequenceId, int sourceFrameCount, bool mayBeTruncated, float durationSeconds, bool provisional,
            List<NumberSignClipFrame> frames, int medianWindow, int averageWindow, int converged, int undeterminable, int failed,
            float tipMedian, float tipP95, float thumb3AffectedPercent, float wristMedian, float wristMax, string notes, bool lscValidated = false)
        {
            this.number = number;
            this.sourceSequenceId = sourceSequenceId;
            this.sourceFrameCount = sourceFrameCount;
            sourceMayBeTruncated = mayBeTruncated;
            this.durationSeconds = durationSeconds;
            durationProvisional = provisional;
            this.frames = frames;
            this.medianWindow = medianWindow;
            this.averageWindow = averageWindow;
            convergedFrames = converged;
            spreadUndeterminableFrames = undeterminable;
            failedFrames = failed;
            tipErrorMedian = tipMedian;
            tipErrorP95 = tipP95;
            this.thumb3AffectedPercent = thumb3AffectedPercent;
            wristRotationMedianDegrees = wristMedian;
            wristRotationMaxDegrees = wristMax;
            this.notes = notes;
            this.lscValidated = lscValidated;
        }
    }
}
