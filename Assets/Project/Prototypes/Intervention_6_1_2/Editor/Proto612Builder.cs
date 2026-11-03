using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Lumina.Core;
using Lumina.Player;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Construye las dos escenas del prototipo 6.1.2 (Prototype_Modular y Prototype_Terrain). Escribe únicamente en
    /// Assets/Project/Prototypes/Intervention_6_1_2/. Los assets de producción solo se referencian: nunca se modifican.
    /// </summary>
    public static class Proto612Builder
    {
        public const string Root = "Assets/Project/Prototypes/Intervention_6_1_2/";
        const string VillageDir = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        const string VillageColor = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat";
        const string HousePalette = "Assets/Project/Materials/Lumina/Lumina_HousePalette.mat";
        const string SenaPrefab = "Assets/Project/Prefabs/Sena.prefab";

        [MenuItem("Lumina/Prototipo 6.1.2/Construir ambas escenas")]
        public static void BuildAll() { BuildModular(); BuildTerrain(); }

        // ================================================================== A: mallas modulares

        public static void BuildModular()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = Materials();
            var root = new GameObject("--- PROTOTIPO MODULAR ---").transform;

            var ground = Child(root, "Terreno");
            var gm = SaveMesh(GroundMesh(), "Meshes/Modular_Ground.asset");
            var g = new GameObject("Suelo"); g.transform.SetParent(ground, false);
            g.AddComponent<MeshFilter>().sharedMesh = gm; g.AddComponent<MeshRenderer>().sharedMaterial = mats["Grass"];
            g.AddComponent<MeshCollider>().sharedMesh = gm;      // un único collider para todo el suelo

            Overlay(ground, "Orillas", RibbonMeshes.RiverBanks(), mats["Bank"], "Meshes/Modular_Banks.asset");
            Overlay(ground, "Plaza", RibbonMeshes.Plaza(), mats["Plaza"], "Meshes/Modular_Plaza.asset");
            for (int i = 0; i < Proto612Layout.Paths.Count; i++)
                Overlay(ground, Proto612Layout.PathName[i], RibbonMeshes.Path(Proto612Layout.Paths[i]), mats["Path"], "Meshes/Modular_" + Proto612Layout.PathName[i] + ".asset");

            Common(root, mats, "modular");
            Save(scene, "Scenes/Prototype_Modular.unity");
        }

        // ================================================================== B: Unity Terrain

        public static void BuildTerrain()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = Materials();
            var root = new GameObject("--- PROTOTIPO TERRAIN ---").transform;

            var ground = Child(root, "Terreno");
            var terrainGo = MakeTerrain(mats);
            terrainGo.transform.SetParent(ground, false);

            Common(root, mats, "terrain");
            Save(scene, "Scenes/Prototype_Terrain.unity");
        }

        const int HeightRes = 257, AlphaRes = 512;
        const float TerrainMinY = -2f, TerrainSizeY = 8f;

        static GameObject MakeTerrain(Dictionary<string, Material> mats)
        {
            string[] names = { "Grass", "Path", "Bank", "Plaza" };
            Color[] cols = { Col("#5C9447"), Col("#E2C79A"), Col("#B79B6E"), Col("#F3E6C8") };
            var layers = new TerrainLayer[4];
            for (int i = 0; i < 4; i++)
            {
                var tex = FlatTexture("Textures/Terrain_" + names[i] + ".png", cols[i]);
                string lp = Root + "Materials/TerrainLayer_" + names[i] + ".terrainlayer";
                AssetDatabase.DeleteAsset(lp);
                var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(4f, 4f), smoothness = 0f, metallic = 0f };
                AssetDatabase.CreateAsset(layer, lp);
                layers[i] = layer;
            }

            var data = new TerrainData { heightmapResolution = HeightRes, alphamapResolution = AlphaRes, baseMapResolution = 256 };
            data.size = new Vector3(Proto612Layout.Half * 2f, TerrainSizeY, Proto612Layout.Half * 2f);
            data.terrainLayers = layers;

            // El asset se crea ANTES de pintar para que las texturas de pesos se guarden como sub-assets con sus cambios.
            string dp = Root + "Terrain/Prototype_TerrainData.asset";
            AssetDatabase.DeleteAsset(dp);
            Directory.CreateDirectory(Path.GetDirectoryName(dp));
            AssetDatabase.CreateAsset(data, dp);

            var heights = new float[HeightRes, HeightRes];
            for (int zi = 0; zi < HeightRes; zi++)
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    float x = -Proto612Layout.Half + xi * (Proto612Layout.Half * 2f) / (HeightRes - 1);
                    float z = -Proto612Layout.Half + zi * (Proto612Layout.Half * 2f) / (HeightRes - 1);
                    heights[zi, xi] = (Proto612Layout.Height(x, z) - TerrainMinY) / TerrainSizeY;
                }
            data.SetHeights(0, 0, heights);

            var alpha = new float[AlphaRes, AlphaRes, 4];
            for (int zi = 0; zi < AlphaRes; zi++)
                for (int xi = 0; xi < AlphaRes; xi++)
                {
                    float x = -Proto612Layout.Half + (xi + 0.5f) * (Proto612Layout.Half * 2f) / AlphaRes;
                    float z = -Proto612Layout.Half + (zi + 0.5f) * (Proto612Layout.Half * 2f) / AlphaRes;
                    var p = new Vector2(x, z);
                    int layer = 0;
                    if (Proto612Layout.Nearest(p, Proto612Layout.River).dist < 3.5f) layer = 2;
                    if (Proto612Layout.DistToPaths(p) < Proto612Layout.PathHalfWidth) layer = 1;
                    if (p.magnitude < Proto612Layout.PlazaRadius) layer = 3;
                    alpha[zi, xi, layer] = 1f;
                }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();


            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.position = new Vector3(-Proto612Layout.Half, TerrainMinY, -Proto612Layout.Half);
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = mats["TerrainLit"];
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            return go;
        }

        // ================================================================== contenido común

        static void Common(Transform root, Dictionary<string, Material> mats, string kind)
        {
            // Agua, barreras y límites: idénticos en ambas escenas.
            var water = new GameObject("Rio_Agua"); water.transform.SetParent(root, false);
            var wm = SaveMesh(RibbonMeshes.Water(), "Meshes/" + kind + "_Water.asset");
            water.AddComponent<MeshFilter>().sharedMesh = wm; water.AddComponent<MeshRenderer>().sharedMaterial = mats["Water"];
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var barrier = new GameObject("Rio_Barrera (invisible)"); barrier.transform.SetParent(root, false);
            barrier.AddComponent<MeshCollider>().sharedMesh = SaveMesh(RibbonMeshes.RiverBarrier(), "Meshes/" + kind + "_RiverBarrier.asset");

            var limits = new GameObject("Limites_Mundo (invisible)"); limits.transform.SetParent(root, false);
            float h = Proto612Layout.Half;
            AddBox(limits, new Vector3(0, 3, h + 1), new Vector3(2 * h + 4, 14, 2));
            AddBox(limits, new Vector3(0, 3, -h - 1), new Vector3(2 * h + 4, 14, 2));
            AddBox(limits, new Vector3(h + 1, 3, 0), new Vector3(2, 14, 2 * h + 4));
            AddBox(limits, new Vector3(-h - 1, 3, 0), new Vector3(2, 14, 2 * h + 4));

            // Casas (prefabs de producción referenciados, con la paleta de casas de Lúmina) y su zona.
            var houses = Child(root, "Casas");
            for (int i = 0; i < 4; i++)
            {
                var c = Proto612Layout.HouseCenter[i];
                var go = Inst(VillageDir + Proto612Layout.HousePrefab[i] + ".prefab", houses, new Vector3(c.x, Proto612Layout.Height(c.x, c.y), c.y), Proto612Layout.HouseYaw[i], 0.5f, Proto612Layout.HouseName[i] + "_Exterior");
                var palette = AssetDatabase.LoadAssetAtPath<Material>(HousePalette);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = palette;
                BoxFromRenderers(go);
                var door = new GameObject("Zona_" + Proto612Layout.HouseName[i]);
                door.transform.SetParent(houses, false);
                var f = Proto612Layout.HouseFront[i];
                door.transform.position = new Vector3(f.x, 0, f.y);
            }

            // Puentes: prefab de la aldea, sin tocar su collider original (se prueba con un CharacterController en Proto612Verify).
            Proto612Layout.BridgeY = MeasureBridgeY();
            var bridges = Child(root, "Puentes");
            for (int i = 0; i < Proto612Layout.Bridges.Length; i++)
            {
                var b = Proto612Layout.Bridges[i];
                var go = Inst(VillageDir + "Bridge.prefab", bridges, new Vector3(b.center.x, Proto612Layout.BridgeY, b.center.y), b.yaw, 1f, "Puente_" + (i + 1));
                Tint(go, mats["Wood"]);
            }

            // Vegetación y mobiliario (misma lista en ambas escenas; colliders simplificados).
            var veg = Child(root, "Vegetacion"); var props = Child(root, "Props");
            var treeMat = mats["Tree"]; var pineMat = mats["Pine"];
            foreach (var it in Proto612Layout.Trees())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab);
                Tint(go, it.prefab.StartsWith("Pine") ? pineMat : treeMat);
                SimplifyCapsule(go, it.scale, 0.4f, 3.2f);
            }
            foreach (var it in Proto612Layout.Rocks())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab);
                Tint(go, mats["Rock"]);
                SimplifyBox(go);
            }
            foreach (var it in Proto612Layout.Flowers())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab);
                Tint(go, mats["Flower"]);
                DisableMeshColliders(go);
            }
            foreach (var it in Proto612Layout.Props())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", props, it.pos, it.yaw, it.scale, it.prefab);
                if (it.prefab == "Bench") { Tint(go, mats["Wood"]); SimplifyBox(go); }
                else { Tint(go, mats["Metal"]); SimplifyCapsule(go, 1f, 0.22f, 3.6f); }
            }
            var lightsRoot = props.gameObject.AddComponent<ThemeLights>();
            var so = new SerializedObject(lightsRoot);
            so.FindProperty("decorativeAnchorPrefix").stringValue = "Lantern";
            so.ApplyModifiedPropertiesWithoutUndo();

            // Sena: solo su modelo (sin interacciones); copia de la instancia del prefab de producción.
            var senaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SenaPrefab);
            if (senaPrefab != null)
            {
                var sena = (GameObject)PrefabUtility.InstantiatePrefab(senaPrefab, root);
                sena.name = "Sena (solo visual)";
                var sp = Proto612Layout.SenaPos;
                sena.transform.SetPositionAndRotation(new Vector3(sp.x, Proto612Layout.Height(sp.x, sp.z), sp.z), Quaternion.Euler(0, Mathf.Atan2(-sp.x, -11f - sp.z) * Mathf.Rad2Deg, 0));
            }

            // Luz y cielo, como en LuminaWorld (la luz solar lleva ThemeAmbient; el cielo diurno es el de producción).
            foreach (var guid in AssetDatabase.FindAssets("FS002_Day t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != "FS002_Day") continue;
                RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(path);
                break;
            }
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.rotation = Quaternion.Euler(45f, -60f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, 0.96f, 0.88f); sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            sunGo.AddComponent<ThemeAmbient>().ApplyTheme(false);

            // Jugador: mismos parámetros que el de producción y el FirstPersonController existente (solo referenciado).
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(Proto612Layout.Spawn, Quaternion.identity);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 45f;
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.65f, 0);
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f; cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            player.AddComponent<FirstPersonController>();
        }

        // ================================================================== utilidades

        static void Save(UnityEngine.SceneManagement.Scene scene, string rel)
        {
            string path = Root + rel;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[P612] Escena guardada: " + path);
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        static void Overlay(Transform parent, string name, Mesh mesh, Material mat, string meshRel)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var m = SaveMesh(mesh, meshRel);
            go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh SaveMesh(Mesh mesh, string rel)
        {
            string path = Root + rel;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject Inst(string prefabPath, Transform parent, Vector3 pos, float yaw, float scale, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError("[P612] Prefab no encontrado: " + prefabPath); return new GameObject(name); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static void Tint(GameObject go, Material m)
        {
            var parent = AssetDatabase.LoadAssetAtPath<Material>(VillageColor);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var arr = r.sharedMaterials; bool dirty = false;
                for (int i = 0; i < arr.Length; i++) if (arr[i] == parent) { arr[i] = m; dirty = true; }
                if (dirty) r.sharedMaterials = arr;
            }
        }

        static void DisableMeshColliders(GameObject go)
        {
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) mc.enabled = false;
        }

        /// <summary>Sustituye el MeshCollider del prefab por una cápsula (dimensiones en metros de mundo).</summary>
        static void SimplifyCapsule(GameObject go, float scale, float worldRadius, float worldHeight)
        {
            DisableMeshColliders(go);
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = worldRadius / scale; c.height = worldHeight / scale; c.center = new Vector3(0, c.height * 0.5f, 0);
        }

        /// <summary>Sustituye el MeshCollider del prefab por una caja con los límites de su malla.</summary>
        static void SimplifyBox(GameObject go) { DisableMeshColliders(go); BoxFromMesh(go); }

        static void BoxFromRenderers(GameObject go) { DisableMeshColliders(go); BoxFromMesh(go); }

        static void BoxFromMesh(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[P612] Sin MeshFilter en la raíz de " + go.name); return; }
            var box = go.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size;
        }

        /// <summary>Mide a qué altura queda el tablero del puente (raycast sobre su collider original, sin modificarlo).</summary>
        static float MeasureBridgeY()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageDir + "Bridge.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(new Vector3(500, 0, 500), Quaternion.identity);
            Physics.SyncTransforms();
            var sb = new StringBuilder("[P612] Tablero del puente (y local por z): ");
            float sum = 0; int n = 0;
            for (float z = -6f; z <= 6.01f; z += 1f)
            {
                if (Physics.Raycast(new Vector3(500f, 6f, 500f + z), Vector3.down, out var hit, 20f)) { sb.Append($"z={z:0.0}:{hit.point.y:0.00} "); if (Mathf.Abs(z) >= 5.99f) { sum += hit.point.y; n++; } }
                else sb.Append($"z={z:0.0}:sin-impacto ");
            }
            Object.DestroyImmediate(go);
            Debug.Log(sb.ToString());
            return n > 0 ? -sum / n : 0f;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size;
        }

        static Color Col(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static Texture2D FlatTexture(string rel, Color c)
        {
            string path = Root + rel;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var px = new Color[64]; for (int i = 0; i < 64; i++) px[i] = c; tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.sRGBTexture = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Mesh GroundMesh()
        {
            const float cell = 1f; int n = Mathf.RoundToInt(Proto612Layout.Half * 2f / cell);
            var verts = new List<Vector3>(n * n * 6); var tris = new List<int>(n * n * 6); var uvs = new List<Vector2>(n * n * 6);
            float H(int i, int j) { float x = -Proto612Layout.Half + i * cell, z = -Proto612Layout.Half + j * cell; return Proto612Layout.Height(x, z); }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x0 = -Proto612Layout.Half + i * cell, z0 = -Proto612Layout.Half + j * cell, x1 = x0 + cell, z1 = z0 + cell;
                    var a = new Vector3(x0, H(i, j), z0); var b = new Vector3(x0, H(i, j + 1), z1);
                    var c = new Vector3(x1, H(i + 1, j + 1), z1); var d = new Vector3(x1, H(i + 1, j), z0);
                    // Diagonal alternada para que el facetado no tenga una dirección privilegiada.
                    if ((i + j) % 2 == 0) { Quad(verts, tris, uvs, a, b, c, a, c, d); }
                    else { Quad(verts, tris, uvs, a, b, d, b, c, d); }
                }
            var mesh = new Mesh { name = "Modular_Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Quad(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a1, Vector3 b1, Vector3 c1, Vector3 a2, Vector3 b2, Vector3 c2)
        {
            int s = v.Count;
            v.Add(a1); v.Add(b1); v.Add(c1); v.Add(a2); v.Add(b2); v.Add(c2);
            for (int k = 0; k < 6; k++) { t.Add(s + k); uv.Add(new Vector2(v[s + k].x * 0.25f, v[s + k].z * 0.25f)); }
        }

        static Dictionary<string, Material> Materials()
        {
            var d = new Dictionary<string, Material>();
            Material Make(string key, Color c, float smooth = 0f)
            {
                string path = Root + "Materials/Proto_" + key + ".mat";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.DeleteAsset(path);
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Proto_" + key };
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(m, path); d[key] = m; return m;
            }
            Make("Grass", Col("#5C9447")); Make("Path", Col("#E2C79A")); Make("Bank", Col("#B79B6E")); Make("Plaza", Col("#F3E6C8"));
            Make("Tree", Col("#5C9E4D")); Make("Pine", Col("#2B7047")); Make("Rock", Col("#85858A")); Make("Flower", Col("#EC7A99"));
            Make("Wood", Col("#8C6138")); Make("Metal", Col("#42424D"));

            // Agua: URP/Lit transparente, turquesa de la paleta, sin sombra.
            var w = Make("Water", new Color(0.18f, 0.77f, 0.71f, 0.78f), 0.6f);
            w.SetFloat("_Surface", 1f); w.SetFloat("_Blend", 0f);
            w.SetOverrideTag("RenderType", "Transparent");
            w.SetFloat("_SrcBlend", 5f); w.SetFloat("_DstBlend", 10f); w.SetFloat("_ZWrite", 0f);
            w.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); w.renderQueue = 3000;
            w.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(w);

            // Material propio del Terrain (URP Terrain/Lit).
            string tp = Root + "Materials/Proto_TerrainLit.mat";
            AssetDatabase.DeleteAsset(tp);
            var tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "Proto_TerrainLit" };
            AssetDatabase.CreateAsset(tm, tp); d["TerrainLit"] = tm;
            AssetDatabase.SaveAssets();
            return d;
        }
    }

    /// <summary>Mallas de cintas (caminos, orillas, agua, plaza, barrera) sobre la función de altura común.</summary>
    public static class RibbonMeshes
    {
        const float Lift = 0.04f;

        public static Mesh Path(List<Vector2> poly) => Strip(poly, Proto612Layout.PathHalfWidth, Lift, 3, "Path");
        public static Mesh RiverBanks() => Strip(Proto612Layout.River, 3.6f, 0.08f, 15, "Banks");

        public static Mesh Water()
        {
            var pts = Proto612Layout.Resample(Proto612Layout.River, 0.75f);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            float acc = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                var n = new Vector2(pts[i].t.y, -pts[i].t.x) * Proto612Layout.RiverWaterHalfWidth;
                v.Add(new Vector3(pts[i].p.x - n.x, Proto612Layout.WaterY, pts[i].p.y - n.y)); uv.Add(new Vector2(0, acc));
                v.Add(new Vector3(pts[i].p.x + n.x, Proto612Layout.WaterY, pts[i].p.y + n.y)); uv.Add(new Vector2(1, acc));
                if (i > 0) acc += 0.2f;
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int a = i * 2; t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            var m = new Mesh { name = "Water" }; m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static Mesh Plaza()
        {
            const int seg = 48; float r = Proto612Layout.PlazaRadius;
            var v = new List<Vector3> { new Vector3(0, Proto612Layout.Height(0, 0) + Lift, 0) }; var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                v.Add(new Vector3(x, Proto612Layout.Height(x, z) + Lift, z));
            }
            for (int i = 1; i <= seg; i++) t.AddRange(new[] { 0, i + 1, i });
            var m = new Mesh { name = "Plaza" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Muro invisible a cada lado del río, con huecos donde hay puentes. Una sola malla = un solo collider.</summary>
        public static Mesh RiverBarrier()
        {
            var pts = Proto612Layout.Resample(Proto612Layout.River, 0.5f);
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (float side in new[] { -1f, 1f })
            {
                int runStart = -1;
                for (int i = 0; i <= pts.Count; i++)
                {
                    bool open = i < pts.Count && !NearBridge(pts[i].p);
                    if (open && runStart < 0) runStart = v.Count;
                    if (open)
                    {
                        var n = new Vector2(pts[i].t.y, -pts[i].t.x) * (Proto612Layout.BarrierOffset * side);
                        var p = pts[i].p + n;
                        v.Add(new Vector3(p.x, -2.5f, p.y)); v.Add(new Vector3(p.x, 3f, p.y));
                        if (v.Count - runStart >= 4)
                        {
                            int b = v.Count - 4;
                            t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2, b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                        }
                    }
                    else runStart = -1;
                }
            }
            var m = new Mesh { name = "RiverBarrier" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        static bool NearBridge(Vector2 p)
        {
            foreach (var b in Proto612Layout.Bridges) if ((p - b.center).magnitude < 2.9f) return true;
            return false;
        }

        static Mesh Strip(IList<Vector2> poly, float halfWidth, float lift, int across, string name)
        {
            var pts = Proto612Layout.Resample(poly, 0.5f);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                var n = new Vector2(pts[i].t.y, -pts[i].t.x);
                for (int k = 0; k < across; k++)
                {
                    float s = across == 1 ? 0f : (k / (float)(across - 1)) * 2f - 1f;
                    var p = pts[i].p + n * (halfWidth * s);
                    v.Add(new Vector3(p.x, Proto612Layout.Height(p.x, p.y) + lift, p.y)); uv.Add(new Vector2(k / (float)(across - 1), i * 0.5f));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < across - 1; k++)
                {
                    int a = i * across + k, b = a + 1, c = a + across, d = c + 1;
                    t.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
