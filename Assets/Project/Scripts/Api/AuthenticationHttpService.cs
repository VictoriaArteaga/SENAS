using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Lumina.Auth;
using Lumina.Core;

namespace Lumina.Api
{
    /// <summary>
    /// Implementación de <see cref="IAuthenticationService"/> sobre el backend (POST /auth/register, /auth/login,
    /// /auth/logout). Los tokens quedan en <see cref="ApiSession"/>, solo en memoria. Tras entrar con éxito carga el perfil,
    /// de modo que el nombre, el avatar y las preferencias salen del backend.
    ///
    /// Notas del contrato:
    ///  · El backend no tiene «nombre de usuario»: el campo del formulario no se envía (el nombre visible es <c>PlayerName</c>).
    ///  · El backend aún no ofrece recuperación de contraseña: <see cref="RequestPasswordResetAsync"/> responde que no está disponible.
    /// </summary>
    public sealed class AuthenticationHttpService : IAuthenticationService
    {
        public async Task<AuthResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var body = new JsonObject().Add("email", email).Add("password", password).ToString();
                var tokens = await ApiClient.PostAsync<TokensResponse>("/auth/login", body, authenticated: false, ct: cancellationToken);
                return await CompleteAsync(tokens, cancellationToken);
            }
            catch (ApiException e)
            {
                return Fail(e);
            }
        }

        public async Task<AuthResult> RegisterAsync(RegistrationData data, CancellationToken cancellationToken = default)
        {
            try
            {
                var body = new JsonObject()
                    .Add("email", data.Email)
                    .Add("password", data.Password)
                    .Add("displayName", data.PlayerName);
                if (!string.IsNullOrEmpty(data.AvatarId)) body.Add("avatarId", data.AvatarId);

                var tokens = await ApiClient.PostAsync<TokensResponse>("/auth/register", body.ToString(), authenticated: false, ct: cancellationToken);
                return await CompleteAsync(tokens, cancellationToken);
            }
            catch (ApiException e)
            {
                return Fail(e);
            }
        }

        public Task<AuthResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthResult.Fail(AuthStatus.Failed, Loc.T("auth.resetUnavailable")));

        public async Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (ApiSession.HasSession) await ApiClient.PostNoContentAsync("/auth/logout", true, cancellationToken);
            }
            catch (ApiException e)
            {
                // Cerrar sesión nunca debe quedar bloqueado por la red: igual se borran los tokens locales.
                Debug.LogWarning("[Api] El logout no llegó al servidor: " + e.Message);
            }
            finally
            {
                ApiSession.Clear();
            }
        }

        /// <summary>Guarda los tokens y carga el perfil. Si el perfil no se puede cargar, no se deja una sesión a medias.</summary>
        private static async Task<AuthResult> CompleteAsync(TokensResponse tokens, CancellationToken ct)
        {
            ApiSession.Set(tokens);
            try
            {
                var profile = await ProfileStore.LoadAsync(ct);
                return AuthResult.Ok(profile.displayName, userId: tokens.user?.id ?? profile.userId, avatarId: profile.avatarId);
            }
            catch
            {
                ApiSession.Clear();
                ProfileStore.Clear();
                throw;
            }
        }

        private static AuthResult Fail(ApiException e)
        {
            string message = ApiMessages.For(e);
            switch (e.Code)
            {
                case ApiCodes.InvalidCredentials: return AuthResult.Fail(AuthStatus.InvalidCredentials, message);
                case ApiCodes.EmailInUse: return AuthResult.Fail(AuthStatus.EmailAlreadyUsed, message);
                default: return AuthResult.Fail(AuthStatus.Failed, message);
            }
        }
    }

    /// <summary>
    /// Elige el servicio de autenticación al arrancar el juego: el HTTP del backend, o la simulación si
    /// <see cref="ApiConfig.UseMockAuthentication"/> está activado (solo para desarrollo de interfaz).
    /// </summary>
    internal static class AuthBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ChooseService()
        {
            if (ApiConfig.Current.UseMockAuthentication)
            {
                AuthServices.Current = new AuthenticationMockService();
                Debug.LogWarning("[Api] Autenticación SIMULADA (ApiConfig.useMockAuthentication). El progreso y las prácticas necesitan el backend.");
            }
            else
            {
                AuthServices.Current = new AuthenticationHttpService();
            }
        }
    }
}
