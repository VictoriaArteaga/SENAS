using System;
using System.Text.RegularExpressions;

namespace Lumina.Api
{
    /// <summary>
    /// Códigos de seña y de nivel del backend (backend/docs/integration-contract.md, sección 7). Para lo que Unity ya
    /// implementa se usan los ids de Unity: la letra suelta (A…Z, Ñ) y <c>numeros.N</c>. Meses y saludos no están
    /// implementados en Unity: su referencia es el catálogo del backend (MONTH_*, WORD_*, PHRASE_*).
    /// </summary>
    public static class SignCodes
    {
        public const string AlphabetLevel = "ALPHABET";
        public const string NumbersLevel = "NUMBERS";
        public const string MonthsLevel = "MONTHS";
        public const string GreetingsLevel = "GREETINGS_PHRASES";

        private static readonly Regex NumberPattern = new Regex(@"^numeros\.(?:[0-9]|[12][0-9]|30)$", RegexOptions.Compiled);

        /// <summary>Código de una letra del abecedario (LetterEntry.letter). La Ñ se envía tal cual.</summary>
        public static string ForLetter(string letter) =>
            string.IsNullOrWhiteSpace(letter) ? null : letter.Trim().ToUpperInvariant();

        /// <summary>True si el id es un número de Unity (<c>numeros.0</c> … <c>numeros.30</c>), que es también el código del backend.</summary>
        public static bool IsNumber(string signId) => !string.IsNullOrEmpty(signId) && NumberPattern.IsMatch(signId);

        /// <summary>Nivel del backend al que pertenece un código de seña.</summary>
        public static string LevelFor(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (IsNumber(code)) return NumbersLevel;
            if (code.StartsWith("MONTH_", StringComparison.Ordinal)) return MonthsLevel;
            if (code.StartsWith("WORD_", StringComparison.Ordinal) || code.StartsWith("PHRASE_", StringComparison.Ordinal)) return GreetingsLevel;
            return code.Length == 1 ? AlphabetLevel : null; // una letra suelta
        }

        /// <summary>
        /// Contenido que Unity ya tiene para practicar y enviar al backend (abecedario y números). Meses y saludos y frases se
        /// muestran como «próximamente» hasta que Unity los implemente.
        /// </summary>
        public static bool IsImplementedInUnity(string levelCode) => levelCode == AlphabetLevel || levelCode == NumbersLevel;

        /// <summary>Texto corto para mostrar un código de seña: la letra, o el número de «numeros.N».</summary>
        public static string DisplayName(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            return IsNumber(code) ? code.Substring("numeros.".Length) : code;
        }

        /// <summary>Nombre en español del nivel (el español es la referencia de Loc; Loc.Translate lo pasa a inglés).</summary>
        public static string SpanishLevelName(string levelCode)
        {
            switch (levelCode)
            {
                case AlphabetLevel: return "Abecedario";
                case NumbersLevel: return "Números";
                case MonthsLevel: return "Meses del año";
                case GreetingsLevel: return "Saludos y frases";
                default: return levelCode;
            }
        }
    }
}
