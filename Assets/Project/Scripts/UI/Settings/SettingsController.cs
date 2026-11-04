using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>CONFIGURACIÓN: enlaza los controles con <see cref="GameSettings"/>. Los cambios se guardan y se aplican al instante.</summary>
    public class SettingsController : MonoBehaviour
    {
        // Nombres de idioma en su propia lengua: no se traducen.
        private static readonly string[] LanguageNames = { "Español", "English" };

        [SerializeField] private SceneHeaderUI header;

        [Header("Audio")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sfxValue;

        [Header("Cámara")]
        [SerializeField] private OptionSelectorUI cameraSelector;
        [SerializeField] private Button testCameraButton;
        [SerializeField] private TMP_Text testCameraLabel;
        [SerializeField] private CameraFeedUI cameraFeed;

        [Header("Preferencias")]
        [SerializeField] private OptionSelectorUI languageSelector;
        [SerializeField] private OptionSelectorUI themeSelector;

        [Header("Personalización")]
        [Tooltip("Abre la pantalla para elegir otro avatar (mismo selector que el registro).")]
        [SerializeField] private Button changeAvatarButton;

        [Header("Acciones")]
        [SerializeField] private Button restoreButton;

        private string[] _cameras = new string[0];
        private float _nextSfxPreview;

        private void OnEnable() => GameSettings.LanguageChanged += RefreshLabels;
        private void OnDisable() => GameSettings.LanguageChanged -= RefreshLabels;

        private void Start()
        {
            header.SetTitle("title.settings");
            musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; ShowPercent(musicValue, v); });
            // Los efectos de sonido son los del juego: al mover el control suena el de «seña superada» (sin repetirse a cada pixel) y al
            // soltarlo el de «nivel completado», para oír el volumen elegido.
            sfxSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.SfxVolume = v;
                ShowPercent(sfxValue, v);
                if (v > 0f && Time.unscaledTime >= _nextSfxPreview)
                {
                    _nextSfxPreview = Time.unscaledTime + 0.45f;
                    GameAudio.PlaySignPassed();
                }
            });
            var release = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            release.callback.AddListener(_ => { if (GameSettings.SfxVolume > 0f) GameAudio.PlayLevelCompleted(); });
            var trigger = sfxSlider.gameObject.GetComponent<EventTrigger>() ?? sfxSlider.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Add(release);
            cameraSelector.Changed += i => { GameSettings.CameraDevice = _cameras[i]; GameSettings.Save(); if (cameraFeed.IsRunning) cameraFeed.Show(_cameras[i]); };
            languageSelector.Changed += i => GameSettings.Language = i == 1 ? GameLanguage.English : GameLanguage.Spanish;
            themeSelector.Changed += i => GameSettings.DarkMode = i == 1;
            testCameraButton.onClick.AddListener(ToggleTest);
            if (changeAvatarButton != null) changeAvatarButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.AvatarChange));
            restoreButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); Refresh(); });

            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(musicSlider.gameObject);
        }

        private void Refresh()
        {
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            ShowPercent(musicValue, musicSlider.value);
            ShowPercent(sfxValue, sfxSlider.value);

            languageSelector.SetOptions(LanguageNames, (int)GameSettings.Language);
            themeSelector.SetOptions(ThemeNames(), GameSettings.DarkMode ? 1 : 0);

            var devices = WebCamTexture.devices;
            _cameras = new string[devices.Length];
            int selected = 0;
            for (int i = 0; i < devices.Length; i++)
            {
                _cameras[i] = devices[i].name;
                if (devices[i].name == GameSettings.CameraDevice) selected = i;
            }
            cameraSelector.SetOptions(_cameras, selected);
            testCameraButton.interactable = _cameras.Length > 0;
            cameraFeed.Stop();
            RefreshTest();
        }

        /// <summary>Textos que dependen del idioma y no los gestiona otro componente: nombres del selector de tema y botón de prueba.</summary>
        private void RefreshLabels()
        {
            themeSelector.SetOptions(ThemeNames(), GameSettings.DarkMode ? 1 : 0);
            RefreshTest();
        }

        private static string[] ThemeNames() => new[] { Loc.T("settings.light"), Loc.T("settings.dark") };

        private void ToggleTest()
        {
            if (cameraFeed.IsRunning) cameraFeed.Stop();
            else cameraFeed.Show(_cameras.Length > 0 ? _cameras[cameraSelector.Index] : null);
            RefreshTest();
        }

        private void RefreshTest() => testCameraLabel.text = Loc.T(cameraFeed.IsRunning ? "settings.stop" : "settings.test");

        private static void ShowPercent(TMP_Text label, float value) => label.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
