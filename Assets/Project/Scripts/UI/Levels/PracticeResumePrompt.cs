using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumina.UI
{
    /// <summary>
    /// Después de cerrar la práctica (VOLVER) de una casa de aprendizaje, muestra un indicador como el de la E pero con la tecla P
    /// («Continúa practicando»). Al pulsarla se abre otra vez la práctica de esa casa. Solo aparece mientras el panel está cerrado y
    /// después de haberlo abierto al menos una vez.
    /// </summary>
    public class PracticeResumePrompt : MonoBehaviour
    {
        [Tooltip("Controlador del nivel de la casa (LearningLevelController o AlphabetLevelController): implementa IPracticePanel.")]
        [SerializeField] private MonoBehaviour level;
        [SerializeField] private InteractionPrompt prompt;

        private bool _shown;
        private IPracticePanel _panel;

        private void Update()
        {
            if (_panel == null) _panel = level as IPracticePanel;
            if (_panel == null || prompt == null) return;

            bool show = _panel.WasOpened && !_panel.IsOpen;
            if (show != _shown)
            {
                _shown = show;
                if (show) prompt.Show(); else prompt.Hide();
            }

            var keyboard = Keyboard.current;
            if (show && keyboard != null && keyboard.pKey.wasPressedThisFrame) _panel.OpenPractice();
        }
    }
}
