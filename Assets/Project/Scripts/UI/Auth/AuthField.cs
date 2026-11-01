using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Campo de formulario: entrada de texto con contorno que cambia con el foco, mensaje de error
    /// debajo, sacudida al fallar y (opcional) botón para mostrar u ocultar el contenido.
    /// </summary>
    public class AuthField : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Image outline;
        [SerializeField] private TMP_Text errorLabel;
        [Header("Mostrar / ocultar contenido (opcional)")]
        [SerializeField] private Button revealButton;
        [SerializeField] private TMP_Text revealLabel;
        [Header("Colores del contorno")]
        [SerializeField] private Color normalColor = new Color(0.56f, 0.89f, 0.85f);
        [SerializeField] private Color focusColor = new Color(0.18f, 0.769f, 0.714f);
        [SerializeField] private Color errorColor = new Color(1f, 0.369f, 0.302f);

        private bool _hasError;
        private bool _focused;
        private bool _revealed;
        private Coroutine _shake;

        public string Text => input.text;
        public bool Revealed => _revealed;
        public TMP_InputField Input => input;

        /// <summary>Se emite al pulsar Enter dentro del campo.</summary>
        public event Action Submitted;
        /// <summary>Se emite cuando el usuario cambia el texto.</summary>
        public event Action TextChanged;
        /// <summary>Se emite tras alternar el botón de mostrar/ocultar; el argumento indica si ahora se ve el texto.</summary>
        public event Action<bool> RevealToggled;

        private void Awake()
        {
            if (input == null || outline == null || errorLabel == null)
            {
                Debug.LogError($"[AuthField] Faltan referencias en '{name}'.", this);
                enabled = false;
                return;
            }

            input.onSelect.AddListener(_ => { _focused = true; Refresh(); });
            input.onDeselect.AddListener(_ => { _focused = false; Refresh(); });
            input.onSubmit.AddListener(_ => Submitted?.Invoke());
            input.onValueChanged.AddListener(_ => { if (_hasError) ClearError(); TextChanged?.Invoke(); });

            if (revealButton != null)
            {
                revealButton.onClick.AddListener(() => SetRevealed(!_revealed, true));
                SetRevealed(false, false);
            }

            errorLabel.text = string.Empty;
            errorLabel.gameObject.SetActive(false);
            Refresh();
        }

        public void Focus()
        {
            input.Select();
            input.ActivateInputField();
        }

        public void SetRevealed(bool revealed, bool notify)
        {
            _revealed = revealed;
            input.contentType = revealed ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
            if (revealLabel != null) revealLabel.text = Loc.T(revealed ? "auth.hide" : "auth.show");
            if (notify) RevealToggled?.Invoke(revealed);
        }

        public void SetError(string message)
        {
            _hasError = true;
            errorLabel.text = Loc.Translate(message);
            // El campo está dentro de un layout vertical: al activar el mensaje, lo que hay debajo se desplaza para hacerle sitio.
            errorLabel.gameObject.SetActive(true);
            Refresh();
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(Shake());
        }

        public void ClearError()
        {
            _hasError = false;
            errorLabel.text = string.Empty;
            errorLabel.gameObject.SetActive(false);
            Refresh();
        }

        public void Clear()
        {
            input.text = string.Empty;
            ClearError();
        }

        public void SetInteractable(bool value)
        {
            input.interactable = value;
            if (revealButton != null) revealButton.interactable = value;
        }

        private void Refresh()
        {
            outline.color = _hasError ? errorColor : _focused ? focusColor : normalColor;
        }

        private IEnumerator Shake()
        {
            // Se sacude el cuadro de texto (hijo con anclas estiradas), no el campo: la posición del campo la manda el layout.
            var rect = (RectTransform)input.transform;
            Vector2 home = rect.anchoredPosition;
            const float duration = 0.32f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float fade = 1f - t / duration;
                rect.anchoredPosition = home + Vector2.right * (Mathf.Sin(t * 70f) * 9f * fade);
                yield return null;
            }
            rect.anchoredPosition = home;
            _shake = null;
        }
    }
}
