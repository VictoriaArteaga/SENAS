using System;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Auth;
using Lumina.Core;
using Lumina.UI;
using UnityEngine;

namespace Lumina.Api
{
    /// <summary>
    /// Perfil del jugador en memoria (copia de lo que dice el backend). Se carga al iniciar sesión y se borra al cerrarla.
    /// Los cambios se guardan con PATCH; nada se guarda en disco.
    /// </summary>
    public static class ProfileStore
    {
        public static ProfileDto Current { get; private set; }

        public static event Action Changed;

        public static async Task<ProfileDto> LoadAsync(CancellationToken ct = default)
        {
            Current = await ApiClient.GetAsync<ProfileDto>("/profile", ct);
            Changed?.Invoke();
            return Current;
        }

        /// <summary>PATCH /profile. Solo se envía lo que no es null (nombre y/o preferencias de idioma y tema).</summary>
        public static async Task<ProfileDto> UpdateAsync(string displayName, string language, string theme, CancellationToken ct = default)
        {
            var body = new JsonObject();
            if (displayName != null) body.Add("displayName", displayName);
            if (language != null || theme != null)
            {
                var preferences = new JsonObject();
                if (language != null) preferences.Add("language", language);
                if (theme != null) preferences.Add("theme", theme);
                body.Add("preferences", preferences);
            }
            if (body.IsEmpty) return Current;

            Current = await ApiClient.PatchAsync<ProfileDto>("/profile", body.ToString(), ct);
            Changed?.Invoke();
            return Current;
        }

        /// <summary>PATCH /profile/avatar con un id de AvatarCatalog (luna, nube, rio, sol).</summary>
        public static async Task<ProfileDto> UpdateAvatarAsync(string avatarId, CancellationToken ct = default)
        {
            var body = new JsonObject().Add("avatarId", avatarId).ToString();
            Current = await ApiClient.PatchAsync<ProfileDto>("/profile/avatar", body, ct);
            AuthSession.SelectAvatar(Current.avatarId);
            Changed?.Invoke();
            return Current;
        }

        public static void Clear()
        {
            Current = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Conecta el perfil con los ajustes: al iniciar sesión aplica el idioma y el tema guardados en el backend; cuando el
    /// jugador los cambia con sesión iniciada, los guarda en su perfil (agrupando cambios seguidos en un solo PATCH).
    /// Antes del login y tras cerrar sesión el juego usa los valores por defecto (español, tema claro).
    /// </summary>
    public static class ProfileSync
    {
        private static bool _saving, _dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bind()
        {
            _saving = _dirty = false;
            AuthSession.SignedIn -= OnSignedIn; AuthSession.SignedIn += OnSignedIn;
            AuthSession.SignedOut -= OnSignedOut; AuthSession.SignedOut += OnSignedOut;
            GameSettings.PreferencesEdited -= OnEdited; GameSettings.PreferencesEdited += OnEdited;
        }

        private static void OnSignedIn(string userId)
        {
            var preferences = ProfileStore.Current?.preferences;
            var language = preferences != null && preferences.language == "en" ? GameLanguage.English : GameLanguage.Spanish;
            bool dark = preferences != null && preferences.theme == "dark";
            GameSettings.ApplyPreferences(language, dark);
        }

        private static void OnSignedOut() => ProfileStore.Clear();

        private static async void OnEdited()
        {
            if (!AuthSession.IsSignedIn || !ApiSession.HasSession) return;
            if (_saving) { _dirty = true; return; }
            _saving = true;
            try
            {
                do
                {
                    _dirty = false;
                    string language = GameSettings.Language == GameLanguage.English ? "en" : "es";
                    string theme = GameSettings.DarkMode ? "dark" : "light";
                    await ProfileStore.UpdateAsync(null, language, theme);
                } while (_dirty);
            }
            catch (ApiException e)
            {
                Toast.Show(ApiMessages.For(e));
            }
            finally
            {
                _saving = false;
            }
        }
    }
}
