using System;
using UnityEngine;

namespace Lumina.Alphabet
{
    /// <summary>Cuánta confianza hay en la pose frente a la referencia disponible.</summary>
    public enum PoseValidation
    {
        /// <summary>La forma general de la mano se distingue en la imagen de referencia, pero no está validada por una persona usuaria de LSC.</summary>
        ApproximateFromReference,
        /// <summary>La referencia no permite distinguir la configuración; la pose es provisional.</summary>
        ProvisionalNeedsValidation
    }

    [Serializable]
    public class LetterEntry
    {
        public string letter;
        [TextArea(1, 3)] public string instruction;
        public PoseValidation validation;
        [Tooltip("Pose estática (o pose inicial si hay movimiento).")]
        public HandPose pose;
        [Tooltip("Si no está vacío, la demostración reproduce estos fotogramas en bucle.")]
        public PoseKey[] motion;

        public bool HasMotion => motion != null && motion.Length > 1;
    }

    /// <summary>Datos del abecedario: una entrada por letra, en orden alfabético (con Ñ).</summary>
    [CreateAssetMenu(menuName = "Lumina/LSC Alphabet", fileName = "LscAlphabet")]
    public class LscAlphabet : ScriptableObject
    {
        public LetterEntry[] letters;
    }
}
