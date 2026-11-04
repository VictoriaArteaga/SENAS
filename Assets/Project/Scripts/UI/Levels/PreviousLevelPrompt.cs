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
    /// Indicador fijo «B — Ir al nivel anterior» en cada casa de aprendizaje (menos la primera), igual que el de la N («Ir al siguiente nivel»):
    /// se copia ese mismo indicador de la escena para conservar su aspecto. Aparece siempre que el panel de práctica de la casa esté cerrado,
    /// se haya entrado a la casa desde donde se haya entrado. Se crea solo al cargar la casa; no necesita nada en las escenas.
    /// </summary>
    public class PreviousLevelPrompt : MonoBehaviour
    {
        private const string Key = "B";

        private static readonly string[] Houses = { SceneNames.Alphabet, SceneNames.Numbers, SceneNames.Months, SceneNames.Greetings };

        private InteractionPrompt _prompt;
        private IPracticePanel[] _panels;
        private string _previousScene;
        private bool _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            int index = System.Array.IndexOf(Houses, scene.name);
            if (index <= 0) return; // no es una casa, o es la primera (no hay nivel anterior)
            new GameObject("PreviousLevelPrompt").AddComponent<PreviousLevelPrompt>().Build(Houses[index - 1]);
        }

        private void Build(string previousScene)
        {
            _previousScene = previousScene;

            // Plantilla: el indicador de la N si la casa lo tiene; si no (última casa), cualquier indicador de 640×108 anclado arriba a la izquierda.
            InteractionPrompt template = null;
            var next = FindFirstObjectByType<NextLevelPrompt>(FindObjectsInactive.Include);
            bool hasNext = next != null;
            foreach (var candidate in FindObjectsByType<InteractionPrompt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rect = candidate.transform as RectTransform;
                if (rect == null || rect.sizeDelta != new Vector2(640f, 108f) || rect.anchorMax != new Vector2(0f, 1f)) continue;
                template = candidate;
                if (hasNext && candidate.GetComponentInParent<Canvas>() != null && IsNextPrompt(next, candidate)) break;
            }
            if (template == null) { Destroy(gameObject); return; }

            _prompt = Instantiate(template, template.transform.parent);
            _prompt.name = "PreviousLevelPrompt";
            // Debajo de los indicadores que ya hay (P y N).
            var position = (RectTransform)_prompt.transform;
            position.anchoredPosition = new Vector2(40f, hasNext ? -280f : -160f);

            foreach (var text in _prompt.GetComponentsInChildren<TMP_Text>(true))
            {
                var localized = text.GetComponent<LocalizedText>();
                if (localized != null) Destroy(localized);
                if (text.text.Trim().Length <= 2) text.text = Key;
                else LocalizedText.Bind(text, "levels.previousLevelPrompt");
            }

            var panels = new List<IPracticePanel>();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IPracticePanel panel) panels.Add(panel);
            _panels = panels.ToArray();
        }

        private static bool IsNextPrompt(NextLevelPrompt next, InteractionPrompt candidate) => next.Prompt == candidate;

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
            if (show && keyboard != null && keyboard.bKey.wasPressedThisFrame && !LevelAccess.IsBlocked(_previousScene))
                ScreenFader.Load(_previousScene);
        }
    }
}
