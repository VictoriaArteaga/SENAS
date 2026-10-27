using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Colores provisionales para los modelos de Low Poly Fantasy Village.
    /// El paquete (v0.1.0) referencia una textura de paleta inexistente en Materials/Color.mat, por lo que
    /// los modelos salen blancos. Se crean variantes de ese material (el original no se modifica) con un
    /// color base por categoría y se asignan como override en las instancias de la escena.
    /// Si se restaura la textura original, usar "Quitar colores provisionales" (restaura Color.mat).
    /// </summary>
    public static class LuminaMaterialTint
    {
        private const string ScenePath = "Assets/Project/Scenes/LuminaWorld.unity";
        private const string ParentMaterialPath = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat";
        private const string OutputDir = "Assets/Project/Materials/";
        private const string MaterialPrefix = "Lumina_Village_";

        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            { "House",   new Color(0.93f, 0.85f, 0.70f) },
            { "Tree",    new Color(0.36f, 0.62f, 0.30f) },
            { "Pine",    new Color(0.17f, 0.44f, 0.28f) },
            { "Path",    new Color(0.74f, 0.71f, 0.64f) },
            { "Wood",    new Color(0.55f, 0.38f, 0.22f) },
            { "Metal",   new Color(0.26f, 0.26f, 0.30f) },
            { "Rock",    new Color(0.52f, 0.52f, 0.54f) },
            { "Flower",  new Color(0.93f, 0.48f, 0.60f) },
            { "Terracotta", new Color(0.76f, 0.42f, 0.30f) },
        };

        private static string Category(string prefabName)
        {
            if (prefabName.StartsWith("House")) return "House";
            if (prefabName.StartsWith("Pine Tree")) return "Pine";
            if (prefabName.StartsWith("Tree")) return "Tree";
            if (prefabName.StartsWith("Path")) return "Path";
            if (prefabName.StartsWith("Fence") || prefabName.StartsWith("Bench") || prefabName.StartsWith("Crate")) return "Wood";
            if (prefabName.StartsWith("Lantern")) return "Metal";
            if (prefabName.StartsWith("Rock")) return "Rock";
            if (prefabName.StartsWith("Flower Pot")) return "Terracotta";
            if (prefabName.StartsWith("Flower")) return "Flower";
            return null;
        }

        [MenuItem("Lumina/Aplicar colores provisionales")]
        public static void Apply() => Process(true);

        [MenuItem("Lumina/Quitar colores provisionales")]
        public static void Remove() => Process(false);

        private static void Process(bool apply)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var parent = AssetDatabase.LoadAssetAtPath<Material>(ParentMaterialPath);
            var variants = new Dictionary<string, Material>();
            int changed = 0;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                if (source == null) continue;
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
                if (root == null) continue;
                var rootSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                string category = rootSource != null ? Category(rootSource.name) : null;
                if (category == null) continue;

                var mats = renderer.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    // Solo se reemplaza el material de paleta; "Light" (luz del farol) se conserva.
                    if (apply)
                    {
                        if (mats[i] != parent) continue;
                        mats[i] = GetVariant(variants, parent, category);
                        dirty = true;
                    }
                    else
                    {
                        if (mats[i] == null || !mats[i].name.StartsWith(MaterialPrefix)) continue;
                        mats[i] = parent;
                        dirty = true;
                    }
                }
                if (!dirty) continue;

                renderer.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                changed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lumina] Colores provisionales {(apply ? "aplicados" : "quitados")} en {changed} renderers.");
        }

        private static Material GetVariant(Dictionary<string, Material> cache, Material parent, string category)
        {
            if (cache.TryGetValue(category, out var cached)) return cached;

            // Copia independiente de Color.mat (no una variante: las variantes no se resolvían bien en el primer render).
            string path = OutputDir + MaterialPrefix + category + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(parent) { name = MaterialPrefix + category };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Palette[category]);
            EditorUtility.SetDirty(material);
            cache[category] = material;
            return material;
        }
    }
}
