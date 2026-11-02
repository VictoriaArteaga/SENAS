using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lumina.Core
{
    /// <summary>
    /// Ambiente en gradiente y bruma de una escena según el tema. Existe porque <see cref="ThemeSkybox"/> solo escala la
    /// intensidad del ambiente, y esa intensidad solo actúa con ambiente de skybox: con ambiente en gradiente (Trilight) los
    /// colores no se atenúan y de noche seguirían siendo los de día. Este componente es la solución compatible: ThemeSkybox no se
    /// toca y sigue encargándose del cielo y de la luz direccional; este solo cambia los tres colores del ambiente y la bruma.
    /// Se coloca en cualquier objeto de la escena (en LuminaWorld, en la luz solar; en las casas, en su objeto raíz).
    /// Sin Update: el tema llega por <see cref="GameSettings.ThemeChanged"/>.
    ///
    /// Ciclo de vida: al activarse recuerda lo que había en RenderSettings (los valores de la escena) y aplica el tema en curso. Al
    /// desactivarse o destruirse devuelve ESOS valores, propiedad por propiedad y solo si siguen siendo los que él escribió (si otro
    /// sistema los cambió en el medio, se respetan). No restaura nada si su escena se está descargando o ya no es la activa:
    /// RenderSettings pertenece a la escena activa y escribir ahí sería contaminar a la siguiente. No restaura «los de día»: devuelve
    /// lo que había antes de actuar, sea cual sea el tema.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemeAmbient : MonoBehaviour
    {
        [Header("Día")]
        [SerializeField] private Color daySky = new Color32(0xC2, 0xD6, 0xE8, 0xFF);      // cielo claro
        [SerializeField] private Color dayEquator = new Color32(0xEA, 0xDF, 0xC9, 0xFF);  // horizonte ligeramente cálido
        [SerializeField] private Color dayGround = new Color32(0x60, 0x79, 0x4F, 0xFF);   // suelo: verde apagado
        [SerializeField] private Color dayFog = new Color32(0xD6, 0xE6, 0xF0, 0xFF);

        [Header("Noche")]
        [SerializeField] private Color nightSky = new Color32(0x2F, 0x46, 0x70, 0xFF);
        [SerializeField] private Color nightEquator = new Color32(0x25, 0x37, 0x5A, 0xFF);
        [SerializeField] private Color nightGround = new Color32(0x1C, 0x36, 0x28, 0xFF);
        [SerializeField] private Color nightFog = new Color32(0x1B, 0x2B, 0x4C, 0xFF);

        [Header("Bruma (exponencial)")]
        [SerializeField] private bool fog = true;
        [SerializeField, Range(0f, 0.02f)] private float fogDensity = 0.0028f;

        /// <summary>Las propiedades de RenderSettings que este componente escribe.</summary>
        private struct State
        {
            public AmbientMode mode;
            public Color sky, equator, ground, fogColor;
            public bool fog;
            public FogMode fogMode;
            public float fogDensity;
        }

        private State _before;     // lo que había al activarse (valores de la escena)
        private State _applied;    // lo último que escribió este componente
        private bool _hasBefore, _hasApplied;

        private void OnEnable()
        {
            GameSettings.EnsureBound();
            _before = Read(); _hasBefore = true; _hasApplied = false;
            GameSettings.ThemeChanged -= Apply;
            GameSettings.ThemeChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.ThemeChanged -= Apply;
            Release();
        }

        private void Apply() => ApplyTheme(UiTheme.Dark);

        /// <summary>Aplica los valores de un tema. Lo usa también el constructor de la escena para guardar los de día en RenderSettings.</summary>
        public void ApplyTheme(bool dark)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = dark ? nightSky : daySky;
            RenderSettings.ambientEquatorColor = dark ? nightEquator : dayEquator;
            RenderSettings.ambientGroundColor = dark ? nightGround : dayGround;

            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = dark ? nightFog : dayFog;

            if (_hasBefore) { _applied = Read(); _hasApplied = true; } // solo cuando el componente está activo (no desde el editor)
            DynamicGI.UpdateEnvironment();
        }

        private static State Read() => new State
        {
            mode = RenderSettings.ambientMode,
            sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
            fog = RenderSettings.fog, fogMode = RenderSettings.fogMode, fogDensity = RenderSettings.fogDensity, fogColor = RenderSettings.fogColor,
        };

        /// <summary>Devuelve lo que había antes de actuar, solo en lo que sigue siendo suyo y solo si su escena es la activa.</summary>
        private void Release()
        {
            if (!_hasBefore || !_hasApplied) return;
            _hasApplied = false;

            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || scene != SceneManager.GetActiveScene()) return; // descarga de escena o escena no activa

            if (RenderSettings.ambientMode == _applied.mode) RenderSettings.ambientMode = _before.mode;
            if (RenderSettings.ambientSkyColor == _applied.sky) RenderSettings.ambientSkyColor = _before.sky;
            if (RenderSettings.ambientEquatorColor == _applied.equator) RenderSettings.ambientEquatorColor = _before.equator;
            if (RenderSettings.ambientGroundColor == _applied.ground) RenderSettings.ambientGroundColor = _before.ground;
            if (RenderSettings.fog == _applied.fog) RenderSettings.fog = _before.fog;
            if (RenderSettings.fogMode == _applied.fogMode) RenderSettings.fogMode = _before.fogMode;
            if (Mathf.Approximately(RenderSettings.fogDensity, _applied.fogDensity)) RenderSettings.fogDensity = _before.fogDensity;
            if (RenderSettings.fogColor == _applied.fogColor) RenderSettings.fogColor = _before.fogColor;
            DynamicGI.UpdateEnvironment();
        }
    }
}
