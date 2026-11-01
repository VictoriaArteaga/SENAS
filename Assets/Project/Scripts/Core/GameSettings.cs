using System;
using Lumina.Auth;
using UnityEngine;

namespace Lumina.Core
{
    public enum GameLanguage { Spanish = 0, English = 1 }

    /// <summary>
    /// Ajustes locales del jugador (PlayerPrefs) y única fuente de verdad de idioma y tema. Los valores se guardan; aplicar el
    /// volumen/cámara es trabajo de cada sistema, y los eventos <see cref="LanguageChanged"/> / <see cref="ThemeChanged"/>
    /// avisan a la interfaz para que se actualice sin recargar escenas.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultVolume = 0.8f;
        /// <summary>Nivel de calidad tras «Restaurar»: el penúltimo (o el único) de la lista del proyecto.</summary>
        public static int DefaultQualityLevel => Mathf.Max(0, QualitySettings.names.Length - 2);
        private const string MusicKey = "settings.music", SfxKey = "settings.sfx", CameraKey = "settings.camera", QualityKey = "settings.quality";
        private const string LanguageKey = "settings.language", DarkKeyPrefix = "settings.dark.user.";

        public static event Action LanguageChanged;
        public static event Action ThemeChanged;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, DefaultVolume);
            set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); // TODO(audio): enviar a un AudioMixer cuando exista.
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, DefaultVolume);
            set => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); // TODO(audio): enviar a un AudioMixer cuando exista.
        }

        /// <summary>Nombre del dispositivo de cámara elegido; vacío = el primero disponible.</summary>
        public static string CameraDevice
        {
            get => PlayerPrefs.GetString(CameraKey, string.Empty);
            set => PlayerPrefs.SetString(CameraKey, value ?? string.Empty);
        }

        public static int QualityLevel
        {
            get => QualitySettings.GetQualityLevel();
            set
            {
                int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, true);
                PlayerPrefs.SetInt(QualityKey, level);
            }
        }

        /// <summary>Idioma de la interfaz. Español por defecto; cambiarlo avisa a toda la UI al instante.</summary>
        public static GameLanguage Language
        {
            get => PlayerPrefs.GetInt(LanguageKey, 0) == 1 ? GameLanguage.English : GameLanguage.Spanish;
            set
            {
                if (Language == value) return;
                PlayerPrefs.SetInt(LanguageKey, (int)value);
                Save();
                LanguageChanged?.Invoke();
            }
        }

        /// <summary>Enlaza la sesión con el tema (una sola vez, sin duplicar suscripciones). Se llama sola al arrancar el juego.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void BindSession()
        {
            _userId = null; _activeDark = false; // arranque (y recarga de dominio): sin sesión, tema claro
            EnsureBound();
        }

        /// <summary>Suscribe el tema a la sesión (idempotente: nunca duplica) y, si ya hay una sesión activa, adopta su preferencia.</summary>
        public static void EnsureBound()
        {
            AuthSession.SignedIn -= BeginSession; AuthSession.SignedIn += BeginSession;
            AuthSession.SignedOut -= EndSession; AuthSession.SignedOut += EndSession;
            if (AuthSession.IsSignedIn && _userId == null && !string.IsNullOrEmpty(AuthSession.UserId)) BeginSession(AuthSession.UserId);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void Trace(string message) => Debug.Log("[Tema] " + message);

        private static bool _activeDark;   // tema en uso ahora mismo; no se persiste (siempre arranca en claro)
        private static string _userId;     // cuenta con sesión iniciada; null = nadie

        private static string DarkKey(string userId) => DarkKeyPrefix + userId;

        /// <summary>Preferencia guardada de una cuenta (claro si nunca eligió).</summary>
        public static bool GetThemePreference(string userId) => !string.IsNullOrEmpty(userId) && PlayerPrefs.GetInt(DarkKey(userId), 0) == 1;

        /// <summary>
        /// Tema ACTIVO. Leerlo da el tema que se está mostrando; asignarlo lo aplica al instante y, con sesión iniciada,
        /// guarda además la preferencia de esa cuenta (sin sesión no se guarda nada).
        /// </summary>
        public static bool DarkMode
        {
            get => _activeDark;
            set
            {
                if (!string.IsNullOrEmpty(_userId))
                {
                    PlayerPrefs.SetInt(DarkKey(_userId), value ? 1 : 0);
                    Save();
                    Trace($"preferencia guardada: {DarkKey(_userId)} = {(value ? 1 : 0)}");
                }
                else Trace("tema cambiado SIN sesión: solo activo, no se guarda");
                SetActiveTheme(value);
            }
        }

        /// <summary>Inicio de sesión confirmado: el tema activo pasa a ser la preferencia de la cuenta (claro si nunca eligió, también en cuentas nuevas: cada cuenta tiene su propia clave).</summary>
        private static void BeginSession(string userId)
        {
            _userId = string.IsNullOrEmpty(userId) ? null : userId;
            Trace($"sesión iniciada: id='{_userId}' preferencia oscuro={GetThemePreference(_userId)}");
            SetActiveTheme(GetThemePreference(_userId));
        }

        /// <summary>Cierre de sesión confirmado: el tema activo vuelve a claro; la preferencia guardada de la cuenta no se toca.</summary>
        private static void EndSession()
        {
            _userId = null;
            Trace("sesión cerrada: tema activo -> claro");
            SetActiveTheme(false);
        }

        private static void SetActiveTheme(bool dark)
        {
            if (_activeDark == dark) return;
            _activeDark = dark;
            ThemeChanged?.Invoke();
        }

        /// <summary>Reaplica la calidad guardada; llamar al abrir una pantalla que dependa de ella.</summary>
        public static void ApplySaved()
        {
            if (PlayerPrefs.HasKey(QualityKey)) QualityLevel = PlayerPrefs.GetInt(QualityKey);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void ResetToDefaults()
        {
            MusicVolume = DefaultVolume;
            SfxVolume = DefaultVolume;
            CameraDevice = string.Empty;
            PlayerPrefs.DeleteKey(QualityKey);
            QualitySettings.SetQualityLevel(DefaultQualityLevel, true);
            Save();
            Language = GameLanguage.Spanish;
            DarkMode = false;
        }
    }
}
