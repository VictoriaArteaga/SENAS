using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Lumina.Player;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Importa un avatar estático de AVATARS2 ya convertido a FBX (sin rig): configura el importador del FBX y de sus texturas,
    /// crea un material URP/Lit por parte y el prefab Avatar_{id}. No toca los GLB originales ni los prefabs existentes.
    /// Espera: Assets/Project/Models/Avatars2/{id}/{id}.fbx y Textures/{id}_{parte}_{albedo|normal|metalsmooth}.*
    /// (metalsmooth: R = metálico, A = suavidad, es decir el _MetallicGlossMap de URP/Lit).
    /// Menú: Lumina > Avatares2 > Importar Carol (idempotente).
    /// </summary>
    public static class Avatars2Importer
    {
        private const string ModelsDir = "Assets/Project/Models/Avatars2/";
        private const string MaterialsDir = "Assets/Project/Materials/Avatars2/";
        private const string PrefabsDir = "Assets/Project/Prefabs/PlayerAvatars/";

        [MenuItem("Lumina/Avatares2/Importar Carol")]
        public static void ImportCarol() => Import("carol");

        /// <summary>Línea de comandos: -executeMethod Lumina.EditorTools.Avatars2Importer.ImportFromArgs -avatar tom [-avatar neon ...]</summary>
        public static void ImportFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-avatar") Import(args[i + 1]);
        }

        /// <summary>Partes (sufijo del nombre de la malla) que ese avatar oculta cuando es el cuerpo del jugador en primera persona.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string[]> HiddenInFirstPerson = new()
        {
            { "carol", new[] { "head", "hair", "cap" } },   // el cuello (Carol_neck) queda visible; la cabeza sigue dando sombra
        };

        /// <param name="id">Identificador del avatar (carpeta, texturas y materiales).</param>
        /// <param name="fbxName">Nombre del FBX dentro de la carpeta (sin extensión); por defecto el id.</param>
        /// <param name="prefabName">Nombre del prefab a crear; por defecto Avatar_{id}.</param>
        public static void Import(string id, string fbxName = null, string prefabName = null)
        {
            string dir = ModelsDir + id + "/";
            string fbx = dir + (fbxName ?? id) + ".fbx";
            string texDir = dir + "Textures/";

            // Texturas: albedo en sRGB, normales como mapa de normales, metalsmooth lineal (no es color).
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                string file = Path.GetFileNameWithoutExtension(path);
                bool changed = false;
                var type = file.EndsWith("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != type) { importer.textureType = type; changed = true; }
                bool srgb = file.EndsWith("_albedo");
                if (type == TextureImporterType.Default && importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
                if (file.EndsWith("_albedo") && importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None)
                { importer.alphaSource = TextureImporterAlphaSource.None; changed = true; }   // material opaco: el alfa del PNG es relleno, no transparencia
                if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
                if (importer.maxTextureSize != 4096) { importer.maxTextureSize = 4096; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Compressed) { importer.textureCompression = TextureImporterCompression.Compressed; changed = true; }
                if (changed) importer.SaveAndReimport();   // sin cambios no se reescribe nada (los .meta y las texturas quedan como estaban)
            }

            // FBX: solo malla. Sin animación ni Avatar; los materiales se crean aparte.
            var model = (ModelImporter)AssetImporter.GetAtPath(fbx);
            bool modelChanged = model.animationType != ModelImporterAnimationType.None || model.importAnimation
                || model.materialImportMode != ModelImporterMaterialImportMode.None || model.importCameras || model.importLights
                || model.isReadable || model.meshCompression != ModelImporterMeshCompression.Off
                || model.importNormals != ModelImporterNormals.Import || model.importTangents != ModelImporterTangents.CalculateMikk;
            if (modelChanged)
            {
                model.animationType = ModelImporterAnimationType.None;
                model.importAnimation = false;
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                model.importCameras = false;
                model.importLights = false;
                model.isReadable = false;
                model.meshCompression = ModelImporterMeshCompression.Off;
                model.importNormals = ModelImporterNormals.Import;
                model.importTangents = ModelImporterTangents.CalculateMikk;
                model.SaveAndReimport();
            }

            Directory.CreateDirectory(MaterialsDir + id);
            Directory.CreateDirectory(PrefabsDir);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial = BuildMaterial(id, MaterialPart(PartOf(renderer, id)), texDir);
            }

            if (HiddenInFirstPerson.TryGetValue(id, out var hidden))
            {
                var component = instance.AddComponent<FirstPersonHiddenParts>();
                var parts = instance.GetComponentsInChildren<MeshRenderer>(true).Where(r => hidden.Contains(PartOf(r, id))).ToArray();
                var serialized = new SerializedObject(component);
                var list = serialized.FindProperty("parts");
                list.arraySize = parts.Length;
                for (int i = 0; i < parts.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Avatars2Importer] FirstPersonHiddenParts: {string.Join(", ", parts.Select(r => r.name))}");
            }

            string prefabPath = PrefabsDir + (prefabName ?? "Avatar_" + id) + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Avatars2Importer] Prefab creado: {prefabPath}");
            Report(prefab);
        }

        private static string PartOf(Renderer renderer, string id) => renderer.gameObject.name.Substring(id.Length + 1).ToLowerInvariant();   // "Carol_body" -> "body"

        /// <summary>El cuello comparte la textura y el material de la cabeza (de ahí sale en el modelo original).</summary>
        private static string MaterialPart(string part) => part == "neck" ? "head" : part;

        private static Material BuildMaterial(string id, string part, string texDir)
        {
            string path = $"{MaterialsDir}{id}/{id}_{part}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;   // un material existente (compartido con otros prefabs) no se reescribe
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"{id}_{part}" };
            AssetDatabase.CreateAsset(material, path);

            material.SetTexture("_BaseMap", LoadTexture(texDir, $"{id}_{part}_albedo"));
            material.SetColor("_BaseColor", Color.white);

            var normal = LoadTexture(texDir, $"{id}_{part}_normal");
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            SetKeyword(material, "_NORMALMAP", normal != null);

            var metalSmooth = LoadTexture(texDir, $"{id}_{part}_metalsmooth");
            material.SetTexture("_MetallicGlossMap", metalSmooth);
            material.SetFloat("_Metallic", 1f);        // con mapa, el factor multiplica la textura
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);   // suavidad en el alfa del mapa metálico
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metalSmooth != null);

            var emission = LoadTexture(texDir, $"{id}_{part}_emission");
            if (emission != null)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white);   // emissiveFactor [1,1,1] del glTF
                material.EnableKeyword("_EMISSION");
            }

            material.SetFloat("_Cull", 0f);            // el glTF original es doubleSided (ropa, falda, pelo)
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string dir, string name)
        {
            string guid = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { dir })
                .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        private static void Report(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var bounds = new Bounds();
            bool first = true;
            int tris = 0, missing = 0;
            foreach (var r in renderers)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                tris += mesh.triangles.Length / 3;
                var b = r.transform.TransformBounds(mesh.bounds);
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
                if (r.sharedMaterial == null || r.sharedMaterial.GetTexture("_BaseMap") == null) missing++;
                Debug.Log($"[Avatars2Importer]   {r.name}: tris={mesh.triangles.Length / 3} mat={r.sharedMaterial?.name} centro={r.transform.TransformBounds(mesh.bounds).center:F3}");
            }
            Debug.Log($"[Avatars2Importer] {prefab.name}: renderers={renderers.Length} tris={tris} min={bounds.min:F3} max={bounds.max:F3} tamaño={bounds.size:F3} sinTextura={missing}");
            Debug.Log($"[Avatars2Importer] Componentes en la raíz: {string.Join(", ", prefab.GetComponents<Component>().Select(c => c.GetType().Name))}");
        }

        private static Bounds TransformBounds(this Transform t, Bounds local)
        {
            var b = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sy in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                        b.Encapsulate(t.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(sx, sy, sz))));
            return b;
        }
    }
}
