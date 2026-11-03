using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;
using Lumina.Core;
using Lumina.Player;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.1.3: construye Prototype_Modular_RiverCentral.unity (río central que separa dos sectores de dos casas).
    /// Conserva intactas las escenas de la 6.1.2: escribe una escena nueva y mallas con prefijo «Rc_», y solo REFERENCIA los
    /// materiales Proto_* ya existentes (no los recrea, para no cambiar sus GUID y no romper Prototype_Terrain).
    /// </summary>
    public static class Proto612BuilderV2
    {
        const string Root = Proto612Builder.Root;
        const string VillageDir = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        const string VillageColor = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat";
        const string HousePalette = "Assets/Project/Materials/Lumina/Lumina_HousePalette.mat";
        const string SenaPrefab = "Assets/Project/Prefabs/Sena.prefab";
        public const string ScenePath = Root + "Scenes/Prototype_Modular_RiverCentral.unity";
        public static float BridgeY;

        [MenuItem("Lumina/Prototipo 6.1.2/Construir modular con río central (6.1.3)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = LoadMaterials();
            var root = new GameObject("--- PROTOTIPO MODULAR · RÍO CENTRAL ---").transform;

            // Suelo, plaza, explanada, caminos y orillas.
            var ground = Child(root, "Terreno");
            var gm = SaveMesh(GroundMesh(), "Meshes/Rc_Ground.asset");
            var g = new GameObject("Suelo"); g.transform.SetParent(ground, false);
            g.AddComponent<MeshFilter>().sharedMesh = gm; g.AddComponent<MeshRenderer>().sharedMaterial = mats["Grass"];
            g.AddComponent<MeshCollider>().sharedMesh = gm;
            Overlay(ground, "Orillas", RibbonV2.Strip(Proto612LayoutV2.River, 3.6f, 0.08f, 15, "Banks"), mats["Bank"], "Meshes/Rc_Banks.asset");
            Overlay(ground, "Plaza", RibbonV2.Disc(Proto612LayoutV2.PlazaCenter, Proto612LayoutV2.PlazaRadius), mats["Plaza"], "Meshes/Rc_Plaza.asset");
            Overlay(ground, "Explanada", RibbonV2.Disc(Proto612LayoutV2.EsplanadeCenter, Proto612LayoutV2.EsplanadeRadius), mats["Plaza"], "Meshes/Rc_Esplanade.asset");
            for (int i = 0; i < Proto612LayoutV2.Paths.Count; i++)
                Overlay(ground, Proto612LayoutV2.PathName[i], RibbonV2.Strip(Proto612LayoutV2.Paths[i], Proto612LayoutV2.PathHalfWidth, 0.04f, 3, "Path"), mats["Path"], "Meshes/Rc_" + Proto612LayoutV2.PathName[i] + ".asset");

            // Agua, barrera invisible y límites del mundo.
            var water = new GameObject("Rio_Agua"); water.transform.SetParent(root, false);
            water.AddComponent<MeshFilter>().sharedMesh = SaveMesh(RibbonV2.Water(), "Meshes/Rc_Water.asset");
            var wr = water.AddComponent<MeshRenderer>(); wr.sharedMaterial = mats["Water"]; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var barrier = new GameObject("Rio_Barrera (invisible)"); barrier.transform.SetParent(root, false);
            barrier.AddComponent<MeshCollider>().sharedMesh = SaveMesh(RibbonV2.Barrier(), "Meshes/Rc_RiverBarrier.asset");
            var limits = new GameObject("Limites_Mundo (invisible)"); limits.transform.SetParent(root, false);
            float h = Proto612LayoutV2.Half;
            AddBox(limits, new Vector3(0, 3, h + 1), new Vector3(2 * h + 4, 14, 2)); AddBox(limits, new Vector3(0, 3, -h - 1), new Vector3(2 * h + 4, 14, 2));
            AddBox(limits, new Vector3(h + 1, 3, 0), new Vector3(2, 14, 2 * h + 4)); AddBox(limits, new Vector3(-h - 1, 3, 0), new Vector3(2, 14, 2 * h + 4));

            // Casas por sector.
            var houses = Child(root, "Casas");
            var west = Child(houses, "Sector_Oeste"); var east = Child(houses, "Sector_Este");
            var palette = AssetDatabase.LoadAssetAtPath<Material>(HousePalette);
            for (int i = 0; i < Proto612LayoutV2.Houses.Length; i++)
            {
                var hs = Proto612LayoutV2.Houses[i]; var pv = hs.Pivot;
                var go = Inst(VillageDir + hs.prefab + ".prefab", hs.center.x < 0 ? west : east, new Vector3(pv.x, Proto612LayoutV2.Height(pv.x, pv.y), pv.y), hs.yaw, 0.5f, hs.name + "_Exterior");
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = palette;
                DisableMeshColliders(go); BoxFromMesh(go);
                var door = new GameObject("Entrada_" + hs.name); door.transform.SetParent(go.transform.parent, false);
                var f = Proto612LayoutV2.DoorPoint(i, 0f) + hs.Front * 1.2f; door.transform.position = new Vector3(f.x, 0, f.y);
                door.transform.rotation = Quaternion.LookRotation(new Vector3(-hs.Front.x, 0, -hs.Front.y));     // mira a la casa
            }

            // Puentes (prefab original; collider original, sin modificar).
            BridgeY = MeasureBridgeY();
            var bridges = Child(root, "Puentes");
            for (int i = 0; i < 2; i++)
            {
                var b = Proto612LayoutV2.Bridges[i];
                var go = Inst(VillageDir + "Bridge.prefab", bridges, new Vector3(b.center.x, BridgeY, b.center.y), b.yaw, 1f, "Puente_" + (i + 1));
                Tint(go, mats["Wood"]);
            }

            // Vegetación y mobiliario.
            var veg = Child(root, "Vegetacion"); var props = Child(root, "Props");
            foreach (var it in Proto612LayoutV2.Trees())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab);
                Tint(go, it.prefab.StartsWith("Pine") ? mats["Pine"] : mats["Tree"]); SimplifyCapsule(go, it.scale, 0.4f, 3.2f);
            }
            foreach (var it in Proto612LayoutV2.Rocks()) { var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab); Tint(go, mats["Rock"]); DisableMeshColliders(go); BoxFromMesh(go); }
            foreach (var it in Proto612LayoutV2.Flowers()) { var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab); Tint(go, mats["Flower"]); DisableMeshColliders(go); }
            foreach (var it in Proto612LayoutV2.Props())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", props, it.pos, it.yaw, it.scale, it.prefab);
                if (it.prefab == "Bench") { Tint(go, mats["Wood"]); DisableMeshColliders(go); BoxFromMesh(go); }
                else { Tint(go, mats["Metal"]); SimplifyCapsule(go, 1f, 0.22f, 3.6f); }
            }
            var tl = props.gameObject.AddComponent<ThemeLights>();
            var so = new SerializedObject(tl); so.FindProperty("decorativeAnchorPrefix").stringValue = "Lantern";
            so.FindProperty("maxDecorativeLights").intValue = 10;          // 6.1.4: un farol = una luz (el valor por defecto de producción es 4)
            so.FindProperty("maxDecorativeLightsLowEnd").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildSigns(root);

            // Sena (solo visual), de cara al spawn.
            var senaPrefab = MakeSenaVariant();
            if (senaPrefab != null)
            {
                var sena = (GameObject)PrefabUtility.InstantiatePrefab(senaPrefab, root); sena.name = "Sena (presencia, sin interacciones)";
                var sp = Proto612LayoutV2.SenaPos; var sw = Proto612LayoutV2.Spawn;
                sena.transform.SetPositionAndRotation(new Vector3(sp.x, Proto612LayoutV2.Height(sp.x, sp.z), sp.z), Quaternion.Euler(0, Mathf.Atan2(sw.x - sp.x, sw.z - sp.z) * Mathf.Rad2Deg, 0));
            }

            // Luz y cielo como en LuminaWorld.
            foreach (var guid in AssetDatabase.FindAssets("FS002_Day t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != "FS002_Day") continue;
                RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(path); break;
            }
            var sunGo = new GameObject("Directional Light"); sunGo.transform.rotation = Quaternion.Euler(45f, -60f, 0f);
            var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.96f, 0.88f); sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun; sunGo.AddComponent<ThemeAmbient>().ApplyTheme(false);

            // Jugador de primera persona (mismos parámetros que producción; FirstPersonController solo referenciado).
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(Proto612LayoutV2.Spawn, Quaternion.Euler(0, Proto612LayoutV2.SpawnYaw, 0));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 45f;
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; camGo.transform.SetParent(player.transform, false); camGo.transform.localPosition = new Vector3(0, 1.65f, 0);
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f; cam.fieldOfView = 60f; camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            player.AddComponent<FirstPersonController>();
            AddTestHelper(player);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[P612-V2] Escena guardada: " + ScenePath + " | puente Y=" + BridgeY.ToString("0.00"));
        }

        // ------------------------------------------------------------------ 6.1.4
        /// <summary>Variante de Sena.prefab dentro del prototipo: sin movimiento raíz y con la presencia (giro + saludo). El original no se toca.</summary>
        static GameObject MakeSenaVariant()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SenaPrefab);
            if (src == null) { Debug.LogError("[P612-V2] Falta Sena.prefab"); return null; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;     // evita que Idle/Wave desplacen a Sena
            inst.AddComponent<Proto614SenaGreeter>();
            string path = Root + "Prefabs/Sena_Proto.prefab"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            var variant = PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            return variant;
        }

        static Material SignMat(string key, Color c)
        {
            string path = Root + "Materials/Proto_" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Proto_" + key };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            AssetDatabase.CreateAsset(m, path); return m;
        }

        static void BuildSigns(Transform root)
        {
            var ink = SignMat("SignInk", new Color32(0x0F, 0x3A, 0x44, 0xFF));
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Proto_Wood.mat");
            var accent = new Dictionary<string, Material>
            {
                ["Turquoise"] = SignMat("AccentTurquoise", new Color32(0x2E, 0xC4, 0xB6, 0xFF)),
                ["Coral"] = SignMat("AccentCoral", new Color32(0xFF, 0x5E, 0x4D, 0xFF)),
                ["Sand"] = SignMat("AccentSand", new Color32(0xFA, 0xE3, 0xBD, 0xFF)),
            };
            var cream = new Color32(0xFF, 0xF9, 0xEC, 0xFF);
            var parent = Child(root, "Senalizacion");
            foreach (var sg in Proto612LayoutV2.Signs())
            {
                var rootGo = new GameObject(sg.name); rootGo.transform.SetParent(parent, false);
                float gy = Proto612LayoutV2.Height(sg.pos.x, sg.pos.y);
                rootGo.transform.SetPositionAndRotation(new Vector3(sg.pos.x, gy, sg.pos.y), Quaternion.LookRotation(new Vector3(-sg.face.x, 0f, -sg.face.y)));   // +Z local = lejos del lector
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Poste"; post.transform.SetParent(rootGo.transform, false);
                post.transform.localPosition = new Vector3(0, 1.3f, 0); post.transform.localScale = new Vector3(0.16f, 2.6f, 0.16f); post.GetComponent<Renderer>().sharedMaterial = wood;
                int n = sg.lines.Length;
                for (int i = 0; i < n; i++)
                {
                    float y = n == 1 ? 2.0f : (i == 0 ? 2.35f : 1.65f);
                    MakeBoard(rootGo.transform, "Tablero" + (i + 1), y, sg.lines[i], ink, accent[sg.accents[i]], cream, sg.doubleSided);
                }
            }
        }

        static void MakeBoard(Transform parent, string name, float y, string text, Material ink, Material accent, Color32 cream, bool doubleSided)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube); board.name = name; board.transform.SetParent(parent, false);
            board.transform.localPosition = new Vector3(0, y, 0); board.transform.localScale = new Vector3(2.3f, 0.58f, 0.1f); board.GetComponent<Renderer>().sharedMaterial = ink;
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = "Franja"; bar.transform.SetParent(board.transform, false);
            bar.transform.localPosition = new Vector3(0, 0.5f, 0); bar.transform.localScale = new Vector3(1f, 0.16f, 1.12f); bar.GetComponent<Renderer>().sharedMaterial = accent;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                var t = new GameObject(side == 0 ? "Texto" : "Texto_reverso").AddComponent<TextMeshPro>();
                t.transform.SetParent(parent, false);
                t.transform.localPosition = new Vector3(0, y, side == 0 ? -0.056f : 0.056f); t.transform.localRotation = Quaternion.Euler(0, side == 0 ? 0f : 180f, 0);
                t.rectTransform.sizeDelta = new Vector2(2.1f, 0.5f);
                t.text = text; t.color = cream; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
                t.enableAutoSizing = true; t.fontSizeMin = 1f; t.fontSizeMax = 4f; t.enableWordWrapping = false;
            }
        }

        static void AddTestHelper(GameObject player)
        {
            var h = player.AddComponent<Proto614TestHelper>();
            var stops = new List<Proto614TestHelper.Stop>();
            Vector3 G(Vector2 p) => new Vector3(p.x, Proto612LayoutV2.Height(p.x, p.y) + 0.1f, p.y);
            stops.Add(new Proto614TestHelper.Stop { label = "spawn", position = Proto612LayoutV2.Spawn, yaw = Proto612LayoutV2.SpawnYaw });
            for (int hi = 0; hi < Proto612LayoutV2.Houses.Length; hi++)
            { var hs = Proto612LayoutV2.Houses[hi];
                stops.Add(new Proto614TestHelper.Stop { label = hs.name, position = G(Proto612LayoutV2.DoorPoint(hi, 0f) + hs.Front * 6.5f), yaw = Mathf.Atan2(-hs.Front.x, -hs.Front.y) * Mathf.Rad2Deg }); }
            foreach (var b in Proto612LayoutV2.Bridges)
                stops.Add(new Proto614TestHelper.Stop { label = "puente", position = G(b.center - b.axis * 9f), yaw = b.yaw });
            var sp = new Vector2(Proto612LayoutV2.SenaPos.x, Proto612LayoutV2.SenaPos.z); var toSpawn = (new Vector2(Proto612LayoutV2.Spawn.x, Proto612LayoutV2.Spawn.z) - sp).normalized;
            stops.Add(new Proto614TestHelper.Stop { label = "Sena", position = G(sp + toSpawn * 3f), yaw = Mathf.Atan2(-toSpawn.x, -toSpawn.y) * Mathf.Rad2Deg });
            h.stops = stops.ToArray();
        }

        // ------------------------------------------------------------------ utilidades (copias de las de la 6.1.2)
        static Dictionary<string, Material> LoadMaterials()
        {
            var d = new Dictionary<string, Material>();
            foreach (var key in new[] { "Grass", "Path", "Bank", "Plaza", "Tree", "Pine", "Rock", "Flower", "Wood", "Metal", "Water" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Proto_" + key + ".mat");
                if (m == null) Debug.LogError("[P612-V2] Falta el material Proto_" + key);
                d[key] = m;
            }
            return d;
        }

        static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }

        static void Overlay(Transform parent, string name, Mesh mesh, Material mat, string meshRel)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, meshRel); go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh SaveMesh(Mesh mesh, string rel)
        {
            string path = Root + rel; Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        static GameObject Inst(string prefabPath, Transform parent, Vector3 pos, float yaw, float scale, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError("[P612-V2] Prefab no encontrado: " + prefabPath); return new GameObject(name); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name; go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * scale;
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

        static void DisableMeshColliders(GameObject go) { foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) mc.enabled = false; }

        static void SimplifyCapsule(GameObject go, float scale, float worldRadius, float worldHeight)
        {
            DisableMeshColliders(go);
            var c = go.AddComponent<CapsuleCollider>(); c.radius = worldRadius / scale; c.height = worldHeight / scale; c.center = new Vector3(0, c.height * 0.5f, 0);
        }

        static void BoxFromMesh(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var box = go.AddComponent<BoxCollider>(); box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size) { var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size; }

        static float MeasureBridgeY()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageDir + "Bridge.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(new Vector3(500, 0, 500), Quaternion.identity); Physics.SyncTransforms();
            float sum = 0; int n = 0;
            foreach (float z in new[] { -6f, 6f }) if (Physics.Raycast(new Vector3(500f, 6f, 500f + z), Vector3.down, out var hit, 20f)) { sum += hit.point.y; n++; }
            Object.DestroyImmediate(go);
            return n > 0 ? -sum / n : 0f;
        }

        static Mesh GroundMesh()
        {
            const float cell = 1f; int n = Mathf.RoundToInt(Proto612LayoutV2.Half * 2f / cell);
            var verts = new List<Vector3>(n * n * 6); var tris = new List<int>(n * n * 6); var uvs = new List<Vector2>(n * n * 6);
            float Hh(int i, int j) => Proto612LayoutV2.Height(-Proto612LayoutV2.Half + i * cell, -Proto612LayoutV2.Half + j * cell);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x0 = -Proto612LayoutV2.Half + i * cell, z0 = -Proto612LayoutV2.Half + j * cell, x1 = x0 + cell, z1 = z0 + cell;
                    var a = new Vector3(x0, Hh(i, j), z0); var b = new Vector3(x0, Hh(i, j + 1), z1); var c = new Vector3(x1, Hh(i + 1, j + 1), z1); var d = new Vector3(x1, Hh(i + 1, j), z0);
                    if ((i + j) % 2 == 0) Quad(verts, tris, uvs, a, b, c, a, c, d); else Quad(verts, tris, uvs, a, b, d, b, c, d);
                }
            var mesh = new Mesh { name = "Rc_Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Quad(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a1, Vector3 b1, Vector3 c1, Vector3 a2, Vector3 b2, Vector3 c2)
        {
            int s = v.Count; v.Add(a1); v.Add(b1); v.Add(c1); v.Add(a2); v.Add(b2); v.Add(c2);
            for (int k = 0; k < 6; k++) { t.Add(s + k); uv.Add(new Vector2(v[s + k].x * 0.25f, v[s + k].z * 0.25f)); }
        }
    }

    /// <summary>Cintas, discos, agua y barrera sobre la función de altura de la 6.1.3.</summary>
    public static class RibbonV2
    {
        public static Mesh Strip(IList<Vector2> poly, float halfWidth, float lift, int across, string name)
        {
            var pts = Proto612Layout.Resample(poly, 0.5f);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                var n = new Vector2(pts[i].t.y, -pts[i].t.x);
                for (int k = 0; k < across; k++)
                {
                    float s = (k / (float)(across - 1)) * 2f - 1f; var p = pts[i].p + n * (halfWidth * s);
                    v.Add(new Vector3(p.x, Proto612LayoutV2.Height(p.x, p.y) + lift, p.y)); uv.Add(new Vector2(k / (float)(across - 1), i * 0.5f));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < across - 1; k++) { int a = i * across + k, b = a + 1, c = a + across, d = c + 1; t.AddRange(new[] { a, c, b, b, c, d }); }
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public static Mesh Disc(Vector2 c, float r)
        {
            const int seg = 48; var v = new List<Vector3> { new Vector3(c.x, Proto612LayoutV2.Height(c.x, c.y) + 0.06f, c.y) }; var t = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r; v.Add(new Vector3(x, Proto612LayoutV2.Height(x, z) + 0.06f, z)); }
            for (int i = 1; i <= seg; i++) t.AddRange(new[] { 0, i + 1, i });
            var m = new Mesh { name = "Disc" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public static Mesh Water()
        {
            var pts = Proto612Layout.Resample(Proto612LayoutV2.River, 0.75f);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>(); float acc = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                var n = new Vector2(pts[i].t.y, -pts[i].t.x) * Proto612LayoutV2.RiverWaterHalfWidth;
                v.Add(new Vector3(pts[i].p.x - n.x, Proto612LayoutV2.WaterY, pts[i].p.y - n.y)); uv.Add(new Vector2(0, acc));
                v.Add(new Vector3(pts[i].p.x + n.x, Proto612LayoutV2.WaterY, pts[i].p.y + n.y)); uv.Add(new Vector2(1, acc));
                if (i > 0) acc += 0.2f;
            }
            for (int i = 0; i < pts.Count - 1; i++) { int a = i * 2; t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            var m = new Mesh { name = "Water" }; m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static bool NearBridge(Vector2 p)
        {
            foreach (var b in Proto612LayoutV2.Bridges) if ((p - b.center).magnitude < Proto612LayoutV2.BridgeGap) return true;
            return false;
        }

        /// <summary>Muro invisible a cada lado del río, con un hueco del ancho del tablero en cada puente. Una sola malla = un collider.</summary>
        public static Mesh Barrier()
        {
            var pts = Proto612Layout.Resample(Proto612LayoutV2.River, 0.5f);
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
                        var n = new Vector2(pts[i].t.y, -pts[i].t.x) * (Proto612LayoutV2.BarrierOffset * side); var p = pts[i].p + n;
                        v.Add(new Vector3(p.x, -2.5f, p.y)); v.Add(new Vector3(p.x, 3f, p.y));
                        if (v.Count - runStart >= 4) { int b = v.Count - 4; t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2, b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
                    }
                    else runStart = -1;
                }
            }
            var m = new Mesh { name = "RiverBarrier" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }
    }
}
