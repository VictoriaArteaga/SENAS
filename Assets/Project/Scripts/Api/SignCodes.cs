using System;
using System.Text.RegularExpressions;

namespace Lumina.Api
{
    /// <summary>
    /// Códigos de seña y de nivel del backend (backend/docs/integration-contract.md, sección 7). Para lo que Unity ya
    /// implementa se usan los ids de Unity para letras y números. Los saludos conservan sus ids locales para la IA y
    /// se convierten aquí a los códigos oficiales WORD_*/PHRASE_* del backend.
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

        /// <summary>Convierte los ids internos de saludos al código canónico que recibe la API.</summary>
        public static string BackendCodeFor(string signId)
        {
            switch (signId)
            {
                case "saludos.buenos_dias": return "PHRASE_GOOD_MORNING";
                case "saludos.buenas_tardes": return "PHRASE_GOOD_AFTERNOON";
                case "saludos.buenas_noches": return "PHRASE_GOOD_EVENING";
                case "saludos.hola": return "WORD_HELLO";
                case "saludos.adios": return "WORD_GOODBYE";
                case "saludos.si": return "WORD_YES";
                case "saludos.no": return "WORD_NO";
                case "saludos.como_estas": return "PHRASE_HOW_ARE_YOU";
                case "saludos.gracias": return "WORD_THANK_YOU";
                case "saludos.bien": return "PHRASE_FINE_THANK_YOU";
                case "saludos.de_nada": return "PHRASE_YOU_ARE_WELCOME";
                case "meses.enero": return "MONTH_JANUARY";
                case "meses.febrero": return "MONTH_FEBRUARY";
                case "meses.marzo": return "MONTH_MARCH";
                case "meses.abril": return "MONTH_APRIL";
                case "meses.mayo": return "MONTH_MAY";
                case "meses.junio": return "MONTH_JUNE";
                case "meses.julio": return "MONTH_JULY";
                case "meses.agosto": return "MONTH_AUGUST";
                case "meses.septiembre": return "MONTH_SEPTEMBER";
                case "meses.octubre": return "MONTH_OCTOBER";
                case "meses.noviembre": return "MONTH_NOVEMBER";
                case "meses.diciembre": return "MONTH_DECEMBER";
                default: return signId;
            }
        }

        /// <summary>Nivel del backend al que pertenece un código de seña.</summary>
        public static string LevelFor(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            code = BackendCodeFor(code);
            if (IsNumber(code)) return NumbersLevel;
            if (code.StartsWith("MONTH_", StringComparison.Ordinal)) return MonthsLevel;
            if (code.StartsWith("WORD_", StringComparison.Ordinal) || code.StartsWith("PHRASE_", StringComparison.Ordinal)) return GreetingsLevel;
            return code.Length == 1 ? AlphabetLevel : null; // una letra suelta
        }

        /// <summary>
        /// Contenido implementado en Unity y listo para evaluación en el backend.
        /// </summary>
        public static bool IsImplementedInUnity(string levelCode) =>
            levelCode == AlphabetLevel || levelCode == NumbersLevel || levelCode == GreetingsLevel || levelCode == MonthsLevel;

        /// <summary>Texto corto para mostrar un código de seña: la letra, o el número de «numeros.N».</summary>
        public static string DisplayName(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            if (code.StartsWith("MONTH_", StringComparison.Ordinal))
                foreach (var month in new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" })
                    if (BackendCodeFor("meses." + month) == code) return char.ToUpperInvariant(month[0]) + month.Substring(1);
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
