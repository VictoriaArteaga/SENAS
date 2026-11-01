using Lumina.Signs;

namespace Lumina.Core
{
    /// <summary>Lo último que se practicó, para que Results sepa qué mostrar. Solo en memoria.</summary>
    public static class PracticeSession
    {
        public static SignEntry Sign { get; private set; }
        public static SignAttemptResult? Result { get; private set; }

        /// <summary>True si la seña viene de una casa de aprendizaje: Practice abre directo en APRENDE con esa seña.</summary>
        public static bool StartAtLearning { get; set; }
        /// <summary>Escena de nivel a la que vuelve VOLVER desde APRENDE (null = flujo normal de Practice).</summary>
        public static string ReturnScene { get; set; }

        public static void Begin(SignEntry sign) { Sign = sign; Result = null; }

        /// <summary>Practica una seña elegida en una casa de aprendizaje.</summary>
        public static void BeginFromLevel(SignEntry sign, string levelScene) { Begin(sign); StartAtLearning = true; ReturnScene = levelScene; }
        public static void Finish(SignAttemptResult? result) => Result = result;
    }
}
