using System;
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
        private const string LanguageKey = "settings.language", DarkKey = "settings.dark";

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

        /// <summary>Tema oscuro. Claro por defecto; cambiarlo avisa a toda la UI al instante.</summary>
        public static bool DarkMode
        {
            get => PlayerPrefs.GetInt(DarkKey, 0) == 1;
            set
            {
                if (DarkMode == value) return;
                PlayerPrefs.SetInt(DarkKey, value ? 1 : 0);
                Save();
                ThemeChanged?.Invoke();
            }
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
