using System;
using Lumina.Auth;
using UnityEngine;

namespace Lumina.Core
{
    public enum GameLanguage { Spanish = 0, English = 1 }

    /// <summary>
    /// Ajustes del jugador y única fuente de verdad de idioma y tema. Volumen, cámara y calidad son del dispositivo y se guardan
    /// en PlayerPrefs. El IDIOMA y el TEMA son del jugador: viven en su perfil del backend (los aplica <c>Lumina.Api.ProfileSync</c>
    /// al iniciar sesión y los guarda cuando cambian); aquí solo están en memoria, y antes del login valen los valores por defecto
    /// (español, tema claro). Los eventos <see cref="LanguageChanged"/> / <see cref="ThemeChanged"/> avisan a la interfaz para que
    /// se actualice sin recargar escenas.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultVolume = 0.8f;
        /// <summary>Nivel de calidad tras «Restaurar»: el penúltimo (o el único) de la lista del proyecto.</summary>
        public static int DefaultQualityLevel => Mathf.Max(0, QualitySettings.names.Length - 2);
        private const string MusicKey = "settings.music", SfxKey = "settings.sfx", CameraKey = "settings.camera", QualityKey = "settings.quality";

        public static event Action LanguageChanged;
        public static event Action ThemeChanged;
        /// <summary>Se emite cuando el JUGADOR cambia el idioma o el tema (no al aplicar las preferencias de su perfil).</summary>
        public static event Action PreferencesEdited;

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

        private static GameLanguage _language = GameLanguage.Spanish;

        /// <summary>Idioma de la interfaz. Español por defecto; cambiarlo avisa a toda la UI al instante (y, con sesión, se guarda en el perfil).</summary>
        public static GameLanguage Language
        {
            get => _language;
            set
            {
                if (_language == value) return;
                _language = value;
                LanguageChanged?.Invoke();
                PreferencesEdited?.Invoke();
            }
        }

        /// <summary>Aplica idioma y tema que vienen del perfil (o los valores por defecto) sin tratarlos como un cambio del jugador.</summary>
        public static void ApplyPreferences(GameLanguage language, bool dark)
        {
            if (_language != language) { _language = language; LanguageChanged?.Invoke(); }
            SetActiveTheme(dark);
        }

        /// <summary>Enlaza el cierre de sesión con los valores por defecto (una sola vez, sin duplicar suscripciones). Se llama sola al arrancar el juego.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void BindSession()
        {
            _language = GameLanguage.Spanish; _activeDark = false; // arranque (y recarga de dominio): sin sesión, valores por defecto
            EnsureBound();
        }

        /// <summary>Suscribe el cierre de sesión (idempotente: nunca duplica).</summary>
        public static void EnsureBound()
        {
            AuthSession.SignedOut -= EndSession; AuthSession.SignedOut += EndSession;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void Trace(string message) => Debug.Log("[Tema] " + message);

        private static bool _activeDark;   // tema en uso ahora mismo; no se persiste (siempre arranca en claro)

        /// <summary>
        /// Tema ACTIVO. Leerlo da el tema que se está mostrando; asignarlo lo aplica al instante y, con sesión iniciada,
        /// se guarda en el perfil del backend (sin sesión solo queda activo en esta ejecución).
        /// </summary>
        public static bool DarkMode
        {
            get => _activeDark;
            set
            {
                if (_activeDark == value) return;
                Trace("tema cambiado por el jugador: " + (value ? "oscuro" : "claro"));
                SetActiveTheme(value);
                PreferencesEdited?.Invoke();
            }
        }

        /// <summary>Cierre de sesión confirmado: vuelven los valores por defecto (español, tema claro). Lo guardado en el perfil no se toca.</summary>
        private static void EndSession()
        {
            Trace("sesión cerrada: idioma y tema por defecto");
            ApplyPreferences(GameLanguage.Spanish, false);
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
