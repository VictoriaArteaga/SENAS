using UnityEngine;

namespace Lumina.UI
{
    /// <summary>
    /// Sena en las pantallas de acceso: dice frases cortas con la burbuja de diálogo del juego (DialogueBubble,
    /// sin su botón de acción) y reacciona con las animaciones que ya trae el avatar (Wave, Clap).
    /// </summary>
    public class SenaSpeaker : MonoBehaviour
    {
        private static readonly int WaveTrigger = Animator.StringToHash("Wave");
        private static readonly int ClapTrigger = Animator.StringToHash("Clap");

        [SerializeField] private DialogueBubble bubble;
        [Tooltip("Objeto raíz del modelo de Sena; se busca su Animator activo.")]
        [SerializeField] private GameObject senaModel;
        [SerializeField] private string speakerName = "Sena";

        private Animator _animator;

        private void Awake()
        {
            if (bubble == null) Debug.LogError($"[SenaSpeaker] Falta 'bubble' en '{name}'.", this);
            if (senaModel != null) _animator = senaModel.GetComponentInChildren<Animator>();
        }

        public void Say(string message)
        {
            if (bubble == null || !bubble.isActiveAndEnabled) return;
            if (bubble.IsOpen) bubble.SetMessage(message, string.Empty, string.Empty);
            else bubble.Show(speakerName, message, string.Empty, string.Empty);
        }

        public void Hide()
        {
            if (bubble != null) bubble.Close();
        }

        public void Wave() => Trigger(WaveTrigger);
        public void Clap() => Trigger(ClapTrigger);

        private void Trigger(int hash)
        {
            if (_animator != null && _animator.isActiveAndEnabled) _animator.SetTrigger(hash);
        }
    }
}
