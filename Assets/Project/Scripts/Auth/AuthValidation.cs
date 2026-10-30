using System.Text.RegularExpressions;

namespace Lumina.Auth
{
    /// <summary>Validaciones locales de formularios. Devuelven null si el valor es válido, o un mensaje amable si no lo es.</summary>
    public static class AuthValidation
    {
        public const int MinPasswordLength = 6;
        public const int MinUsernameLength = 3;
        public const int MaxUsernameLength = 20;

        // Formato básico: algo@dominio.ext, sin espacios. No pretende cubrir todo el estándar.
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
        private static readonly Regex UsernamePattern = new Regex(@"^[A-Za-z0-9_.]+$", RegexOptions.Compiled);

        public static string Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Escribe tu correo electrónico.";
            return EmailPattern.IsMatch(value.Trim()) ? null : "Revisa el correo: debe verse como nombre@correo.com";
        }

        public static string Password(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Escribe tu contraseña.";
            return value.Length >= MinPasswordLength ? null : $"Usa al menos {MinPasswordLength} caracteres.";
        }

        public static string Required(string value, string fieldDescription)
        {
            return string.IsNullOrWhiteSpace(value) ? $"Escribe {fieldDescription}." : null;
        }

        public static string Username(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Elige un nombre de usuario.";
            string v = value.Trim();
            if (v.Length < MinUsernameLength) return $"Usa al menos {MinUsernameLength} caracteres.";
            if (v.Length > MaxUsernameLength) return $"Usa máximo {MaxUsernameLength} caracteres.";
            return UsernamePattern.IsMatch(v) ? null : "Solo letras, números, punto y guion bajo.";
        }

        public static string PasswordConfirmation(string password, string confirmation)
        {
            if (string.IsNullOrEmpty(confirmation)) return "Repite tu contraseña.";
            return confirmation == password ? null : "Las contraseñas no coinciden.";
        }
    }
}
