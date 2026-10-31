using TMPro;
using UnityEngine;

namespace Lumina.UI
{
    /// <summary>Tarjeta de dato: valor grande y etiqueta debajo.</summary>
    public class StatCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text captionLabel;

        public void Set(string value, string caption = null)
        {
            valueLabel.text = value;
            if (caption != null) captionLabel.text = caption;
        }
    }
}
