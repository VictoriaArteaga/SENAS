using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Lumina.Interaction;
using Lumina.NPC;
using Lumina.Player;
using Sunbox.Avatars;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Construye (de forma repetible) el contenido de LuminaWorld.unity con los assets importados.
    /// Menú: Lumina > Construir escena LuminaWorld.
    /// </summary>
    public static class LuminaSceneBuilder
    {
        private const string ScenePath = "Assets/Project/Scenes/LuminaWorld.unity";
        private const string VillageDir = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        private const string AvatarPrefabPath = "Assets/Models/SunboxGames/Avatars/Prefabs/Avatar.prefab";
        private const string AvatarResources = "Assets/Models/SunboxGames/Avatars/Resources/";
        private const string SenaPrefabPath = "Assets/Project/Prefabs/Sena.prefab";
        private const string GroundMaterialPath = "Assets/Project/Materials/Lumina_Ground.mat";
        private const string RootName = "--- LUMINA ---";

        // Casas y árboles del paquete son grandes frente a un humano de 1.8 m; se reducen a una escala coherente.
        private const float HouseScale = 0.5f;
        private const float TreeScale = 0.7f;

        [MenuItem("Lumina/Construir escena LuminaWorld")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Idempotente: elimina lo generado anteriormente (la cámara original se conserva).
            var camera = Camera.main != null ? Camera.main.gameObject : null;
            if (camera != null) camera.transform.SetParent(null);
            DestroyIfExists(RootName);
            DestroyIfExists("Player");

            var root = new GameObject(RootName).transform;
            var environment = Child(root, "Environment");
            var ground = Child(environment, "Ground");
            var paths = Child(environment, "Paths");
            var buildings = Child(environment, "Buildings");
            var vegetation = Child(environment, "Vegetation");
            var props = Child(environment, "Props");
            var characters = Child(root, "Characters");
            var spawn = Child(root, "PlayerSpawn");

            BuildLighting();
            BuildGround(ground);
            BuildPaths(paths);
            BuildBuildings(buildings);
            BuildProps(props);
            BuildVegetation(vegetation);

            spawn.position = new Vector3(0f, 0.1f, -20f);
            spawn.rotation = Quaternion.identity;
            BuildPlayer(spawn, camera);
            BuildSena(characters);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lumina] Escena LuminaWorld construida y guardada.");
        }

        // ---------- Iluminación y cielo ----------

        private static void BuildLighting()
        {
            string[] guids = AssetDatabase.FindAssets("FS002_Day t:Material");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != "FS002_Day") continue;
                RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(path);
                break;
            }
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = false;

            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
            }
            DynamicGI.UpdateEnvironment();
        }

        // ---------- Suelo ----------

        private static void BuildGround(Transform parent)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "Lumina_Ground" };
                material.SetColor("_BaseColor", new Color(0.36f, 0.58f, 0.28f));
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }

            // El paquete de aldea no incluye una superficie modular de suelo: un plano con material es suficiente.
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "GroundPlane";
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            plane.transform.localScale = new Vector3(15f, 1f, 15f); // 150 x 150 m
            plane.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------- Caminos y plaza ----------

        private static void BuildPaths(Transform parent)
        {
            var plaza = Child(parent, "Plaza");
            var plazaTile = LoadVillage("Path_2");
            const float tile = 4.6f;
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    Place(plazaTile, plaza, new Vector3(x * tile, 0f, z * tile), 0f, 1f, "PlazaTile");

            var road = Child(parent, "MainRoad");
            var roadTile = LoadVillage("Path_1");
            for (int i = 0; i < 4; i++)
                Place(roadTile, road, new Vector3(0f, 0f, -9.2f - i * tile), 0f, 1f, "RoadTile");
        }

        // ---------- Edificios ----------

        private static void BuildBuildings(Transform parent)
        {
            Place(LoadVillage("House_1"), parent, new Vector3(-17f, 0f, 4f), 90f, HouseScale, "House_1");
            Place(LoadVillage("House_2"), parent, new Vector3(17f, 0f, 6f), -90f, HouseScale, "House_2");
            Place(LoadVillage("House_3"), parent, new Vector3(0f, 0f, 22f), 180f, HouseScale, "House_3");
            Place(LoadVillage("House_2"), parent, new Vector3(-16f, 0f, -16f), 90f, HouseScale, "House_2b");
            Place(LoadVillage("House_1"), parent, new Vector3(16f, 0f, -14f), -90f, HouseScale, "House_1b");
        }

        // ---------- Decoración ----------

        private static void BuildProps(Transform parent)
        {
            var bench = LoadVillage("Bench");
            Place(bench, parent, new Vector3(-8f, 0f, 0f), 90f, 1f, "Bench");
            Place(bench, parent, new Vector3(8f, 0f, 0f), -90f, 1f, "Bench");

            var lantern = LoadVillage("Lantern");
            foreach (var p in new[] { new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, 8f), new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f) })
                Place(lantern, parent, p, 0f, 1f, "Lantern");

            var fence = LoadVillage("Fence");
            for (int i = 0; i < 4; i++)
            {
                Place(fence, parent, new Vector3(-12f + i * 3.2f, 0f, 12f), 0f, 1f, "Fence");
                Place(fence, parent, new Vector3(12f - i * 3.2f, 0f, 12f), 0f, 1f, "Fence");
            }

            Place(LoadVillage("Crate"), parent, new Vector3(-12.5f, 0f, -8f), 20f, 1f, "Crate");
            Place(LoadVillage("Crate"), parent, new Vector3(-12.5f, 0f, -9.2f), 5f, 1f, "Crate");
            Place(LoadVillage("Flower Pot"), parent, new Vector3(-6f, 0f, 6f), 0f, 1f, "FlowerPot");
            Place(LoadVillage("Flower Pot"), parent, new Vector3(6f, 0f, 6f), 0f, 1f, "FlowerPot");
        }

        // ---------- Vegetación ----------

        private static void BuildVegetation(Transform parent)
        {
            var rng = new System.Random(2026);
            string[] trees = { "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Pine Tree_1", "Pine Tree_2", "Pine Tree_3" };
            string[] flowers = { "Flower_1", "Flower_2", "Flower_3", "Flower_4", "Flower_5" };
            string[] rocks = { "Rock_1", "Rock_2", "Rock_3" };

            int placed = 0, attempts = 0;
            while (placed < 45 && attempts < 1000)
            {
                attempts++;
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = Mathf.Lerp(26f, 60f, (float)rng.NextDouble());
                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (pos.z < -10f && Mathf.Abs(pos.x) < 6f) continue; // mantener libre el camino de entrada
                if (Overlaps(pos, 12f)) continue;

                float scale = TreeScale * Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble());
                Place(LoadVillage(trees[rng.Next(trees.Length)]), parent, pos, (float)rng.NextDouble() * 360f, scale, "Tree");
                placed++;
            }

            for (int i = 0; i < 20; i++)
            {
                float x = Mathf.Lerp(-22f, 22f, (float)rng.NextDouble());
                float z = Mathf.Lerp(-26f, 20f, (float)rng.NextDouble());
                var pos = new Vector3(x, 0f, z);
                if (Mathf.Abs(x) < 8f && z > -10f && z < 8f) continue; // plaza despejada
                if (Mathf.Abs(x) < 3.5f && z < 0f) continue;            // camino
                if (Overlaps(pos, 4f)) continue;
                string name = i % 4 == 0 ? rocks[rng.Next(rocks.Length)] : flowers[rng.Next(flowers.Length)];
                Place(LoadVillage(name), parent, pos, (float)rng.NextDouble() * 360f, 1f, name);
            }
        }

        // Evita colocar vegetación sobre casas ya instanciadas.
        private static bool Overlaps(Vector3 pos, float margin)
        {
            var env = GameObject.Find(RootName);
            if (env == null) return false;
            var buildings = env.transform.Find("Environment/Buildings");
            if (buildings == null) return false;
            foreach (Transform b in buildings)
            {
                var flat = b.position - pos;
                flat.y = 0f;
                if (flat.magnitude < 8f + margin) return true;
            }
            var plazaFlat = new Vector3(pos.x, 0f, pos.z);
            return plazaFlat.magnitude < 10f;
        }

        // ---------- Jugador ----------

        private static void BuildPlayer(Transform spawn, GameObject existingCamera)
        {
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;

            // Se reutiliza la Main Camera existente para no duplicar cámaras ni AudioListeners.
            if (existingCamera == null)
            {
                existingCamera = new GameObject("Main Camera");
                existingCamera.tag = "MainCamera";
                existingCamera.AddComponent<Camera>();
                existingCamera.AddComponent<AudioListener>();
            }
            existingCamera.transform.SetParent(player.transform, false);
            existingCamera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            existingCamera.transform.localRotation = Quaternion.identity;
            existingCamera.GetComponent<Camera>().nearClipPlane = 0.1f;

            player.AddComponent<FirstPersonController>();
        }

        // ---------- Sena ----------

        private static void BuildSena(Transform characters)
        {
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);

            // Variante de prefab sobre el Avatar oficial del paquete (se conserva el original sin modificar).
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
            temp.name = "Sena";
            var custom = temp.GetComponent<AvatarCustomization>();

            var so = new SerializedObject(custom);
            so.FindProperty("CurrentGender").enumValueIndex = (int)AvatarCustomization.AvatarGender.Female;
            so.FindProperty("ClothingItemTop").objectReferenceValue = LoadClothing("Tops/ClothingItem_Blouse");
            so.FindProperty("ClothingItemBottom").objectReferenceValue = LoadClothing("Bottoms/ClothingItem_Skirt_01");
            so.FindProperty("ClothingItemShoes").objectReferenceValue = LoadClothing("Shoes/ClothingItem_Flats");
            so.ApplyModifiedPropertiesWithoutUndo();

            TryApply(custom);

            var variant = PrefabUtility.SaveAsPrefabAsset(temp, SenaPrefabPath);
            Object.DestroyImmediate(temp);

            var sena = new GameObject("Sena");
            sena.transform.SetParent(characters, false);
            sena.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));

            var model = (GameObject)PrefabUtility.InstantiatePrefab(variant, sena.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // El jugador no debe atravesar a Sena.
            var capsule = sena.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            sena.AddComponent<ProximityInteractable>();
            sena.AddComponent<SenaPresenter>();
        }

        private static ClothingItem LoadClothing(string relativePath)
        {
            var item = AssetDatabase.LoadAssetAtPath<ClothingItem>(AvatarResources + "ClothingItems/" + relativePath + ".asset");
            if (item == null) Debug.LogWarning("[Lumina] No se encontró la prenda: " + relativePath);
            return item;
        }

        // El sistema del paquete aplica la personalización en Start(); en el editor se intenta aplicarla ya.
        private static void TryApply(AvatarCustomization custom)
        {
            try
            {
                custom.SetGender(AvatarCustomization.AvatarGender.Female, true);
                custom.UpdateCustomization();
                custom.UpdateClothing();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Lumina] No se pudo aplicar la personalización en modo editor (se aplicará en Play): " + e.Message);
            }
        }

        // ---------- Utilidades ----------

        private static GameObject LoadVillage(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageDir + name + ".prefab");
            if (prefab == null) Debug.LogError("[Lumina] Prefab no encontrado: " + name);
            return prefab;
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale, string name)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
