using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = Lumina.Prototype612.R621Layout;
using H = Lumina.Prototype612.Proto612LayoutV2;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.2: identidad visual y temática de las cuatro aldeas. Reconstruye la escena base de 6.2.1 (R621Builder, idempotente) y añade
    /// la decoración bajo «--- DECORACION R622 ---». Todo es nuevo y exclusivo del prototipo (mallas Meshes/R622_*, materiales R622_*).
    /// Las manos de la Aldea de los Saludos son esculturas decorativas abstractas: NO representan ni etiquetan ninguna seña de la LSC.
    /// </summary>
    public static class R622Builder
    {
        const string Root = Proto612Builder.Root;
        const string VillageDir = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        public const string ScenePath = R621Builder.ScenePath;
        public const string DecoName = "--- DECORACION R622 ---";

        static Dictionary<string, Material> _m; static readonly Dictionary<string, Mesh> _mesh = new Dictionary<string, Mesh>();
        static readonly List<(Vector2 p, float r)> Occ = new List<(Vector2, float)>();
        static int _count;

        [MenuItem("Lumina/Prototipo 6.2.2/Construir decoración temática")]
        public static void Build()
        {
            R621Builder.Build();                                   // escena base nueva (con la corrección del empalme del puente centro-sur)
            _mesh.Clear(); Occ.Clear(); _count = 0; _entries = null; _m = Mats();
            var old = GameObject.Find(DecoName); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(DecoName).transform;
            SeedOccupancy();
            Abecedario(Child(root, "Aldea_Abecedario"));
            Numeros(Child(root, "Aldea_Numeros"));
            Meses(Child(root, "Aldea_Meses"));
            Saludos(Child(root, "Aldea_Saludos"));
            Mundo(Child(root, "Mundo_Vegetacion"));
            // Mallas R622 huérfanas de construcciones anteriores (recursos propios de esta intervención): se eliminan para no acumular.
            var keep = new HashSet<string>(); foreach (var k in _mesh.Keys) keep.Add("R622_" + k + ".asset");
            foreach (var f in Directory.GetFiles(Root + "Meshes", "R622_*.asset")) if (!keep.Contains(Path.GetFileName(f))) AssetDatabase.DeleteAsset(f.Replace((char)92, '/'));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[R622] Decoración creada: " + _count + " objetos, " + _mesh.Count + " mallas únicas.");
        }

        public static void BuildTwiceAndCompare()
        {
            Build(); var a = CountByName(); int ma = Directory.GetFiles(Root + "Meshes", "R622_*.asset").Length;
            Build(); var b = CountByName(); int mb = Directory.GetFiles(Root + "Meshes", "R622_*.asset").Length;
            var sb = new System.Text.StringBuilder("[R622-IDEMPOTENCIA]\n"); bool same = a.Count == b.Count && ma == mb; int total = 0;
            foreach (var kv in a) { total += kv.Value; if (!b.TryGetValue(kv.Key, out var n) || n != kv.Value) { same = false; sb.AppendLine($"  difiere '{kv.Key}': {kv.Value} → {(b.TryGetValue(kv.Key, out var m) ? m : 0)}"); } }
            sb.AppendLine($"Objetos por nombre: {a.Count} nombres distintos, {total} objetos; mallas R622: {ma} y {mb}; segunda construcción {(same ? "IDÉNTICA" : "DIFERENTE")}");
            File.WriteAllText(Root + "Evidence/r622_idempotencia.txt", sb.ToString()); Debug.Log(sb.ToString());
        }

        static Dictionary<string, int> CountByName()
        {
            var d = new Dictionary<string, int>();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) d[go.name] = d.TryGetValue(go.name, out var n) ? n + 1 : 1;
            return d;
        }

        // ================================================================== materiales
        static readonly (string key, string hex)[] Palette =
        {
            ("Cream", "#FFF9EC"), ("Ink", "#0F3A44"), ("Turquoise", "#2EC4B6"), ("Coral", "#FF5E4D"), ("Sand", "#FAE3BD"), ("SandDark", "#D8BE8E"), ("Stone", "#BDB6A4"), ("StoneTop", "#E6DCC6"),
            ("Wood", "#8C6138"), ("Trunk", "#7A5230"), ("Stem", "#4F9B45"), ("Center", "#FFC93C"),
            ("Green1", "#5FA64B"), ("Green2", "#4C9A5A"), ("Green3", "#7DBB55"), ("Pine", "#2B7047"), ("Hedge", "#3F8F4A"),
            ("SpringLeaf", "#9AD66E"), ("SummerLeaf", "#3E8E41"), ("AutumnOrange", "#E68A2E"), ("AutumnRed", "#C8452F"), ("WinterLeaf", "#C6DDE6"), ("WinterPine", "#6FA3B5"),
            ("FlowerPink", "#F58DB0"), ("FlowerWhite", "#FFF4F0"), ("FlowerYellow", "#FFD34D"), ("FlowerOrange", "#FF9A3C"), ("FlowerRed", "#E5484D"), ("FlowerBlue", "#7CB6F2"), ("FlowerLilac", "#B79CF0"),
            ("TowerBody", "#F6E3C0"), ("BedSpring", "#BFE3A0"), ("BedSummer", "#78BC5C"), ("BedAutumn", "#D9B26A"), ("BedWinter", "#EAF4F8"),
            ("TileWinter", "#CFE6F2"), ("TileSpring", "#F6C6D8"), ("TileSummer", "#FFE08A"), ("TileAutumn", "#F0A35E"),
        };

        static Dictionary<string, Material> Mats()
        {
            var d = new Dictionary<string, Material>();
            foreach (var (key, hex) in Palette) { ColorUtility.TryParseHtmlString(hex, out var c); d[key] = NewMat(key, c); }
            return d;
        }

        static Material NewMat(string key, Color c)
        {
            string path = Root + "Materials/R622_" + key + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); return m; }
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "R622_" + key };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.CreateAsset(m, path); return m;
        }

        static Material[] Ms(params string[] keys) { var r = new Material[keys.Length]; for (int i = 0; i < keys.Length; i++) r[i] = _m[keys[i]]; return r; }

        // ================================================================== utilidades de colocación
        static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static Vector3 G(Vector2 p, float dy = 0f) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);
        static float Yaw(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        static Mesh GetMesh(string key, System.Func<string, Mesh> make)
        {
            if (_mesh.TryGetValue(key, out var m)) return m;
            string path = Root + "Meshes/R622_" + key + ".asset"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            m = make("R622_" + key); AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path); _mesh[key] = m; return m;
        }

        static GameObject Place(Transform parent, string name, Mesh mesh, Material[] mats, Vector2 p, float yaw, float scale = 1f, float lift = 0f, Vector3? euler = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(G(p, lift), Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(euler ?? Vector3.zero)); go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); _count++; return go;
        }

        static void Capsule(GameObject go, float radius, float height, float centerY = -1f) { var c = go.AddComponent<CapsuleCollider>(); c.radius = radius; c.height = height; c.center = new Vector3(0, centerY < 0 ? height * 0.5f : centerY, 0); }
        static void Box(GameObject go, Vector3 center, Vector3 size) { var b = go.AddComponent<BoxCollider>(); b.center = center; b.size = size; }

        // ---- ocupación del suelo (círculos) y reglas de libertad
        static void SeedOccupancy()
        {
            foreach (var it in L.Trees()) Occ.Add((new Vector2(it.pos.x, it.pos.z), 1.0f));
            foreach (var it in L.Rocks()) Occ.Add((new Vector2(it.pos.x, it.pos.z), 0.9f));
            foreach (var it in L.Props()) Occ.Add((new Vector2(it.pos.x, it.pos.z), it.prefab == "Bench" ? 1.3f : 0.6f));
            foreach (var s in L.Signs()) Occ.Add((s.pos, 0.9f));
            Occ.Add((new Vector2(L.Spawn.x, L.Spawn.z), 3.2f)); Occ.Add((new Vector2(L.SenaPos.x, L.SenaPos.z), 1.8f)); Occ.Add((L.PlazaCenter, L.PlazaRadius + 0.8f));
            foreach (var h in H.Houses) Occ.Add((h.center, 4.6f));
        }

        static bool Free(Vector2 p, float r) { foreach (var (q, rr) in Occ) if (Vector2.Distance(p, q) < r + rr) return false; return true; }
        static void Take(Vector2 p, float r) => Occ.Add((p, r));

        /// <summary>Entradas a una plaza temática: puntos de los caminos a 6 m del centro de la plaza.</summary>
        static List<Vector2>[] _entries;
        static List<Vector2> Entries(int v)
        {
            if (_entries == null) { _entries = new List<Vector2>[4]; for (int k = 0; k < 4; k++) _entries[k] = ComputeEntries(k); }
            return _entries[v];
        }

        static List<Vector2> ComputeEntries(int v)
        {
            var res = new List<Vector2>();
            foreach (var path in L.Paths)
            {
                if ((path[0] - L.HubCenter[v]).magnitude < 0.6f || (path[path.Count - 1] - L.HubCenter[v]).magnitude < 0.6f)
                    foreach (var (q, _) in Proto612Layout.Resample(path, 0.5f)) { if (Mathf.Abs((q - L.HubCenter[v]).magnitude - 6f) < 0.3f) { res.Add(q); break; } }
            }
            return res;
        }

        /// <summary>Punto libre: fuera de caminos (con margen), de los pasillos de entrada a las plazas, de las puertas, de los ejes de puentes, del río y de lo ya colocado.</summary>
        static bool Ok(Vector2 p, float r, float riverMin = 6.5f)
        {
            if (Mathf.Abs(p.x) > 42.5f || Mathf.Abs(p.y) > 42.5f) return false;
            if (L.LaneDist(p) < 1.25f + r + 0.5f) return false;
            if (L.DistToDoorCorridors(p) < r + 3f) return false;
            if (L.AxisDist(p) < r + 3f) return false;
            if (L.DistToRiver(p) < riverMin + r) return false;
            for (int v = 0; v < 4; v++) foreach (var e in Entries(v)) if (Proto612Layout.Nearest(p, new[] { L.HubCenter[v], e + (e - L.HubCenter[v]).normalized * 3f }).dist < 1.6f + r) return false;
            return Free(p, r);
        }

        static Vector2 Pre(int v, float d) => H.DoorPoint(v, 0f) + H.Houses[v].Front * d;
        static Vector2 Dir(Vector2 from, Vector2 to) => (to - from).normalized;
        static Vector2 Polar(Vector2 c, float deg, float r) { float a = deg * Mathf.Deg2Rad; return c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r; }
        static float AngleOf(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;   // 0° = +Z (norte), 90° = +X

        // ---- vegetación genérica
        static void TreeAt(Transform parent, string kind, string leaf, Vector2 p, float scale, float yaw)
        {
            var mesh = GetMesh("Tree_" + kind, n => R622Meshes.Tree(kind, n));
            var go = Place(parent, "Arbol_" + kind, mesh, Ms("Trunk", leaf), p, yaw, scale);
            if (kind == "shrub") Capsule(go, 0.55f, 0.7f); else if (kind == "hedge") Box(go, new Vector3(0, 0.45f, 0), new Vector3(1.3f, 0.9f, 1.3f)); else Capsule(go, 0.32f, 2.4f);
            Take(p, kind == "shrub" ? 0.8f * scale : (kind == "hedge" ? 0.9f * scale : 0.9f * scale));
        }

        static void FlowersAt(Transform parent, int count, float radius, int seed, bool ordered, string petal, Vector2 p, float yaw)
        {
            if (!ordered) { count = count <= 9 ? 8 : 12; radius = radius <= 0.65f ? 0.5f : 0.8f; seed = 620 + seed % 3; }       // pocas variantes de malla reutilizadas con giros y posiciones distintas
            var mesh = GetMesh($"Flores_{count}_{(int)(radius * 10)}_{seed}_{(ordered ? "o" : "d")}", n => R622Meshes.FlowerPatch(count, radius, seed, ordered, n));
            Place(parent, "Flores", mesh, Ms("Stem", petal, "Center"), p, yaw, 1f, 0f); Take(p, radius * 0.6f);
        }

        static Material GlyphMat(string key) => _m[key];

        static GameObject GlyphObj(Transform parent, char c, float h, float depth, float stroke, string color, Vector2 p, float yaw, float lift = 0f, Vector3? euler = null, string nameTag = "")
        {
            var mesh = GetMesh($"Glifo_{c}_{(int)(h * 100)}_{(int)(depth * 100)}", n => R622Meshes.GlyphMesh(c, h, depth, stroke, n));
            return Place(parent, "Letra_" + c + nameTag, mesh, new[] { GlyphMat(color) }, p, yaw, 1f, lift, euler);
        }

        // ================================================================== ABECEDARIO (noroeste): letras, juego y jardines libres
        static void Abecedario(Transform t)
        {
            int v = 0; var hub = L.HubCenter[v]; var rings = L.ReserveRings[v]; var toApproach = Dir(hub, Pre(v, 7f)); float face = Yaw(toApproach);
            // Monumento central: tres letras grandes (A B C) sobre una peana, mirando hacia afuera.
            var plinth = Place(t, "Monumento_Peana", GetMesh("Peana_2_4", n => R622Meshes.Plinth(2.4f, 0.3f, n)), Ms("Stone", "StoneTop"), hub, 0f); Capsule(plinth, 2.3f, 0.5f); Take(hub, 3.0f);
            string[] letters = { "A", "B", "C" }; string[] colors = { "Turquoise", "Coral", "Sand" };
            for (int i = 0; i < 3; i++)
            {
                float ang = face + (i - 1) * 120f + 0f; var dir = new Vector2(Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad));
                var go = GlyphObj(t, letters[i][0], 2.3f, 0.5f, 0.55f, colors[i], hub + dir * 1.15f, ang, 0.38f, null, "_Monumento");
                Box(go, new Vector3(0, 1.15f, 0), new Vector3(1.4f, 2.3f, 0.5f));
            }
            // Letras altas (puntos de referencia) en las tres reservas, de cara a la plaza.
            string[] tall = { "L", "M", "O" }; string[] tcol = { "Coral", "Turquoise", "Sand" };
            for (int i = 0; i < 3; i++)
            {
                var p = rings[i]; float y = Yaw(Dir(p, Pre(v, 7f)));
                Place(t, "Reserva_Base", GetMesh("Base_Reserva", n => R622Meshes.Flat("circle", 2.2f, 0.18f, n)), Ms("SandDark"), p, 0f, 1f, 0f);
                var go = GlyphObj(t, tall[i][0], 3.4f, 0.65f, 0.78f, tcol[i], p, y, 0.2f, null, "_Alta"); Box(go, new Vector3(0, 1.7f, 0), new Vector3(2.0f, 3.4f, 0.65f)); Take(p, 1.6f);
            }
            // Letras pequeñas y juguetonas (inclinadas) en el borde de la plaza.
            string small = "DEZCD"; var rng = new System.Random(6221); int placed = 0;
            for (int k = 0; k < 400 && placed < small.Length; k++)
            {
                var p = Polar(hub, (float)rng.NextDouble() * 360f, 5.0f + (float)rng.NextDouble() * 4.5f);
                if (!Ok(p, 0.8f)) continue;
                var go = GlyphObj(t, small[placed], 1.0f, 0.3f, 0.3f, new[] { "Turquoise", "Coral", "Sand", "Turquoise", "Coral" }[placed], p, face + (float)(rng.NextDouble() * 80 - 40), 0.05f, new Vector3(0, 0, (float)(rng.NextDouble() * 24 - 12)), "_Juego");
                Box(go, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1.0f, 0.3f)); Take(p, 0.8f); placed++;
            }
            // Letras tumbadas en el suelo de la plaza (patrón alrededor del monumento), sin colliders.
            string flat = "ABCDEL"; for (int i = 0; i < flat.Length; i++)
            {
                float a = face + 40f + i * 50f; var p = Polar(hub, a, 3.45f); if (Proto612Layout.Nearest(p, new[] { hub, Pre(v, 7f) }).dist < 1.4f) continue;
                var mesh = GetMesh($"GlifoPlano_{flat[i]}", n => R622Meshes.GlyphMesh(flat[i], 0.8f, 0.05f, 0.24f, n));
                Place(t, "Letra_" + flat[i] + "_Suelo", mesh, new[] { GlyphMat(i % 2 == 0 ? "Cream" : "SandDark") }, p, a + 180f, 1f, 0.07f, new Vector3(90, 0, 0));
            }
            // Jardines creativos: parches libres de colores mezclados y árboles de formas distintas, en composición abierta.
            string[] pet = { "FlowerPink", "FlowerYellow", "FlowerLilac", "FlowerWhite", "FlowerOrange", "FlowerBlue" }; string[] kinds = { "round", "umbrella", "cluster", "tall", "round" }; string[] leaves = { "Green3", "SpringLeaf", "Green1", "Green2", "Green3" };
            int pk = 0, tk = 0;
            for (int k = 0; k < 160 && (pk < 8 || tk < 5); k++)
            {
                var p = Polar(hub, (float)rng.NextDouble() * 360f, 5.5f + (float)rng.NextDouble() * 6.5f);
                if (tk < 5 && k % 3 == 0 && Ok(p, 1.3f)) { TreeAt(t, kinds[tk], leaves[tk], p, 0.9f + (float)rng.NextDouble() * 0.4f, (float)rng.NextDouble() * 360f); tk++; }
                else if (pk < 8 && Ok(p, 1.1f)) { FlowersAt(t, 10 + (pk % 3) * 4, 0.9f, 620 + pk, false, pet[pk % pet.Length], p, (float)rng.NextDouble() * 360f); pk++; }
            }
        }

        // ================================================================== NÚMEROS (suroeste): cifras, geometría y orden
        static void Numeros(Transform t)
        {
            int v = 1; var hub = L.HubCenter[v]; var rings = L.ReserveRings[v]; var toApproach = Dir(hub, Pre(v, 7f)); float face = Yaw(toApproach);
            Place(t, "Monumento_Peana", GetMesh("Peana_2_5", n => R622Meshes.Plinth(2.5f, 0.2f, n)), Ms("Stone", "StoneTop"), hub, 0f); Take(hub, 3.0f);
            // Secuencia ascendente 1–5: cinco columnas de altura creciente en arco, con la cifra en relieve en la cara frontal.
            string[] bcol = { "Turquoise", "Coral", "Sand", "Turquoise", "Coral" };
            for (int i = 0; i < 5; i++)
            {
                float ang = face + (i - 2) * 30f; var dir = new Vector2(Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad)); var p = hub + dir * 1.75f; float h = 0.8f + i * 0.5f;
                var block = Place(t, "Columna_" + (i + 1), GetMesh($"Columna_{i}", n => { var mb = new MB(1); R622Meshes.BevelBox(mb, 0, Matrix4x4.TRS(new Vector3(0, h * 0.5f, 0), Quaternion.identity, Vector3.one), new Vector3(0.95f, h, 0.95f), 0.12f); return mb.Build(n); }), Ms(bcol[i]), p, ang, 1f, 0.2f);
                Box(block, new Vector3(0, h * 0.5f, 0), new Vector3(0.95f, h, 0.95f));
                var digit = GetMesh($"Cifra_{i + 1}", n => R622Meshes.GlyphMesh((char)('1' + i), 0.55f, 0.1f, 0.14f, n));
                var d = Place(block.transform, "Cifra_" + (i + 1), digit, Ms("Cream"), Vector2.zero, 0f); d.transform.localPosition = new Vector3(0, h * 0.5f - 0.2f, 0.46f); d.transform.localRotation = Quaternion.identity; d.transform.localScale = Vector3.one;
            }
            // Cifras altas en las reservas.
            string[] tall = { "7", "0", "2" }; string[] tcol = { "Coral", "Turquoise", "Sand" };
            for (int i = 0; i < 3; i++)
            {
                var p = rings[i]; var go = GlyphObj(t, tall[i][0], 3.2f, 0.6f, 0.72f, tcol[i], p, Yaw(Dir(p, Pre(v, 7f))), 0.2f, null, "_Alta"); Box(go, new Vector3(0, 1.6f, 0), new Vector3(1.9f, 3.2f, 0.6f));
                Place(t, "Reserva_Base", GetMesh("Base_Reserva_Cuadrada", n => R622Meshes.Flat("square", 2.2f, 0.18f, n)), Ms("SandDark"), p, Yaw(Dir(p, hub)), 1f, 0f); Take(p, 1.6f);
            }
            // Mosaico geométrico en el suelo de la plaza: círculo, cuadrado y triángulo alternados (sin colliders).
            string[] shapes = { "circle", "square", "tri" }; string[] scol = { "Turquoise", "Coral", "Sand", "Cream" }; int n8 = 0;
            for (int i = 0; i < 10; i++)
            {
                float a = face + 36f * i + 18f; var p = Polar(hub, a, 3.4f); if (Proto612Layout.Nearest(p, new[] { hub, Pre(v, 7f) }).dist < 1.3f) continue;
                string sh = shapes[n8 % 3]; var mesh = GetMesh("Forma_" + sh, n => R622Meshes.Flat(sh, 0.75f, 0.05f, n)); Place(t, "Forma_" + sh, mesh, Ms(scol[n8 % 4]), p, a, 1f, 0.07f); n8++;
            }
            // Pirámides de bloques (3-2-1) como juego de cantidades.
            int piramides = 0; var rng = new System.Random(6222);
            for (int k = 0; k < 80 && piramides < 2; k++)
            {
                var p = Polar(hub, face + 100f + (float)rng.NextDouble() * 160f, 6.2f + (float)rng.NextDouble() * 2.5f); if (!Ok(p, 1.4f)) continue;
                var root = new GameObject("Piramide_Bloques"); root.transform.SetParent(t, false); root.transform.position = G(p); root.transform.rotation = Quaternion.Euler(0, Yaw(Dir(p, hub)), 0);
                var cube = GetMesh("Bloque", n => { var mb = new MB(1); R622Meshes.BevelBox(mb, 0, Matrix4x4.TRS(new Vector3(0, 0.35f, 0), Quaternion.identity, Vector3.one), new Vector3(0.7f, 0.7f, 0.7f), 0.08f); return mb.Build(n); });
                int[] rows = { 3, 2, 1 }; string[] cc = { "Turquoise", "Coral", "Sand" };
                for (int r = 0; r < 3; r++) for (int c = 0; c < rows[r]; c++)
                { var b = Place(root.transform, "Bloque", cube, Ms(cc[r]), Vector2.zero, 0f); b.transform.localPosition = new Vector3((c - (rows[r] - 1) * 0.5f) * 0.72f, r * 0.7f, 0f); b.transform.localRotation = Quaternion.identity; }
                Box(root, new Vector3(0, 0.55f, 0), new Vector3(2.2f, 1.1f, 0.7f)); _count++; Take(p, 1.5f); piramides++;
            }
            // Jardín ordenado: parches de 3, 5 y 8 flores en fila, seto geométrico y árboles en secuencia creciente.
            var along = new Vector2(toApproach.y, -toApproach.x); int[] counts = { 3, 5, 8 }; var gstart = hub + along * 7.5f - toApproach * 1.0f;
            for (int i = 0; i < 3; i++) { var p = gstart + along * (i * 2.0f); if (Ok(p, 0.8f)) FlowersAt(t, counts[i], 0.5f, 630 + i, true, new[] { "FlowerYellow", "FlowerWhite", "FlowerBlue" }[i], p, AngleOf(along)); }
            for (int i = 0; i < 5; i++) { var p = hub - along * 6.5f + toApproach * (-3.2f + i * 1.45f); if (Ok(p, 0.8f)) TreeAt(t, "hedge", "Hedge", p, 0.95f, AngleOf(along)); }
            for (int i = 0; i < 4; i++) { var p = hub - along * 9.0f + toApproach * (-4.0f + i * 2.8f); if (Ok(p, 1.2f)) TreeAt(t, "round", i % 2 == 0 ? "Green1" : "Green3", p, 0.7f + i * 0.17f, 0f); }
        }

        // ================================================================== MESES (noreste): reloj, calendario y estaciones
        static void Meses(Transform t)
        {
            int v = 2; var hub = L.HubCenter[v]; var rings = L.ReserveRings[v]; var toApproach = Dir(hub, Pre(v, 7f)); float face = Yaw(toApproach);
            // Torre del reloj: cuatro esferas de reloj, techo piramidal coral.
            var tower = Place(t, "Torre_Reloj", GetMesh("Torre", n => R622Meshes.ClockTower(n)), Ms("TowerBody", "Coral", "Ink"), hub, face, 1f, 0f);
            Box(tower, new Vector3(0, 2.4f, 0), new Vector3(2.1f, 4.8f, 2.1f)); Take(hub, 2.2f);
            var clock = GetMesh("Reloj_0_8", n => R622Meshes.ClockFace(0.8f, n));
            for (int s = 0; s < 4; s++)
            {
                var c = Place(tower.transform, "Reloj_" + (s + 1), clock, Ms("Coral", "Cream", "Ink"), Vector2.zero, 0f);
                var q = Quaternion.Euler(0, s * 90f, 0); c.transform.localRotation = q; c.transform.localPosition = q * new Vector3(0, 3.2f, 0.92f); c.transform.localScale = Vector3.one;
            }
            // Anillos concéntricos y 12 marcas horarias en el suelo de la plaza (forma circular).
            Place(t, "Anillo_Reloj_1", GetMesh("Anillo_3_3_2", n => R622Meshes.RingFlat(3.0f, 3.2f, 0.05f, 36, n)), Ms("Coral"), hub, 0f, 1f, 0.07f);
            Place(t, "Anillo_Reloj_2", GetMesh("Anillo_3_9_4", n => R622Meshes.RingFlat(3.85f, 4.0f, 0.05f, 36, n)), Ms("Cream"), hub, 0f, 1f, 0.07f);
            for (int i = 0; i < 12; i++) { var p = Polar(hub, i * 30f, 3.5f); bool big = i % 3 == 0; Place(t, "Hora_" + (i + 1), GetMesh(big ? "Marca_hora_grande" : "Marca_hora_pequena", n => R622Meshes.Flat("circle", big ? 0.34f : 0.2f, 0.05f, n)), Ms(big ? "Coral" : "Ink"), p, 0f, 1f, 0.07f); }
            // Calendario en el mundo: hoja con cabecera y anillos, 12 casillas de color por estación, con la abreviatura de cada mes.
            var cp = rings[0]; var cdir = Dir(cp, hub); float cy = Yaw(cdir);
            var cal = Place(t, "Calendario", GetMesh("Calendario", n => R622Meshes.Calendar(n)), Ms("Cream", "Coral", "Ink", "TileWinter", "TileSpring", "TileSummer", "TileAutumn"), cp, cy, 1.15f, 0f);
            Box(cal, new Vector3(0, 1.55f, 0), new Vector3(3.3f, 3.1f, 0.4f)); Take(cp, 2.0f);
            string[] mes = { "ENE", "FEB", "MAR", "ABR", "MAY", "JUN", "JUL", "AGO", "SEP", "OCT", "NOV", "DIC" };
            for (int m = 0; m < 12; m++)
            {
                int row = m / 4, col = m % 4; var go = new GameObject("Mes_" + mes[m]); go.transform.SetParent(cal.transform, false); go.transform.localPosition = new Vector3(-1.2f + col * 0.8f, 1.95f - row * 0.62f, 0.165f); go.transform.localRotation = Quaternion.Euler(0, 180, 0);      // TMP se lee desde su lado −Z: se gira para leerse desde el frente del calendario
                var tm = go.AddComponent<TextMeshPro>(); tm.text = mes[m]; tm.fontSize = 2.2f; tm.fontStyle = FontStyles.Bold; tm.alignment = TextAlignmentOptions.Center; tm.color = new Color32(0x0F, 0x3A, 0x44, 0xFF); tm.rectTransform.sizeDelta = new Vector2(0.66f, 0.3f); tm.enableWordWrapping = false;
            }
            // Cuatro parterres de estación: primavera, verano, otoño, invierno.
            string[] bedName = { "Primavera", "Verano", "Otono", "Invierno" }; string[] bedMat = { "BedSpring", "BedSummer", "BedAutumn", "BedWinter" };
            string[] treeK = { "round", "umbrella", "cluster", "cone" }; string[][] leaf = { new[] { "SpringLeaf", "FlowerPink" }, new[] { "SummerLeaf", "FlowerYellow" }, new[] { "AutumnOrange", "AutumnRed" }, new[] { "WinterPine", "WinterLeaf" } };
            string[][] fl = { new[] { "FlowerPink", "FlowerWhite" }, new[] { "FlowerYellow", "FlowerOrange" }, new[] { "FlowerOrange", "FlowerRed" }, new[] { "FlowerWhite", "FlowerBlue" } };
            var spots = new List<Vector2> { rings[1], rings[2] }; var rng = new System.Random(6223);
            for (int k = 0; k < 600 && spots.Count < 4; k++) { var p = Polar(hub, (float)rng.NextDouble() * 360f, 6.0f + (float)rng.NextDouble() * 3.5f); if (Ok(p, 2.2f)) { spots.Add(p); Take(p, 2.2f); } }
            for (int s = 0; s < spots.Count && s < 4; s++)
            {
                var p = spots[s]; Take(p, 2.4f);
                Place(t, "Parterre_" + bedName[s], GetMesh("Parterre_" + bedName[s], n => R622Meshes.Flat("circle", 4.4f, 0.1f, n)), Ms(bedMat[s]), p, 0f, 1f, 0.0f);
                TreeAt(t, treeK[s], leaf[s][0], p + new Vector2(0.2f, 0.1f), 0.95f, s * 50f);
                for (int b = 0; b < 3; b++) { var q = Polar(p, 100f + b * 120f, 1.5f); FlowersAt(t, 8, 0.45f, 640 + s * 3 + b, false, fl[s][b % 2], q, b * 40f); }
                for (int b = 0; b < 2; b++) { var q = Polar(p, 160f + b * 140f, 1.1f); TreeAt(t, "shrub", leaf[s][1] == "FlowerPink" || leaf[s][1] == "WinterLeaf" ? (s == 0 ? "SpringLeaf" : "WinterLeaf") : (s == 1 ? "SummerLeaf" : "AutumnRed"), q, 0.7f, b * 70f); }
            }
        }

        // ================================================================== SALUDOS (sureste): bienvenida, encuentro y manos decorativas
        static void Saludos(Transform t)
        {
            int v = 3; var hub = L.HubCenter[v]; var rings = L.ReserveRings[v]; var toApproach = Dir(hub, Pre(v, 7f)); float face = Yaw(toApproach);
            // Escultura central: cinco manos abiertas y estilizadas que se abren como pétalos (decorativas; no son señas).
            var ped = Place(t, "Monumento_Peana", GetMesh("Peana_1_2", n => R622Meshes.Plinth(1.2f, 0.5f, n)), Ms("Stone", "StoneTop"), hub, 0f); Take(hub, 2.0f);
            var handMesh = GetMesh("Mano", n => R622Meshes.HandMesh(n)); string[] hcol = { "Coral", "Turquoise", "Sand", "Cream", "Coral" };
            var sculpt = new GameObject("Escultura_Manos"); sculpt.transform.SetParent(t, false); sculpt.transform.position = G(hub, 0.78f); _count++;
            for (int i = 0; i < 5; i++)
            {
                float ang = face + i * 72f; var h = Place(sculpt.transform, "Mano_decorativa_" + (i + 1), handMesh, Ms(hcol[i]), Vector2.zero, 0f);
                h.transform.localPosition = Vector3.zero; h.transform.localRotation = Quaternion.Euler(0, ang, 0) * Quaternion.Euler(34f, 0, 0); h.transform.localScale = Vector3.one * 1.25f; h.transform.position += Quaternion.Euler(0, ang, 0) * Vector3.forward * 0.4f;
            }
            Capsule(sculpt, 1.45f, 3.0f);
            // Anillo social de bancos (frente al centro) con hueco en las entradas, y parterres cálidos entre ellos.
            var benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageDir + "Bench.prefab"); int benches = 0;
            for (int i = 0; i < 12 && benches < 6; i++)
            {
                float a = face + 35f + i * 50f; var p = Polar(hub, a, 3.3f);
                if (Proto612Layout.Nearest(p, new[] { hub, Pre(v, 7f) }).dist < 1.7f) continue; bool entry = false; foreach (var e in Entries(v)) if (Proto612Layout.Nearest(p, new[] { hub, e + (e - hub).normalized * 3f }).dist < 1.9f) entry = true; if (entry) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab, t); go.name = "Banco_Plaza_Social"; go.transform.SetPositionAndRotation(G(p), Quaternion.Euler(0, Yaw(Dir(p, hub)), 0));
                Retint(go, "Wood"); foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) mc.enabled = false; var mf = go.GetComponent<MeshFilter>(); var bx = go.AddComponent<BoxCollider>(); bx.center = mf.sharedMesh.bounds.center; bx.size = mf.sharedMesh.bounds.size; _count++; benches++;
            }
            string[] pet = { "FlowerPink", "FlowerOrange", "FlowerYellow", "FlowerLilac", "FlowerWhite", "FlowerRed" }; int pk = 0; var rng = new System.Random(6224);
            for (int i = 0; i < 24 && pk < 8; i++) { var p = Polar(hub, face + 30f + i * 15f, 5.0f); if (Ok(p, 0.9f, 5f)) { FlowersAt(t, 10, 0.7f, 650 + pk, false, pet[pk % 6], p, i * 33f); pk++; } }
            // Reservas: dos manos grandes sobre peana (bienvenida) y un rincón acogedor con parterre y bancos.
            for (int i = 0; i < 2; i++)
            {
                var p = rings[i]; float y = Yaw(Dir(p, hub));
                Place(t, "Reserva_Peana", GetMesh("Peana_0_9", n => R622Meshes.Plinth(0.95f, 0.45f, n)), Ms("Stone", "StoneTop"), p, 0f); Take(p, 1.6f);
                var hnd = Place(t, "Mano_decorativa_peana", handMesh, Ms(i == 0 ? "Coral" : "Turquoise"), p, y, 1.7f, 0.75f, new Vector3(8, 0, 0)); Capsule(hnd, 0.55f, 2.2f);
            }
            var cp = rings[2]; Place(t, "Rincon_Parterre", GetMesh("Parterre_Rincon", n => R622Meshes.Flat("circle", 5.0f, 0.1f, n)), Ms("BedSummer"), cp, 0f); Take(cp, 2.6f);
            TreeAt(t, "cluster", "Green3", cp, 0.9f, 20f);
            for (int b = 0; b < 2; b++) { var p = Polar(cp, 90f + b * 180f, 2.0f); var go = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab, t); go.name = "Banco_Rincon"; go.transform.SetPositionAndRotation(G(p), Quaternion.Euler(0, Yaw(Dir(p, cp)), 0)); Retint(go, "Wood"); foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) mc.enabled = false; var mf = go.GetComponent<MeshFilter>(); var bx = go.AddComponent<BoxCollider>(); bx.center = mf.sharedMesh.bounds.center; bx.size = mf.sharedMesh.bounds.size; _count++; }
            for (int b = 0; b < 4; b++) FlowersAt(t, 9, 0.5f, 660 + b, false, pet[(b + 1) % 6], Polar(cp, 20f + b * 90f, 1.6f), b * 50f);
            // Jardín acogedor: arbustos y flores cálidas agrupados alrededor de la plaza.
            int sk = 0; for (int k = 0; k < 200 && sk < 8; k++)
            {
                var p = Polar(hub, face + 25f + (float)rng.NextDouble() * 310f, 6.0f + (float)rng.NextDouble() * 5.5f);
                if (!Ok(p, 1.1f)) continue;
                if (sk % 2 == 0) TreeAt(t, sk % 4 == 0 ? "cluster" : "round", sk % 4 == 0 ? "Green1" : "Green3", p, 0.85f + (float)rng.NextDouble() * 0.3f, (float)rng.NextDouble() * 360f); else TreeAt(t, "shrub", "Green2", p, 0.9f, (float)rng.NextDouble() * 360f);
                sk++;
            }
        }

        static void Retint(GameObject go, string mat)
        {
            var parent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { var arr = r.sharedMaterials; for (int i = 0; i < arr.Length; i++) if (arr[i] == parent) arr[i] = _m[mat]; r.sharedMaterials = arr; }
        }

        // ================================================================== resto del mundo: más árboles, arbustos y flores variados
        static void Mundo(Transform t)
        {
            var rng = new System.Random(6225); string[] kinds = { "round", "cone", "tall", "cluster", "umbrella" };
            string[][] leaf = { new[] { "Green1", "Green3", "SpringLeaf" }, new[] { "Pine", "WinterPine" }, new[] { "Green2", "Green1" }, new[] { "Green3", "SummerLeaf" }, new[] { "Green1", "Green2", "SpringLeaf" } };
            int trees = 0, shrubs = 0, patches = 0;
            for (int k = 0; k < 4000 && (trees < 34 || shrubs < 26 || patches < 22); k++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 41f, (float)(rng.NextDouble() * 2 - 1) * 41f);
                bool nearHub = false; for (int v = 0; v < 4; v++) if (Vector2.Distance(p, L.HubCenter[v]) < 12.5f) nearHub = true; if (nearHub) continue;
                int choice = k % 3;
                if (choice == 0 && trees < 34 && Ok(p, 1.3f, 7f)) { int kk = rng.Next(kinds.Length); string[] lf = leaf[kk]; TreeAt(t, kinds[kk], lf[rng.Next(lf.Length)], p, 0.8f + (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 360f); trees++; }
                else if (choice == 1 && shrubs < 26 && Ok(p, 0.9f, 4.5f)) { TreeAt(t, "shrub", new[] { "Green1", "Green2", "Green3", "SpringLeaf" }[rng.Next(4)], p, 0.8f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 360f); shrubs++; }
                else if (choice == 2 && patches < 22 && Ok(p, 0.9f, 4.2f)) { string[] pet = { "FlowerPink", "FlowerYellow", "FlowerLilac", "FlowerWhite", "FlowerOrange", "FlowerBlue", "FlowerRed" }; FlowersAt(t, 8 + rng.Next(8), 0.8f, 700 + patches, false, pet[rng.Next(pet.Length)], p, (float)rng.NextDouble() * 360f); patches++; }
            }
        }
    }
}
