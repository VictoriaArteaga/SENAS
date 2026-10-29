using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>
    /// Conecta los botones del menú principal. Solo gestiona avisos informativos y la salida de la
    /// aplicación; la navegación entre escenas y los datos de usuario quedan para fases posteriores.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Botones principales")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button progressButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Aviso informativo")]
        [SerializeField] private GameObject noticePanel;
        [SerializeField] private TMP_Text noticeTitle;
        [SerializeField] private TMP_Text noticeBody;
        [SerializeField] private Button noticeCloseButton;

        [Header("Preparado para la fase siguiente")]
        [SerializeField] private string worldSceneName = "LuminaWorld";

        private Selectable _returnSelection;

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayClicked);
            practiceButton.onClick.AddListener(() => ShowComingSoon(practiceButton, "PRÁCTICA"));
            progressButton.onClick.AddListener(() => ShowComingSoon(progressButton, "MI PROGRESO"));
            settingsButton.onClick.AddListener(() => ShowComingSoon(settingsButton, "CONFIGURACIÓN"));
            quitButton.onClick.AddListener(OnQuitClicked);
            noticeCloseButton.onClick.AddListener(CloseNotice);
            noticePanel.SetActive(false);
        }

        private void Start()
        {
            // Permite navegar con teclado o mando desde el primer fotograma.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        private void Update()
        {
            if (noticePanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseNotice();
        }

        private void OnPlayClicked()
        {
            // La carga de worldSceneName se conecta en la fase de navegación entre escenas.
            Debug.Log($"[MainMenu] JUGAR: pendiente de conectar con la escena '{worldSceneName}'.");
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            ShowNotice(quitButton, "SALIR", "La salida solo funciona en una compilación. En el Editor, detén el modo Play.");
#else
            Application.Quit();
#endif
        }

        private void ShowComingSoon(Selectable origin, string featureName)
        {
            ShowNotice(origin, featureName, "Esta función estará disponible próximamente.");
        }

        private void ShowNotice(Selectable origin, string title, string body)
        {
            _returnSelection = origin;
            noticeTitle.text = title;
            noticeBody.text = body;
            noticePanel.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(noticeCloseButton.gameObject);
        }

        private void CloseNotice()
        {
            noticePanel.SetActive(false);
            if (_returnSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_returnSelection.gameObject);
        }
    }
}
