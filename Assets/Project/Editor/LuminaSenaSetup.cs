using UnityEditor;
using UnityEngine;
using Sunbox.Avatars;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Ajustes de apariencia de Sena aplicados solo sobre su prefab (el Avatar.prefab del paquete no se toca).
    /// </summary>
    public static class LuminaSenaSetup
    {
        private const string SenaPrefabPath = "Assets/Project/Prefabs/Sena.prefab";

        // Índices de variación del sistema de ropa del paquete.
        private const int BlouseWhite = 1;
        private const int SkirtBlack = 0;
        private const int FlatsBlack = 0;

        [MenuItem("Lumina/Aplicar apariencia de Sena")]
        public static void Apply()
        {
            var contents = PrefabUtility.LoadPrefabContents(SenaPrefabPath);
            try
            {
                var custom = contents.GetComponent<AvatarCustomization>();
                var so = new SerializedObject(custom);
                so.FindProperty("ClothingItemTopVariationIndex").intValue = BlouseWhite;
                so.FindProperty("ClothingItemBottomVariationIndex").intValue = SkirtBlack;
                so.FindProperty("ClothingItemShoesVariationIndex").intValue = FlatsBlack;
                so.ApplyModifiedPropertiesWithoutUndo();

                // La ropa queda instanciada dentro del prefab: se regenera para reflejar las variaciones.
                custom.UpdateClothing();
                PrefabUtility.SaveAsPrefabAsset(contents, SenaPrefabPath);
                Debug.Log("[Lumina] Apariencia de Sena actualizada (blusa blanca, falda negra).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
