using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Player;
using Lumina.UI;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Integra los seis avatares de AVATARS2 (carol, luna-crimson, tom, crimson, neon, tensy) en el sistema existente, sin reconstruir escenas:
    ///  1) AvatarCatalog: añade los seis ids (el prefab es Avatar_{id}.prefab de Prefabs/PlayerAvatars). Los cuatro antiguos (luna, nube, rio, sol)
    ///     se conservan intactos y siguen primero en la lista, así que los perfiles existentes siguen resolviéndose.
    ///  2) RegisterScene: el selector (AvatarSelectorController) pasa a mostrar los seis nuevos, instanciando EXACTAMENTE los prefabs del catálogo
    ///     (los mismos que usa PlayerAvatar). Las cuatro instancias antiguas del pivote se desactivan (no se borran) y se reposicionan los puntos indicadores.
    /// Idempotente. No toca backend, AVATAR_IDS, persistencia, LuminaAuthSceneBuilder ni los prefabs/FBX/materiales aprobados.
    /// Menú: Lumina > Avatares2 > Integrar en catálogo y selector.
    /// </summary>
    public static class Avatars2Integration
    {
        private const string CatalogPath = "Assets/Project/Data/AvatarCatalog.asset";
        private const string RegisterScenePath = "Assets/Project/Scenes/RegisterScene.unity";
        // Los ids y nombres ya no se repiten aquí: salen de LuminaAvatarRoster (misma fuente que usan los builders).

        [MenuItem("Lumina/Avatares2/Integrar en catálogo y selector")]
        public static void Run()
        {
            var catalog = UpdateCatalog();
            UpdateRegisterScene(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Avatars2Integration] Integración terminada.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static AvatarCatalog UpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            // Mismo método no destructivo que usa el builder: añade lo que falte, nunca quita ni reordena.
            if (LuminaWorldAuthIntegration.MergeCatalog(catalog)) EditorUtility.SetDirty(catalog);
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("avatars");
            Debug.Log($"[Avatars2Integration] AvatarCatalog: {list.arraySize} entradas ({string.Join(", ", Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue))})");
            return catalog;
        }

        private static void UpdateRegisterScene(AvatarCatalog catalog)
        {
            var scene = EditorSceneManager.OpenScene(RegisterScenePath, OpenSceneMode.Single);
            var selector = Object.FindFirstObjectByType<AvatarSelectorController>();
            var so = new SerializedObject(selector);
            var pivot = (Transform)so.FindProperty("pivot").objectReferenceValue;

            var approved = LuminaAvatarRoster.Selectable;
            var legacyIds = LuminaAvatarRoster.Legacy.Select(e => e.Id).ToList();

            // 1) Instancias antiguas del pivote: desactivadas (no borradas).
            foreach (Transform child in pivot)
                if (legacyIds.Contains(child.name)) child.gameObject.SetActive(false);

            // 2) Instancias de los seis prefabs del catálogo (los mismos que usa PlayerAvatar).
            var models = new List<GameObject>();
            foreach (var entry in approved)
            {
                string id = entry.Id;
                var existing = pivot.Find(id);
                GameObject instance;
                if (existing != null) instance = existing.gameObject;
                else
                {
                    var prefab = catalog.Find(id).prefab;
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
                    instance.name = id;
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                }
                instance.SetActive(true);
                models.Add(instance);
            }

            // 3) options (id, nombre, modelo)
            var options = so.FindProperty("options");
            options.arraySize = approved.Count;
            for (int i = 0; i < approved.Count; i++)
            {
                var e = options.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = approved[i].Id;
                e.FindPropertyRelative("displayName").stringValue = approved[i].DisplayName;
                e.FindPropertyRelative("model").objectReferenceValue = models[i];
            }

            // 4) Puntos indicadores: seis, con la misma fórmula del builder (centrados en x=470, separación 40, y=184).
            var dots = so.FindProperty("dots");
            var dotImages = new List<Image>();
            for (int i = 0; i < dots.arraySize; i++) dotImages.Add((Image)dots.GetArrayElementAtIndex(i).objectReferenceValue);
            while (dotImages.Count < approved.Count)
            {
                var template = dotImages[dotImages.Count - 1];
                var clone = Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = "Dot" + (dotImages.Count + 1);
                dotImages.Add(clone.GetComponent<Image>());
            }
            for (int i = 0; i < approved.Count; i++)
            {
                var rt = dotImages[i].rectTransform;
                rt.anchoredPosition = new Vector2(470f + (i - (approved.Count - 1) * 0.5f) * 40f, 184f);
                dotImages[i].name = "Dot" + (i + 1);
            }
            dots.arraySize = approved.Count;
            for (int i = 0; i < approved.Count; i++) dots.GetArrayElementAtIndex(i).objectReferenceValue = dotImages[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Avatars2Integration] RegisterScene guardada: selector con " + approved.Count + " avatares.");
        }
    }
}
