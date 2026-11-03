using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Lumina.Core;
using Lumina.Player;
using L = Lumina.Prototype612.R621Layout;
using H = Lumina.Prototype612.Proto612LayoutV2;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.1: construye Prototype_Modular_FourVillages.unity. Escribe solo en la carpeta del prototipo; no toca las escenas
    /// 6.1.2 / 6.1.4 ni ningún recurso compartido (los prefabs y materiales de producción solo se referencian). Idempotente: siempre parte
    /// de una escena nueva y sobrescribe únicamente sus propios recursos (prefijo R621_ / Meshes/R621_*).
    /// </summary>
    public static class R621Builder
    {
        const string Root = Proto612Builder.Root;
        const string VillageDir = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        const string VillageColor = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat";
        const string HousePalette = "Assets/Project/Materials/Lumina/Lumina_HousePalette.mat";
        public const string ScenePath = Root + "Scenes/Prototype_Modular_FourVillages.unity";
        public static float BridgeY;

        [MenuItem("Lumina/Prototipo 6.2.1/Construir cuatro aldeas")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = LoadMaterials();
            var root = new GameObject("--- PROTOTIPO · CUATRO ALDEAS ---").transform;

            // Suelo, plaza común, plazas temáticas, reservas, caminos y orillas.
            var ground = Child(root, "Terreno");
            var g = new GameObject("Suelo"); g.transform.SetParent(ground, false);
            var gm = SaveMesh(GroundMesh(), "Meshes/R621_Ground.asset");
            g.AddComponent<MeshFilter>().sharedMesh = gm; g.AddComponent<MeshRenderer>().sharedMaterial = mats["Grass"]; g.AddComponent<MeshCollider>().sharedMesh = gm;
            Overlay(ground, "Orillas", R621Mesh.Banks(), mats["Bank"], "Meshes/R621_Banks.asset");
            Overlay(ground, "Plaza_Comun", R621Mesh.Disc(L.PlazaCenter, L.PlazaRadius, 0.06f), mats["Plaza"], "Meshes/R621_Plaza.asset");
            for (int v = 0; v < 4; v++) Overlay(ground, "PlazaTematica_" + H.Houses[v].name, R621Mesh.Disc(L.HubCenter[v], L.HubRadius, 0.06f), mats["Hub" + v], "Meshes/R621_Hub" + v + ".asset");
            var reserves = Child(root, "Reservas");
            for (int v = 0; v < 4; v++)
                for (int k = 0; k < L.ReserveRings[v].Length; k++)
                {
                    var c = L.ReserveRings[v][k];
                    Overlay(reserves, "Reserva_" + H.Houses[v].name + "_" + (k + 1), R621Mesh.Ring(c, 1.15f, 1.65f), mats["Ring"], "Meshes/R621_Reserve" + v + "_" + k + ".asset");
                }
            for (int i = 0; i < L.Paths.Count; i++)
                Overlay(ground, L.PathName[i], R621Mesh.Strip(L.Paths[i], _ => L.PathHalfWidth, 0.04f, 3, "Path"), mats["Path"], "Meshes/R621_" + L.PathName[i] + ".asset");

            // Agua, barrera invisible y límites.
            var water = new GameObject("Rio_Agua"); water.transform.SetParent(root, false);
            water.AddComponent<MeshFilter>().sharedMesh = SaveMesh(R621Mesh.Water(), "Meshes/R621_Water.asset");
            var wr = water.AddComponent<MeshRenderer>(); wr.sharedMaterial = mats["Water"]; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var barrier = new GameObject("Rio_Barrera (invisible)"); barrier.transform.SetParent(root, false);
            barrier.AddComponent<MeshCollider>().sharedMesh = SaveMesh(R621Mesh.Barrier(), "Meshes/R621_RiverBarrier.asset");
            var limits = new GameObject("Limites_Mundo (invisible)"); limits.transform.SetParent(root, false);
            float hh = L.Half;
            AddBox(limits, new Vector3(0, 3, hh + 1), new Vector3(2 * hh + 4, 14, 2)); AddBox(limits, new Vector3(0, 3, -hh - 1), new Vector3(2 * hh + 4, 14, 2));
            AddBox(limits, new Vector3(hh + 1, 3, 0), new Vector3(2, 14, 2 * hh + 4)); AddBox(limits, new Vector3(-hh - 1, 3, 0), new Vector3(2, 14, 2 * hh + 4));

            // Casas (prefabs de producción referenciados; misma posición y orientación que en 6.1.3/6.1.4).
            var houses = Child(root, "Casas"); var palette = AssetDatabase.LoadAssetAtPath<Material>(HousePalette);
            for (int i = 0; i < 4; i++)
            {
                var hs = H.Houses[i]; var pv = hs.Pivot;
                var go = Inst(VillageDir + hs.prefab + ".prefab", houses, new Vector3(pv.x, L.Height(pv.x, pv.y), pv.y), hs.yaw, 0.5f, hs.name + "_Exterior");
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = palette;
                DisableMeshColliders(go); BoxFromMesh(go);
                var door = new GameObject("Entrada_" + hs.name); door.transform.SetParent(houses, false);
                var f = H.DoorPoint(i, 0f) + hs.Front * 1.2f; door.transform.position = new Vector3(f.x, 0, f.y);
            }

            // Cuatro puentes: mismo prefab original (collider original), cada uno con su tono de madera.
            BridgeY = MeasureBridgeY();
            var bridges = Child(root, "Puentes");
            for (int i = 0; i < 4; i++)
            {
                var b = L.Bridges[i];
                var go = Inst(VillageDir + "Bridge.prefab", bridges, new Vector3(b.center.x, BridgeY, b.center.y), b.yaw, 1f, L.BridgeName[i].Replace(' ', '_'));
                Tint(go, mats["BridgeWood" + i]);
            }

            // Vegetación existente (reducida y alejada de reservas) y mobiliario mínimo.
            var veg = Child(root, "Vegetacion"); var props = Child(root, "Props");
            foreach (var it in L.Trees()) { var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab); Tint(go, it.prefab.StartsWith("Pine") ? mats["Pine"] : mats["Tree"]); SimplifyCapsule(go, it.scale, 0.4f, 3.2f); }
            foreach (var it in L.Rocks()) { var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab); Tint(go, mats["Rock"]); DisableMeshColliders(go); BoxFromMesh(go); }
            foreach (var it in L.Flowers()) { var go = Inst(VillageDir + it.prefab + ".prefab", veg, it.pos, it.yaw, it.scale, it.prefab); Tint(go, mats["Flower"]); DisableMeshColliders(go); }
            foreach (var it in L.Props())
            {
                var go = Inst(VillageDir + it.prefab + ".prefab", props, it.pos, it.yaw, it.scale, it.prefab);
                if (it.prefab == "Bench") { Tint(go, mats["Wood"]); DisableMeshColliders(go); BoxFromMesh(go); } else { Tint(go, mats["Metal"]); SimplifyCapsule(go, 1f, 0.22f, 3.6f); }
            }
            var tl = props.gameObject.AddComponent<ThemeLights>();
            var so = new SerializedObject(tl);
            so.FindProperty("decorativeAnchorPrefix").stringValue = "Lantern";
            so.FindProperty("maxDecorativeLights").intValue = 12;           // un farol = una luz (producción: 4); solo en la instancia del prototipo
            so.FindProperty("maxDecorativeLightsLowEnd").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildSigns(root, mats);

            // Sena: variante del prototipo 6.1.4 (solo se referencia; no se regenera).
            var senaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Sena_Proto.prefab");
            if (senaPrefab == null) Debug.LogError("[R621] Falta Prefabs/Sena_Proto.prefab (6.1.4)");
            else
            {
                var sena = (GameObject)PrefabUtility.InstantiatePrefab(senaPrefab, root); sena.name = "Sena (presencia, sin interacciones)";
                var sp = L.SenaPos; var sw = L.Spawn;
                sena.transform.SetPositionAndRotation(new Vector3(sp.x, L.Height(sp.x, sp.z), sp.z), Quaternion.Euler(0, Mathf.Atan2(sw.x - sp.x, sw.z - sp.z) * Mathf.Rad2Deg, 0));
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

            // Jugador de primera persona (mismos parámetros que producción).
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(L.Spawn, Quaternion.Euler(0, L.SpawnYaw, 0));
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
            Debug.Log("[R621] Escena guardada: " + ScenePath + " | puente Y=" + BridgeY.ToString("0.00"));
        }

        /// <summary>Comprobación de idempotencia: construye dos veces y compara el recuento de objetos por nombre.</summary>
        public static void BuildTwiceAndCompare()
        {
            Build(); var a = CountByName();
            Build(); var b = CountByName();
            var sb = new System.Text.StringBuilder("[R621-IDEMPOTENCIA]\n"); bool same = a.Count == b.Count; int total = 0;
            foreach (var kv in a) { total += kv.Value; if (!b.TryGetValue(kv.Key, out var n) || n != kv.Value) { same = false; sb.AppendLine($"  difiere '{kv.Key}': {kv.Value} → {(b.TryGetValue(kv.Key, out var m) ? m : 0)}"); } }
            sb.AppendLine($"Objetos por nombre: {a.Count} nombres distintos, {total} objetos; segunda construcción {(same ? "IDÉNTICA" : "DIFERENTE")}");
            File.WriteAllText(Root + "Evidence/r621_idempotencia.txt", sb.ToString()); Debug.Log(sb.ToString());
        }

        static Dictionary<string, int> CountByName()
        {
            var d = new Dictionary<string, int>();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) d[go.name] = d.TryGetValue(go.name, out var n) ? n + 1 : 1;
            return d;
        }

        // ------------------------------------------------------------------ materiales (los Proto_* de 6.1.x solo se cargan; los R621_* se crean si faltan)
        static Dictionary<string, Material> LoadMaterials()
        {
            var d = new Dictionary<string, Material>();
            foreach (var key in new[] { "Grass", "Path", "Bank", "Plaza", "Tree", "Pine", "Rock", "Flower", "Wood", "Metal", "Water", "SignInk", "AccentTurquoise", "AccentCoral", "AccentSand" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Proto_" + key + ".mat");
                if (m == null) Debug.LogError("[R621] Falta el material Proto_" + key);
                d[key] = m;
            }
            d["AccentInk"] = NewMat("AccentCream", new Color32(0xFF, 0xF9, 0xEC, 0xFF));
            string[] hub = { "#9EE3DA", "#FFB9AF", "#FFE08F", "#B9D3D8" };
            for (int v = 0; v < 4; v++) d["Hub" + v] = NewMat("Hub" + v, Col(hub[v]));
            d["Ring"] = NewMat("Ring", Col("#D8BE8E"));
            string[] bw = { "#8C6138", "#966A3E", "#7F5733", "#9B6F40" };
            for (int i = 0; i < 4; i++) d["BridgeWood" + i] = NewMat("BridgeWood" + i, Col(bw[i]));
            return d;
        }

        static Material NewMat(string key, Color c)
        {
            string path = Root + "Materials/R621_" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); return m; }
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "R621_" + key };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.CreateAsset(m, path); return m;
        }

        static Color Col(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // ------------------------------------------------------------------ señales
        static void BuildSigns(Transform root, Dictionary<string, Material> mats)
        {
            var ink = mats["SignInk"]; var wood = mats["Wood"]; var cream = new Color32(0xFF, 0xF9, 0xEC, 0xFF);
            var accent = new Dictionary<string, Material> { ["Turquoise"] = mats["AccentTurquoise"], ["Coral"] = mats["AccentCoral"], ["Sand"] = mats["AccentSand"], ["Ink"] = mats["AccentInk"] };
            var parent = Child(root, "Senalizacion");
            foreach (var sg in L.Signs())
            {
                var rootGo = new GameObject(sg.name); rootGo.transform.SetParent(parent, false);
                rootGo.transform.SetPositionAndRotation(new Vector3(sg.pos.x, L.Height(sg.pos.x, sg.pos.y), sg.pos.y), Quaternion.LookRotation(new Vector3(-sg.face.x, 0f, -sg.face.y)));
                int n = sg.lines.Length; float topY = 2.0f + (n - 1) * 0.65f; float postH = Mathf.Max(2.6f, topY + 0.7f);
                float maxW = 0f; foreach (var line in sg.lines) maxW = Mathf.Max(maxW, Mathf.Clamp(0.15f * line.Length, 2.3f, 3.9f));
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Poste"; post.transform.SetParent(rootGo.transform, false);
                post.transform.localPosition = new Vector3(-maxW * 0.5f + 0.1f, postH * 0.5f, 0);          // en el borde: no tapa las letras
                post.transform.localScale = new Vector3(0.16f, postH, 0.16f); post.GetComponent<Renderer>().sharedMaterial = wood;
                for (int i = 0; i < n; i++) MakeBoard(rootGo.transform, "Tablero" + (i + 1), topY - i * 0.65f, sg.lines[i], ink, accent[sg.accents[i]], cream, sg.doubleSided);
            }
        }

        static void MakeBoard(Transform parent, string name, float y, string text, Material ink, Material accent, Color32 cream, bool doubleSided)
        {
            float width = Mathf.Clamp(0.15f * text.Length, 2.3f, 3.9f);
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube); board.name = name; board.transform.SetParent(parent, false);
            board.transform.localPosition = new Vector3(0, y, 0); board.transform.localScale = new Vector3(width, 0.58f, 0.1f); board.GetComponent<Renderer>().sharedMaterial = ink;
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = "Franja"; bar.transform.SetParent(board.transform, false);
            bar.transform.localPosition = new Vector3(0, 0.5f, 0); bar.transform.localScale = new Vector3(1f, 0.16f, 1.12f); bar.GetComponent<Renderer>().sharedMaterial = accent;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                var t = new GameObject(side == 0 ? "Texto" : "Texto_reverso").AddComponent<TextMeshPro>();
                t.transform.SetParent(parent, false);
                t.transform.localPosition = new Vector3(0, y, side == 0 ? -0.056f : 0.056f); t.transform.localRotation = Quaternion.Euler(0, side == 0 ? 0f : 180f, 0);
                t.rectTransform.sizeDelta = new Vector2(width - 0.55f, 0.5f);
                t.transform.localPosition += new Vector3(0.15f, 0f, 0f);                       // deja libre el borde del poste
                t.text = text; t.color = cream; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
                t.enableAutoSizing = true; t.fontSizeMin = 1f; t.fontSizeMax = 4f; t.enableWordWrapping = false;
            }
        }

        static void AddTestHelper(GameObject player)
        {
            var h = player.AddComponent<R621TestHelper>(); var stops = new List<R621TestHelper.Stop>();
            Vector3 G(Vector2 p) => new Vector3(p.x, L.Height(p.x, p.y) + 0.1f, p.y);
            stops.Add(new R621TestHelper.Stop { label = "spawn", position = L.Spawn, yaw = L.SpawnYaw });
            for (int i = 0; i < 4; i++) { var hs = H.Houses[i]; stops.Add(new R621TestHelper.Stop { label = hs.name, position = G(H.DoorPoint(i, 0f) + hs.Front * 6.5f), yaw = Mathf.Atan2(-hs.Front.x, -hs.Front.y) * Mathf.Rad2Deg }); }
            for (int b = 0; b < 4; b++) stops.Add(new R621TestHelper.Stop { label = L.BridgeName[b], position = G(L.Bridges[b].center - L.Bridges[b].axis * 9f), yaw = L.Bridges[b].yaw });
            h.stops = stops.ToArray();
        }

        // ------------------------------------------------------------------ utilidades
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
            if (prefab == null) { Debug.LogError("[R621] Prefab no encontrado: " + prefabPath); return new GameObject(name); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name; go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * scale; return go;
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
            var mf = go.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return;
            var box = go.AddComponent<BoxCollider>(); box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size) { var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size; }

        static float MeasureBridgeY()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageDir + "Bridge.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(new Vector3(500, 0, 500), Quaternion.identity); Physics.SyncTransforms();
            float sum = 0; int n = 0; var sb = new System.Text.StringBuilder("[R621] Tablero del puente (y local por z): ");
            for (float z = -6f; z <= 6.01f; z += 1f) if (Physics.Raycast(new Vector3(500f, 6f, 500f + z), Vector3.down, out var hit, 20f)) { sb.Append($"z={z:0}:{hit.point.y:0.00} "); if (Mathf.Abs(z) >= 5.99f) { sum += hit.point.y; n++; } }
            Object.DestroyImmediate(go); Debug.Log(sb.ToString());
            return n > 0 ? -sum / n : 0f;
        }

        static Mesh GroundMesh()
        {
            const float cell = 1f; int n = Mathf.RoundToInt(L.Half * 2f / cell);
            var verts = new List<Vector3>(n * n * 6); var tris = new List<int>(n * n * 6); var uvs = new List<Vector2>(n * n * 6);
            float Hh(int i, int j) => L.Height(-L.Half + i * cell, -L.Half + j * cell);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x0 = -L.Half + i * cell, z0 = -L.Half + j * cell, x1 = x0 + cell, z1 = z0 + cell;
                    var a = new Vector3(x0, Hh(i, j), z0); var b = new Vector3(x0, Hh(i, j + 1), z1); var c = new Vector3(x1, Hh(i + 1, j + 1), z1); var d = new Vector3(x1, Hh(i + 1, j), z0);
                    if ((i + j) % 2 == 0) Quad(verts, tris, uvs, a, b, c, a, c, d); else Quad(verts, tris, uvs, a, b, d, b, c, d);
                }
            var mesh = new Mesh { name = "R621_Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static void Quad(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a1, Vector3 b1, Vector3 c1, Vector3 a2, Vector3 b2, Vector3 c2)
        {
            int s = v.Count; v.Add(a1); v.Add(b1); v.Add(c1); v.Add(a2); v.Add(b2); v.Add(c2);
            for (int k = 0; k < 6; k++) { t.Add(s + k); uv.Add(new Vector2(v[s + k].x * 0.25f, v[s + k].z * 0.25f)); }
        }
    }

    /// <summary>Mallas del río (anchura variable), orillas irregulares, barrera, cintas, discos y anillos sobre la altura de 6.2.1.</summary>
    public static class R621Mesh
    {
        static List<float> Arc(List<(Vector2 p, Vector2 t)> pts)
        {
            var a = new List<float>(pts.Count) { 0f };
            for (int i = 1; i < pts.Count; i++) a.Add(a[i - 1] + (pts[i].p - pts[i - 1].p).magnitude);
            return a;
        }

        public static Mesh Strip(IList<Vector2> poly, System.Func<int, float> halfWidth, float lift, int across, string name)
        {
            var pts = Proto612Layout.Resample(poly, 0.5f); float hw = halfWidth(0);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                var n = new Vector2(pts[i].t.y, -pts[i].t.x);
                for (int k = 0; k < across; k++)
                {
                    float s = (k / (float)(across - 1)) * 2f - 1f; var p = pts[i].p + n * (hw * s);
                    v.Add(new Vector3(p.x, R621Layout.Height(p.x, p.y) + lift, p.y)); uv.Add(new Vector2(k / (float)(across - 1), i * 0.5f));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++) for (int k = 0; k < across - 1; k++) { int a = i * across + k, b = a + 1, c = a + across, d = c + 1; t.AddRange(new[] { a, c, b, b, c, d }); }
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        /// <summary>Franja de arena a lo largo del río con borde exterior irregular a cada lado (anchura base 3,5·W + variación).</summary>
        public static Mesh Banks()
        {
            var pts = Proto612Layout.Resample(R621Layout.River, 0.5f); var arc = Arc(pts); const int across = 17;
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                var n = new Vector2(pts[i].t.y, -pts[i].t.x); float w = R621Layout.Width(arc[i]);
                float left = 3.5f * w + 0.7f + R621Layout.BankJitter(arc[i], 0), right = 3.5f * w + 0.7f + R621Layout.BankJitter(arc[i], 1);
                for (int k = 0; k < across; k++)
                {
                    float s = (k / (float)(across - 1)) * 2f - 1f; float off = s < 0 ? s * left : s * right; var p = pts[i].p + n * off;
                    v.Add(new Vector3(p.x, R621Layout.Height(p.x, p.y) + 0.08f, p.y)); uv.Add(new Vector2(k / (float)(across - 1), arc[i] * 0.2f));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++) for (int k = 0; k < across - 1; k++) { int a = i * across + k, b = a + 1, c = a + across, d = c + 1; t.AddRange(new[] { a, c, b, b, c, d }); }
            var m = new Mesh { name = "Banks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public static Mesh Water()
        {
            var pts = Proto612Layout.Resample(R621Layout.River, 0.5f); var arc = Arc(pts);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                float hw = R621Layout.WaterHalfBase * R621Layout.Width(arc[i]) + 0.25f; var n = new Vector2(pts[i].t.y, -pts[i].t.x) * hw;
                v.Add(new Vector3(pts[i].p.x - n.x, R621Layout.WaterY, pts[i].p.y - n.y)); uv.Add(new Vector2(0, arc[i] * 0.2f));
                v.Add(new Vector3(pts[i].p.x + n.x, R621Layout.WaterY, pts[i].p.y + n.y)); uv.Add(new Vector2(1, arc[i] * 0.2f));
            }
            for (int i = 0; i < pts.Count - 1; i++) { int a = i * 2; t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            var m = new Mesh { name = "Water" }; m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public static Mesh Disc(Vector2 c, float r, float lift)
        {
            const int seg = 48; var v = new List<Vector3> { new Vector3(c.x, R621Layout.Height(c.x, c.y) + lift, c.y) }; var t = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r; v.Add(new Vector3(x, R621Layout.Height(x, z) + lift, z)); }
            for (int i = 1; i <= seg; i++) t.AddRange(new[] { 0, i + 1, i });
            var m = new Mesh { name = "Disc" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public static Mesh Ring(Vector2 c, float r0, float r1)
        {
            const int seg = 32; var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var p0 = c + d * r0; var p1 = c + d * r1;
                v.Add(new Vector3(p0.x, R621Layout.Height(p0.x, p0.y) + 0.07f, p0.y)); v.Add(new Vector3(p1.x, R621Layout.Height(p1.x, p1.y) + 0.07f, p1.y));
            }
            for (int i = 0; i < seg; i++) { int a = i * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            var m = new Mesh { name = "Ring" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static bool NearBridge(Vector2 p)
        {
            foreach (var b in R621Layout.Bridges) if ((p - b.center).magnitude < R621Layout.BridgeGap) return true;
            return false;
        }

        /// <summary>Muro invisible a cada lado, justo fuera del agua (1,75·W + 0,5 m), con un hueco del ancho del tablero en cada puente.</summary>
        public static Mesh Barrier()
        {
            var pts = Proto612Layout.Resample(R621Layout.River, 0.5f); var arc = Arc(pts);
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
                        float off = (R621Layout.WaterHalfBase * R621Layout.Width(arc[i]) + 0.5f) * side;
                        var p = pts[i].p + new Vector2(pts[i].t.y, -pts[i].t.x) * off;
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
