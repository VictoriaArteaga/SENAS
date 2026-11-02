using System;
using System.Collections.Generic;
using System.Text;
using Lumina.Alphabet;
using Senas.AI.MediaPipe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumina.Animation.Landmarks.EditorTools
{
    /// <summary>
    /// Convierte una secuencia de NumberLandmarkTemplates.asset (solo lectura) en un <see cref="NumberSignClip"/>.
    /// Pasos por fotograma: conversión con <see cref="LandmarkToHandPoseConverter"/> (RigCalibrated, FlexionPlane, memoria de separación por secuencia),
    /// política del Thumb3, y suavizado sobre los ÁNGULOS DE SALIDA ya envueltos (mediana y media móvil); los landmarks originales no se tocan.
    /// La muñeca queda en cero. La secuencia de cada número se elige con una regla fijada de antemano (ver <see cref="BatchOne"/>).
    /// </summary>
    public static class NumberSignClipBaker
    {
        public const string TemplatesPath = "Assets/Project/Data/NumberLandmarkTemplates.asset";
        public const string ClipFolder = "Assets/Project/Data/NumberClips";
        public const string FallbackScene = "Assets/Project/Scenes/Animation/NumberPoseTest.unity";

        /// <summary>Fotogramas por segundo con los que se calcula la duración PROVISIONAL (fotogramas medianos del número / este valor).</summary>
        public const float ProvisionalFps = 30f;
        public const int Thumb3HoldFrames = 15;
        public const int Thumb3ReturnFrames = 5;
        public const float MinConvergedFraction = 0.95f;
        /// <summary>Si la mediana del giro de muñeca de la secuencia supera este valor, no se hornea (queda para el experimento de muñeca).</summary>
        public const float MaxWristMedianDegrees = 30f;

        /// <summary>
        /// Lote 1: número → índice de la secuencia en _motionTemplates. Regla fijada antes de elegir: entre las secuencias del número con ≥ 95 % de
        /// fotogramas Converged (definición nueva), sin outliers y sin casi estáticas, la de menor distancia DTW media (21 canales) a las demás elegibles.
        /// La regla se evaluó fuera del baker (análisis de la auditoría); el baker solo comprueba el ≥ 95 % y que la etiqueta coincida.
        /// </summary>
        public static readonly (int number, int sequenceId)[] BatchOne = { (6, 5), (7, 16), (8, 19), (9, 27), (25, 187) };

        /// <summary>
        /// Lote 2 (misma regla de selección). El 30 (secuencia 227) queda fuera: su giro de muñeca mediano es 58,8° (> 30°).
        /// Los candidatos 24, 28 y 29 llegan al tope de 90 fotogramas (posible truncamiento).
        /// </summary>
        public static readonly (int number, int sequenceId)[] BatchTwo = { (20, 132), (24, 173), (27, 285), (28, 213), (29, 293) };

        /// <summary>Lote 3 (misma regla). Los dos candidatos llegan al tope de 90 fotogramas (posible truncamiento); el 22 queda justo bajo el límite de giro (29°).</summary>
        public static readonly (int number, int sequenceId)[] BatchThree = { (22, 155), (23, 168) };

        [Serializable]
        public struct Settings
        {
            public int medianWindow;
            public int averageWindow;
            public static Settings Default => new Settings { medianWindow = 3, averageWindow = 5 };
        }

        [MenuItem("SENAS/Animación/Hornear clips de números (lote 1: 6, 7, 8, 9, 25)")]
        public static void BakeBatchOneMenu() => BakeBatchOne();

        [MenuItem("SENAS/Animación/Hornear clips de números (lote 2: 20, 24, 27, 28, 29)")]
        public static void BakeBatchTwoMenu() => BakeBatchTwo();

        /// <summary>Hornea el lote 1 y devuelve el informe de texto. Lo usa el menú y se puede llamar desde -executeMethod.</summary>
        public static string BakeBatchOne() => BakeBatch(BatchOne);

        [MenuItem("SENAS/Animación/Hornear clips de números (lote 3: 22, 23)")]
        public static void BakeBatchThreeMenu() => BakeBatchThree();

        /// <summary>Hornea el lote 3 y devuelve el informe de texto.</summary>
        public static string BakeBatchThree() => BakeBatch(BatchThree);

        /// <summary>Hornea el lote 2 y devuelve el informe de texto.</summary>
        public static string BakeBatchTwo() => BakeBatch(BatchTwo);

        public static string BakeBatch((int number, int sequenceId)[] batch)
        {
            var report = new StringBuilder();
            var scenesToClose = new List<UnityEngine.SceneManagement.Scene>();
            var driver = UnityEngine.Object.FindFirstObjectByType<HandPoseDriver>();
            if (driver == null)
            {
                var scene = EditorSceneManager.OpenScene(FallbackScene, OpenSceneMode.Additive);
                scenesToClose.Add(scene);
                driver = UnityEngine.Object.FindFirstObjectByType<HandPoseDriver>();
            }
            try
            {
                if (driver == null) throw new InvalidOperationException("No hay ningún HandPoseDriver en las escenas abiertas.");
                foreach (var (number, id) in batch)
                    report.AppendLine(Bake(driver, number, id, Settings.Default));
            }
            finally
            {
                foreach (var s in scenesToClose) EditorSceneManager.CloseScene(s, true);
            }
            Debug.Log(report.ToString());
            return report.ToString();
        }

        /// <summary>Hornea una secuencia. Devuelve el resumen; lanza si la secuencia no cumple la guarda de convergencia.</summary>
        public static string Bake(HandPoseDriver driver, int number, int sequenceId, Settings settings)
        {
            var set = AssetDatabase.LoadAssetAtPath<AlphabetLandmarkTemplateSet>(TemplatesPath);
            if (set == null) throw new InvalidOperationException("No se encontró " + TemplatesPath);
            var motion = set.MotionTemplates;
            if (sequenceId < 0 || sequenceId >= motion.Count) throw new ArgumentOutOfRangeException(nameof(sequenceId));
            var seq = motion[sequenceId];
            if (seq.Letter != number.ToString()) throw new InvalidOperationException($"La secuencia {sequenceId} es del número {seq.Letter}, no del {number}.");

            if (!RigRest.TryCapture(driver.transform, driver, out var rest, FlexionMeasure.FlexionPlane))
                throw new InvalidOperationException("No se pudo capturar el reposo del rig.");

            int n = seq.Frames.Count;
            var poses = new HandPose[n];
            var thumb3Applied = new bool[n];
            var classes = new ConvergenceClass[n];
            var tips = new List<float>();
            var memory = new SpreadMemory();
            var wristAngles = new float[n];
            Quaternion firstFrame = Quaternion.identity;
            int converged = 0, undeterminable = 0, failed = 0;
            for (int i = 0; i < n; i++)
            {
                var lm = ToArray(seq.Frames[i].NormalizedLandmarks);
                if (!LandmarkToHandPoseConverter.TryConvert(lm, rest, driver, SeparationMethod.RigCalibrated, true, 15f, out var r, memory))
                    throw new InvalidOperationException($"Conversión inválida en el fotograma {i} de la secuencia {sequenceId}.");
                poses[i] = r.Pose;
                thumb3Applied[i] = r.Thumb.Length == RigRest.ThumbJointCount && r.Thumb[2].Stable;
                classes[i] = r.Class;
                for (int f = 1; f < RigRest.FingerCount; f++) tips.Add(r.TipError[f]);
                switch (r.Class)
                {
                    case ConvergenceClass.Converged: converged++; break;
                    case ConvergenceClass.SpreadUndeterminable: undeterminable++; break;
                    default: failed++; break;
                }
                if (LandmarkHandFrame.TryCreate(lm[0], lm[9], lm[5], lm[17], out var frame))
                {
                    var q = frame.ToRotation();
                    if (i == 0) firstFrame = q;
                    wristAngles[i] = Quaternion.Angle(firstFrame, q);
                }
            }

            if (converged < Mathf.CeilToInt(MinConvergedFraction * n))
                throw new InvalidOperationException($"Secuencia {sequenceId}: solo {converged}/{n} fotogramas Converged (< {MinConvergedFraction:P0}); no se hornea.");

            // 1) Política del Thumb3: mantener el último valor aplicado hasta 15 fotogramas y volver al reposo con una transición de 5.
            int affected = ApplyThumb3Policy(poses, thumb3Applied);

            // 2) Suavizado de los ángulos de salida (mediana y media móvil), con los ángulos ya envueltos y desenvueltos por canal.
            Smooth(poses, settings.medianWindow, settings.averageWindow);

            // 3) Reposición de metadatos y guardado.
            var frames = new List<NumberSignClipFrame>(n);
            for (int i = 0; i < n; i++)
            {
                var p = poses[i];
                p.wristEuler = Vector3.zero; p.wristOffset = Vector3.zero; p.adduction = 0f;
                frames.Add(new NumberSignClipFrame { time = seq.Frames[i].Time, pose = p });
            }
            if (frames[frames.Count - 1].time < 1f - 1e-4f)
                Debug.LogWarning($"[Baker] El último fotograma de la secuencia {sequenceId} termina en t={frames[frames.Count - 1].time}, no en 1.");

            var counts = new List<int>();
            foreach (var m in motion) if (m.Letter == number.ToString()) counts.Add(m.Frames.Count);
            counts.Sort();
            float medianFrames = counts.Count % 2 == 1 ? counts[counts.Count / 2] : 0.5f * (counts[counts.Count / 2 - 1] + counts[counts.Count / 2]);
            float duration = medianFrames / ProvisionalFps;

            tips.Sort();
            float tipMedian = tips[tips.Count / 2], tipP95 = tips[Mathf.Min(tips.Count - 1, (int)(tips.Count * 0.95f))];
            var wsorted = (float[])wristAngles.Clone(); Array.Sort(wsorted);
            float wristMedian = wsorted[n / 2], wristMax = wsorted[n - 1];
            float thumbPct = 100f * affected / n;
            if (wristMedian > MaxWristMedianDegrees)
                throw new InvalidOperationException($"Secuencia {sequenceId}: giro de muñeca mediano {wristMedian:0.0}° > {MaxWristMedianDegrees:0}°; no se hornea (queda para el experimento de muñeca).");

            // Re-hornear no cambia la validación: se conserva el valor del clip existente (falso si no existía).
            string path = $"{ClipFolder}/Number_{number:00}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NumberSignClip>(path);
            bool validated = existing != null && existing.LscValidated;

            var clip = ScriptableObject.CreateInstance<NumberSignClip>();
            clip.Configure(number, sequenceId, n, n >= 90, duration, true, frames, settings.medianWindow, settings.averageWindow,
                converged, undeterminable, failed, tipMedian, tipP95, thumbPct, wristMedian, wristMax,
                $"Horneado desde la secuencia {sequenceId} de NumberLandmarkTemplates.asset. Duración PROVISIONAL ({medianFrames:0.#} fotogramas medianos / {ProvisionalFps:0} = {duration:0.00} s). " +
                "Muñeca en cero (rotación no aplicada). Validación LSC pendiente de un hablante o intérprete; sin referencias para este número." +
                (n >= 90 ? " La secuencia llega al tope de 90 fotogramas: posible truncamiento." : ""), validated);

            EnsureFolder();
            // Si ya existe se sobrescribe en el mismo asset para conservar su guid (la escena lo referencia).
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                existing.name = $"Number_{number:00}"; // CopySerialized copia también el nombre (vacío)
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(clip);
            }
            else AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            return $"Número {number}: secuencia {sequenceId}, {n} fotogramas{(n >= 90 ? " (TOPE 90, posible truncamiento)" : "")}, duración provisional {duration:0.00} s. " +
                   $"Converged {converged}, SpreadUndeterminable {undeterminable}, otros {failed}. Punta mediana {tipMedian:0.000} / p95 {tipP95:0.000}. " +
                   $"Thumb3 afectado {thumbPct:0.0} % ({affected}/{n}). Giro de muñeca respecto al primer fotograma: mediana {wristMedian:0.0}°, máx {wristMax:0.0}° (no aplicado). → {path}";
        }

        // Devuelve cuántos fotogramas tuvieron el Thumb3 pendiente (mantenido o en reposo) en vez del valor convertido.
        private static int ApplyThumb3Policy(HandPose[] poses, bool[] applied)
        {
            float last = 0f;      // último valor aplicado; 0 = reposo si nunca hubo
            int run = 0;          // fotogramas consecutivos con el Thumb3 pendiente
            int affected = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                var p = poses[i];
                if (applied[i])
                {
                    last = p.thumb.distal;
                    run = 0;
                }
                else
                {
                    run++;
                    affected++;
                    float value;
                    if (run <= Thumb3HoldFrames) value = last;
                    else if (run <= Thumb3HoldFrames + Thumb3ReturnFrames) value = Mathf.Lerp(last, 0f, (run - Thumb3HoldFrames) / (float)Thumb3ReturnFrames);
                    else value = 0f;
                    p.thumb.distal = value;
                }
                poses[i] = p;
            }
            return affected;
        }

        // Canales: spread, proximal, middle, distal y spreadMiddle de los cinco dedos (el spreadMiddle solo lo usa el pulgar).
        private const int ChannelsPerFinger = 5;
        private const int Channels = 5 * ChannelsPerFinger;

        private static void Smooth(HandPose[] poses, int medianWindow, int averageWindow)
        {
            int n = poses.Length;
            var data = new float[Channels][];
            for (int c = 0; c < Channels; c++) data[c] = new float[n];
            for (int i = 0; i < n; i++) Read(poses[i], data, i);

            for (int c = 0; c < Channels; c++)
            {
                var x = Unwrap(data[c]);
                if (medianWindow > 1) x = Median(x, medianWindow);
                if (averageWindow > 1) x = Average(x, averageWindow);
                for (int i = 0; i < n; i++) data[c][i] = Wrap(x[i]);
            }
            for (int i = 0; i < n; i++) poses[i] = Write(poses[i], data, i);
        }

        private static float Wrap(float d)
        {
            d %= 360f;
            if (d > 180f) d -= 360f; else if (d <= -180f) d += 360f;
            return d;
        }

        // Quita los saltos de 360° para que mediana y media no mezclen lados de la envoltura.
        private static float[] Unwrap(float[] v)
        {
            var o = new float[v.Length];
            o[0] = v[0];
            for (int i = 1; i < v.Length; i++)
            {
                float d = Wrap(v[i] - o[i - 1]);
                o[i] = o[i - 1] + d;
            }
            return o;
        }

        private static float[] Median(float[] v, int w)
        {
            int k = w / 2, n = v.Length;
            var o = new float[n];
            var buf = new float[w];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < w; j++) buf[j] = v[Mathf.Clamp(i - k + j, 0, n - 1)];
                Array.Sort(buf);
                o[i] = buf[w / 2];
            }
            return o;
        }

        private static float[] Average(float[] v, int w)
        {
            int k = w / 2, n = v.Length;
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int j = 0; j < w; j++) s += v[Mathf.Clamp(i - k + j, 0, n - 1)];
                o[i] = s / w;
            }
            return o;
        }

        private static void Read(HandPose p, float[][] d, int i)
        {
            var fs = new[] { p.thumb, p.index, p.middle, p.ring, p.pinky };
            for (int f = 0; f < 5; f++)
            {
                d[f * 5 + 0][i] = fs[f].spread; d[f * 5 + 1][i] = fs[f].proximal; d[f * 5 + 2][i] = fs[f].middle;
                d[f * 5 + 3][i] = fs[f].distal; d[f * 5 + 4][i] = fs[f].spreadMiddle;
            }
        }

        private static HandPose Write(HandPose p, float[][] d, int i)
        {
            FingerPose Set(FingerPose fp, int f)
            {
                fp.spread = d[f * 5 + 0][i]; fp.proximal = d[f * 5 + 1][i]; fp.middle = d[f * 5 + 2][i];
                fp.distal = d[f * 5 + 3][i]; fp.spreadMiddle = d[f * 5 + 4][i];
                return fp;
            }
            p.thumb = Set(p.thumb, 0); p.index = Set(p.index, 1); p.middle = Set(p.middle, 2); p.ring = Set(p.ring, 3); p.pinky = Set(p.pinky, 4);
            return p;
        }

        private static Vector3[] ToArray(IReadOnlyList<Vector3> l)
        {
            var a = new Vector3[l.Count];
            for (int i = 0; i < a.Length; i++) a[i] = l[i];
            return a;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder)) AssetDatabase.CreateFolder("Assets/Project/Data", "NumberClips");
        }
    }
}
