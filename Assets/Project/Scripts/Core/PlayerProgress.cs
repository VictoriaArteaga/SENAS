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

    /// <summary>Origen de los datos de progreso. Implementarlo con Firebase y asignarlo a <see cref="PlayerProgressService.Provider"/>.</summary>
    public interface IPlayerProgressProvider
    {
        PlayerProgress Load();
    }

    /// <summary>Punto único de acceso al progreso del jugador (mismo patrón que AuthServices).</summary>
    public static class PlayerProgressService
    {
        private static IPlayerProgressProvider _provider;

        public static IPlayerProgressProvider Provider
        {
            get => _provider ??= new EmptyProgressProvider();
            set => _provider = value;
        }

        public static PlayerProgress Load() => Provider.Load();

        /// <summary>Estado inicial real de un jugador sin historial: nada aprendido y solo el primer nivel abierto.</summary>
        // TODO(Firebase): sustituir por un proveedor que lea el progreso persistido del usuario.
        private class EmptyProgressProvider : IPlayerProgressProvider
        {
            public PlayerProgress Load() => new PlayerProgress
            {
                learningLevels = new[]
                {
                    new LearningLevelProgress { title = "Primeras señas", state = LearningLevelState.InProgress },
                    new LearningLevelProgress { title = "Mi mundo", state = LearningLevelState.Locked },
                    new LearningLevelProgress { title = "Situaciones cotidianas", state = LearningLevelState.Locked },
                    new LearningLevelProgress { title = "Reto universitario", state = LearningLevelState.Locked },
                }
            };
        }
    }
}
