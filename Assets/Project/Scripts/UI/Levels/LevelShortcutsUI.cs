using UnityEngine;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>Accesos de Practice a las cuatro casas de aprendizaje (Abecedario, Números, Meses, Saludos y frases). Usa la navegación existente (ScreenFader).</summary>
    public class LevelShortcutsUI : MonoBehaviour
    {
        [SerializeField] private Button alphabetButton;
        [SerializeField] private Button numbersButton;
        [SerializeField] private Button monthsButton;
        [SerializeField] private Button greetingsButton;

        private void Awake()
        {
            if (alphabetButton != null) alphabetButton.onClick.AddListener(() => Enter(SceneNames.Alphabet));
            numbersButton.onClick.AddListener(() => Enter(SceneNames.Numbers));
            monthsButton.onClick.AddListener(() => Enter(SceneNames.Months));
            greetingsButton.onClick.AddListener(() => Enter(SceneNames.Greetings));
        }

        /// <summary>Entra a la casa salvo que el backend marque su nivel como bloqueado (entonces avisa y se queda).</summary>
        private static void Enter(string scene)
        {
            if (LevelAccess.IsBlocked(scene)) return;
            ScreenFader.Load(scene);
        }
    }
}
