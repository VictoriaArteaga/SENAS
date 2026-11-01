using System;

namespace Lumina.Auth
{
    /// <summary>
    /// Punto único de acceso al servicio de autenticación. Para pasar a un servicio real basta con asignar
    /// <see cref="Current"/> al arrancar el juego; los controladores de interfaz no cambian.
    /// </summary>
    public static class AuthServices
    {
        private static IAuthenticationService _current;

        public static IAuthenticationService Current
        {
            get => _current ??= new AuthenticationMockService();
            set => _current = value;
        }
    }

    /// <summary>
    /// Estado de sesión SOLO en memoria durante la ejecución (simulación visual). No se guarda en disco,
    /// en PlayerPrefs ni en ningún servicio; desaparece al cerrar el juego.
    /// </summary>
    public static class AuthSession
    {
        public static bool IsSignedIn { get; private set; }
        public static string DisplayName { get; private set; }
        /// <summary>Identificador estable de la cuenta activa (null sin sesión). Las preferencias por usuario se guardan con él.</summary>
        public static string UserId { get; private set; }

        /// <summary>Inicio de sesión confirmado (id de cuenta). Lo escucha <c>GameSettings</c> para fijar el tema del usuario.</summary>
        public static event Action<string> SignedIn;
        /// <summary>Cierre de sesión confirmado.</summary>
        public static event Action SignedOut;

        public static string NormalizeUserId(string raw) => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant();

        /// <summary>
        /// Avatar elegido en esta ejecución (null si no se eligió ninguno). Sobrevive a «cerrar sesión»: como no hay cuentas
        /// persistentes, así el flujo Registro → Login → Mundo conserva la elección hasta que se cierre el juego o se elija otra.
        /// </summary>
        public static string AvatarId { get; private set; }

        public static void SignIn(string displayName, string avatarId = null, string userId = null)
        {
            IsSignedIn = true;
            DisplayName = displayName;
            UserId = NormalizeUserId(userId);
            SignedIn?.Invoke(UserId);
            if (!string.IsNullOrEmpty(avatarId)) AvatarId = avatarId;
        }

        /// <summary>Registra el avatar elegido (botón SELECCIONAR del registro), con o sin sesión iniciada.</summary>
        public static void SelectAvatar(string avatarId)
        {
            if (!string.IsNullOrEmpty(avatarId)) AvatarId = avatarId;
        }

        public static void Clear()
        {
            IsSignedIn = false;
            DisplayName = null;
            UserId = null;
            SignedOut?.Invoke();
        }
    }
}
