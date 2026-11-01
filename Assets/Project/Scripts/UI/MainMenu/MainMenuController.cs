using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Conecta los botones del menú principal. Gestiona la carga del mundo, los avisos informativos y la
    /// salida de la aplicación; los datos de usuario quedan para fases posteriores.
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

        [Header("Navegación")]
        [SerializeField] private string worldSceneName = "LuminaWorld";

        private Selectable _returnSelection;
        private string _noticeTitleKey, _noticeBodyKey;

        private void OnEnable() => GameSettings.LanguageChanged += RefreshNotice;
        private void OnDisable() => GameSettings.LanguageChanged -= RefreshNotice;

        private void RefreshNotice()
        {
            if (_noticeTitleKey == null) return;
            noticeTitle.text = Loc.T(_noticeTitleKey);
            noticeBody.text = Loc.T(_noticeBodyKey);
        }

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayClicked);
            practiceButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Practice));
            progressButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Profile));
            settingsButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Settings));
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
            if (string.IsNullOrWhiteSpace(worldSceneName))
            {
                Debug.LogError("[MainMenu] El nombre de la escena del mundo está vacío.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(worldSceneName))
            {
                Debug.LogError($"[MainMenu] La escena '{worldSceneName}' no está registrada en Build Settings.");
                return;
            }

            SceneManager.LoadScene(worldSceneName);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            ShowNotice(quitButton, "menu.quit", "menu.quitEditor");
#else
            Application.Quit();
#endif
        }

        private void ShowNotice(Selectable origin, string titleKey, string bodyKey)
        {
            _returnSelection = origin;
            _noticeTitleKey = titleKey; _noticeBodyKey = bodyKey;
            RefreshNotice();
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
