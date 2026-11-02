using System;
using System.Globalization;
using System.Text;

namespace Lumina.Api
{
    // DTO de la API (backend/docs/integration-contract.md). Son clases [Serializable] con campos públicos para JsonUtility:
    // los nombres de los campos son exactamente los del JSON. JsonUtility no distingue "null" de "ausente": un objeto o una
    // cadena que no viene queda vacío, y un número que no viene queda en 0.

    [Serializable] public class UserDto { public string id; public string email; public string role; }

    [Serializable]
    public class TokensResponse
    {
        public string accessToken;
        public string refreshToken;
        public string tokenType;
        public int expiresIn;
        public UserDto user;
    }

    [Serializable] public class PreferencesDto { public string language; public string theme; }

    [Serializable]
    public class ProfileDto
    {
        public string userId;
        public string email;
        public string displayName;
        public string avatarId;
        public PreferencesDto preferences;
        public int totalXp;
        public string createdAt;
    }

    [Serializable]
    public class LevelSummaryDto
    {
        public string code;
        public string name;
        public int position;
        public bool unlocked;
        public int requiredXp;
        public int missingXp;
        public int passedSigns;
        public int totalSigns;
        public int earnedStars;
        public int possibleStars;
    }

    [Serializable] public class LevelListDto { public LevelSummaryDto[] items; }

    public static class SignStatusNames
    {
        public const string Locked = "LOCKED";
        public const string Available = "AVAILABLE";
        public const string Passed = "PASSED";
    }

    [Serializable]
    public class LevelSignDto
    {
        public string code;
        public string name;
        public string type;
        public int position;
        public string status;
        /// <summary>0–100. 0 si todavía no hay intentos (JsonUtility no distingue null de 0; usa <see cref="bestStars"/> o el estado).</summary>
        public float bestPrecision;
        public int bestStars;
    }

    [Serializable]
    public class LevelDetailDto
    {
        public string code;
        public string name;
        public int position;
        public bool unlocked;
        public int requiredXp;
        public int missingXp;
        public int passedSigns;
        public int totalSigns;
        public int earnedStars;
        public int possibleStars;
        public LevelSignDto[] signs;
    }

    [Serializable] public class LevelReferenceDto { public string code; public string name; public int requiredXp; }
    [Serializable] public class NextLevelDto { public string code; public string name; public int requiredXp; public int missingXp; }

    [Serializable]
    public class ProgressSummaryDto
    {
        public int totalXp;
        /// <summary>Vacío (code == "") si no hay ninguno.</summary>
        public LevelReferenceDto highestUnlockedLevel;
        /// <summary>Vacío (code == "") si ya están todos desbloqueados.</summary>
        public NextLevelDto nextLevel;
        public int passedSigns;
        public int totalSigns;
        public int totalStars;
        public int totalAttempts;
        public float passRate;
    }

    [Serializable]
    public class EvaluationResponse
    {
        public string evaluationId;
        public string attemptId;
        public string signCode;
        public bool passed;
        /// <summary>0–100, tal como la guardó el backend.</summary>
        public float precision;
        /// <summary>0 si el intento no aprobó.</summary>
        public int stars;
        /// <summary>XP que otorgó el backend (solo la mejora sobre lo ya ganado en esa seña).</summary>
        public int awardedXp;
        public int totalXp;
        public float bestPrecision;
        public int bestStars;
        /// <summary>Código de la seña desbloqueada por este intento; vacío si ninguna.</summary>
        public string unlockedSign;
        public string[] unlockedLevels;
    }

    [Serializable]
    public class HistoryItemDto
    {
        public string signCode;
        public float precision;
        public bool passed;
    }

    [Serializable] public class HistoryDto { public HistoryItemDto[] items; public int total; }

    [Serializable]
    public class ApiErrorBody
    {
        public int statusCode;
        public string code;
        public string message;
        public string[] details;
        public string path;
    }

    /// <summary>
    /// Constructor mínimo de JSON para los cuerpos de las peticiones. JsonUtility serializa todos los campos (también los
    /// vacíos) y el backend rechaza propiedades no declaradas o cadenas vacías en campos opcionales, así que los cuerpos se
    /// arman a mano con solo lo que se quiere enviar.
    /// </summary>
    public sealed class JsonObject
    {
        private readonly StringBuilder _sb = new StringBuilder("{");
        private bool _first = true;

        private JsonObject Key(string name)
        {
            if (!_first) _sb.Append(',');
            _first = false;
            _sb.Append('"').Append(Escape(name)).Append("\":");
            return this;
        }

        public JsonObject Add(string name, string value)
        {
            Key(name);
            if (value == null) _sb.Append("null"); else _sb.Append('"').Append(Escape(value)).Append('"');
            return this;
        }

        public JsonObject Add(string name, int value) { Key(name); _sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
        public JsonObject Add(string name, bool value) { Key(name); _sb.Append(value ? "true" : "false"); return this; }

        /// <summary>Número decimal con punto, sin notación científica (el backend acepta como máximo 2 decimales).</summary>
        public JsonObject Add(string name, double value) { Key(name); _sb.Append(value.ToString("0.##", CultureInfo.InvariantCulture)); return this; }

        public JsonObject Add(string name, JsonObject value) { Key(name); _sb.Append(value.ToString()); return this; }

        public bool IsEmpty => _first;

        public override string ToString() => _sb.ToString() + "}";

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
