using UnityEngine;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>Accesos de Practice a las casas de aprendizaje (Números, Meses, Saludos y frases). Usa la navegación existente (ScreenFader).</summary>
    public class LevelShortcutsUI : MonoBehaviour
    {
        [SerializeField] private Button numbersButton;
        [SerializeField] private Button monthsButton;
        [SerializeField] private Button greetingsButton;

        private void Awake()
        {
            numbersButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Numbers));
            monthsButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Months));
            greetingsButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Greetings));
        }
    }
}
