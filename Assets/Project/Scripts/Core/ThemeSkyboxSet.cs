using System;
using UnityEngine;

namespace Lumina.Core
{
    /// <summary>Pares de cielo diurno → nocturno que usa <see cref="ThemeSkybox"/>. Solo referencia materiales; nunca los modifica.</summary>
    [CreateAssetMenu(fileName = "ThemeSkyboxSet", menuName = "Lumina/Theme Skybox Set")]
    public class ThemeSkyboxSet : ScriptableObject
    {
        [Serializable]
        public struct Pair
        {
            public Material day;
            public Material night;
        }

        public Pair[] pairs;

        /// <summary>Cielo nocturno que corresponde al diurno de la escena; null si la escena usa otro cielo (no se toca).</summary>
        public Material NightFor(Material day)
        {
            if (day == null || pairs == null) return null;
            foreach (var p in pairs)
                if (p.day == day) return p.night;
            return null;
        }
    }
}
