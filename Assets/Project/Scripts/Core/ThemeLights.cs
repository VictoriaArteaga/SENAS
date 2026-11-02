using System.Collections.Generic;
using UnityEngine;

namespace Lumina.Core
{
    /// <summary>
    /// Luces de una escena que cambian con el tema claro/oscuro. Complementa a <see cref="ThemeSkybox"/> (que solo toca el cielo, el
    /// ambiente y las luces direccionales) y NO toca el cielo, la luz direccional, RenderSettings ni el postprocesado.
    ///
    /// Se coloca UNA vez en un objeto que sea ancestro de las luces (en las casas, el objeto raíz de la casa) y no necesita
    /// referencias: recoge sola, por prefijo de nombre, las luces que cuelgan de él. Hace dos cosas:
    ///  · Grupos (<see cref="groups"/>): multiplica la intensidad ORIGINAL de cada luz según el tema (p. ej. las luces de ventana,
    ///    que representan el día, bajan de noche). La intensidad original se guarda una vez y cada cambio parte de ella, así que
    ///    alternar el tema no acumula cambios.
    ///  · Luces decorativas nocturnas: bajo cada objeto cuyo nombre empiece por <see cref="decorativeAnchorPrefix"/> (los faroles
    ///    de LuminaWorld) crea en tiempo de ejecución una luz puntual suave, activa solo de noche. No se guardan en la escena, por
    ///    lo que reconstruir la escena nunca las duplica. En calidad baja (móvil) se limita el número y nunca hay sombras.
    /// El tema se recibe por <see cref="GameSettings.ThemeChanged"/>; no hay Update ni preferencias propias.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemeLights : MonoBehaviour
    {
        [System.Serializable]
        public class Group
        {
            [Tooltip("Las luces cuyo nombre empieza por este texto pertenecen al grupo.")]
            public string namePrefix;
            [Tooltip("Multiplicador de la intensidad original con el tema claro.")]
            [Min(0f)] public float dayMultiplier = 1f;
            [Tooltip("Multiplicador de la intensidad original con el tema oscuro.")]
            [Min(0f)] public float nightMultiplier = 1f;

            public Group(string namePrefix, float dayMultiplier, float nightMultiplier)
            {
                this.namePrefix = namePrefix; this.dayMultiplier = dayMultiplier; this.nightMultiplier = nightMultiplier;
            }
        }

        [Header("Luces de la casa (por nombre)")]
        [SerializeField] private Group[] groups =
        {
            new Group("LuzCalida", 1f, 1f),     // lámparas: conservan su calidez de día y de noche
            new Group("LuzVentana", 1f, 0.6f),  // representan la luz del día: bajan ligeramente de noche
        };

        [Header("Luces decorativas nocturnas (faroles)")]
        [Tooltip("Vacío = no se crean luces decorativas. Cada objeto cuyo nombre empiece por este texto recibe una luz puntual de noche.")]
        [SerializeField] private string decorativeAnchorPrefix = "";
        [SerializeField] private Color decorativeColor = new Color(1f, 0.78f, 0.5f);
        [SerializeField, Min(0f)] private float decorativeIntensity = 1.6f;
        [SerializeField, Min(0.1f)] private float decorativeRange = 6.5f;
        [Tooltip("Si el objeto tiene una malla con un material cuyo nombre empieza por este texto, la luz se coloca en ese submesh (la lámpara).")]
        [SerializeField] private string emissiveMaterialPrefix = "Light";
        [Tooltip("Posición de la luz respecto al objeto si no se encuentra el submesh emisivo.")]
        [SerializeField] private Vector3 fallbackOffset = new Vector3(0f, 2.6f, 0f);
        [SerializeField, Min(0)] private int maxDecorativeLights = 4;
        [Tooltip("Máximo en el nivel de calidad móvil (ver lowEndQualityName). Con 0 no hay luces decorativas en móvil: queda solo el efecto emisivo del material.")]
        [SerializeField, Min(0)] private int maxDecorativeLightsLowEnd = 2;
        [Tooltip("Texto que identifica el nivel de calidad móvil (el proyecto tiene los niveles «PC» y «Mobile»).")]
        [SerializeField] private string lowEndQualityName = "Mobile";

        private const string DecorativeName = "ThemeLight";

        private readonly Dictionary<Light, float> _originals = new Dictionary<Light, float>();
        private readonly List<Light> _decorative = new List<Light>();
        private bool _collected;

        private void OnEnable()
        {
            GameSettings.EnsureBound();
            GameSettings.ThemeChanged -= Apply;
            GameSettings.ThemeChanged += Apply;
            Collect();
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.ThemeChanged -= Apply;
            RestoreOriginals(); // al desactivarse no deja la escena con la atenuación de un tema
        }

        /// <summary>Vuelve a recoger las luces (útil si se añadieron o renombraron luces después de arrancar).</summary>
        [ContextMenu("Recoger luces de nuevo")]
        public void Recollect()
        {
            RestoreOriginals();
            _collected = false;
            _originals.Clear();
            Collect();
            Apply();
        }

        private void Collect()
        {
            if (_collected) return;
            _collected = true;
            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light == null || light.type == LightType.Directional || light.name == DecorativeName) continue;
                if (GroupOf(light) != null) _originals[light] = light.intensity; // se guarda UNA vez: base de todos los cambios
            }
            CreateDecorativeLights();
        }

        private void Apply()
        {
            bool dark = UiTheme.Dark;
            foreach (var pair in _originals)
            {
                var light = pair.Key;
                if (light == null) continue;
                var group = GroupOf(light);
                float multiplier = group == null ? 1f : (dark ? group.nightMultiplier : group.dayMultiplier);
                light.intensity = pair.Value * multiplier; // siempre desde el valor original: no se acumula
            }
            ApplyDecorative(dark);
        }

        private Group GroupOf(Light light)
        {
            foreach (var group in groups)
                if (group != null && !string.IsNullOrEmpty(group.namePrefix) && light.name.StartsWith(group.namePrefix)) return group;
            return null;
        }

        private void RestoreOriginals()
        {
            foreach (var pair in _originals)
                if (pair.Key != null) pair.Key.intensity = pair.Value;
            foreach (var light in _decorative)
                if (light != null) light.enabled = false;
        }

        // ---------- Luces decorativas ----------

        private void CreateDecorativeLights()
        {
            _decorative.Clear();
            if (string.IsNullOrEmpty(decorativeAnchorPrefix)) return;

            foreach (var anchor in GetComponentsInChildren<Transform>(true))
            {
                if (anchor == transform || anchor.name == DecorativeName || !anchor.name.StartsWith(decorativeAnchorPrefix)) continue;

                // Una sola luz por objeto aunque el componente se reactive: la que ya exista se reutiliza.
                Light light = null;
                var existing = anchor.Find(DecorativeName);
                if (existing != null) light = existing.GetComponent<Light>();
                if (light == null)
                {
                    var go = existing != null ? existing.gameObject : new GameObject(DecorativeName);
                    go.hideFlags = HideFlags.DontSave; // no se serializa en la escena
                    go.transform.SetParent(anchor, false);
                    light = go.AddComponent<Light>();
                }

                light.transform.position = LampPosition(anchor);
                light.type = LightType.Point;
                light.color = decorativeColor;
                light.intensity = decorativeIntensity;
                light.range = decorativeRange;
                light.shadows = LightShadows.None; // nunca sombras en luces decorativas
                light.enabled = false;
                _decorative.Add(light);
            }
        }

        /// <summary>Centro del submesh emisivo (la lámpara) del objeto, o su posición más el desplazamiento de reserva.</summary>
        private Vector3 LampPosition(Transform anchor)
        {
            if (!string.IsNullOrEmpty(emissiveMaterialPrefix))
            {
                foreach (var renderer in anchor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length && i < filter.sharedMesh.subMeshCount; i++)
                        if (materials[i] != null && materials[i].name.StartsWith(emissiveMaterialPrefix))
                            return renderer.transform.TransformPoint(filter.sharedMesh.GetSubMesh(i).bounds.center);
                }
            }
            return anchor.TransformPoint(fallbackOffset);
        }

        private void ApplyDecorative(bool dark)
        {
            if (_decorative.Count == 0) return;
            int limit = IsLowEnd() ? maxDecorativeLightsLowEnd : maxDecorativeLights;

            // Se priorizan los puntos más cercanos al jugador (la cámara) para gastar pocas luces donde se ven.
            var cam = Camera.main;
            if (cam != null)
            {
                var from = cam.transform.position;
                _decorative.RemoveAll(l => l == null);
                _decorative.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            }

            for (int i = 0; i < _decorative.Count; i++)
                if (_decorative[i] != null) _decorative[i].enabled = dark && i < limit;
        }

        private bool IsLowEnd()
        {
            if (string.IsNullOrEmpty(lowEndQualityName)) return false;
            var names = QualitySettings.names;
            int level = QualitySettings.GetQualityLevel();
            return level >= 0 && level < names.Length && names[level].IndexOf(lowEndQualityName, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
