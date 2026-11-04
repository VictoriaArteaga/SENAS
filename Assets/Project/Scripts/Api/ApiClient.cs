using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Lumina.Auth;
using Lumina.Core;
using Lumina.UI;

namespace Lumina.Api
{
    /// <summary>
    /// Cliente HTTP del backend sobre UnityWebRequest (async/await con Task, como el resto del proyecto).
    ///  · Añade Authorization: Bearer en las rutas protegidas.
    ///  · Ante un 401 en una ruta protegida hace UN refresh y repite la petición una vez; si el refresh falla porque la sesión
    ///    ya no es válida, cierra la sesión y lleva al jugador a Login. Nunca hay dos refresh a la vez.
    ///  · Devuelve errores tipados (<see cref="ApiException"/>); jamás inventa un resultado.
    /// </summary>
    public static class ApiClient
    {
        private static Task<bool> _refreshing;
        private static int _refreshingGeneration = -1;

        public static Task<T> GetAsync<T>(string path, CancellationToken ct = default) where T : class =>
            SendAsync<T>("GET", path, null, true, ct);

        public static Task<T> PostAsync<T>(string path, string json, bool authenticated = true, CancellationToken ct = default) where T : class =>
            SendAsync<T>("POST", path, json, authenticated, ct);

        public static Task<T> PatchAsync<T>(string path, string json, CancellationToken ct = default) where T : class =>
            SendAsync<T>("PATCH", path, json, true, ct);

        /// <summary>POST sin cuerpo de respuesta (por ejemplo /auth/logout, que responde 204).</summary>
        public static async Task PostNoContentAsync(string path, bool authenticated = true, CancellationToken ct = default) =>
            await SendWithAuthAsync("POST", path, null, authenticated, ct);

        /// <summary>Codifica un segmento de ruta (por ejemplo «Ñ» o «numeros.5»).</summary>
        public static string Segment(string value) => Uri.EscapeDataString(value ?? string.Empty);

        private static async Task<T> SendAsync<T>(string method, string path, string json, bool authenticated, CancellationToken ct) where T : class
        {
            var raw = await SendWithAuthAsync(method, path, json, authenticated, ct);
            if (string.IsNullOrWhiteSpace(raw.Text)) return null;
            string text = raw.Text.TrimStart();
            // JsonUtility no lee un arreglo en la raíz: se envuelve como {"items": [...]} (el DTO debe tener `items`).
            if (text.StartsWith("[", StringComparison.Ordinal)) text = "{\"items\":" + text + "}";
            return Parse<T>(text);
        }

        public static T Parse<T>(string text) where T : class
        {
            try
            {
                var value = JsonUtility.FromJson<T>(text);
                if (value == null) throw new ArgumentException("JSON vacío");
                return value;
            }
            catch (Exception e)
            {
                throw new ApiException(ApiErrorKind.Server, 0, string.Empty, "El servidor devolvió una respuesta que no se pudo interpretar: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- autenticación y refresh

        private static async Task<Raw> SendWithAuthAsync(string method, string path, string json, bool authenticated, CancellationToken ct)
        {
            if (!authenticated)
            {
                var open = await RawAsync(method, path, json, null, ct);
                if (open.Status >= 400) throw ToException(open);
                return open;
            }

            if (!ApiSession.HasSession) throw ApiException.NotSignedIn();
            int generation = ApiSession.TokenGeneration;
            if (ApiSession.AccessExpiresSoon) await RefreshOrThrowAsync(generation);
            if (!ApiSession.HasSession) throw SessionEnded();

            generation = ApiSession.TokenGeneration;
            string accessToken = ApiSession.AccessToken;
            var raw = await RawAsync(method, path, json, accessToken, ct);
            if (raw.Status == 401)
            {
                // Si otra petición ya rotó los tokens mientras esta estaba en vuelo, reutiliza el par nuevo.
                // El backend revoca la sesión si recibe dos veces el mismo refresh token.
                if (ApiSession.TokenGeneration == generation)
                    await RefreshOrThrowAsync(generation);
                if (!ApiSession.HasSession) throw SessionEnded();

                generation = ApiSession.TokenGeneration;
                raw = await RawAsync(method, path, json, ApiSession.AccessToken, ct);
                if (raw.Status == 401)
                {
                    // No cierres una sesión que haya sido sustituida por otra petición concurrente.
                    if (ApiSession.TokenGeneration != generation && ApiSession.HasSession)
                    {
                        generation = ApiSession.TokenGeneration;
                        raw = await RawAsync(method, path, json, ApiSession.AccessToken, ct);
                    }
                    if (raw.Status == 401 && ApiSession.TokenGeneration == generation) EndSession();
                    if (raw.Status == 401) throw SessionEnded();
                }
            }
            if (raw.Status >= 400) throw ToException(raw);
            return raw;
        }

        private static async Task RefreshOrThrowAsync(int expectedGeneration)
        {
            if (ApiSession.TokenGeneration != expectedGeneration)
            {
                if (ApiSession.HasSession) return;
                throw SessionEnded();
            }
            if (!await RefreshAsync(expectedGeneration)) throw SessionEnded();
        }

        /// <summary>
        /// Rota los tokens. True si hay tokens nuevos; false si la sesión ya no es válida (se cierra la sesión). Un fallo de red
        /// o un 5xx/429 NO cierra la sesión: se lanza el error para que el llamador lo muestre.
        /// </summary>
        public static Task<bool> RefreshAsync() => RefreshAsync(ApiSession.TokenGeneration);

        private static async Task<bool> RefreshAsync(int expectedGeneration)
        {
            if (!ApiSession.HasSession) { EndSession(); return false; }
            if (ApiSession.TokenGeneration != expectedGeneration) return ApiSession.HasSession;
            if (_refreshing != null)
            {
                Task<bool> inFlight = _refreshing;
                int inFlightGeneration = _refreshingGeneration;
                bool result = await inFlight;
                if (ApiSession.TokenGeneration != expectedGeneration) return ApiSession.HasSession;
                return inFlightGeneration == expectedGeneration && result;
            }

            var task = DoRefreshAsync(expectedGeneration, ApiSession.RefreshToken);
            _refreshing = task;
            _refreshingGeneration = expectedGeneration;
            try { return await task; }
            finally
            {
                if (_refreshing == task)
                {
                    _refreshing = null;
                    _refreshingGeneration = -1;
                }
            }
        }

        private static async Task<bool> DoRefreshAsync(int expectedGeneration, string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                if (ApiSession.TokenGeneration == expectedGeneration) EndSession();
                return ApiSession.HasSession;
            }

            var body = new JsonObject().Add("refreshToken", refreshToken).ToString();
            var raw = await RawAsync("POST", "/auth/refresh", body, null, default);
            if (raw.Status == 200)
            {
                if (ApiSession.TokenGeneration != expectedGeneration) return ApiSession.HasSession;
                ApiSession.Set(Parse<TokensResponse>(raw.Text));
                return true;
            }
            if (ApiSession.TokenGeneration != expectedGeneration) return ApiSession.HasSession;
            if (raw.Status == 401 || raw.Status == 403) { EndSession(); return false; }
            throw ToException(raw);
        }

        private static ApiException SessionEnded() =>
            new ApiException(ApiErrorKind.Auth, 401, ApiCodes.InvalidSession, "La sesión terminó. Inicia sesión de nuevo.");

        /// <summary>Borra los tokens y la sesión en memoria y lleva al jugador a Login (salvo que ya esté ahí).</summary>
        public static void EndSession(bool navigateToLogin = true)
        {
            bool hadSession = ApiSession.HasSession || AuthSession.IsSignedIn;
            ApiSession.Clear();
            if (!hadSession) return;
            AuthSession.Clear(); // avisa a los almacenes (perfil, progreso) para que borren su caché

            if (!navigateToLogin || !Application.isPlaying) return;
            if (SceneManager.GetActiveScene().name == SceneNames.Login) return;
            Toast.Show(Loc.T("api.err.sessionEnded"));
            ScreenFader.Load(SceneNames.Login);
        }

        // ---------------------------------------------------------------- petición única

        private struct Raw { public long Status; public string Text; }

        private static async Task<Raw> RawAsync(string method, string path, string json, string bearer, CancellationToken ct)
        {
            var config = ApiConfig.Current;
            string baseUrl = config.BaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
                throw new ApiException(ApiErrorKind.NotConfigured, 0, string.Empty, "La URL de la API no está configurada (ApiConfig).");
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new ApiException(ApiErrorKind.NotConfigured, 0, string.Empty, "En producción la API debe usar https.");
#endif
            ct.ThrowIfCancellationRequested();

            using (var request = new UnityWebRequest(baseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Accept", "application/json");
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(bearer)) request.SetRequestHeader("Authorization", "Bearer " + bearer);
                request.timeout = config.TimeoutSeconds;

                var done = new TaskCompletionSource<bool>();
                var operation = request.SendWebRequest();
                operation.completed += _ => done.TrySetResult(true);
                using (ct.Register(() => { request.Abort(); done.TrySetCanceled(); }))
                    await done.Task;

                switch (request.result)
                {
                    case UnityWebRequest.Result.Success:
                    case UnityWebRequest.Result.ProtocolError: // 4xx/5xx: el cuerpo trae el error del backend
                        return new Raw { Status = request.responseCode, Text = request.downloadHandler.text };
                    case UnityWebRequest.Result.ConnectionError:
                        if (IsTimeout(request.error))
                            throw new ApiException(ApiErrorKind.Timeout, 0, string.Empty, "El servidor tardó demasiado en responder.");
                        throw new ApiException(ApiErrorKind.Network, 0, string.Empty, "No hay conexión con el servidor.");
                    default:
                        throw new ApiException(ApiErrorKind.Server, request.responseCode, string.Empty, "No se pudo leer la respuesta del servidor.");
                }
            }
        }

        private static bool IsTimeout(string error) =>
            !string.IsNullOrEmpty(error) && error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;

        private static ApiException ToException(Raw raw)
        {
            ApiErrorBody body = null;
            if (!string.IsNullOrWhiteSpace(raw.Text) && raw.Text.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                try { body = JsonUtility.FromJson<ApiErrorBody>(raw.Text); } catch { /* cuerpo que no es del backend */ }
            }

            string code = body != null && body.code != null ? body.code : string.Empty;
            string message = body != null && !string.IsNullOrEmpty(body.message) ? body.message : "Error " + raw.Status;
            ApiErrorKind kind =
                raw.Status >= 500 ? ApiErrorKind.Server :
                code == ApiCodes.Unauthorized || code == ApiCodes.InvalidSession ? ApiErrorKind.Auth :
                ApiErrorKind.Client;
            return new ApiException(kind, raw.Status, code, message, body?.details);
        }
    }
}
