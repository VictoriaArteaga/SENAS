using Lumina.Signs;

namespace Lumina.Core
{
    /// <summary>Lo último que se practicó, para que Results sepa qué mostrar. Solo en memoria.</summary>
    public static class PracticeSession
    {
        public static SignEntry Sign { get; private set; }
        public static SignAttemptResult? Result { get; private set; }

        public static void Begin(SignEntry sign) { Sign = sign; Result = null; }
        public static void Finish(SignAttemptResult? result) => Result = result;
    }
}
