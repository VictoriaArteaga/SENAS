using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>Cabecera común de las pantallas: título y botón VOLVER (a la escena indicada, por defecto el menú principal).</summary>
    public class SceneHeaderUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text titleShadow;
        [SerializeField] private Button backButton;
        [SerializeField] private string backScene = SceneNames.MainMenu;

        private string _titleKey;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(GoBack);
        }

        private void OnEnable() => GameSettings.LanguageChanged += RefreshTitle;
        private void OnDisable() => GameSettings.LanguageChanged -= RefreshTitle;

        private void Update()
        {
            // Escape equivale a VOLVER en todas las pantallas, igual que cerrar el aviso del menú.
            if (backButton != null && backButton.IsInteractable() && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                backButton.onClick.Invoke();
        }

        /// <summary>Fija el título con un id de localización (p. ej. «title.practice»); se traduce al cambiar el idioma.</summary>
        public void SetTitle(string titleKey)
        {
            _titleKey = titleKey;
            RefreshTitle();
        }

        private void RefreshTitle()
        {
            if (_titleKey == null) return;
            string title = Loc.T(_titleKey);
            if (titleLabel != null) titleLabel.text = title;
            if (titleShadow != null) titleShadow.text = title;
        }

        /// <summary>Cambia el destino de VOLVER (p. ej. un estado interno de la pantalla en vez de otra escena).</summary>
        public void SetBackHandler(UnityEngine.Events.UnityAction handler)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(handler);
        }

        public void ResetBackHandler() => SetBackHandler(GoBack);

        private void GoBack() => ScreenFader.Load(backScene);
    }
}
