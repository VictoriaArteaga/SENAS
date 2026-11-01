using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumina.Core
{
    /// <summary>
    /// Lleva el tema al entorno 3D: en modo oscuro cambia el cielo diurno de la escena por su pareja nocturna y atenúa el sol.
    /// El estado diurno de cada escena se captura al cargarla y se restaura tal cual en modo claro, así que nunca se acumulan
    /// cambios. Las escenas sin cielo (o con un cielo sin pareja) no se tocan. El tema sigue viniendo de <see cref="UiTheme"/>.
    /// </summary>
    public static class ThemeSkybox
    {
        private const string SetPath = "ThemeSkyboxSet";
        private const float NightSunIntensity = 0.28f;
        private static readonly Color NightSunTint = new Color(0.55f, 0.65f, 0.95f);
        private const float NightAmbientIntensity = 0.9f;

        private static ThemeSkyboxSet _set;
        private static Material _daySky, _nightSky;
        private static float _dayAmbient;
        private static readonly List<(Light light, float intensity, Color color)> DaySuns = new List<(Light, float, Color)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            GameSettings.EnsureBound();
            _set = Resources.Load<ThemeSkyboxSet>(SetPath);
            SceneManager.sceneLoaded += (scene, mode) => CaptureAndApply();
            GameSettings.ThemeChanged -= Apply;
            GameSettings.ThemeChanged += Apply;
            CaptureAndApply();
        }

        private static void CaptureAndApply()
        {
            _daySky = _nightSky = null;
            DaySuns.Clear();
            if (_set == null) return;

            _nightSky = _set.NightFor(RenderSettings.skybox);
            if (_nightSky == null) return; // escena sin entorno 3D (o con un cielo ajeno al conjunto)

            _daySky = RenderSettings.skybox;
            _dayAmbient = RenderSettings.ambientIntensity;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) DaySuns.Add((light, light.intensity, light.color));
            Apply();
        }

        private static void Apply()
        {
            if (_daySky == null || _nightSky == null) return;
            bool dark = UiTheme.Dark;

            RenderSettings.skybox = dark ? _nightSky : _daySky;
            RenderSettings.ambientIntensity = dark ? _dayAmbient * NightAmbientIntensity : _dayAmbient;
            foreach (var (light, intensity, color) in DaySuns)
            {
                if (light == null) continue;
                light.intensity = dark ? intensity * NightSunIntensity : intensity;
                light.color = dark ? color * NightSunTint : color;
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
