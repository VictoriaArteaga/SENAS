using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Textura de paleta y material de las casas de la aldea (House_1, House_2 y House_3 de Low Poly Fantasy Village).
    /// Esos modelos tienen un único material y un único submesh, y colorean cada parte con un punto fijo de UV sobre una
    /// textura de paleta que el paquete no incluye. En vez de tocar las mallas, se pinta una paleta propia de 128×128 con
    /// un color en cada texel que usan los UV. La correspondencia texel → parte se comprobó renderizando las tres casas con
    /// una paleta de depuración (un color distinto por texel); es la misma en las tres casas.
    /// Menú: Lumina > Materiales > Generar paleta y material de las casas (idempotente).
    /// </summary>
    public static class LuminaHousePalette
    {
        public const string TexturePath = "Assets/Project/Textures/Lumina/Lumina_HousePalette.png";
        public const string MaterialPath = "Assets/Project/Materials/Lumina/Lumina_HousePalette.mat";
        private const int Size = 128;

        // Colores de la paleta de Lúmina y derivados cálidos.
        private const string Coral = "FF5E4D", Cream = "FFF9EC", Sand = "FAE3BD", SandDark = "D9BE8C", Ink = "0F3A44";
        private const string Turquoise = "2EC4B6", TurquoiseDark = "17877F", TurquoiseSoft = "8FE3D9";
        private const string WoodWarm = "A8744A", WoodDark = "7A4E2D", WoodLight = "C9955E", SoftYellow = "FFD66B", StoneWarm = "A8835A";

        // Texel (x, y desde abajo) → color. Las partes están verificadas con la paleta de depuración sobre las tres casas.
        private static readonly (int x, int y, string hex, string part)[] Cells =
        {
            (92, 66,  Coral,         "techo (y toldos)"),
            (71, 70,  Cream,         "paredes de revoque"),
            (103, 123, SandDark,     "zócalo / cimiento de la planta baja"),
            (89, 57,  WoodWarm,      "vigas y entramado de madera, barandillas"),
            (92, 55,  Sand,          "marcos y alféizares de las ventanas"),
            (89, 54,  WoodDark,      "marco de la puerta"),
            (88, 54,  Turquoise,     "tablones de la puerta (claros)"),
            (87, 55,  TurquoiseDark, "tablones de la puerta (oscuros)"),
            (88, 118, Ink,           "herrajes de la puerta (bisagras y aldaba)"),
            (89, 53,  SoftYellow,    "detalle de la puerta (pomo)"),
            (84, 54,  WoodLight,     "suelo del balcón y escalones"),
            (90, 64,  SandDark,      "chimenea (cuerpo)"),
            (90, 65,  StoneWarm,     "chimenea (cara en sombra)"),
            (115, 114, Ink,          "chimenea (remate)"),
        };

        // Los cristales usan un rectángulo de UV (sus cuatro esquinas están en estos texels), no un único punto.
        private const int GlassX0 = 20, GlassX1 = 27, GlassY0 = 100, GlassY1 = 108;

        [MenuItem("Lumina/Materiales/Generar paleta y material de las casas")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(TexturePath)));
            Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(MaterialPath)));

            var pixels = new Color32[Size * Size];
            var background = Hex(Cream);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
            foreach (var cell in Cells) pixels[cell.y * Size + cell.x] = Hex(cell.hex);
            for (int y = GlassY0; y <= GlassY1; y++)
                for (int x = GlassX0; x <= GlassX1; x++) pixels[y * Size + x] = Hex(TurquoiseSoft);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(Path.GetFullPath(TexturePath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;
            importer.SaveAndReimport();

            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lumina_HousePalette" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", map);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lumina] Paleta y material de las casas generados: " + TexturePath);
        }

        /// <summary>Material de las casas; lo genera si todavía no existe.</summary>
        public static Material Load()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null || material.GetTexture("_BaseMap") == null)
            {
                Build();
                material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }
            return material;
        }

        private static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
