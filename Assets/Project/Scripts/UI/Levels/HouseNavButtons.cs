using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Lumina.Api;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Indicador «Q — Volver a Practicar» dentro de las casas de aprendizaje, solo si se llegó a la casa desde el panel de Practice
    /// (<see cref="LevelShortcutsUI"/>); entrando por la puerta de Lúmina no aparece. Copia el indicador de la P para conservar el mismo
    /// diseño y letra, y va debajo de los que ya hay (P, N y B). Se oculta mientras el panel de práctica de la casa está abierto.
    /// </summary>
    public class HouseNavButtons : MonoBehaviour
    {
        private const string Key = "Q";
        private const float Step = 120f;

        private static readonly string[] Houses = { SceneNames.Alphabet, SceneNames.Numbers, SceneNames.Months, SceneNames.Greetings };

        /// <summary>True si la casa actual se abrió desde el panel de Practice. Se apaga al salir a cualquier escena que no sea una casa.</summary>
        public static bool FromPractice { get; set; }

        private InteractionPrompt _prompt;
        private IPracticePanel[] _panels;
        private bool _shown;

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
            // Plantilla: un indicador de 640×108 anclado arriba a la izquierda (el de la P).
            InteractionPrompt template = null;
            foreach (var candidate in FindObjectsByType<InteractionPrompt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rect = candidate.transform as RectTransform;
                if (rect == null || rect.sizeDelta != new Vector2(640f, 108f) || rect.anchorMax != new Vector2(0f, 1f)) continue;
                template = candidate;
                break;
            }
            if (template == null) { Destroy(gameObject); return; }

            // Debajo de los indicadores que hay: P siempre, N si hay nivel siguiente y B si hay nivel anterior.
            int above = 1;
            if (FindFirstObjectByType<NextLevelPrompt>(FindObjectsInactive.Include) != null) above++;
            if (houseIndex > 0) above++;

            _prompt = Instantiate(template, template.transform.parent);
            _prompt.name = "BackToPracticePrompt";
            ((RectTransform)_prompt.transform).anchoredPosition = new Vector2(40f, -40f - above * Step);

            foreach (var text in _prompt.GetComponentsInChildren<TMP_Text>(true))
            {
                var localized = text.GetComponent<LocalizedText>();
                if (localized != null) Destroy(localized);
                if (text.text.Trim().Length <= 2) text.text = Key;
                else LocalizedText.Bind(text, "levels.backToPractice");
            }

            var panels = new List<IPracticePanel>();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IPracticePanel panel) panels.Add(panel);
            _panels = panels.ToArray();
        }

        private void Update()
        {
            if (_prompt == null || _panels == null) return;

            bool open = false;
            foreach (var panel in _panels) if (panel != null && panel.IsOpen) { open = true; break; }
            bool show = !open;
            if (show != _shown)
            {
                _shown = show;
                if (show) _prompt.Show(); else _prompt.Hide();
            }

            var keyboard = Keyboard.current;
            if (show && keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                FromPractice = false;
                ScreenFader.Load(SceneNames.Practice);
            }
        }
    }
}
