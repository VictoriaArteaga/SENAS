using System;

namespace Lumina.Core
{
    public enum LearningLevelState { Locked, InProgress, Completed }

    [Serializable]
    public class LearningLevelProgress
    {
        public string title;
        public LearningLevelState state;
    }

    /// <summary>Datos de progreso que muestra «MI PROGRESO». Solo datos: sin lógica ni dependencia de la interfaz.</summary>
    [Serializable]
    public class PlayerProgress
    {
        public int level = 1;
        public string levelTitle = "EXPLORADOR DE LÚMINA";
        public int xp;
        /// <summary>XP necesaria para el siguiente nivel; 0 si todavía no se conoce.</summary>
        public int xpForNextLevel;
        public int signsLearned;
        /// <summary>Precisión media 0-100; negativa si aún no hay intentos evaluados.</summary>
        public float averagePrecision = -1f;
        public int stars;
        public LearningLevelProgress[] learningLevels;

        public float XpFraction => xpForNextLevel > 0 ? Math.Clamp((float)xp / xpForNextLevel, 0f, 1f) : 0f;
    }

    /// <summary>Origen de los datos de progreso. El de producción es <c>Lumina.Api.BackendProgressProvider</c> (caché del backend).</summary>
    public interface IPlayerProgressProvider
    {
        PlayerProgress Load();
    }

    /// <summary>Punto único de acceso al progreso del jugador (mismo patrón que AuthServices). No guarda nada en local.</summary>
    public static class PlayerProgressService
    {
        private static IPlayerProgressProvider _provider;

        public static IPlayerProgressProvider Provider
        {
            get => _provider ??= new Lumina.Api.BackendProgressProvider();
            set => _provider = value;
        }

        public static PlayerProgress Load() => Provider.Load();
    }
}
