using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>Selector «‹ valor ›» navegable con teclado, mando o ratón. Cicla entre las opciones.</summary>
    public class OptionSelectorUI : MonoBehaviour
    {
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text valueLabel;

        private string[] _options = Array.Empty<string>();
        private int _index;

        public event Action<int> Changed;
        public int Index => _index;

        private void Awake()
        {
            previousButton.onClick.AddListener(() => Step(-1));
            nextButton.onClick.AddListener(() => Step(1));
        }

        public void SetOptions(string[] options, int index)
        {
            _options = options ?? Array.Empty<string>();
            bool any = _options.Length > 0;
            previousButton.interactable = nextButton.interactable = _options.Length > 1;
            _index = any ? Mathf.Clamp(index, 0, _options.Length - 1) : 0;
            valueLabel.text = any ? _options[_index] : "—";
        }

        private void Step(int delta)
        {
            if (_options.Length == 0) return;
            _index = (_index + delta + _options.Length) % _options.Length;
            valueLabel.text = _options[_index];
            Changed?.Invoke(_index);
        }
    }
}
