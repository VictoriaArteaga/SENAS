using Lumina.Auth;
using Lumina.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lumina.Core.Tests
{
    /// <summary>El tema es preferencia de cada cuenta; el tema activo vuelve a claro al cerrar sesión.</summary>
    public class ThemePerUserTests
    {
        private const string A = "ana@correo.com", B = "beto@correo.com";

        [SetUp] public void Clean() { GameSettings.BindSession(); AuthSession.Clear(); PlayerPrefs.DeleteKey("settings.dark.user." + A); PlayerPrefs.DeleteKey("settings.dark.user." + B); }
        [TearDown] public void Restore() => Clean();

        [Test]
        public void FlujoDeDosUsuarios()
        {
            int events = 0; System.Action count = () => events++;
            GameSettings.ThemeChanged += count;
            try
            {
                AuthSession.SignIn("Ana", null, A);
                Assert.IsFalse(GameSettings.DarkMode, "cuenta sin preferencia empieza en claro");
                GameSettings.DarkMode = true;                                   // P1
                AuthSession.Clear();
                Assert.IsFalse(GameSettings.DarkMode, "al cerrar sesión vuelve a claro");
                Assert.IsTrue(GameSettings.GetThemePreference(A), "la preferencia de A se conserva");

                AuthSession.SignIn("Ana", null, "  ANA@correo.com ");             // P2 (id normalizado)
                Assert.IsTrue(GameSettings.DarkMode);
                AuthSession.Clear();

                AuthSession.SignIn("Beto", null, B);                            // P3
                Assert.IsFalse(GameSettings.DarkMode, "B no hereda el oscuro de A");
                GameSettings.DarkMode = true; GameSettings.DarkMode = false;    // P4
                AuthSession.Clear();
                AuthSession.SignIn("Beto", null, B);
                Assert.IsFalse(GameSettings.DarkMode);
                AuthSession.Clear();

                AuthSession.SignIn("Ana", null, A);                             // P5
                Assert.IsTrue(GameSettings.DarkMode);

                ResetIsolated();                                                // P7
                Assert.IsFalse(GameSettings.DarkMode);
                Assert.IsFalse(GameSettings.GetThemePreference(A));
                Assert.IsFalse(GameSettings.GetThemePreference(B), "B seguía en claro");
                AuthSession.Clear();
                GameSettings.DarkMode = true;                                   // sin sesión: no se guarda
                Assert.IsFalse(GameSettings.GetThemePreference(A));
                AuthSession.Clear();
                Assert.IsTrue(events > 0);
            }
            finally { GameSettings.ThemeChanged -= count; }
        }

        [Test]
        public void RestaurarSoloAfectaALaCuentaActual()
        {
            AuthSession.SignIn("Ana", null, A); GameSettings.DarkMode = true; AuthSession.Clear();
            AuthSession.SignIn("Beto", null, B); GameSettings.DarkMode = true;
            ResetIsolated();
            Assert.IsFalse(GameSettings.GetThemePreference(B));
            Assert.IsTrue(GameSettings.GetThemePreference(A), "la preferencia de A no se toca");
        }

        // ResetToDefaults también toca volumen, cámara, calidad e idioma: se guardan y restauran para no alterar el editor.
        private static void ResetIsolated()
        {
            float music = GameSettings.MusicVolume, sfx = GameSettings.SfxVolume;
            string cam = GameSettings.CameraDevice; var lang = GameSettings.Language;
            int quality = QualitySettings.GetQualityLevel(); bool hadQuality = PlayerPrefs.HasKey("settings.quality");
            try { GameSettings.ResetToDefaults(); }
            finally
            {
                GameSettings.MusicVolume = music; GameSettings.SfxVolume = sfx; GameSettings.CameraDevice = cam; GameSettings.Language = lang;
                QualitySettings.SetQualityLevel(quality, true);
                if (hadQuality) PlayerPrefs.SetInt("settings.quality", quality); else PlayerPrefs.DeleteKey("settings.quality");
                GameSettings.Save();
            }
        }

        [Test]
        public void CuentaNuevaNoHeredaYEmpiezaEnClaro()
        {
            AuthSession.SignIn("Ana", null, A); GameSettings.DarkMode = true; AuthSession.Clear();
            AuthSession.SignIn("Beto", null, B);
            Assert.IsFalse(GameSettings.DarkMode);
            Assert.IsFalse(GameSettings.GetThemePreference(B));
            Assert.IsTrue(GameSettings.GetThemePreference(A));
        }
    }
}
