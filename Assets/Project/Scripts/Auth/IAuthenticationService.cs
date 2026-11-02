using System.Threading;
using System.Threading.Tasks;

namespace Lumina.Auth
{
    public enum AuthStatus
    {
        Success,
        InvalidCredentials,
        EmailAlreadyUsed,
        UsernameTaken,
        Failed
    }

    /// <summary>Resultado de una operación de autenticación. La interfaz solo conoce este tipo, no el proveedor.</summary>
    public readonly struct AuthResult
    {
        public readonly AuthStatus Status;
        public readonly string Message;
        public readonly string DisplayName;
        /// <summary>Identificador estable de la cuenta (uid del proveedor; en la simulación, el correo normalizado).</summary>
        public readonly string UserId;
        /// <summary>Avatar de la cuenta (id de AvatarCatalog) cuando el servicio lo conoce; null si no.</summary>
        public readonly string AvatarId;

        public bool Succeeded => Status == AuthStatus.Success;

        public AuthResult(AuthStatus status, string message = null, string displayName = null, string userId = null, string avatarId = null)
        {
            Status = status;
            Message = message;
            DisplayName = displayName;
            UserId = userId;
            AvatarId = avatarId;
        }

        public static AuthResult Ok(string displayName = null, string message = null, string userId = null, string avatarId = null) => new AuthResult(AuthStatus.Success, message, displayName, userId, avatarId);
        public static AuthResult Fail(AuthStatus status, string message) => new AuthResult(status, message);
    }

    /// <summary>Datos que pide el formulario de registro. No contiene lógica.</summary>
    public sealed class RegistrationData
    {
        public string PlayerName;
        public string Username;
        public string Email;
        public string Password;
        public string AvatarId;
    }

    /// <summary>
    /// Contrato de autenticación que usan los controladores de interfaz. Lo implementan el servicio HTTP del backend
    /// (<c>Lumina.Api.AuthenticationHttpService</c>, el que se usa por defecto) y la simulación
    /// <see cref="AuthenticationMockService"/> (opción de desarrollo en <c>ApiConfig</c>). Se elige asignando
    /// <see cref="AuthServices.Current"/>, sin tocar las pantallas.
    /// </summary>
    public interface IAuthenticationService
    {
        Task<AuthResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<AuthResult> RegisterAsync(RegistrationData data, CancellationToken cancellationToken = default);
        Task<AuthResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);
        Task SignOutAsync(CancellationToken cancellationToken = default);
    }
}
