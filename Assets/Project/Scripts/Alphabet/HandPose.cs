using System;
using UnityEngine;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Ángulos (en grados) de las tres articulaciones de un dedo. Positivo = flexión hacia la palma.
    /// En el pulgar, "spread" abre el pulgar hacia fuera de la mano; en los demás separa el dedo.
    /// </summary>
    [Serializable]
    public struct FingerPose
    {
        public float spread;
        public float proximal;
        public float middle;
        public float distal;

        public FingerPose(float spread, float proximal, float middle, float distal)
        {
            this.spread = spread; this.proximal = proximal; this.middle = middle; this.distal = distal;
        }

        public static FingerPose Lerp(FingerPose a, FingerPose b, float t) => new FingerPose(
            Mathf.Lerp(a.spread, b.spread, t), Mathf.Lerp(a.proximal, b.proximal, t),
            Mathf.Lerp(a.middle, b.middle, t), Mathf.Lerp(a.distal, b.distal, t));
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
        [Tooltip("Rotación extra de la muñeca (grados) sobre la orientación base de la demostración.")]
        public Vector3 wristEuler;
        [Tooltip("Desplazamiento de la muñeca (metros) respecto a la posición base. Se usa en letras con movimiento.")]
        public Vector3 wristOffset;

        public static HandPose Lerp(HandPose a, HandPose b, float t) => new HandPose
        {
            thumb = FingerPose.Lerp(a.thumb, b.thumb, t),
            index = FingerPose.Lerp(a.index, b.index, t),
            middle = FingerPose.Lerp(a.middle, b.middle, t),
            ring = FingerPose.Lerp(a.ring, b.ring, t),
            pinky = FingerPose.Lerp(a.pinky, b.pinky, t),
            wristEuler = Vector3.Lerp(a.wristEuler, b.wristEuler, t),
            wristOffset = Vector3.Lerp(a.wristOffset, b.wristOffset, t)
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
