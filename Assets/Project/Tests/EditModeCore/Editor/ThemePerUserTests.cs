using Lumina.Auth;
using Lumina.Core;
using NUnit.Framework;

namespace Lumina.Core.Tests
{
    /// <summary>
    /// Idioma y tema son preferencias del perfil del backend: aquí solo viven en memoria. Antes del login y tras cerrar sesión
    /// valen los valores por defecto (español, claro); lo que el jugador cambia lo avisa <see cref="GameSettings.PreferencesEdited"/>
    /// (que es lo que dispara el guardado en el perfil) y lo que viene del perfil se aplica sin avisar.
    /// </summary>
    public class ThemePerUserTests
    {
        private const string A = "11111111-1111-1111-1111-111111111111", B = "22222222-2222-2222-2222-222222222222";

        [SetUp] public void Clean() { GameSettings.BindSession(); AuthSession.Clear(); }
        [TearDown] public void Restore() => Clean();

        [Test]
        public void ArrancaEnClaroYEspanol()
        {
            Assert.IsFalse(GameSettings.DarkMode);
            Assert.AreEqual(GameLanguage.Spanish, GameSettings.Language);
        }

        [Test]
        public void ApplyPreferencesAplicaSinMarcarloComoCambioDelJugador()
        {
            int edits = 0, themeEvents = 0, languageEvents = 0;
            System.Action onEdit = () => edits++, onTheme = () => themeEvents++, onLanguage = () => languageEvents++;
            GameSettings.PreferencesEdited += onEdit; GameSettings.ThemeChanged += onTheme; GameSettings.LanguageChanged += onLanguage;
            try
            {
                GameSettings.ApplyPreferences(GameLanguage.English, true);
                Assert.IsTrue(GameSettings.DarkMode);
                Assert.AreEqual(GameLanguage.English, GameSettings.Language);
                Assert.AreEqual(0, edits, "lo que viene del perfil no es una edición del jugador");
                Assert.AreEqual(1, themeEvents);
                Assert.AreEqual(1, languageEvents);

                GameSettings.ApplyPreferences(GameLanguage.English, true); // sin cambios: sin eventos
                Assert.AreEqual(1, themeEvents);
                Assert.AreEqual(1, languageEvents);
            }
            finally { GameSettings.PreferencesEdited -= onEdit; GameSettings.ThemeChanged -= onTheme; GameSettings.LanguageChanged -= onLanguage; }
        }

        [Test]
        public void CambiarTemaOIdiomaEsUnaEdicionDelJugador()
        {
            int edits = 0;
            System.Action onEdit = () => edits++;
            GameSettings.PreferencesEdited += onEdit;
            try
            {
                GameSettings.DarkMode = true;
                GameSettings.Language = GameLanguage.English;
                Assert.AreEqual(2, edits);

                GameSettings.DarkMode = true;                    // mismo valor: no es un cambio
                GameSettings.Language = GameLanguage.English;
                Assert.AreEqual(2, edits);
            }
            finally { GameSettings.PreferencesEdited -= onEdit; }
        }

        [Test]
        public void CerrarSesionVuelveALosValoresPorDefecto()
        {
            AuthSession.SignIn("Ana", null, A);
            GameSettings.ApplyPreferences(GameLanguage.English, true);
            AuthSession.Clear();
            Assert.IsFalse(GameSettings.DarkMode, "al cerrar sesión vuelve a claro");
            Assert.AreEqual(GameLanguage.Spanish, GameSettings.Language, "y a español");
        }

        [Test]
        public void LaSiguienteCuentaNoHeredaLasPreferenciasDeLaAnterior()
        {
            AuthSession.SignIn("Ana", null, A);
            GameSettings.ApplyPreferences(GameLanguage.English, true);
            AuthSession.Clear();

            AuthSession.SignIn("Beto", null, B); // su perfil trae sus propias preferencias; sin aplicarlas, valen los por defecto
            Assert.IsFalse(GameSettings.DarkMode);
            Assert.AreEqual(GameLanguage.Spanish, GameSettings.Language);
        }

        [Test]
        public void IdiomaYTemaNoSeGuardanEnDisco()
        {
            // Se borran las claves que pudo dejar la versión anterior en este equipo: ahora el código no debe volver a escribirlas.
            UnityEngine.PlayerPrefs.DeleteKey("settings.language");
            UnityEngine.PlayerPrefs.DeleteKey("settings.dark.user." + A);
            GameSettings.ApplyPreferences(GameLanguage.English, true);
            GameSettings.DarkMode = false; GameSettings.Language = GameLanguage.Spanish;
            GameSettings.Save();
            Assert.IsFalse(UnityEngine.PlayerPrefs.HasKey("settings.language"));
            Assert.IsFalse(UnityEngine.PlayerPrefs.HasKey("settings.dark.user." + A));
        }

        [Test]
        public void ElAvatarSeBorraAlCerrarSesion()
        {
            AuthSession.SignIn("Ana", "luna", A);
            Assert.AreEqual("luna", AuthSession.AvatarId);
            AuthSession.Clear();
            Assert.IsNull(AuthSession.AvatarId);
        }
    }
}
