using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lumina.Interaction;
using Lumina.NPC;
using Lumina.Player;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Conecta LuminaWorld con el flujo de acceso, de forma repetible y sin reconstruir la escena:
    ///  · el avatar elegido en el registro se pone como cuerpo del jugador (PlayerAvatar + AvatarCatalog);
    ///  · el último botón de la presentación de Sena pasa a decir COMENZAR y abre el Nivel 1 (escena AlphabetHouse).
    /// Menú: Lumina &gt; Autenticación &gt; Integrar avatar y COMENZAR en LuminaWorld.
    /// </summary>
    public static class LuminaWorldAuthIntegration
    {
        private const string WorldPath = "Assets/Project/Scenes/LuminaWorld.unity";
        private const string CatalogPath = "Assets/Project/Data/AvatarCatalog.asset";
        private const string LevelScene = "AlphabetHouse";
        private const string StartLabel = "COMENZAR";

        /// <summary>Reconstruye Login/Register (formularios con layout) e integra LuminaWorld, en una sola ejecución.</summary>
        public static void RebuildAndApply()
        {
            LuminaAuthSceneBuilder.RebuildLoginAndRegister();
            Apply();
        }

        [MenuItem("Lumina/Autenticación/Integrar avatar y COMENZAR en LuminaWorld")]
        public static void Apply()
        {
            var catalog = EnsureCatalog();

            var scene = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);

            var controller = Object.FindFirstObjectByType<Lumina.Player.FirstPersonController>();
            if (controller == null) throw new System.InvalidOperationException("No hay FirstPersonController en LuminaWorld.");
            var playerAvatar = controller.GetComponent<PlayerAvatar>() ?? controller.gameObject.AddComponent<PlayerAvatar>();
            SetRef(playerAvatar, "catalog", catalog);

            var welcome = Object.FindFirstObjectByType<SenaWelcomeInteraction>();
            if (welcome == null) throw new System.InvalidOperationException("No hay SenaWelcomeInteraction en LuminaWorld.");
            var so = new SerializedObject(welcome);
            so.FindProperty("finishLabel").stringValue = StartLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            var entrance = welcome.GetComponent<SceneEntrance>() ?? welcome.gameObject.AddComponent<SceneEntrance>();
            var entranceSo = new SerializedObject(entrance);
            entranceSo.FindProperty("sceneName").stringValue = LevelScene;
            entranceSo.ApplyModifiedPropertiesWithoutUndo();

            // Una sola llamada persistente: al terminar la presentación se entra al Nivel 1.
            bool wired = false;
            var calls = welcome.onWelcomeCompleted;
            for (int i = 0; i < calls.GetPersistentEventCount(); i++)
                if (calls.GetPersistentTarget(i) == entrance && calls.GetPersistentMethodName(i) == nameof(SceneEntrance.Enter)) wired = true;
            if (!wired) UnityEventTools.AddVoidPersistentListener(calls, entrance.Enter);

            EditorUtility.SetDirty(welcome);
            EditorUtility.SetDirty(entrance);
            EditorUtility.SetDirty(playerAvatar);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lumina] LuminaWorld integrado: avatar del jugador y botón COMENZAR → " + LevelScene);
        }

        /// <summary>
        /// Carga (o crea) AvatarCatalog.asset y se asegura de que contenga las 10 entradas de LuminaAvatarRoster. NO lo sobrescribe: ver MergeCatalog.
        /// </summary>
        private static AvatarCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (MergeCatalog(catalog)) EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>
        /// Añade al catálogo las entradas de LuminaAvatarRoster que falten (los 4 antiguos primero y después los 6 de AVATARS2). Es idempotente y NO
        /// destructivo: nunca elimina, reordena ni reemplaza entradas existentes (ni sus prefabs ni nombres); solo rellena el prefab de una entrada
        /// existente si estaba vacío. Devuelve true si cambió algo. Trabaja sobre el objeto recibido (no guarda nada por sí mismo).
        /// </summary>
        internal static bool MergeCatalog(AvatarCatalog catalog)
        {
            bool changed = false;
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("avatars");
            foreach (var wanted in LuminaAvatarRoster.Catalog)
            {
                int index = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == wanted.Id) { index = i; break; }

                var prefab = wanted.LoadPrefab();
                if (index < 0)
                {
                    if (prefab == null) throw new System.InvalidOperationException("Falta el prefab del avatar " + wanted.Id + " (" + wanted.PrefabPath + ")");
                    index = list.arraySize;
                    list.arraySize++;
                    var added = list.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("id").stringValue = wanted.Id;
                    added.FindPropertyRelative("displayName").stringValue = wanted.DisplayName;
                    added.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    changed = true;
                }
                else
                {
                    var existing = list.GetArrayElementAtIndex(index).FindPropertyRelative("prefab");
                    if (existing.objectReferenceValue == null && prefab != null) { existing.objectReferenceValue = prefab; changed = true; }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
