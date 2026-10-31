using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Mantiene un texto traducido al idioma activo. Solo actualiza el texto si nadie lo ha cambiado desde la última vez:
    /// los textos que escribe un controlador (títulos de estado, mensajes) los refresca ese controlador.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private TMP_Text _label;
        private string _last;

        private void OnEnable()
        {
            _label = GetComponent<TMP_Text>();
            GameSettings.LanguageChanged += Refresh;
            if (_last == null) _last = _label.text; // copia de plantilla: no pisar el texto que ponga el controlador
            Refresh();
        }

        private void OnDisable() => GameSettings.LanguageChanged -= Refresh;

        public static void Bind(TMP_Text label, string key)
        {
            var localized = label.GetComponent<LocalizedText>();
            if (localized == null) localized = label.gameObject.AddComponent<LocalizedText>();
            localized.key = key;
            // El inglés suele ser más largo: en rótulos de tamaño fijo el texto se reduce solo lo necesario para no cortarse ni solaparse.
            if (Loc.LongerInEnglish(key) && !label.enableAutoSizing)
            {
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = label.fontSize * 0.5f;
                label.enableAutoSizing = true;
            }
            localized._last = label.text;
            localized.Refresh();
        }

        private void Refresh()
        {
            if (_label == null || string.IsNullOrEmpty(key) || _label.text != _last) return;
            _last = Loc.T(key);
            _label.text = _last;
        }
    }

    /// <summary>Enlaza al idioma todos los textos de una escena que coinciden con una cadena de <see cref="LocTable"/>.</summary>
    public static class LocalizationBinder
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => Bind(scene);
            Bind(SceneManager.GetActiveScene());
        }

        private static void Bind(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.GetComponent<LocalizedText>() != null) continue;
                    if (label.GetComponentInParent<TMP_InputField>() != null && label.name == "Text") continue; // lo que escribe la persona
                    if (Loc.TryKeyForSpanish(label.text, out var key)) LocalizedText.Bind(label, key);
                }
            }
        }
    }
}
