using System;
using UnityEngine;

namespace Lumina.Alphabet
{
    // CONVENCIONES (válidas para la mano derecha del pack "Free VR Hands", validadas con HandPoseDriver):
    //
    //  Dedos (índice, medio, anular, meñique) — ejes locales de cada hueso J_Right_Hand<Dedo><n>
    //    · proximal / middle / distal : flexión sobre el eje X local. Positivo = hacia la palma; 0 = pose de reposo del rig.
    //    · spread                     : giro en el plano de la palma (eje Z local). Positivo = hacia el lado del meñique.
    //    · adduction                  : ver FingerPose.adduction.
    //
    //  Pulgar — ejes locales de J_Right_HandThumb<n>
    //    · proximal / middle / distal : flexión sobre el eje Z local. Positivo = hacia la palma.
    //    · spread                     : apertura de la 1.ª articulación (eje X local). Negativo = pulgar separado de la mano.
    //    · twist                      : rotación axial de la 1.ª articulación (eje Y local, a lo largo del hueso).
    //    · spreadMiddle               : apertura de la 2.ª articulación (eje X local).
    //    · opposition                 : mezcla 0..1 hacia el preset de oposición del HandPoseDriver.
    //
    //  Muñeca — hueso J_Right; las direcciones se describen en términos anatómicos (mano derecha, palma hacia la cámara):
    //    · wristEuler  (grados, se compone como reposo * Quaternion.Euler(x, y, z) sobre los ejes locales de J_Right):
    //        X  flexión de la muñeca: + lleva las puntas de los dedos hacia el lado de la palma; − hacia el dorso.
    //        Y  giro axial alrededor del eje del antebrazo/dedos (pronación/supinación): gira la palma sobre sí misma.
    //        Z  inclinación en el plano de la palma: + inclina los dedos hacia el lado cubital (meñique) cuando la
    //           palma mira a la cámara y los dedos apuntan arriba (hacia la izquierda de la pantalla).
    //    · wristOffset (metros, en el espacio local del hueso padre "Root", independiente de cómo esté girada la instancia):
    //        X  hacia el dorso de la mano (con la palma hacia la cámara: se aleja de ella).
    //        Y  hacia el antebrazo (con los dedos arriba: baja). Un valor negativo lleva la mano hacia las puntas.
    //        Z  hacia el lado radial / del pulgar (con la palma hacia la cámara y la mano derecha: derecha de la pantalla).

    /// <summary>
    /// Ángulos (en grados) de un dedo. Todos los campos nuevos valen 0 por defecto, de modo que las poses
    /// guardadas antes de añadirlos se comportan igual que antes.
    /// </summary>
    [Serializable]
    public struct FingerPose
    {
        [Tooltip("Dedos: giro en el plano de la palma (+ hacia el meñique). Pulgar: apertura de la 1.ª articulación (− separa el pulgar).")]
        public float spread;
        public float proximal;
        public float middle;
        public float distal;

        [Header("Ampliaciones (0 = comportamiento anterior)")]
        [Tooltip("Dedos: aducción individual, se suma a la de la mano. 0 = reposo del rig; 1 = paralelo al dedo medio (juntos); negativo = más separado que el reposo. No afecta al pulgar.")]
        public float adduction;
        [Tooltip("Pulgar: rotación axial de la 1.ª articulación (grados).")]
        public float twist;
        [Tooltip("Pulgar: apertura de la 2.ª articulación (grados).")]
        public float spreadMiddle;
        [Tooltip("Pulgar: 0 = sin oposición, 1 = preset de oposición completo del HandPoseDriver. Se suma a los ángulos anteriores.")]
        [Range(0f, 1f)] public float opposition;

        public FingerPose(float spread, float proximal, float middle, float distal)
        {
            this.spread = spread; this.proximal = proximal; this.middle = middle; this.distal = distal;
            adduction = 0f; twist = 0f; spreadMiddle = 0f; opposition = 0f;
        }

        public static FingerPose Lerp(FingerPose a, FingerPose b, float t) => new FingerPose
        {
            spread = Mathf.Lerp(a.spread, b.spread, t),
            proximal = Mathf.Lerp(a.proximal, b.proximal, t),
            middle = Mathf.Lerp(a.middle, b.middle, t),
            distal = Mathf.Lerp(a.distal, b.distal, t),
            adduction = Mathf.Lerp(a.adduction, b.adduction, t),
            twist = Mathf.Lerp(a.twist, b.twist, t),
            spreadMiddle = Mathf.Lerp(a.spreadMiddle, b.spreadMiddle, t),
            opposition = Mathf.Lerp(a.opposition, b.opposition, t)
        };
    }

    /// <summary>Configuración completa de una mano: cinco dedos y la orientación/posición de la muñeca.</summary>
    [Serializable]
    public struct HandPose
    {
        public FingerPose thumb;
        public FingerPose index;
        public FingerPose middle;
        public FingerPose ring;
        public FingerPose pinky;

        [Tooltip("Rotación extra de la muñeca (grados). Ejes: X flexión, Y giro axial, Z inclinación (ver comentario de cabecera).")]
        public Vector3 wristEuler;
        [Tooltip("Desplazamiento de la muñeca (metros). Ejes: X dorso, Y antebrazo, Z lado del pulgar (ver comentario de cabecera).")]
        public Vector3 wristOffset;

        [Tooltip("Aducción de todos los dedos respecto al dedo medio: 0 = reposo del rig (separados), 1 = juntos y paralelos, negativo = más abiertos. Se suma a la de cada dedo.")]
        public float adduction;

        public static HandPose Lerp(HandPose a, HandPose b, float t) => new HandPose
        {
            thumb = FingerPose.Lerp(a.thumb, b.thumb, t),
            index = FingerPose.Lerp(a.index, b.index, t),
            middle = FingerPose.Lerp(a.middle, b.middle, t),
            ring = FingerPose.Lerp(a.ring, b.ring, t),
            pinky = FingerPose.Lerp(a.pinky, b.pinky, t),
            wristEuler = Vector3.Lerp(a.wristEuler, b.wristEuler, t),
            wristOffset = Vector3.Lerp(a.wristOffset, b.wristOffset, t),
            adduction = Mathf.Lerp(a.adduction, b.adduction, t)
        };
    }

    /// <summary>Fotograma clave de una letra con movimiento (J, Z...).</summary>
    [Serializable]
    public struct PoseKey
    {
        public float time;
        public HandPose pose;
    }
}
