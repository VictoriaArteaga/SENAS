using UnityEngine;

namespace Lumina.Animation.Landmarks
{
    /// <summary>
    /// Marco de una mano a partir de cuatro puntos: muñeca, nudillo (MCP) del medio, del índice y del meñique.
    /// Finger = muñeca → MCP medio; Ulnar = MCP índice → MCP meñique (perpendicular a Finger);
    /// Palm = Ulnar × Finger. Es el mismo criterio para los landmarks y para el rig, así que los ángulos
    /// medidos en ambos espacios son comparables. Función pura, sin estado.
    /// </summary>
    public readonly struct LandmarkHandFrame
    {
        public readonly Vector3 Finger;
        public readonly Vector3 Ulnar;
        public readonly Vector3 Palm;

        private LandmarkHandFrame(Vector3 finger, Vector3 ulnar, Vector3 palm)
        {
            Finger = finger;
            Ulnar = ulnar;
            Palm = palm;
        }

        /// <summary>
        /// Rotación de la base (Finger como adelante, Palm como arriba). Con ella, R_rig · R_datos⁻¹ lleva cualquier
        /// dirección de los landmarks al mundo del rig. Es una rotación propia: no refleja la mano.
        /// </summary>
        public Quaternion ToRotation() => Quaternion.LookRotation(Finger, Palm);

        public static bool TryCreate(Vector3 wrist, Vector3 middleKnuckle, Vector3 indexKnuckle, Vector3 pinkyKnuckle, out LandmarkHandFrame frame)
        {
            frame = default;

            var finger = middleKnuckle - wrist;
            if (finger.sqrMagnitude < 1e-12f) return false;
            finger.Normalize();

            var ulnar = pinkyKnuckle - indexKnuckle;
            ulnar -= Vector3.Dot(ulnar, finger) * finger;
            if (ulnar.sqrMagnitude < 1e-12f) return false;
            ulnar.Normalize();

            var palm = Vector3.Cross(ulnar, finger).normalized;
            frame = new LandmarkHandFrame(finger, ulnar, palm);
            return true;
        }
    }

    /// <summary>
    /// Cómo se miden las flexiones (proximal, media, distal).
    /// </summary>
    public enum FlexionMeasure
    {
        /// <summary>
        /// Ángulo 3D entre segmentos consecutivos con el signo del eje cubital. Incluye la desviación lateral entre los
        /// segmentos, de modo que cerca de 0° el valor tiene una banda muerta (|ángulo| ≥ desviación lateral) y cambia de
        /// signo de golpe. Es el comportamiento original; sigue seleccionable (línea base Legacy del 0).
        /// </summary>
        Legacy,

        /// <summary>
        /// Los segmentos se proyectan antes al plano de flexión (perpendicular al eje cubital) y se mide el ángulo con
        /// signo en ese plano: continuo y monótono a través de 0°. La separación se mide igual que en Legacy.
        /// Es la medida por defecto desde la aprobación posterior al diagnóstico de la banda muerta.
        /// </summary>
        FlexionPlane,
    }

    /// <summary>
    /// Ángulos de un dedo en grados. Proximal, Middle y Distal son flexiones (positivo = hacia la palma);
    /// Spread es la separación en el plano de la palma respecto al dedo medio (positivo = hacia el meñique).
    /// </summary>
    public readonly struct FingerAngles
    {
        public readonly float Proximal;
        public readonly float Middle;
        public readonly float Distal;
        public readonly float Spread;

        public FingerAngles(float proximal, float middle, float distal, float spread)
        {
            Proximal = proximal;
            Middle = middle;
            Distal = distal;
            Spread = spread;
        }

        /// <summary>
        /// Mide un dedo a partir de cinco puntos consecutivos (muñeca, base, articulación 2, articulación 3, punta).
        /// <paramref name="middleFirstSegment"/> es el segmento base→articulación 2 del dedo medio, que sirve de referencia para la separación.
        /// </summary>
        public static FingerAngles Measure(Vector3 wrist, Vector3 knuckle, Vector3 joint2, Vector3 joint3, Vector3 tip,
            Vector3 middleFirstSegment, LandmarkHandFrame frame, FlexionMeasure measure = FlexionMeasure.Legacy)
        {
            var d0 = knuckle - wrist;
            var d1 = joint2 - knuckle;
            var d2 = joint3 - joint2;
            var d3 = tip - joint3;

            if (measure == FlexionMeasure.FlexionPlane)
            {
                d0 = Vector3.ProjectOnPlane(d0, frame.Ulnar);
                var p1 = Vector3.ProjectOnPlane(d1, frame.Ulnar);
                var p2 = Vector3.ProjectOnPlane(d2, frame.Ulnar);
                var p3 = Vector3.ProjectOnPlane(d3, frame.Ulnar);
                return new FingerAngles(
                    Vector3.SignedAngle(d0, p1, frame.Ulnar),
                    Vector3.SignedAngle(p1, p2, frame.Ulnar),
                    Vector3.SignedAngle(p2, p3, frame.Ulnar),
                    SpreadAngle(middleFirstSegment, d1, frame.Palm));
            }

            // La flexión gira alrededor del eje cubital; con este criterio, positivo lleva el dedo hacia la palma.
            return new FingerAngles(
                Vector3.SignedAngle(d0, d1, frame.Ulnar),
                Vector3.SignedAngle(d1, d2, frame.Ulnar),
                Vector3.SignedAngle(d2, d3, frame.Ulnar),
                SpreadAngle(middleFirstSegment, d1, frame.Palm));
        }

        // Separación en el plano perpendicular a la palma. El eje es −Palm, de modo que positivo = hacia el meñique.
        private static float SpreadAngle(Vector3 reference, Vector3 finger, Vector3 palm)
        {
            var r = Vector3.ProjectOnPlane(reference, palm);
            var f = Vector3.ProjectOnPlane(finger, palm);
            return Vector3.SignedAngle(r, f, -palm);
        }
    }
}
