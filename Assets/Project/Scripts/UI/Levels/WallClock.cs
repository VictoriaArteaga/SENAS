using System;
using UnityEngine;

namespace Lumina.UI
{
    /// <summary>Reloj de pared de la Casa de los Meses: las agujas siguen la hora del sistema.</summary>
    public class WallClock : MonoBehaviour
    {
        [SerializeField] private Transform hourHand;
        [SerializeField] private Transform minuteHand;
        [Tooltip("Dirección (en local) hacia la que mira quien ve el reloj: las agujas giran en sentido horario visto desde ahí.")]
        [SerializeField] private Vector3 axis = Vector3.back;

        private void Update()
        {
            var now = DateTime.Now;
            float minutes = now.Minute + now.Second / 60f;
            if (minuteHand != null) minuteHand.localRotation = Quaternion.AngleAxis(minutes * 6f, axis);
            if (hourHand != null) hourHand.localRotation = Quaternion.AngleAxis(((now.Hour % 12) + minutes / 60f) * 30f, axis);
        }
    }
}
