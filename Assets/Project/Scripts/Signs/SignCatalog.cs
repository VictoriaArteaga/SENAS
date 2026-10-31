using System;
using UnityEngine;
using Lumina.Core;

namespace Lumina.Signs
{
    [Serializable]
    public class SignEntry
    {
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string instruction = "Observa cómo se realiza la seña.";
        [TextArea(1, 3)] public string instructionEn = "";

        /// <summary>Descripción en el idioma activo; si falta la inglesa se usa la española.</summary>
        public string GetInstruction()
        {
            if (Loc.Current == GameLanguage.English && !string.IsNullOrWhiteSpace(instructionEn)) return instructionEn;
            return string.IsNullOrWhiteSpace(instruction) ? Loc.T("practice.defaultInstruction") : instruction;
        }
    }

    [Serializable]
    public class SignCategory
    {
        public string id;
        public string displayName;

        /// <summary>Nombre de la categoría en el idioma activo (id «cat.&lt;id&gt;» de la tabla); si no existe, el nombre del asset.</summary>
        public string LocalizedName => Loc.Has("cat." + id) ? Loc.T("cat." + id) : displayName;

        public SignEntry[] signs = Array.Empty<SignEntry>();
    }

    /// <summary>Categorías y señas disponibles para practicar. Añadir una categoría es añadir un elemento al asset.</summary>
    [CreateAssetMenu(menuName = "Lumina/Sign Catalog", fileName = "SignCatalog")]
    public class SignCatalog : ScriptableObject
    {
        public SignCategory[] categories = Array.Empty<SignCategory>();
    }

    /// <summary>Resultado de un intento de práctica. Lo rellenará el sistema de reconocimiento.</summary>
    public readonly struct SignAttemptResult
    {
        public readonly string signId;
        public readonly bool evaluated;
        public readonly float precision;
        /// <summary>Estrellas y XP ganadas; negativos si el dato no existe.</summary>
        public readonly int stars;
        public readonly int xp;
        public readonly string feedback;

        public SignAttemptResult(string signId, bool evaluated, float precision, int stars = -1, int xp = -1, string feedback = null)
        {
            this.signId = signId; this.evaluated = evaluated; this.precision = precision;
            this.stars = stars; this.xp = xp; this.feedback = feedback;
        }
    }

    /// <summary>
    /// Punto de integración para el reconocimiento (MediaPipe). PracticeController lo invoca durante el estado de práctica;
    /// mientras no haya implementación, la práctica no produce resultados.
    /// </summary>
    public interface ISignRecognizer
    {
        void Begin(SignEntry sign, UnityEngine.UI.RawImage cameraView);
        void Stop();
        event Action<SignAttemptResult> Completed;
    }
}
