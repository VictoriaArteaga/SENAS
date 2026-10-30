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
        public static string AvatarId { get; private set; }

        public static void SignIn(string displayName, string avatarId = null)
        {
            IsSignedIn = true;
            DisplayName = displayName;
            if (!string.IsNullOrEmpty(avatarId)) AvatarId = avatarId;
        }

        public static void Clear()
        {
            IsSignedIn = false;
            DisplayName = null;
            AvatarId = null;
        }
    }
}
