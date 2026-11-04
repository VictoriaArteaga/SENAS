using System.Text.RegularExpressions;

namespace Lumina.Auth
{
    /// <summary>Validaciones locales de formularios. Devuelven null si el valor es válido, o un mensaje amable si no lo es.</summary>
    public static class AuthValidation
    {
        // Reglas del backend (POST /auth/register): contraseña de 8 a 72 caracteres con al menos una letra y un número; nombre de 2 a 40.
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 72;
        public const int MaxEmailLength = 254;
        public const int MinDisplayNameLength = 2;
        public const int MaxDisplayNameLength = 40;
        public const int MinUsernameLength = 3;
        public const int MaxUsernameLength = 20;

        // Formato básico: algo@dominio.ext, sin espacios. No pretende cubrir todo el estándar.
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
        private static readonly Regex UsernamePattern = new Regex(@"^[A-Za-z0-9_.]+$", RegexOptions.Compiled);

        public static string Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Escribe tu correo electrónico.";
            if (value.Trim().Length > MaxEmailLength) return $"¡Llegaste al máximo de {MaxEmailLength} caracteres!";
            string email = value.Trim();
            if (email.IndexOf('@') < 0) return "El correo debe tener un @.";
            return EmailPattern.IsMatch(email) ? null : "Revisa el formato: nombre@correo.com";
        }

        /// <summary>Contraseña NUEVA (registro): 8–72 caracteres, con al menos una letra y un número.</summary>
        public static string Password(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Escribe tu contraseña.";
            if (value.Length < MinPasswordLength) return $"Usa al menos {MinPasswordLength} caracteres.";
            if (value.Length > MaxPasswordLength) return $"¡Llegaste al máximo de {MaxPasswordLength} caracteres!";
            bool hasLetter = false, hasDigit = false;
            foreach (char c in value)
            {
                if (char.IsLetter(c)) hasLetter = true;
                else if (char.IsDigit(c)) hasDigit = true;
            }
            if (!hasLetter) return "Incluye al menos una letra.";
            return hasDigit ? null : "Incluye al menos un número.";
        }

        /// <summary>Contraseña al INICIAR sesión: solo se exige que exista; el backend es quien la valida.</summary>
        public static string LoginPassword(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Escribe tu contraseña.";
            return value.Length <= MaxPasswordLength ? null : $"¡Llegaste al máximo de {MaxPasswordLength} caracteres!";
        }

        /// <summary>Nombre visible (registro): 2–40 caracteres.</summary>
        public static string DisplayName(string value)
        {
            string v = value?.Trim();
            if (string.IsNullOrEmpty(v)) return "Escribe tu nombre.";
            if (v.Length < MinDisplayNameLength) return $"Usa al menos {MinDisplayNameLength} caracteres.";
            return v.Length <= MaxDisplayNameLength ? null : $"¡Llegaste al máximo de {MaxDisplayNameLength} caracteres!";
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
            if (v.Length > MaxUsernameLength) return $"¡Llegaste al máximo de {MaxUsernameLength} caracteres!";
            return UsernamePattern.IsMatch(v) ? null : "Solo letras, números, punto y guion bajo.";
        }

        public static string PasswordConfirmation(string password, string confirmation)
        {
            if (string.IsNullOrEmpty(confirmation)) return "Repite tu contraseña.";
            return confirmation == password ? null : "Las contraseñas no coinciden.";
        }
    }
}
