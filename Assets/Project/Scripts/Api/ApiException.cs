using System;

namespace Lumina.Api
{
    public enum ApiErrorKind
    {
        /// <summary>No hay conexión o el servidor no responde.</summary>
        Network,
        /// <summary>La petición superó el tiempo máximo.</summary>
        Timeout,
        /// <summary>El servidor respondió con un error 5xx o con algo que no se pudo interpretar.</summary>
        Server,
        /// <summary>El backend rechazó la petición (4xx) con un código de error.</summary>
        Client,
        /// <summary>No hay sesión válida (401/403 de sesión) o el jugador no ha iniciado sesión.</summary>
        Auth,
        /// <summary>Falta configurar la URL de la API.</summary>
        NotConfigured
    }

    /// <summary>Error tipado de la API. Nunca se inventa un resultado: ante un fallo se lanza esta excepción.</summary>
    public sealed class ApiException : Exception
    {
        public ApiErrorKind Kind { get; }
        /// <summary>Código HTTP (0 si no hubo respuesta).</summary>
        public long StatusCode { get; }
        /// <summary>Código legible por máquina del backend (por ejemplo SIGN_LOCKED); vacío si no hubo respuesta del backend.</summary>
        public string Code { get; }
        public string[] Details { get; }

        public ApiException(ApiErrorKind kind, long statusCode, string code, string message, string[] details = null)
            : base(string.IsNullOrEmpty(message) ? code : message)
        {
            Kind = kind;
            StatusCode = statusCode;
            Code = code ?? string.Empty;
            Details = details ?? Array.Empty<string>();
        }

        /// <summary>True si reintentar la misma petición (con el mismo attemptId, si aplica) puede funcionar.</summary>
        public bool IsRetryable =>
            Kind == ApiErrorKind.Network || Kind == ApiErrorKind.Timeout ||
            (Kind == ApiErrorKind.Server && StatusCode >= 500) || StatusCode == 429;

        public static ApiException NotSignedIn() =>
            new ApiException(ApiErrorKind.Auth, 401, ApiCodes.Unauthorized, "No hay una sesión iniciada.");
    }

    /// <summary>Códigos de error del backend (backend/docs/integration-contract.md, sección 8).</summary>
    public static class ApiCodes
    {
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string InvalidSession = "INVALID_SESSION";
        public const string InactiveUser = "INACTIVE_USER";
        public const string EmailInUse = "EMAIL_IN_USE";
        public const string InvalidAvatar = "INVALID_AVATAR";
        public const string InvalidAvatarConfig = "INVALID_AVATAR_CONFIG";
        public const string LevelNotFound = "LEVEL_NOT_FOUND";
        public const string SignNotFound = "SIGN_NOT_FOUND";
        public const string LevelLocked = "LEVEL_LOCKED";
        public const string SignLocked = "SIGN_LOCKED";
        public const string DuplicateAttempt = "DUPLICATE_ATTEMPT";
        public const string InconsistentResult = "INCONSISTENT_RESULT";
        public const string TooManyRequests = "TOO_MANY_REQUESTS";
        public const string InternalError = "INTERNAL_ERROR";
    }
}
