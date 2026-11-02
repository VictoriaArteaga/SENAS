using System.Collections.Generic;
using Lumina.Alphabet;
using UnityEngine;

namespace Lumina.Animation.Landmarks
{
    /// <summary>
    /// Medidas del rig en reposo, tomadas de los huesos reales con <see cref="HandPoseDriver.SetPose"/> en pose cero.
    /// Se usan como referencia: cada valor de <see cref="HandPose"/> es el ángulo del landmark menos el del reposo.
    /// </summary>
    public sealed class RigRest
    {
        public const int FingerCount = 5;
        public const int BonesPerFinger = 4;
        public const int ThumbJointCount = 3;

        // Orden de dedos igual que en LandmarkToHandPoseConverter: pulgar, índice, medio, anular, meñique.
        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        public LandmarkHandFrame Frame;
        public Transform Wrist;
        public Transform[,] Chain;
        public FingerAngles[] Fingers;
        /// <summary>Cómo se miden las flexiones; el mismo modo se usa para los landmarks (ver <see cref="FlexionMeasure"/>).</summary>
        public FlexionMeasure Mode;

        /// <summary>Rotación mundial de J_Right_Hand en reposo (padre de Thumb1).</summary>
        public Quaternion HandRotation;
        /// <summary>Rotación local de reposo de Thumb1..Thumb3, la misma que el driver multiplica por la pose.</summary>
        public Quaternion[] ThumbRestLocal;
        /// <summary>Dirección del hueso hijo (Thumb2..Thumb4) en el marco local de cada articulación del pulgar.</summary>
        public Vector3[] ThumbChildDirection;

        public Quaternion FrameRotation => Frame.ToRotation();

        public static bool TryCapture(Transform handRoot, HandPoseDriver driver, out RigRest rest, FlexionMeasure mode = FlexionMeasure.FlexionPlane)
        {
            rest = null;
            if (handRoot == null || driver == null) return false;

            driver.SetPose(new HandPose());
            if (!driver.IsReady) return false;

            var byName = new Dictionary<string, Transform>();
            foreach (var t in handRoot.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);

            var r = new RigRest { Wrist = FindBone(byName, "J_Right_Hand"), Chain = new Transform[FingerCount, BonesPerFinger], Mode = mode };
            for (int f = 0; f < FingerCount; f++)
                for (int j = 0; j < BonesPerFinger; j++)
                    r.Chain[f, j] = FindBone(byName, "J_Right_Hand" + FingerNames[f] + (j + 1));

            if (r.Wrist == null) return false;
            for (int f = 0; f < FingerCount; f++)
                for (int j = 0; j < BonesPerFinger; j++)
                    if (r.Chain[f, j] == null) return false;

            // Marco del rig: muñeca, MCP medio, MCP índice, MCP meñique (mismo criterio que los landmarks 0, 9, 5, 17).
            if (!LandmarkHandFrame.TryCreate(r.Wrist.position, r.Chain[2, 0].position, r.Chain[1, 0].position, r.Chain[4, 0].position, out r.Frame))
                return false;

            r.HandRotation = r.Wrist.rotation;
            r.ThumbRestLocal = new Quaternion[ThumbJointCount];
            r.ThumbChildDirection = new Vector3[ThumbJointCount];
            for (int j = 0; j < ThumbJointCount; j++)
            {
                r.ThumbRestLocal[j] = r.Chain[0, j].localRotation;
                r.ThumbChildDirection[j] = r.Chain[0, j + 1].localPosition.normalized;
            }

            r.Fingers = r.MeasureCurrent();
            rest = r;
            return true;
        }

        /// <summary>Mide los cinco dedos con las posiciones actuales de los huesos, respecto al marco del reposo.</summary>
        public FingerAngles[] MeasureCurrent()
        {
            var middleSegment = Chain[2, 1].position - Chain[2, 0].position;
            var result = new FingerAngles[FingerCount];
            for (int f = 0; f < FingerCount; f++)
            {
                result[f] = FingerAngles.Measure(
                    Wrist.position,
                    Chain[f, 0].position, Chain[f, 1].position, Chain[f, 2].position, Chain[f, 3].position,
                    middleSegment, Frame, Mode);
            }
            return result;
        }

        /// <summary>Dirección mundial actual de los tres segmentos del pulgar (Thumb1→2, 2→3, 3→4).</summary>
        public Vector3[] MeasureThumbDirections()
        {
            var d = new Vector3[ThumbJointCount];
            for (int j = 0; j < ThumbJointCount; j++)
                d[j] = (Chain[0, j + 1].position - Chain[0, j].position).normalized;
            return d;
        }

        private static Transform FindBone(Dictionary<string, Transform> byName, string name)
            => byName.TryGetValue(name, out var t) ? t : null;
    }

    /// <summary>
    /// Cómo se calcula la separación (spread) de los cuatro dedos largos.
    /// </summary>
    public enum SeparationMethod
    {
        /// <summary>Ángulo del landmark menos el ángulo del rig en reposo (flexión cero). Sesgado por la flexión.</summary>
        RestOffset,

        /// <summary>
        /// Calibración contra el propio rig: se aplica la pose, se mide el ángulo real de cada dedo y se corrige
        /// cada valor con el error medido, hasta converger. Absorbe el sesgo de la flexión y el acoplamiento
        /// entre separación y flexión (la separación gira el plano de flexión en HandPoseDriver).
        /// Un único paso con la separación a cero (referencia a flexión igual) se probó y empeoró anular y meñique.
        /// </summary>
        RigCalibrated,
    }

    /// <summary>Resultado de resolver una articulación del pulgar con los ejes reales del driver.</summary>
    public readonly struct ThumbJointResult
    {
        /// <summary>Giro alrededor de X local (apertura), grados. En la última articulación no existe y vale 0.</summary>
        public readonly float Spread;
        /// <summary>Giro alrededor de Z local (flexión), grados.</summary>
        public readonly float Curl;
        /// <summary>Ángulo entre la dirección reconstruida con (Spread, Curl) y la dirección objetivo, en grados.</summary>
        public readonly float ReconstructionError;
        /// <summary>Verdadero si el error de reconstrucción está dentro de la tolerancia y la articulación se aplica.</summary>
        public readonly bool Stable;

        public ThumbJointResult(float spread, float curl, float reconstructionError, bool stable)
        {
            Spread = spread;
            Curl = curl;
            ReconstructionError = reconstructionError;
            Stable = stable;
        }
    }

    public sealed class ConversionResult
    {
        public HandPose Pose;
        /// <summary>Ángulos absolutos medidos en los landmarks (pulgar, índice, medio, anular, meñique).</summary>
        public FingerAngles[] Target;
        /// <summary>Resultado por articulación del pulgar; vacío si no se resolvió.</summary>
        public ThumbJointResult[] Thumb;
        /// <summary>Dirección objetivo mundial de los tres segmentos del pulgar.</summary>
        public Vector3[] ThumbTargetWorld;
        /// <summary>Iteraciones de calibración usadas (0 si el método no calibra).</summary>
        public int CalibrationIterations;
        /// <summary>
        /// Mayor error angular (grados) de la pose conservada, medido en el rig antes de corregirla. Si la calibración
        /// converge es el de la última iteración; si no, el de la mejor iteración medida.
        /// </summary>
        public float CalibrationResidual;
        /// <summary>Verdadero si el error máximo bajó de la tolerancia de calibración (0,25°) dentro del máximo de iteraciones.</summary>
        public bool CalibrationConverged;
        /// <summary>
        /// Pose de los cuatro dedos largos antes de calibrar (ángulo del landmark − reposo del rig), en el orden
        /// pulgar, índice, medio, anular, meñique. El pulgar no se calibra y va vacío. Comparada con <see cref="Pose"/>
        /// da cuánto movió la calibración cada articulación.
        /// </summary>
        public FingerPose[] PreCalibration;
    }

    /// <summary>
    /// Convierte los 21 landmarks de una plantilla estática en un <see cref="HandPose"/> relativo al reposo del rig.
    /// Muñeca y desplazamiento quedan en cero. Los cuatro dedos largos se calculan con ángulos relativos al reposo;
    /// el pulgar se resuelve articulación por articulación con los ejes y el orden de rotación de HandPoseDriver.
    /// </summary>
    public static class LandmarkToHandPoseConverter
    {
        public const int LandmarkCount = 21;

        // Índices de MediaPipe: 0 muñeca, 9 MCP medio, 5 MCP índice, 17 MCP meñique.
        // Cada cadena es base, articulación 2, articulación 3 y punta. Orden: pulgar, índice, medio, anular, meñique.
        private static readonly int[,] Chains =
        {
            { 1, 2, 3, 4 },
            { 5, 6, 7, 8 },
            { 9, 10, 11, 12 },
            { 13, 14, 15, 16 },
            { 17, 18, 19, 20 },
        };

        /// <summary>
        /// Calcula la pose. Flexiones: ángulo del landmark − ángulo del reposo. Separación: ver <see cref="SeparationMethod"/>.
        /// Con <see cref="SeparationMethod.RigCalibrated"/> el método aplica poses de prueba en el driver para medir el rig;
        /// el llamador debe aplicar después la pose final.
        /// </summary>
        public static bool TryConvert(IReadOnlyList<Vector3> landmarks, RigRest rest, HandPoseDriver driver,
            SeparationMethod method, bool solveThumb, float toleranceDegrees, out ConversionResult result)
        {
            result = null;
            if (landmarks == null || landmarks.Count != LandmarkCount || rest == null || driver == null) return false;

            var wrist = landmarks[0];
            if (!LandmarkHandFrame.TryCreate(wrist, landmarks[9], landmarks[5], landmarks[17], out var dataFrame)) return false;

            var middleSegment = landmarks[10] - landmarks[9];
            var target = new FingerAngles[RigRest.FingerCount];
            for (int f = 0; f < RigRest.FingerCount; f++)
            {
                target[f] = FingerAngles.Measure(
                    wrist,
                    landmarks[Chains[f, 0]], landmarks[Chains[f, 1]], landmarks[Chains[f, 2]], landmarks[Chains[f, 3]],
                    middleSegment, dataFrame, rest.Mode);
            }

            var pose = new HandPose
            {
                thumb = new FingerPose(),
                index = ToFingerPose(target[1], rest.Fingers[1]),
                middle = ToFingerPose(target[2], rest.Fingers[2]),
                ring = ToFingerPose(target[3], rest.Fingers[3]),
                pinky = ToFingerPose(target[4], rest.Fingers[4]),
                wristEuler = Vector3.zero,
                wristOffset = Vector3.zero,
                adduction = 0f,
            };

            var preCalibration = new[] { pose.thumb, pose.index, pose.middle, pose.ring, pose.pinky };

            int iterations = 0;
            float residual = 0f;
            bool converged = false;
            if (method == SeparationMethod.RigCalibrated)
            {
                // Se guarda la mejor pose medida: con algunos landmarks (p. ej. índice en hiperextensión) la corrección
                // no converge de forma monótona y la última iteración no es la mejor.
                var best = pose;
                float bestResidual = float.MaxValue;
                for (; iterations < MaxCalibrationIterations; iterations++)
                {
                    driver.SetPose(pose);
                    var measured = rest.MeasureCurrent();
                    var measuredPose = pose;
                    residual = 0f;
                    pose.index = Correct(pose.index, target[1], measured[1], ref residual);
                    pose.middle = Correct(pose.middle, target[2], measured[2], ref residual);
                    pose.ring = Correct(pose.ring, target[3], measured[3], ref residual);
                    pose.pinky = Correct(pose.pinky, target[4], measured[4], ref residual);
                    if (residual < bestResidual) { bestResidual = residual; best = measuredPose; }
                    if (residual < CalibrationToleranceDegrees) { iterations++; converged = true; break; }
                }

                if (!converged)
                {
                    // La última corrección no se había medido todavía: se mide y se compara con la mejor.
                    driver.SetPose(pose);
                    var measured = rest.MeasureCurrent();
                    float finalResidual = 0f;
                    Correct(pose.index, target[1], measured[1], ref finalResidual);
                    Correct(pose.middle, target[2], measured[2], ref finalResidual);
                    Correct(pose.ring, target[3], measured[3], ref finalResidual);
                    Correct(pose.pinky, target[4], measured[4], ref finalResidual);
                    if (finalResidual < bestResidual) { bestResidual = finalResidual; best = pose; }
                    pose = best;
                    residual = bestResidual;
                }
            }

            var thumb = new ThumbJointResult[0];
            var thumbTargets = new Vector3[0];
            if (solveThumb)
            {
                thumbTargets = ThumbTargetDirections(landmarks, rest, dataFrame);
                thumb = SolveThumb(rest, thumbTargets, toleranceDegrees);
                pose.thumb = ToThumbPose(thumb);
            }

            result = new ConversionResult
            {
                Pose = pose,
                Target = target,
                Thumb = thumb,
                ThumbTargetWorld = thumbTargets,
                CalibrationIterations = iterations,
                CalibrationResidual = residual,
                CalibrationConverged = converged,
                PreCalibration = preCalibration,
            };
            return IsFinite(pose);
        }

        private const int MaxCalibrationIterations = 30;
        private const float CalibrationToleranceDegrees = 0.25f;

        // Suma al valor actual el error medido (landmark − rig) y registra el mayor error absoluto antes de corregir.
        private static FingerPose Correct(FingerPose p, FingerAngles target, FingerAngles measured, ref float worst)
        {
            float dSpread = target.Spread - measured.Spread;
            float dProximal = target.Proximal - measured.Proximal;
            float dMiddle = target.Middle - measured.Middle;
            float dDistal = target.Distal - measured.Distal;
            worst = Mathf.Max(worst, Mathf.Abs(dSpread), Mathf.Abs(dProximal), Mathf.Abs(dMiddle), Mathf.Abs(dDistal));
            p.spread += dSpread;
            p.proximal += dProximal;
            p.middle += dMiddle;
            p.distal += dDistal;
            return p;
        }

        /// <summary>
        /// Errores tras aplicar la pose: ángulo medido en el rig − ángulo del landmark, en grados.
        /// Filas: dedos (pulgar…meñique). Columnas: separación, proximal, media, distal.
        /// </summary>
        public static float[,] Errors(FingerAngles[] target, FingerAngles[] applied)
        {
            var e = new float[RigRest.FingerCount, 4];
            for (int f = 0; f < RigRest.FingerCount; f++)
            {
                e[f, 0] = applied[f].Spread - target[f].Spread;
                e[f, 1] = applied[f].Proximal - target[f].Proximal;
                e[f, 2] = applied[f].Middle - target[f].Middle;
                e[f, 3] = applied[f].Distal - target[f].Distal;
            }
            return e;
        }

        /// <summary>Ángulo, en grados, entre la dirección real de cada segmento del pulgar y la objetivo.</summary>
        public static float[] ThumbErrors(Vector3[] targetWorld, Vector3[] appliedWorld)
        {
            var e = new float[targetWorld.Length];
            for (int j = 0; j < e.Length; j++) e[j] = Vector3.Angle(targetWorld[j], appliedWorld[j]);
            return e;
        }

        // Dirección de cada segmento del pulgar en los landmarks, llevada al mundo del rig con una rotación propia
        // (marco de la mano del rig · inversa del marco de la mano de los landmarks).
        private static Vector3[] ThumbTargetDirections(IReadOnlyList<Vector3> landmarks, RigRest rest, LandmarkHandFrame dataFrame)
        {
            var toRig = rest.FrameRotation * Quaternion.Inverse(dataFrame.ToRotation());
            var targets = new Vector3[RigRest.ThumbJointCount];
            for (int j = 0; j < RigRest.ThumbJointCount; j++)
                targets[j] = (toRig * (landmarks[Chains[0, j + 1]] - landmarks[Chains[0, j]])).normalized;
            return targets;
        }

        // Resuelve Thumb1..Thumb3 en cadena. Para cada articulación:
        //   1. dirección objetivo en el marco local de la articulación (padre aplicado · reposo),
        //   2. giro que la alinea con la dirección del hueso hijo en reposo, con los ejes del driver,
        //   3. comprobación: se reconstruye la dirección y se mide el error.
        // El driver compone: Thumb1 = Rx(spread)·Ry(twist)·Rz(curl); Thumb2 = Rx(spreadMiddle)·Rz(curl); Thumb3 = Rz(curl).
        // El giro Y de Thumb1 va a lo largo del hueso y no cambia la dirección del hijo: no se puede determinar con landmarks y queda en 0.
        private static ThumbJointResult[] SolveThumb(RigRest rest, Vector3[] targetsWorld, float toleranceDegrees)
        {
            var results = new ThumbJointResult[RigRest.ThumbJointCount];
            var parent = rest.HandRotation;
            for (int j = 0; j < RigRest.ThumbJointCount; j++)
            {
                var frame = parent * rest.ThumbRestLocal[j];
                var targetLocal = (Quaternion.Inverse(frame) * targetsWorld[j]).normalized;
                var joint = SolveJoint(rest.ThumbChildDirection[j], targetLocal, j < 2, toleranceDegrees);
                results[j] = joint;

                // La cadena sigue con lo que realmente se aplicará: una articulación inestable queda en reposo.
                var q = Quaternion.identity;
                if (joint.Stable)
                    q = Quaternion.AngleAxis(joint.Spread, Vector3.right) * Quaternion.AngleAxis(joint.Curl, Vector3.forward);
                parent = frame * q;
            }
            return results;
        }

        // Busca (a, c) con Rx(a)·Rz(c)·d = t. Rz conserva la componente z de d, así que a se obtiene de
        // (Rx(−a)·t).z = d.z y c es el giro en el plano XY que falta. Con un solo eje (Rz) a = 0.
        private static ThumbJointResult SolveJoint(Vector3 childRest, Vector3 targetLocal, bool hasSpreadAxis, float toleranceDegrees)
        {
            var d = childRest.normalized;
            var t = targetLocal.normalized;

            float a = 0f;
            if (hasSpreadAxis)
            {
                float r = Mathf.Sqrt(t.y * t.y + t.z * t.z);
                if (r > 1e-6f)
                {
                    float psi = Mathf.Atan2(t.y, t.z);
                    float basis = Mathf.Acos(Mathf.Clamp(d.z / r, -1f, 1f));
                    float a1 = Wrap(basis - psi);
                    float a2 = Wrap(-basis - psi);
                    a = Mathf.Abs(a1) <= Mathf.Abs(a2) ? a1 : a2;
                }
            }

            float aDegrees = a * Mathf.Rad2Deg;
            var tPrime = Quaternion.AngleAxis(-aDegrees, Vector3.right) * t;
            float c = Wrap(Mathf.Atan2(tPrime.y, tPrime.x) - Mathf.Atan2(d.y, d.x));
            float cDegrees = c * Mathf.Rad2Deg;

            var reconstructed = Quaternion.AngleAxis(aDegrees, Vector3.right) * Quaternion.AngleAxis(cDegrees, Vector3.forward) * d;
            float error = Vector3.Angle(reconstructed, t);
            bool finite = float.IsFinite(aDegrees) && float.IsFinite(cDegrees) && float.IsFinite(error);
            return new ThumbJointResult(aDegrees, cDegrees, error, finite && error <= toleranceDegrees);
        }

        private static float Wrap(float radians)
        {
            while (radians > Mathf.PI) radians -= 2f * Mathf.PI;
            while (radians < -Mathf.PI) radians += 2f * Mathf.PI;
            return radians;
        }

        private static FingerPose ToThumbPose(ThumbJointResult[] joints)
        {
            var p = new FingerPose();
            if (joints.Length != RigRest.ThumbJointCount) return p;
            if (joints[0].Stable) { p.spread = joints[0].Spread; p.proximal = joints[0].Curl; }
            if (joints[1].Stable) { p.spreadMiddle = joints[1].Spread; p.middle = joints[1].Curl; }
            if (joints[2].Stable) { p.distal = joints[2].Curl; }
            return p;
        }

        private static FingerPose ToFingerPose(FingerAngles data, FingerAngles reference)
            => new FingerPose(
                data.Spread - reference.Spread,
                data.Proximal - reference.Proximal,
                data.Middle - reference.Middle,
                data.Distal - reference.Distal);

        private static bool IsFinite(HandPose p)
        {
            return IsFinite(p.thumb) && IsFinite(p.index) && IsFinite(p.middle) && IsFinite(p.ring) && IsFinite(p.pinky);
        }

        private static bool IsFinite(FingerPose f)
        {
            return float.IsFinite(f.spread) && float.IsFinite(f.proximal) && float.IsFinite(f.middle) && float.IsFinite(f.distal)
                && float.IsFinite(f.spreadMiddle);
        }
    }
}
