using UnityEngine;
using UnityEngine.InputSystem;
using Lumina.Api;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Cuando el nivel de la casa ya está completado (todas sus señas superadas, según el backend) y el panel de práctica está cerrado,
    /// muestra un indicador como el de la E con la tecla N («Ir al siguiente nivel»). Al pulsarla entra a la casa del nivel siguiente, salvo que
    /// el backend la marque bloqueada (entonces avisa y se queda).
    /// </summary>
    public class NextLevelPrompt : MonoBehaviour
    {
        [Tooltip("Controlador del nivel de la casa (implementa IPracticePanel).")]
        [SerializeField] private MonoBehaviour level;
        [SerializeField] private InteractionPrompt prompt;
        [Tooltip("Código del nivel en el backend (ALPHABET, NUMBERS, MONTHS…).")]
        [SerializeField] private string levelCode;
        [Tooltip("Escena de la casa del nivel siguiente.")]
        [SerializeField] private string nextScene;

        private bool _shown;
        private IPracticePanel _panel;

        /// <summary>Indicador en pantalla (la N); <see cref="PreviousLevelPrompt"/> lo copia para su indicador del nivel anterior.</summary>
        public InteractionPrompt Prompt => prompt;

        /// <summary>True si todas las señas del nivel están superadas.</summary>
        public static bool IsLevelCompleted(string code)
        {
            var detail = ProgressStore.GetLevel(code);
            return detail != null && detail.totalSigns > 0 && detail.passedSigns >= detail.totalSigns;
        }

        private void Update()
        {
            if (_panel == null) _panel = level as IPracticePanel;
            if (_panel == null || prompt == null || string.IsNullOrEmpty(nextScene)) return;

            bool show = !_panel.IsOpen && IsLevelCompleted(levelCode);
            if (show != _shown)
            {
                _shown = show;
                if (show) prompt.Show(); else prompt.Hide();
            }

            var keyboard = Keyboard.current;
            if (show && keyboard != null && keyboard.nKey.wasPressedThisFrame && !LevelAccess.IsBlocked(nextScene))
                ScreenFader.Load(nextScene);
        }
    }
}
