using UnityEngine;

namespace Lumina.Core
{
    /// <summary>
    /// Paleta del tema oscuro de Lúmina y la tabla de equivalencias claro → oscuro. Las escenas siguen escritas con la paleta
    /// clara (la identidad original); en modo oscuro cada color de superficie o de texto se sustituye por su pareja, y los
    /// acentos (coral y turquesa) se conservan.
    /// </summary>
    public static class UiTheme
    {
        public static bool Dark => GameSettings.DarkMode;

        private enum Role { None, Cream, CreamWarm, Sand, SandDark, Ink, TurquoiseDark, CoralDark, Turquoise, TurquoiseSoft, Coral, ErrorRed }

        private static readonly (Role role, Color color)[] Light =
        {
            (Role.Cream, new Color(1f, 0.976f, 0.925f)),
            (Role.CreamWarm, new Color(1f, 0.953f, 0.863f)),
            (Role.Sand, new Color(0.98f, 0.89f, 0.74f)),
            (Role.SandDark, new Color(0.851f, 0.745f, 0.549f)),
            (Role.Ink, new Color(0.059f, 0.227f, 0.267f)),
            (Role.TurquoiseDark, new Color(0.09f, 0.529f, 0.498f)),
            (Role.CoralDark, new Color(0.812f, 0.29f, 0.247f)),
            (Role.Turquoise, new Color(0.18f, 0.769f, 0.714f)),
            (Role.TurquoiseSoft, new Color(0.56f, 0.89f, 0.85f)),
            (Role.Coral, new Color(1f, 0.369f, 0.302f)),
            (Role.ErrorRed, new Color(0.78f, 0.22f, 0.18f)),
        };

        // Noche de Lúmina: azul petróleo profundo en lugar de negro, con el crema como color de texto.
        private static readonly Color NightCream = new Color(0.075f, 0.196f, 0.231f);     // tarjetas
        private static readonly Color NightCreamWarm = new Color(0.106f, 0.259f, 0.302f);  // superficies secundarias
        private static readonly Color NightSand = new Color(0.141f, 0.314f, 0.357f);       // cajas, barras, píldoras
        private static readonly Color NightSandDark = new Color(0.039f, 0.118f, 0.141f);   // sombras y bordes
        private static readonly Color NightInk = new Color(0.024f, 0.11f, 0.133f);         // marcos y selección
        private static readonly Color TextCream = new Color(1f, 0.953f, 0.863f);
        private static readonly Color TextTurquoise = new Color(0.37f, 0.84f, 0.8f);
        private static readonly Color TextCoral = new Color(1f, 0.5f, 0.44f);
        private static readonly Color TextError = new Color(1f, 0.56f, 0.5f);

        /// <summary>Color de fondo del fundido entre escenas.</summary>
        public static Color FadeColor => Dark ? NightCream : new Color(1f, 0.976f, 0.925f);

        /// <summary>Velo que oscurece el mundo 3D detrás de la interfaz en modo oscuro.</summary>
        public static readonly Color Dim = new Color(0.02f, 0.09f, 0.12f, 0.42f);

        private static Role Identify(Color c)
        {
            Role best = Role.None; float bestDistance = 0.05f; // estrecho: el blanco puro (caras con sprite del menú) no debe confundirse con el crema
            foreach (var (role, color) in Light)
            {
                float d = Mathf.Sqrt(Sq(c.r - color.r) + Sq(c.g - color.g) + Sq(c.b - color.b));
                if (d < bestDistance) { bestDistance = d; best = role; }
            }
            return best;
        }

        private static float Sq(float v) => v * v;

        /// <summary>True si el color claro es una superficie que pasa a oscuro (y por tanto el texto encima debe aclararse).</summary>
        public static bool IsSurface(Color light)
        {
            var r = Identify(light);
            return r == Role.Cream || r == Role.CreamWarm || r == Role.Sand || r == Role.SandDark;
        }

        /// <summary>Color de relleno (Image) en el tema actual. <paramref name="light"/> es el color original, claro.</summary>
        public static Color Surface(Color light)
        {
            if (!Dark) return light;
            Color? mapped = Identify(light) switch
            {
                Role.Cream => NightCream,
                Role.CreamWarm => NightCreamWarm,
                Role.Sand => NightSand,
                Role.SandDark => NightSandDark,
                Role.Ink => NightInk,
                _ => (Color?)null
            };
            return mapped.HasValue ? new Color(mapped.Value.r, mapped.Value.g, mapped.Value.b, light.a) : light;
        }

        /// <summary>
        /// Color de texto en el tema actual. <paramref name="faceLight"/> es el color claro de lo que hay detrás (null si es el
        /// fondo): el texto oscuro solo se aclara sobre superficies oscuras; sobre botones turquesa/coral o campos blancos se conserva.
        /// </summary>
        public static Color Text(Color light, Color? faceLight = null)
        {
            if (!Dark) return light;
            Color? mapped = null;
            switch (Identify(light))
            {
                case Role.Ink: if (!faceLight.HasValue || IsSurface(faceLight.Value)) mapped = TextCream; break;
                case Role.TurquoiseDark: mapped = TextTurquoise; break;
                case Role.CoralDark: mapped = TextCoral; break;
                case Role.ErrorRed: mapped = TextError; break;
            }
            return mapped.HasValue ? new Color(mapped.Value.r, mapped.Value.g, mapped.Value.b, light.a) : light;
        }
    }
}
