using System;

namespace Lumina.Signs
{
    /// <summary>
    /// SOLUCIÓN TEMPORAL para el abecedario. El reconocedor de números (<c>MediaPipeNumberSignRecognizer</c>, en la IA) ya entrega
    /// precisión 0–100, estrellas y XP; el evaluador de letras (<c>AlphabetLandmarkEvaluator</c>) solo entrega la similitud
    /// 0–1. El backend exige que estrellas y XP cuadren con la tabla oficial, así que este adaptador aplica EXACTAMENTE la misma
    /// tabla que usa el reconocedor de números:
    ///   similitud ≥ 0,88 → 3 estrellas · ≥ 0,74 → 2 · ≥ 0,60 → 1 · XP = estrellas × 10.
    /// Los umbrales no están como constantes accesibles en el código de la IA (son literales dentro de Completed), por eso se
    /// repiten aquí. Cuando la IA entregue estrellas y XP para las letras, este adaptador se elimina.
    /// </summary>
    public static class ScoreAdapter
    {
        public const double PassPercent = 60.0, TwoStarsPercent = 74.0, ThreeStarsPercent = 88.0;
        public const int XpPerStar = 10;

        public readonly struct Score
        {
            /// <summary>Precisión 0–100 con como máximo 2 decimales (lo que acepta el backend).</summary>
            public readonly double Precision;
            public readonly int Stars;
            public readonly int Xp;
            public bool Passed => Stars > 0;

            public Score(double precision, int stars, int xp) { Precision = precision; Stars = stars; Xp = xp; }
        }

        /// <summary>
        /// Resultado de una letra a partir de la similitud 0–1 del evaluador. Las estrellas se deciden como lo hace el reconocedor de
        /// números (comparando la similitud float con 0,88 / 0,74 / 0,60) y la precisión se calcula como él (<c>similitud * 100f</c> en
        /// float), de modo que ambos coinciden. Después la precisión se deja dentro de la franja de sus estrellas: así el backend,
        /// que verifica estrellas y XP contra la tabla oficial, nunca rechaza el resultado por un redondeo de coma flotante
        /// (por ejemplo, 0,88f vale en realidad 0,87999999…).
        /// </summary>
        public static Score FromSimilarity(float similarity)
        {
            float s = Math.Clamp(similarity, 0f, 1f);
            int stars = s >= 0.88f ? 3 : s >= 0.74f ? 2 : s >= 0.60f ? 1 : 0;
            double precision = ToApiPrecision((float)(s * 100f));
            switch (stars)
            {
                case 3: precision = Math.Max(precision, ThreeStarsPercent); break;
                case 2: precision = Math.Clamp(precision, TwoStarsPercent, 87.99); break;
                case 1: precision = Math.Clamp(precision, PassPercent, 73.99); break;
                default: precision = Math.Min(precision, 59.99); break;
            }
            return new Score(precision, stars, stars * XpPerStar);
        }

        /// <summary>Estrellas de la tabla oficial para una precisión 0–100 (0 si no aprueba).</summary>
        public static int StarsFor(double precision) =>
            precision >= ThreeStarsPercent ? 3 : precision >= TwoStarsPercent ? 2 : precision >= PassPercent ? 1 : 0;

        /// <summary>
        /// Deja la precisión 0–100 con 2 decimales TRUNCANDO (no redondeando): así nunca sube a un umbral que la IA no alcanzó
        /// (por ejemplo 87,996 pasa a 87,99 y sigue siendo 2 estrellas, igual que decidió el reconocedor).
        /// </summary>
        public static double ToApiPrecision(double percent)
        {
            double clamped = Math.Clamp(percent, 0.0, 100.0);
            return Math.Floor(clamped * 100.0) / 100.0;
        }
    }
}
