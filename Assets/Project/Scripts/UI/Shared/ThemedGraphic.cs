using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Pinta un Image o un texto con el tema activo. Recuerda el color original (claro) y lo sustituye por su pareja oscura
    /// según <see cref="UiTheme"/>; reacciona a <see cref="GameSettings.ThemeChanged"/> sin recargar la escena.
    /// Se añade solo al cargar cada escena (<see cref="ThemeBinder"/>); nadie lo coloca a mano.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemedGraphic : MonoBehaviour
    {
        // Serializados para que las copias (Instantiate de plantillas) conserven el color claro y no el ya oscurecido.
        [SerializeField] private Color original;
        [SerializeField] private bool captured;

        private Graphic _graphic;
        private bool _changed;

        public Color Original => original;

        /// <summary>Color que debe mostrarse ahora (lo usa UIButtonAnimator como color base del botón).</summary>
        public Color Current { get { Capture(); return Resolve(); } }

        private bool IsText => _graphic is TMP_Text;

        private void Awake() => _graphic = GetComponent<Graphic>();

        /// <summary>Memoriza el color original. Se llama al añadir el componente: los objetos inactivos (plantillas) no ejecutan OnEnable.</summary>
        public void Capture()
        {
            if (captured) return;
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            original = _graphic.color;
            captured = true;
        }

        private void OnEnable()
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            Capture();
            GameSettings.ThemeChanged += Apply;
            Apply();
        }

        private void OnDisable() => GameSettings.ThemeChanged -= Apply;

        /// <summary>Fija un color claro nuevo (p. ej. el que decide un controlador) y lo muestra con el tema actual.</summary>
        public static void Set(Graphic graphic, Color lightColor)
        {
            var themed = graphic.GetComponent<ThemedGraphic>();
            if (themed == null) { graphic.color = lightColor; return; } // aún sin tema (p. ej. antes del enlace de escena)
            themed.original = lightColor; themed.captured = true;
            themed.Apply();
        }

        public void Apply()
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic == null) return;
            Capture();
            var target = Resolve();
            if (!_changed && target == original) return;
            _graphic.color = target;
            _changed = target != original;
        }

        private Color Resolve()
        {
            if (!IsText) return UiTheme.Surface(original);
            return UiTheme.Text(original, FaceBehind());
        }

        /// <summary>Color claro del Image opaco más cercano por encima del texto (el «botón» o «tarjeta» sobre el que está escrito).</summary>
        private Color? FaceBehind()
        {
            for (var t = transform.parent; t != null; t = t.parent)
            {
                var image = t.GetComponent<Image>();
                if (image == null || image.color.a < 0.5f) continue;
                var themed = t.GetComponent<ThemedGraphic>();
                return themed != null && themed.captured ? themed.original : image.color;
            }
            return null;
        }
    }

    /// <summary>Velo oscuro sobre el mundo 3D que solo se ve en modo oscuro.</summary>
    public class ThemeDim : MonoBehaviour
    {
        private Image _image;

        private void OnEnable()
        {
            _image = GetComponent<Image>();
            GameSettings.ThemeChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => GameSettings.ThemeChanged -= Refresh;
        private void Refresh() { if (_image != null) _image.enabled = UiTheme.Dark; }
    }

    /// <summary>Enlaza el tema a todas las interfaces de cada escena al cargarla (incluido el menú, que está escrito a mano).</summary>
    public static class ThemeBinder
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => Bind(scene);
            Bind(SceneManager.GetActiveScene());
        }

        private static void Bind(Scene scene)
        {
            var added = new List<ThemedGraphic>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace || canvas.sortingOrder >= 1000) continue;
                    if (canvas.transform.Find("ThemeDim") == null) AddDim(canvas);

                    foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
                    {
                        if (graphic is RawImage) continue;                       // vídeo de cámara
                        if (graphic.name == "ThemeDim" || graphic.name == "StatusLabel") continue; // StatusMessage elige su propio color
                        if (graphic.GetComponent<ThemedGraphic>() != null) continue;
                        var themed = graphic.gameObject.AddComponent<ThemedGraphic>();
                        themed.Capture();
                        added.Add(themed);
                    }
                }
            }
            // Segunda pasada: ya existen todos, así que cada texto ve el color claro real de lo que tiene detrás.
            foreach (var themed in added) themed.Apply();
        }

        private static void AddDim(Canvas canvas)
        {
            var go = new GameObject("ThemeDim", typeof(RectTransform), typeof(Image), typeof(ThemeDim));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = UiTheme.Dim;
            image.raycastTarget = false;
            image.enabled = UiTheme.Dark;
        }
    }
}
