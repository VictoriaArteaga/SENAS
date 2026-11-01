using System;
using TMPro;
using UnityEngine;
using Lumina.Core;
using Lumina.Signs;

namespace Lumina.UI
{
    public enum ItemLabelFormat { Digit, Word, MonthAbbreviation, MonthNumber, Phrase }

    [Serializable]
    public struct ItemLabel
    {
        public TMP_Text label;
        public ItemLabelFormat format;
    }

    /// <summary>
    /// Objeto físico de una casa de aprendizaje (un bloque con un número, una hoja del calendario, una burbuja con una frase).
    /// Sus textos 3D siguen al idioma activo y el objeto se agranda un poco mientras es el elemento abierto.
    /// </summary>
    public class LearningItemTarget : MonoBehaviour
    {
        [SerializeField] private LevelKind kind;
        [SerializeField] private int index;
        [SerializeField] private ItemLabel[] labels = Array.Empty<ItemLabel>();
        [SerializeField] private float highlightScale = 1.12f;

        private Vector3 _baseScale;
        private bool _highlighted;

        public int Index => index;

        private void Awake() => _baseScale = transform.localScale;
        private void OnEnable() { GameSettings.LanguageChanged += Refresh; Refresh(); }
        private void OnDisable() => GameSettings.LanguageChanged -= Refresh;

        public void SetHighlighted(bool on) => _highlighted = on;

        private void Update()
        {
            var target = _highlighted ? _baseScale * highlightScale : _baseScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        private LevelItem Item => LearningLevels.Get(kind).items[index];

        /// <summary>Texto del indicador «E — …» cuando el jugador mira este objeto.</summary>
        public string PromptText()
        {
            switch (kind)
            {
                case LevelKind.Numbers: return Loc.T("levels.inspectNumber", index + 1);
                case LevelKind.Months: return Loc.T("levels.inspectMonth", Item.Name);
                default: return Loc.T("levels.inspectPhrase", Item.Name);
            }
        }

        private void Refresh()
        {
            var item = Item;
            foreach (var entry in labels)
            {
                if (entry.label == null) continue;
                switch (entry.format)
                {
                    case ItemLabelFormat.Digit: entry.label.text = (index + 1).ToString(); break;
                    case ItemLabelFormat.Word: entry.label.text = item.Name.ToUpperInvariant(); break;
                    case ItemLabelFormat.MonthAbbreviation: entry.label.text = item.Name.Substring(0, Mathf.Min(3, item.Name.Length)).ToUpperInvariant(); break;
                    case ItemLabelFormat.MonthNumber: entry.label.text = (index + 1).ToString("00"); break;
                    default: entry.label.text = item.Name; break;
                }
            }
        }
    }
}
