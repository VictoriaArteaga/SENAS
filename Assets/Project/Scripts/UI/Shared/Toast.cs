using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>
    /// Aviso breve en la parte baja de la pantalla («toast») para errores que no pertenecen a un formulario: sin conexión,
    /// sesión terminada, nivel bloqueado. Se crea solo la primera vez que se usa y sobrevive a los cambios de escena.
    /// </summary>
    public sealed class Toast : MonoBehaviour
    {
        private static Toast _instance;

        private CanvasGroup _group;
        private TMP_Text _label;
        private Coroutine _routine;

        public static void Show(string message, float seconds = 3.5f)
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(message)) return;
            if (_instance == null) _instance = Create();
            _instance.Display(message, seconds);
        }

        private static Toast Create()
        {
            var root = new GameObject("Toast", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 70f);
            rect.sizeDelta = new Vector2(900f, 90f);
            panel.GetComponent<Image>().color = new Color(0.059f, 0.227f, 0.267f, 0.95f);
            panel.GetComponent<Image>().raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 8f);
            textRect.offsetMax = new Vector2(-24f, -8f);
            var label = textObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 30f;
            label.color = new Color(1f, 0.976f, 0.925f);
            label.raycastTarget = false;

            var toast = root.AddComponent<Toast>();
            toast._group = panel.GetComponent<CanvasGroup>();
            toast._group.alpha = 0f;
            toast._group.blocksRaycasts = false;
            toast._group.interactable = false;
            toast._label = label;
            return toast;
        }

        private void Display(string message, float seconds)
        {
            _label.text = message;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(seconds));
        }

        private IEnumerator Run(float seconds)
        {
            const float fade = 0.2f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime) { _group.alpha = t / fade; yield return null; }
            _group.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime) { _group.alpha = 1f - t / fade; yield return null; }
            _group.alpha = 0f;
            _routine = null;
        }
    }
}
