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
        [SerializeField] private LearningLevelController level;
        [SerializeField] private InteractionPrompt prompt;

        private bool _shown;

        private void Update()
        {
            if (level == null || prompt == null) return;

            bool show = level.WasOpened && !level.IsOpen;
            if (show != _shown)
            {
                _shown = show;
                if (show) prompt.Show(); else prompt.Hide();
            }

            var keyboard = Keyboard.current;
            if (show && keyboard != null && keyboard.pKey.wasPressedThisFrame) level.OpenPractice();
        }
    }
}
