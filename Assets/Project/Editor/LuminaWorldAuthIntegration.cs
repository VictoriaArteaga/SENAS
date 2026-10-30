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
        private const string AvatarPrefabDir = "Assets/Project/Prefabs/Avatars/";
        private const string LevelScene = "AlphabetHouse";
        private const string StartLabel = "COMENZAR";

        private static readonly string[,] Avatars = { { "luna", "Luna" }, { "nube", "Nube" }, { "rio", "Río" }, { "sol", "Sol" } };

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

        private static AvatarCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var so = new SerializedObject(catalog);
            var list = so.FindProperty("avatars");
            list.arraySize = Avatars.GetLength(0);
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabDir + "Avatar_" + Avatars[i, 0] + ".prefab");
                if (prefab == null) throw new System.InvalidOperationException("Falta el prefab del avatar " + Avatars[i, 0]);
                element.FindPropertyRelative("id").stringValue = Avatars[i, 0];
                element.FindPropertyRelative("displayName").stringValue = Avatars[i, 1];
                element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
