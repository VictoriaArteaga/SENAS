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

        public bool Succeeded => Status == AuthStatus.Success;

        public AuthResult(AuthStatus status, string message = null, string displayName = null)
        {
            Status = status;
            Message = message;
            DisplayName = displayName;
        }

        public static AuthResult Ok(string displayName = null, string message = null) => new AuthResult(AuthStatus.Success, message, displayName);
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
    /// Contrato de autenticación que usan los controladores de interfaz. Hoy lo implementa
    /// <see cref="AuthenticationMockService"/>; un servicio real (por ejemplo, el de Firebase en la carpeta
    /// Scripts/Firebase) puede sustituirlo asignándolo en <see cref="AuthServices.Current"/>, sin tocar las pantallas.
    /// </summary>
    public interface IAuthenticationService
    {
        Task<AuthResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<AuthResult> RegisterAsync(RegistrationData data, CancellationToken cancellationToken = default);
        Task<AuthResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);
        Task SignOutAsync(CancellationToken cancellationToken = default);
    }
}
