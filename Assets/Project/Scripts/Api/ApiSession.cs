using System;

namespace Lumina.Api
{
    /// <summary>
    /// Tokens de la sesión SOLO en memoria. Nunca se guardan en PlayerPrefs ni en disco: cada vez que se abre el juego
    /// el jugador inicia sesión. (Recordar sesión sería una decisión aparte.)
    /// </summary>
    public static class ApiSession
    {
        private const int ExpiryMarginSeconds = 30;

        public static string AccessToken { get; private set; }
        public static string RefreshToken { get; private set; }
        private static DateTime _accessExpiresUtc;

        public static bool HasSession => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken);

        /// <summary>True si el access token vence en menos de 30 s (se refresca antes de usarlo).</summary>
        public static bool AccessExpiresSoon => DateTime.UtcNow >= _accessExpiresUtc.AddSeconds(-ExpiryMarginSeconds);

        public static void Set(TokensResponse tokens)
        {
            if (tokens == null || string.IsNullOrEmpty(tokens.accessToken) || string.IsNullOrEmpty(tokens.refreshToken))
                throw new ApiException(ApiErrorKind.Server, 0, string.Empty, "La respuesta de autenticación no trae tokens.");
            AccessToken = tokens.accessToken;
            RefreshToken = tokens.refreshToken; // El refresh token rota en cada uso: siempre se sustituye por el nuevo.
            _accessExpiresUtc = DateTime.UtcNow.AddSeconds(tokens.expiresIn > 0 ? tokens.expiresIn : 60);
        }

        public static void Clear()
        {
            AccessToken = null;
            RefreshToken = null;
            _accessExpiresUtc = DateTime.MinValue;
        }
    }
}
