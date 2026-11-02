using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Elige el objeto de aprendizaje al que apunta el jugador (rayo desde el centro de la cámara) y lo abre con E.
    /// Se usa en vez de una zona de proximidad por objeto: con decenas de objetos juntos, una sola E abriría varios a la vez.
    /// </summary>
    public class LearningItemSelector : MonoBehaviour
    {
        [SerializeField] private LearningLevelController level;
        [SerializeField] private InteractionPrompt prompt;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private float maxDistance = 4.5f;

        private LearningItemTarget _current;

        private void OnEnable() => GameSettings.LanguageChanged += RefreshPrompt;
        private void OnDisable() => GameSettings.LanguageChanged -= RefreshPrompt;

        private void Update()
        {
            LearningItemTarget target = null;
            var cam = Camera.main;
            if (!level.IsOpen && cam != null &&
                Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<LearningItemTarget>();

            if (target != _current)
            {
                _current = target;
                if (_current != null) { RefreshPrompt(); prompt.Show(); }
                else prompt.Hide();
            }

            var keyboard = Keyboard.current;
            if (_current != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                int index = _current.Index;
                _current = null;
                prompt.Hide();
                level.Open(index);
            }
        }

        private void RefreshPrompt()
        {
            if (_current != null && promptLabel != null) promptLabel.text = _current.PromptText();
        }
    }
}
