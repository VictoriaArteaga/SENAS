using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Botón MENÚ en el mundo de Lúmina para volver al menú principal. Se crea solo al cargar LuminaWorld.
    /// Con el cursor bloqueado no se puede pulsar: Esc libera el cursor (un clic sobre el botón no lo vuelve a bloquear).
    /// </summary>
    public class WorldMenuButton : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.059f, 0.227f, 0.267f);
        private static readonly Color Cream = new Color(1f, 0.976f, 0.925f);

        private TMP_Text _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneNames.World) return;
            new GameObject("WorldMenuButton").AddComponent<WorldMenuButton>().Build();
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var go = new GameObject("Menu", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            // Arriba a la derecha: la esquina superior izquierda y el centro inferior los usan los avisos de tecla.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(240f, 64f);
            rect.anchoredPosition = new Vector2(-32f, -32f);
            var image = go.GetComponent<Image>();
            image.color = Ink;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 6f);
            labelRect.offsetMax = new Vector2(-16f, -6f);
            _label = labelObject.AddComponent<TextMeshProUGUI>();
            _label.text = Loc.T("common.menu");
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 14f;
            _label.fontSizeMax = 30f;
            _label.fontStyle = FontStyles.Bold;
            _label.color = Cream;
            _label.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ScreenFader.Load(SceneNames.MainMenu));
        }

        private void OnEnable() => GameSettings.LanguageChanged += Retranslate;
        private void OnDisable() => GameSettings.LanguageChanged -= Retranslate;

        private void Retranslate()
        {
            if (_label != null) _label.text = Loc.T("common.menu");
        }
    }
}
