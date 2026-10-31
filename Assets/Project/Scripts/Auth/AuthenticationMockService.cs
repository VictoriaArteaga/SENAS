using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumina.Auth
{
    /// <summary>
    /// Servicio de autenticación SIMULADO, solo para probar la interfaz y la navegación.
    /// No hay cuentas reales: no guarda contraseñas ni perfiles, no usa la red y no escribe nada en disco.
    /// Acepta cualquier dato que ya haya validado el formulario, salvo estos valores reservados que permiten
    /// ensayar los estados de error:
    ///   · correo "error@lumina.test"      → credenciales inválidas (inicio de sesión) / correo en uso (registro).
    ///   · usuario "usado" (cualquier caso) → nombre de usuario ya tomado (registro).
    /// </summary>
    public sealed class AuthenticationMockService : IAuthenticationService
    {
        public const string ReservedErrorEmail = "error@lumina.test";
        public const string ReservedTakenUsername = "usado";

        private readonly int _latencyMilliseconds;

        public AuthenticationMockService(int latencyMilliseconds = 1300)
        {
            _latencyMilliseconds = Math.Max(0, latencyMilliseconds);
        }

        public async Task<AuthResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_latencyMilliseconds, cancellationToken);

            if (string.Equals(email?.Trim(), ReservedErrorEmail, StringComparison.OrdinalIgnoreCase))
                return AuthResult.Fail(AuthStatus.InvalidCredentials, "No encontramos una cuenta con esos datos.");

            return AuthResult.Ok(DisplayNameFromEmail(email));
        }

        public async Task<AuthResult> RegisterAsync(RegistrationData data, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_latencyMilliseconds, cancellationToken);

            if (string.Equals(data.Email?.Trim(), ReservedErrorEmail, StringComparison.OrdinalIgnoreCase))
                return AuthResult.Fail(AuthStatus.EmailAlreadyUsed, "Ese correo ya está en uso.");
            if (string.Equals(data.Username?.Trim(), ReservedTakenUsername, StringComparison.OrdinalIgnoreCase))
                return AuthResult.Fail(AuthStatus.UsernameTaken, "Ese nombre de usuario ya está tomado.");

            return AuthResult.Ok(data.PlayerName?.Trim());
        }

        public async Task<AuthResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_latencyMilliseconds / 2, cancellationToken);
            return AuthResult.Ok(message: "Simulación: no se envió ningún correo.");
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        private static string DisplayNameFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "Explorador";
            string local = email.Trim().Split('@')[0];
            return local.Length == 0 ? "Explorador" : char.ToUpperInvariant(local[0]) + local.Substring(1);
        }
    }
}
