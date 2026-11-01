using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>Nombres de escena del flujo de acceso. Deben coincidir con Build Settings.</summary>
    public static class SceneNames
    {
        public const string Login = "LoginScene";
        public const string Register = "RegisterScene";
        public const string MainMenu = "MainMenu";
        public const string World = "LuminaWorld";
        public const string Practice = "Practice";
        public const string Profile = "Profile"; // Pantalla «MI PROGRESO».
        public const string Settings = "Settings";
        public const string Results = "Results";
    }

    /// <summary>
    /// Transición suave entre escenas (fundido a crema). Se crea sola la primera vez que se usa y sobrevive a la carga.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        private CanvasGroup _group;
        private UnityEngine.UI.Image _fill;
        private bool _busy;

        /// <summary>Carga la escena con un fundido. Devuelve false (y registra el motivo) si no puede cargarse.</summary>
        public static bool Load(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[ScreenFader] El nombre de la escena está vacío.");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[ScreenFader] La escena '{sceneName}' no está registrada en Build Settings.");
                return false;
            }

            var fader = Instance();
            if (fader._busy) return false;
            fader.StartCoroutine(fader.Transition(sceneName));
            return true;
        }

        private static ScreenFader Instance()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(go.transform, false);
            var rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = fill.GetComponent<Image>();
            image.color = Lumina.Core.UiTheme.FadeColor;
            image.raycastTarget = true;

            _instance = go.AddComponent<ScreenFader>();
            _instance._fill = image;
            _instance._group = go.AddComponent<CanvasGroup>();
            _instance._group.alpha = 0f;
            _instance._group.blocksRaycasts = false;
            return _instance;
        }

        private IEnumerator Transition(string sceneName)
        {
            _busy = true;
            if (_fill != null) _fill.color = Lumina.Core.UiTheme.FadeColor;
            _group.blocksRaycasts = true;
            yield return Fade(0f, 1f, 0.3f);

            var operation = SceneManager.LoadSceneAsync(sceneName);
            while (!operation.isDone) yield return null;
            yield return null;

            yield return Fade(1f, 0f, 0.45f);
            _group.blocksRaycasts = false;
            _busy = false;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                _group.alpha = Mathf.Lerp(from, to, k);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
