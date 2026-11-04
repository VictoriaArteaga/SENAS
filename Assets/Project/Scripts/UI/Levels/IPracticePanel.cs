namespace Lumina.UI
{
    /// <summary>
    /// Panel de práctica de una casa de aprendizaje (Abecedario, Números, Meses o Saludos). Lo usa <see cref="PracticeResumePrompt"/> para
    /// ofrecer «P — Continúa practicando» después de cerrarlo.
    /// </summary>
    public interface IPracticePanel
    {
        /// <summary>El panel está abierto ahora mismo.</summary>
        bool IsOpen { get; }

        /// <summary>El panel ya se abrió al menos una vez en esta visita.</summary>
        bool WasOpened { get; }

        /// <summary>Abre la práctica donde corresponda (la primera seña sin superar, o la primera).</summary>
        void OpenPractice();
    }
}
