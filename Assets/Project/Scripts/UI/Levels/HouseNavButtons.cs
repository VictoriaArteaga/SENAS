using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lumina.Api;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Botón VOLVER (a Practice) dentro de las casas de aprendizaje, solo si se llegó a la casa desde el panel
    /// de Practice (<see cref="LevelShortcutsUI"/>). Entrando por la puerta de Lúmina no aparecen. Se crean solos al cargar la casa y se ocultan
    /// mientras el panel de práctica de la casa está abierto. Con el cursor bloqueado (Esc lo libera) no se pueden pulsar.
    /// </summary>
    public class HouseNavButtons : MonoBehaviour
    {
        private static readonly string[] Houses = { SceneNames.Alphabet, SceneNames.Numbers, SceneNames.Months, SceneNames.Greetings };

        private static readonly Color Ink = new Color(0.059f, 0.227f, 0.267f);
        private static readonly Color Cream = new Color(1f, 0.976f, 0.925f);

        /// <summary>True si la casa actual se abrió desde el panel de Practice. Se apaga al salir a cualquier escena que no sea una casa.</summary>
        public static bool FromPractice { get; set; }

        private IPracticePanel[] _panels;
        private GameObject _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            FromPractice = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            int index = System.Array.IndexOf(Houses, scene.name);
            if (index < 0)
            {
                if (scene.name != SceneNames.Practice) FromPractice = false;
                return;
            }
            if (!FromPractice) return;
            new GameObject("HouseNavButtons").AddComponent<HouseNavButtons>().Build(index);
        }

        private void Build(int houseIndex)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _root = gameObject;

            FindStyleSource();

            var back = MakeButton("Volver", "common.back", 0);
            back.onClick.AddListener(() => { FromPractice = false; ScreenFader.Load(SceneNames.Practice); });

            _panels = FindPanels();
        }

        private static IPracticePanel[] FindPanels()
        {
            var list = new System.Collections.Generic.List<IPracticePanel>();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IPracticePanel panel) list.Add(panel);
            return list.ToArray();
        }

        private Image _styleImage;
        private Button _styleButton;
        private TMP_Text _styleLabel;

        /// <summary>Busca un botón con letras ya existente en la casa (preferentemente VOLVER) para copiar su aspecto: imagen, colores y fuente.</summary>
        private void FindStyleSource()
        {
            int bestScore = -1;
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.transform.IsChildOf(transform)) continue;
                var image = button.GetComponent<Image>();
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (image == null || label == null || image.sprite == null) continue;
                string n = button.name.ToLowerInvariant();
                int score = n.Contains("volver") || n.Contains("back") ? 3 : n.Contains("cerrar") || n.Contains("close") ? 2 : 1;
                if (score <= bestScore) continue;
                bestScore = score;
                _styleImage = image; _styleButton = button; _styleLabel = label;
            }
        }

        private Button MakeButton(string objectName, string locKey, int slot)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(300f, 64f);
            rect.anchoredPosition = new Vector2(32f, -32f - slot * 80f);
            var image = go.GetComponent<Image>();
            image.color = Ink;
            if (_styleImage != null)
            {
                image.sprite = _styleImage.sprite;
                image.type = _styleImage.type;
                image.color = _styleImage.color;
                image.pixelsPerUnitMultiplier = _styleImage.pixelsPerUnitMultiplier;
            }

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 6f);
            labelRect.offsetMax = new Vector2(-16f, -6f);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = Loc.T(locKey);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 30f;
            label.fontStyle = FontStyles.Bold;
            label.color = Cream;
            label.raycastTarget = false;
            if (_styleLabel != null)
            {
                label.font = _styleLabel.font;
                label.fontSharedMaterial = _styleLabel.fontSharedMaterial;
                label.fontStyle = _styleLabel.fontStyle;
                label.color = _styleLabel.color;
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (_styleButton != null) button.colors = _styleButton.colors;
            _labels.Add((label, locKey));
            return button;
        }

        private readonly System.Collections.Generic.List<(TMP_Text label, string key)> _labels = new System.Collections.Generic.List<(TMP_Text, string)>();

        private void OnEnable() => GameSettings.LanguageChanged += Retranslate;
        private void OnDisable() => GameSettings.LanguageChanged -= Retranslate;

        private void Retranslate()
        {
            foreach (var (label, key) in _labels) if (label != null) label.text = Loc.T(key);
        }

        private void Update()
        {
            if (_panels == null) return;
            bool open = false;
            foreach (var panel in _panels) if (panel != null && panel.IsOpen) { open = true; break; }
            foreach (Transform child in transform) child.gameObject.SetActive(!open);
        }
    }
}
