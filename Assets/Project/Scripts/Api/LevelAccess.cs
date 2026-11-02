using Lumina.Core;
using Lumina.UI;

namespace Lumina.Api
{
    /// <summary>Decide si se puede entrar a una casa de aprendizaje según lo que el backend dice del nivel (bloqueado por XP).</summary>
    public static class LevelAccess
    {
        /// <summary>Nivel del backend que corresponde a una escena de casa (null si la escena no es una casa).</summary>
        public static string LevelOf(string sceneName)
        {
            switch (sceneName)
            {
                case SceneNames.Numbers: return SignCodes.NumbersLevel;
                case SceneNames.Months: return SignCodes.MonthsLevel;
                case SceneNames.Greetings: return SignCodes.GreetingsLevel;
                default: return null;
            }
        }

        /// <summary>
        /// True (y avisa al jugador) si el backend marca el nivel de esa escena como bloqueado. Si todavía no se conoce el estado
        /// (sin sesión o sin datos cargados) no se bloquea la entrada: las prácticas ya exigen el estado del backend.
        /// </summary>
        public static bool IsBlocked(string sceneName)
        {
#if UNITY_EDITOR
            // Permite probar capturas/evaluación de saludos y meses antes de desbloquear esos niveles. Las pruebas locales
            // no se envían al backend ni otorgan XP; los builds conservan el bloqueo oficial.
            if (sceneName == SceneNames.Greetings || sceneName == SceneNames.Months) return false;
#endif
            string code = LevelOf(sceneName);
            if (code == null) return false;
            var level = ProgressStore.FindLevel(code);
            if (level == null || level.unlocked) return false;
            Toast.Show(Loc.T("levels.levelLocked", level.missingXp.ToString("N0")));
            return true;
        }
    }
}
