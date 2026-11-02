using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lumina.Animation.Landmarks
{
    /// <summary>
    /// Fidelidad geométrica, independiente de la calibración: compara distancias de la plantilla de landmarks con las
    /// medidas en los huesos del rig tras aplicar la pose. Todas se normalizan con la longitud muñeca → MCP medio de
    /// cada lado (landmarks 0→9; J_Right_Hand → Middle1), porque el rig y la plantilla tienen proporciones distintas
    /// y no se espera igualdad exacta. No es un aprobado/suspenso: cada medida recibe un nivel descriptivo.
    /// </summary>
    public static class GeometricFidelityReport
    {
        // Umbrales fijados antes de medir; no se ajustan después de ver los resultados.
        public const float MatchLimit = 0.15f;
        public const float ModerateLimit = 0.30f;

        public enum Level { Coincide, Moderada, Notable }

        public readonly struct Measure
        {
            public readonly string Label;
            public readonly float Template;
            public readonly float Rig;
            public float Delta => Rig - Template;
            public Level Level => LevelOf(Delta);

            public Measure(string label, float template, float rig)
            {
                Label = label;
                Template = template;
                Rig = rig;
            }
        }

        public sealed class Report
        {
            public Measure[] Measures;
            /// <summary>
            /// Métrica POSTERIOR, añadida tras ver los resultados de la original: distancia punta-muñeca dividida por la longitud
            /// total del dedo (suma de sus tres segmentos), en plantilla y en rig. No tiene umbrales ni sustituye a <see cref="Measures"/>.
            /// </summary>
            public Measure[] Posterior;
            /// <summary>
            /// Métrica POSTERIOR nº 2, añadida tras ver los resultados de las anteriores, sin umbrales: |punta − MCP| dividida por la
            /// longitud total del dedo (suma de sus tres segmentos). Vale ≈ 1 con el dedo recto y no depende de la razón metacarpo/dedo.
            /// </summary>
            public Measure[] Posterior2;
            public float TemplateScale;
            public float RigScale;
            /// <summary>Medidas de la punta del índice, para el efecto de la anomalía de los landmarks 7/8 (solo informativo).</summary>
            public IndexTipEffect IndexTip;

            public int Count(Level level)
            {
                int n = 0;
                foreach (var m in Measures) if (m.Level == level) n++;
                return n;
            }

            public bool AllFinite()
            {
                foreach (var m in Measures)
                    if (!float.IsFinite(m.Template) || !float.IsFinite(m.Rig)) return false;
                foreach (var m in Posterior)
                    if (!float.IsFinite(m.Template) || !float.IsFinite(m.Rig)) return false;
                foreach (var m in Posterior2)
                    if (!float.IsFinite(m.Template) || !float.IsFinite(m.Rig)) return false;
                return IndexTip.IsFinite;
            }
        }

        /// <summary>
        /// Efecto de la anomalía de los landmarks 7/8 en la punta del índice. No corrige nada: calcula, solo como
        /// contrafactual informativo, dónde quedaría la punta de la plantilla si el segmento distal (7→8) siguiera
        /// la dirección del segmento 6→7, y cuánto cambiaría con ello la distancia punta-muñeca del índice.
        /// </summary>
        public readonly struct IndexTipEffect
        {
            /// <summary>Flexión media y distal del índice en la plantilla, en grados.</summary>
            public readonly float MiddleAngle, DistalAngle;
            public readonly float TipWristTemplate, TipWristTemplateIfStraight, TipWristRig;
            /// <summary>Cuánto se desplaza la punta del contrafactual respecto a la real, normalizado, y su ángulo.</summary>
            public readonly float TipShift, DistalSegmentDeviation;

            public IndexTipEffect(float middle, float distal, float tipTemplate, float tipStraight, float tipRig, float shift, float deviation)
            {
                MiddleAngle = middle; DistalAngle = distal;
                TipWristTemplate = tipTemplate; TipWristTemplateIfStraight = tipStraight; TipWristRig = tipRig;
                TipShift = shift; DistalSegmentDeviation = deviation;
            }

            public bool IsFinite => float.IsFinite(MiddleAngle) && float.IsFinite(DistalAngle) && float.IsFinite(TipWristTemplate)
                && float.IsFinite(TipWristTemplateIfStraight) && float.IsFinite(TipWristRig) && float.IsFinite(TipShift)
                && float.IsFinite(DistalSegmentDeviation);
        }

        private static readonly string[] FingerNames = { "pulgar", "índice", "medio", "anular", "meñique" };
        private static readonly int[] TipLandmarks = { 4, 8, 12, 16, 20 };
        private static readonly int[] ChainStart = { 1, 5, 9, 13, 17 };

        public static Level LevelOf(float delta)
        {
            float a = Mathf.Abs(delta);
            if (a <= MatchLimit) return Level.Coincide;
            return a <= ModerateLimit ? Level.Moderada : Level.Notable;
        }

        /// <summary>Mide la plantilla y el rig. El rig debe tener ya aplicada la pose final.</summary>
        public static bool TryCompute(IReadOnlyList<Vector3> landmarks, RigRest rest, out Report report)
        {
            report = null;
            if (landmarks == null || landmarks.Count != LandmarkToHandPoseConverter.LandmarkCount || rest == null) return false;

            float templateScale = Vector3.Distance(landmarks[0], landmarks[9]);
            float rigScale = Vector3.Distance(rest.Wrist.position, rest.Chain[2, 0].position);
            if (templateScale < 1e-6f || rigScale < 1e-6f) return false;

            var measures = new List<Measure>();
            var wristRig = rest.Wrist.position;
            for (int f = 0; f < RigRest.FingerCount; f++)
            {
                measures.Add(new Measure($"punta-muñeca {FingerNames[f]}",
                    Vector3.Distance(landmarks[0], landmarks[TipLandmarks[f]]) / templateScale,
                    Vector3.Distance(wristRig, rest.Chain[f, 3].position) / rigScale));
            }
            for (int f = 1; f < RigRest.FingerCount; f++)
            {
                measures.Add(new Measure($"pulgar-punta {FingerNames[f]}",
                    Vector3.Distance(landmarks[4], landmarks[TipLandmarks[f]]) / templateScale,
                    Vector3.Distance(rest.Chain[0, 3].position, rest.Chain[f, 3].position) / rigScale));
            }

            var posterior = new List<Measure>();
            var posterior2 = new List<Measure>();
            for (int f = 0; f < RigRest.FingerCount; f++)
            {
                int a = ChainStart[f];
                float templateLength = Vector3.Distance(landmarks[a], landmarks[a + 1]) + Vector3.Distance(landmarks[a + 1], landmarks[a + 2]) + Vector3.Distance(landmarks[a + 2], landmarks[a + 3]);
                float rigLength = Vector3.Distance(rest.Chain[f, 0].position, rest.Chain[f, 1].position) + Vector3.Distance(rest.Chain[f, 1].position, rest.Chain[f, 2].position) + Vector3.Distance(rest.Chain[f, 2].position, rest.Chain[f, 3].position);
                if (templateLength < 1e-6f || rigLength < 1e-6f) return false;
                posterior.Add(new Measure($"punta-muñeca / longitud del dedo {FingerNames[f]}",
                    Vector3.Distance(landmarks[0], landmarks[TipLandmarks[f]]) / templateLength,
                    Vector3.Distance(wristRig, rest.Chain[f, 3].position) / rigLength));
                posterior2.Add(new Measure($"|punta − MCP| / longitud del dedo {FingerNames[f]}",
                    Vector3.Distance(landmarks[a], landmarks[TipLandmarks[f]]) / templateLength,
                    Vector3.Distance(rest.Chain[f, 0].position, rest.Chain[f, 3].position) / rigLength));
            }

            report = new Report
            {
                Measures = measures.ToArray(),
                Posterior = posterior.ToArray(),
                Posterior2 = posterior2.ToArray(),
                TemplateScale = templateScale,
                RigScale = rigScale,
                IndexTip = MeasureIndexTip(landmarks, rest, templateScale, rigScale),
            };
            return report.AllFinite();
        }

        private static IndexTipEffect MeasureIndexTip(IReadOnlyList<Vector3> lm, RigRest rest, float templateScale, float rigScale)
        {
            if (!LandmarkHandFrame.TryCreate(lm[0], lm[9], lm[5], lm[17], out var frame))
                return new IndexTipEffect(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            var d1 = lm[6] - lm[5];
            var d2 = lm[7] - lm[6];
            var d3 = lm[8] - lm[7];
            float middle = Vector3.SignedAngle(d1, d2, frame.Ulnar);
            float distal = Vector3.SignedAngle(d2, d3, frame.Ulnar);

            // Contrafactual: mismo largo del segmento 7→8, con la dirección del segmento 6→7.
            var straightTip = lm[7] + d2.normalized * d3.magnitude;
            float tipTemplate = Vector3.Distance(lm[0], lm[8]) / templateScale;
            float tipStraight = Vector3.Distance(lm[0], straightTip) / templateScale;
            float tipRig = Vector3.Distance(rest.Wrist.position, rest.Chain[1, 3].position) / rigScale;
            float shift = Vector3.Distance(lm[8], straightTip) / templateScale;
            return new IndexTipEffect(middle, distal, tipTemplate, tipStraight, tipRig, shift, Vector3.Angle(d2, d3));
        }

        public static string Format(Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  Fidelidad geométrica (distancias normalizadas por muñeca→MCP medio; plantilla {report.TemplateScale:F3}, rig {report.RigScale:F4} m). " +
                          $"Umbrales fijos: |Δ| ≤ {MatchLimit:F2} coincide, ≤ {ModerateLimit:F2} moderada, > {ModerateLimit:F2} notable. " +
                          "No es independiente de las proporciones del rig: no se espera igualdad exacta.");
            foreach (var m in report.Measures)
                sb.AppendLine($"    {m.Label}: plantilla {m.Template:F2} · rig {m.Rig:F2} · Δ {m.Delta:+0.00;-0.00} → {m.Level}");
            sb.AppendLine($"    Resumen: {report.Count(Level.Coincide)} coinciden, {report.Count(Level.Moderada)} moderadas, {report.Count(Level.Notable)} notables de {report.Measures.Length}.");

            sb.AppendLine("  Métrica POSTERIOR (añadida tras ver los resultados, sin umbrales; no sustituye a la anterior): distancia punta-muñeca / longitud del dedo (3 segmentos).");
            foreach (var m in report.Posterior)
                sb.AppendLine($"    {m.Label}: plantilla {m.Template:F2} · rig {m.Rig:F2} · Δ {m.Delta:+0.00;-0.00}");

            sb.AppendLine("  Métrica POSTERIOR nº 2 (añadida tras ver los resultados, sin umbrales): |punta − MCP| / longitud del dedo (3 segmentos); independiente de la razón metacarpo/dedo.");
            foreach (var m in report.Posterior2)
                sb.AppendLine($"    {m.Label}: plantilla {m.Template:F2} · rig {m.Rig:F2} · Δ {m.Delta:+0.00;-0.00}");

            var t = report.IndexTip;
            sb.AppendLine("  Punta del índice y anomalía de los landmarks 7/8 (informativo, no se corrige): " +
                          $"flexión media {t.MiddleAngle:F1}°, distal {t.DistalAngle:F1}° (desviación 7→8 respecto a 6→7 {t.DistalSegmentDeviation:F1}°). " +
                          $"Punta-muñeca plantilla {t.TipWristTemplate:F2}; si 7→8 siguiera a 6→7 sería {t.TipWristTemplateIfStraight:F2} " +
                          $"(la punta se movería {t.TipShift:F2}); rig {t.TipWristRig:F2}.");
            return sb.ToString();
        }
    }
}
