using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = Lumina.Prototype612.R621Layout;
using H = Lumina.Prototype612.Proto612LayoutV2;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.3: paisajismo, vegetación y transiciones. Abre la escena de 6.2.2 SIN reconstruirla (no toca ningún recurso R621_/R622_), elimina el
    /// grupo «--- PAISAJE R623 ---» si ya existía y lo vuelve a crear de forma determinista (semillas fijas). Mallas Meshes/R623_*, materiales R623_*.
    /// Las manos de la Aldea de los Saludos siguen siendo arte decorativo: este paisajismo no añade ni representa ninguna seña de la LSC.
    /// </summary>
    public static class R623Builder
    {
        const string Root = Proto612Builder.Root;
        public const string ScenePath = R621Builder.ScenePath;
        public const string RootName = "--- PAISAJE R623 ---";

        static Dictionary<string, Material> _m; static readonly Dictionary<string, Mesh> _mesh = new Dictionary<string, Mesh>();
        static readonly List<(Vector2 p, float r)> Occ = new List<(Vector2, float)>();
        static List<Vector2>[] _entries; static int _count;

        // ---- estilo paisajístico de cada aldea (índices de L.HubCenter: 0 Abecedario, 1 Números, 2 Meses, 3 Saludos)
        class Style
        {
            public (string kind, string leaf, string trunk)[] trees; public string[] bushes; public string[] bushLeaf; public (string flower, string layout, string petal)[] flowers;
        }
        static readonly Style[] Styles =
        {
            new Style { // Abecedario: formas suaves, flores variadas en curvas y arcos
                trees = new[] { ("blossom", "BlossomPink", "Trunk"), ("round", "MintLeaf", "Trunk"), ("umbrella", "Green3", "Trunk"), ("sapling", "SpringLeaf", "Trunk"), ("blossom", "BlossomLilac", "Trunk") },
                bushes = new[] { "bush_low", "bush_berry", "bush_low" }, bushLeaf = new[] { "MintLeaf", "Green3", "SpringLeaf" },
                flowers = new[] { ("daisy", "scatter", "FlowerPink"), ("tulip", "curve", "FlowerYellow"), ("poppy", "arc", "FlowerCoral"), ("daisy", "curve", "FlowerLilac"), ("tulip", "scatter", "FlowerTurq"), ("poppy", "scatter", "FlowerOrange") } },
            new Style { // Números: verticales y regulares, patrones geométricos
                trees = new[] { ("poplar", "OakGreen", "Trunk"), ("pine_tall", "Pine", "Trunk"), ("round", "Green2", "Trunk"), ("birch", "Green3", "BirchTrunk") },
                bushes = new[] { "bush_tall", "bush_tall", "bush_low" }, bushLeaf = new[] { "Green2", "Hedge", "OakGreen" },
                flowers = new[] { ("daisy", "grid", "FlowerYellow"), ("tulip", "diamond", "FlowerWhite"), ("daisy", "line", "FlowerBlue"), ("tulip", "grid", "FlowerCoral"), ("spike", "line", "Lavender") } },
            new Style { // Meses: una especie por estación, jardines circulares
                trees = new[] { ("blossom", "BlossomPink", "Trunk"), ("oak", "SummerLeaf", "Trunk"), ("round", "AutumnOrange", "Trunk"), ("cone", "WinterPine", "Trunk"), ("round", "AutumnRed", "DarkTrunk"), ("pine_tall", "WinterPine", "Trunk") },
                bushes = new[] { "bush_berry", "bush_low", "bush_berry" }, bushLeaf = new[] { "SpringLeaf", "SummerLeaf", "AutumnOrange", "WinterLeaf" },
                flowers = new[] { ("daisy", "ring", "FlowerPink"), ("tulip", "ring", "FlowerYellow"), ("poppy", "ring", "FlowerOrange"), ("daisy", "ring", "FlowerWhite"), ("spike", "scatter", "FlowerBlue") } },
            new Style { // Saludos: acogedor, sombras amplias y flores cálidas
                trees = new[] { ("oak", "OliveLeaf", "Trunk"), ("oak", "Green3", "Trunk"), ("round", "Green1", "Trunk"), ("cluster", "OliveLeaf", "Trunk"), ("umbrella", "Green2", "Trunk") },
                bushes = new[] { "bush_low", "bush_berry", "bush_tall" }, bushLeaf = new[] { "OliveLeaf", "Green3", "Green1" },
                flowers = new[] { ("poppy", "scatter", "FlowerCoral"), ("daisy", "scatter", "FlowerOrange"), ("tulip", "arc", "FlowerPink"), ("daisy", "scatter", "FlowerYellow"), ("poppy", "arc", "FlowerRed") } },
        };

        // ================================================================== construcción
        [MenuItem("Lumina/Prototipo 6.2.3/Construir paisajismo")]
        public static void Build()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            _mesh.Clear(); Occ.Clear(); _entries = null; _count = 0; _m = Mats();
            var old = GameObject.Find(RootName); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            SeedOccupancy();
            Principales(Child(root, "Arboles_Principales"));
            Abecedario(Child(root, "Aldea_Abecedario")); Numeros(Child(root, "Aldea_Numeros")); Meses(Child(root, "Aldea_Meses")); Saludos(Child(root, "Aldea_Saludos"));
            Grupos(Child(root, "Transiciones_Grupos"));
            Caminos(Child(root, "Transiciones_Caminos"));
            Rio(Child(root, "Rio_Orillas"));
            Relleno(Child(root, "Transiciones_Relleno"));
            var keep = new HashSet<string>(); foreach (var k in _mesh.Keys) keep.Add("R623_" + k + ".asset");
            foreach (var f in Directory.GetFiles(Root + "Meshes", "R623_*.asset")) if (!keep.Contains(Path.GetFileName(f))) AssetDatabase.DeleteAsset(f.Replace((char)92, '/'));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[R623] Paisajismo creado: " + _count + " objetos, " + _mesh.Count + " mallas R623 únicas.");
        }

        public static void BuildTwiceAndCompare()
        {
            Build(); var a = Snapshot(out string ha); int ma = Directory.GetFiles(Root + "Meshes", "R623_*.asset").Length;
            Build(); var b = Snapshot(out string hb); int mb = Directory.GetFiles(Root + "Meshes", "R623_*.asset").Length;
            var sb = new System.Text.StringBuilder("[R623-IDEMPOTENCIA]\n"); bool same = a.Count == b.Count && ma == mb && ha == hb; int total = 0;
            foreach (var kv in a) { total += kv.Value; if (!b.TryGetValue(kv.Key, out var n) || n != kv.Value) { same = false; sb.AppendLine($"  difiere '{kv.Key}': {kv.Value} → {(b.TryGetValue(kv.Key, out var m) ? m : 0)}"); } }
            sb.AppendLine($"Objetos por nombre en toda la escena: {a.Count} nombres distintos, {total} objetos; mallas R623: {ma} y {mb}");
            sb.AppendLine($"Huella de posiciones/escala/rotación del paisajismo: {ha} y {hb} ({(ha == hb ? "iguales" : "DISTINTAS")})");
            sb.AppendLine("Segunda construcción " + (same ? "IDÉNTICA" : "DIFERENTE"));
            File.WriteAllText(Root + "Evidence/r623_idempotencia.txt", sb.ToString()); Debug.Log(sb.ToString());
        }

        static Dictionary<string, int> Snapshot(out string hash)
        {
            var d = new Dictionary<string, int>(); var txt = new System.Text.StringBuilder();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) d[go.name] = d.TryGetValue(go.name, out var n) ? n + 1 : 1;
            var r = GameObject.Find(RootName);
            if (r != null) foreach (var t in r.GetComponentsInChildren<Transform>(true)) txt.Append(t.name).Append(t.position.ToString("F3")).Append(t.eulerAngles.ToString("F2")).Append(t.localScale.ToString("F3")).Append(';');
            using (var sha = System.Security.Cryptography.SHA256.Create()) hash = System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(txt.ToString()))).Replace("-", "").Substring(0, 16);
            return d;
        }

        // ================================================================== materiales (R622 solo se leen; los nuevos son R623_)
        static readonly (string key, string hex)[] Palette =
        {
            ("BlossomPink", "#F4B6C8"), ("BlossomLilac", "#CDB8F0"), ("OakGreen", "#4F9A48"), ("MintLeaf", "#8FD08A"), ("OliveLeaf", "#86A84A"),
            ("BirchTrunk", "#E8DFCF"), ("DarkTrunk", "#5E3F26"), ("Reed", "#A89A55"), ("ReedTip", "#7A4B2A"), ("Grass", "#79BC52"),
            ("FlowerCoral", "#FF7A68"), ("FlowerTurq", "#5ED4C8"), ("Lavender", "#9B7FE0"), ("BedSoil", "#9B7B55"), ("BedFrame", "#3F8F4A"), ("Curb", "#CFC6B0"),
        };

        static Dictionary<string, Material> Mats()
        {
            var d = new Dictionary<string, Material>();
            foreach (var f in Directory.GetFiles(Root + "Materials", "R622_*.mat")) { var m = AssetDatabase.LoadAssetAtPath<Material>(f.Replace((char)92, '/')); if (m != null) d[Path.GetFileNameWithoutExtension(f).Substring(5)] = m; }
            foreach (var (key, hex) in Palette) { ColorUtility.TryParseHtmlString(hex, out var c); d[key] = NewMat(key, c); }
            return d;
        }

        static Material NewMat(string key, Color c)
        {
            string path = Root + "Materials/R623_" + key + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { if (m.GetColor("_BaseColor") != c) { m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); } return m; }
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "R623_" + key };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            AssetDatabase.CreateAsset(m, path); return m;
        }

        static Material[] Ms(params string[] keys) { var r = new Material[keys.Length]; for (int i = 0; i < keys.Length; i++) r[i] = _m[keys[i]]; return r; }

        // ================================================================== utilidades
        static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static Vector3 G(Vector2 p, float dy = 0f) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);
        static float Yaw(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        static Vector2 Dir(Vector2 from, Vector2 to) => (to - from).normalized;
        static Vector2 Polar(Vector2 c, float deg, float r) { float a = deg * Mathf.Deg2Rad; return c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r; }
        static Vector2 Pre(int v, float d) => H.DoorPoint(v, 0f) + H.Houses[v].Front * d;
        static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static Mesh GetMesh(string key, System.Func<string, Mesh> make)
        {
            if (_mesh.TryGetValue(key, out var m)) return m;
            string path = Root + "Meshes/R623_" + key + ".asset"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            var fresh = make("R623_" + key); var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (ex != null) { ex.Clear(); EditorUtility.CopySerialized(fresh, ex); Object.DestroyImmediate(fresh); m = ex; }      // conserva el GUID del recurso entre construcciones
            else { AssetDatabase.CreateAsset(fresh, path); m = fresh; }
            _mesh[key] = m; return m;
        }

        static GameObject Place(Transform parent, string name, Mesh mesh, Material[] mats, Vector2 p, float yaw, float scale = 1f)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(G(p), Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); _count++; return go;
        }

        static void Capsule(GameObject go, float radius, float height) { var c = go.AddComponent<CapsuleCollider>(); c.radius = radius; c.height = height; c.center = new Vector3(0, height * 0.5f, 0); }

        // ---- ocupación del suelo y reglas de libertad (mismos criterios que 6.2.2, más corredor spawn→Sena y franjas de puente)
        static void SeedOccupancy()
        {
            foreach (var it in L.Trees()) Occ.Add((new Vector2(it.pos.x, it.pos.z), 1.0f));
            foreach (var it in L.Rocks()) Occ.Add((new Vector2(it.pos.x, it.pos.z), 0.9f));
            foreach (var it in L.Props()) Occ.Add((new Vector2(it.pos.x, it.pos.z), it.prefab == "Bench" ? 1.3f : 0.6f));
            foreach (var s in L.Signs()) Occ.Add((s.pos, 0.9f));
            Occ.Add((new Vector2(L.Spawn.x, L.Spawn.z), 3.2f)); Occ.Add((new Vector2(L.SenaPos.x, L.SenaPos.z), 3.0f)); Occ.Add((L.PlazaCenter, L.PlazaRadius + 0.8f));
            foreach (var h in H.Houses) Occ.Add((h.center, 4.6f));
            var deco = GameObject.Find("--- DECORACION R622 ---");       // todo lo ya decorado en 6.2.2: círculo que envuelve cada pieza
            if (deco != null) foreach (var r in deco.GetComponentsInChildren<MeshRenderer>(true)) { var b = r.bounds; Occ.Add((new Vector2(b.center.x, b.center.z), Mathf.Max(b.extents.x, b.extents.z) * 0.95f + 0.1f)); }
        }

        static bool Free(Vector2 p, float r) { foreach (var (q, rr) in Occ) if (Vector2.Distance(p, q) < r + rr) return false; return true; }
        static void Take(Vector2 p, float r) => Occ.Add((p, r));

        static List<Vector2> Entries(int v)
        {
            if (_entries == null) { _entries = new List<Vector2>[4]; for (int k = 0; k < 4; k++) _entries[k] = ComputeEntries(k); }
            return _entries[v];
        }

        static List<Vector2> ComputeEntries(int v)
        {
            var res = new List<Vector2>();
            foreach (var path in L.Paths)
                if ((path[0] - L.HubCenter[v]).magnitude < 0.6f || (path[path.Count - 1] - L.HubCenter[v]).magnitude < 0.6f)
                    foreach (var (q, _) in Proto612Layout.Resample(path, 0.5f)) { if (Mathf.Abs((q - L.HubCenter[v]).magnitude - 6f) < 0.3f) { res.Add(q); break; } }
            return res;
        }

        /// <summary>Punto libre: ≥ 2 m útiles a cada lado de los caminos, 3 m de las puertas y de los ejes de puente, fuera de las plazas, del corredor spawn→Sena, del agua y de lo ya colocado.</summary>
        static bool Ok(Vector2 p, float r, float riverMin = 6f, float axisExtra = 0f)
        {
            if (Mathf.Abs(p.x) > 42.5f || Mathf.Abs(p.y) > 42.5f) return false;
            if (L.LaneDist(p) < 1.25f + r + 0.75f) return false;
            if (L.DistToDoorCorridors(p) < r + 3f) return false;
            if (L.AxisDist(p) < r + 3f + axisExtra) return false;
            if (L.DistToRiver(p) < riverMin + r) return false;
            for (int v = 0; v < 4; v++)
            {
                if (Vector2.Distance(p, L.HubCenter[v]) < 5.4f + r) return false;
                foreach (var e in Entries(v)) if (Proto612Layout.Nearest(p, new[] { L.HubCenter[v], e + (e - L.HubCenter[v]).normalized * 3f }).dist < 1.8f + r) return false;
            }
            if (Proto612Layout.Nearest(p, new[] { new Vector2(L.Spawn.x, L.Spawn.z), new Vector2(L.SenaPos.x, L.SenaPos.z) }).dist < 2.2f + r) return false;
            return Free(p, r);
        }

        /// <summary>Aldea a la que pertenece un punto: sorteo ponderado por cercanía (así, entre dos aldeas se mezclan sus especies = transición).</summary>
        static int PickVillage(Vector2 p, System.Random rng)
        {
            var d = new float[4]; float min = float.MaxValue; for (int v = 0; v < 4; v++) { d[v] = Vector2.Distance(p, L.HubCenter[v]); if (d[v] < min) min = d[v]; }
            var w = new float[4]; float sum = 0; for (int v = 0; v < 4; v++) { w[v] = Mathf.Exp(-(d[v] - min) / 9f); sum += w[v]; }
            float x = (float)rng.NextDouble() * sum; for (int v = 0; v < 4; v++) { x -= w[v]; if (x <= 0) return v; }
            return 0;
        }

        // ================================================================== vegetación: árboles
        static readonly string[] R622Kinds = { "round", "cone", "tall", "cluster", "umbrella" };
        static float CrownRadius(string k)
        {
            switch (k) { case "oak": return 1.5f; case "poplar": return 0.8f; case "pine_tall": return 1.4f; case "sapling": return 0.7f; case "blossom": return 1.3f; case "willow": return 1.7f; case "birch": return 1.0f; case "tall": return 0.9f; case "umbrella": return 1.7f; default: return 1.3f; }
        }
        static (float r, float h) TrunkCollider(string k)
        {
            switch (k) { case "oak": return (0.38f, 3.2f); case "poplar": return (0.28f, 5f); case "pine_tall": return (0.35f, 4.5f); case "sapling": return (0.18f, 1.6f); case "blossom": return (0.3f, 2.6f); case "willow": return (0.35f, 3.6f); case "birch": return (0.22f, 3.4f); case "cone": return (0.3f, 3f); case "tall": return (0.3f, 3.5f); default: return (0.32f, 2.4f); }
        }

        /// <summary>Alcance real de la copa en planta (más ancho que el radio con el que se separan los árboles entre sí).</summary>
        static float Reach(string k)
        {
            switch (k) { case "oak": return 2.0f; case "poplar": return 0.9f; case "pine_tall": return 1.5f; case "sapling": return 0.8f; case "blossom": return 1.5f; case "willow": return 1.9f; case "birch": return 1.1f; case "tall": return 0.9f; case "umbrella": return 1.8f; case "cluster": return 1.6f; default: return 1.35f; }
        }

        static GameObject TreeAt(Transform parent, string kind, string leaf, string trunk, Vector2 p, float scale, float yaw)
        {
            float reach = Reach(kind) * scale;      // la copa entera debe quedar fuera de caminos, puertas, ejes de puente y plazas
            if (L.LaneDist(p) < 1.25f + reach + 0.15f || L.DistToDoorCorridors(p) < reach + 2.5f || L.AxisDist(p) < reach + 3f || Vector2.Distance(p, L.HubCenter.OrderBy(h => Vector2.Distance(h, p)).First()) < L.HubRadius + reach + 0.3f) return null;
            Mesh mesh = System.Array.IndexOf(R622Kinds, kind) >= 0 ? AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/R622_Tree_" + kind + ".asset") : GetMesh("Tree_" + kind, n => R623Meshes.Tree(kind, n));
            var go = Place(parent, "Arbol_" + kind, mesh, Ms(trunk, leaf), p, yaw, scale); var (cr, ch) = TrunkCollider(kind); Capsule(go, cr, ch);
            Take(p, CrownRadius(kind) * scale * 0.8f); return go;
        }

        static GameObject ShrubAt(Transform parent, string kind, string leaf, Vector2 p, float scale, float yaw)
        {
            var go = Place(parent, "Arbusto_" + kind, GetMesh("Arbusto_" + kind, n => R623Meshes.Shrub(kind, n)), Ms(leaf, "FlowerRed"), p, yaw, scale);
            if (kind != "bush_low") Capsule(go, 0.5f, 0.9f);
            Take(p, 0.7f * scale); return go;
        }

        static GameObject TuftAt(Transform parent, Vector2 p, float scale, float yaw) { var go = Place(parent, "Hierba_mata", GetMesh("Arbusto_tuft", n => R623Meshes.Shrub("tuft", n)), Ms("Grass", "Grass"), p, yaw, scale); Take(p, 0.25f * scale); return go; }

        static GameObject ReedsAt(Transform parent, Vector2 p, float scale, float yaw) { var go = Place(parent, "Juncos", GetMesh("Arbusto_reeds", n => R623Meshes.Shrub("reeds", n)), Ms("Reed", "ReedTip"), p, yaw, scale); Take(p, 0.35f * scale); return go; }

        // tamaño estándar por disposición: (número de flores, tamaño, radio ocupado)
        static (int count, float size, float foot) LayoutSpec(string layout)
        {
            switch (layout) { case "ring": return (12, 1.0f, 1.3f); case "arc": return (12, 2.0f, 1.5f); case "curve": return (14, 3.2f, 1.7f); case "line": return (10, 3.0f, 1.6f); case "grid": return (9, 0.45f, 0.9f); case "diamond": return (9, 0.5f, 1.0f); default: return (9, 0.7f, 0.8f); }
        }

        static GameObject FlowersAt(Transform parent, string flower, string layout, string petal, Vector2 p, float yaw, System.Random rng)
        {
            var (count, size, foot) = LayoutSpec(layout); int seed = 6230 + rng.Next(3);
            var go = Place(parent, "Flores_" + flower, GetMesh($"Flores_{flower}_{layout}_{seed}", n => R623Meshes.Flowers(flower, layout, count, size, seed, n)), Ms("Stem", petal, "Center"), p, yaw);
            Take(p, foot * 0.8f); return go;
        }

        static float FlowerFoot(string layout) => LayoutSpec(layout).foot * 0.8f;

        // ================================================================== árboles principales (puntos de referencia visual)
        static void Principales(Transform t)
        {
            var rng = new System.Random(6231); string[] kinds = { "oak", "poplar", "pine_tall" }; string[] leaves = { "OakGreen", "OakGreen", "Pine" };
            // 3 por aldea, en el borde exterior (9–14 m del centro de la plaza), elegidos según el carácter de la aldea
            for (int v = 0; v < 4; v++)
            {
                int placed = 0;
                for (int k = 0; k < 500 && placed < 3; k++)
                {
                    var p = Polar(L.HubCenter[v], R(rng, 0, 360), R(rng, 9f, 14f)); if (!Ok(p, 1.9f, 8f)) continue;
                    var st = Styles[v]; var tr = v == 2 && placed == 0 ? ("oak", "SummerLeaf", "Trunk") : v == 0 ? st.trees[placed % 2 == 0 ? 0 : 4] : v == 1 ? st.trees[placed == 2 ? 1 : 0] : st.trees[placed == 2 ? 3 : 0];
                    if (TreeAt(t, tr.Item1, tr.Item2, tr.Item3, p, tr.Item1 == "poplar" ? R(rng, 1.0f, 1.12f) : tr.Item1 == "pine_tall" ? R(rng, 1.15f, 1.3f) : R(rng, 1.25f, 1.45f), R(rng, 0, 360)) != null) placed++;
                }
            }
            // 4 de referencia en las zonas entre aldeas (puntos medios de los pares contiguos)
            Vector2[] mids = { (L.HubCenter[0] + L.HubCenter[1]) * 0.5f, (L.HubCenter[2] + L.HubCenter[3]) * 0.5f, (L.HubCenter[1] + L.HubCenter[3]) * 0.5f, (L.HubCenter[0] + L.HubCenter[2]) * 0.5f };
            for (int i = 0; i < mids.Length; i++)
                for (int k = 0; k < 400; k++)
                {
                    var p = mids[i] + new Vector2(R(rng, -7f, 7f), R(rng, -7f, 7f)); if (!Ok(p, 1.9f, 8f)) continue;
                    int kk = (i + 1) % 3; if (TreeAt(t, kinds[kk], leaves[kk], "Trunk", p, kk == 1 ? R(rng, 1.0f, 1.1f) : R(rng, 1.2f, 1.4f), R(rng, 0, 360)) != null) break;
                }
        }

        // ================================================================== aldeas
        static void Garden(Transform t, int v, System.Random rng, string[] layouts, int wanted, float rMin, float rMax, float margin = 0f)
        {
            var st = Styles[v]; var hub = L.HubCenter[v]; int placed = 0;
            for (int k = 0; k < 600 && placed < wanted; k++)
            {
                var fl = st.flowers.Where(f => layouts.Contains(f.layout)).ToArray(); if (fl.Length == 0) return;
                var f0 = fl[(placed + k) % fl.Length]; var p = Polar(hub, R(rng, 0, 360), R(rng, rMin, rMax)); float foot = FlowerFoot(f0.layout);
                if (!Ok(p, foot + margin, 5f)) continue;
                FlowersAt(t, f0.flower, f0.layout, f0.petal, p, R(rng, 0, 360), rng); placed++;
            }
        }

        static void SoftTrees(Transform t, int v, System.Random rng, int groups, float rMin, float rMax)
        {
            var st = Styles[v]; var hub = L.HubCenter[v];
            for (int g = 0; g < groups; g++)
                for (int k = 0; k < 300; k++)
                {
                    var c = Polar(hub, R(rng, 0, 360), R(rng, rMin, rMax)); if (!Ok(c, 1.3f, 6.5f)) continue;
                    int n = 2 + rng.Next(2);
                    for (int i = 0; i < n; i++) for (int tries = 0; tries < 20; tries++)
                    {
                        var p = i == 0 ? c : c + new Vector2(R(rng, -2.6f, 2.6f), R(rng, -2.6f, 2.6f)); if (!Ok(p, 1.0f, 6.5f)) continue;
                        var tr = st.trees[rng.Next(st.trees.Length)]; TreeAt(t, tr.kind, tr.leaf, tr.trunk, p, i == 0 ? R(rng, 0.95f, 1.2f) : R(rng, 0.7f, 1.0f), R(rng, 0, 360)); break;
                    }
                    break;
                }
        }

        static void Shrubs(Transform t, int v, System.Random rng, int count, float rMin, float rMax)
        {
            var st = Styles[v]; var hub = L.HubCenter[v]; int placed = 0;
            for (int k = 0; k < 400 && placed < count; k++)
            {
                var p = Polar(hub, R(rng, 0, 360), R(rng, rMin, rMax)); if (!Ok(p, 0.8f, 4.5f)) continue;
                ShrubAt(t, st.bushes[rng.Next(st.bushes.Length)], st.bushLeaf[rng.Next(st.bushLeaf.Length)], p, R(rng, 0.8f, 1.25f), R(rng, 0, 360)); placed++;
            }
        }

        // ---- Abecedario: curvas, arcos y flores de muchos colores; árboles suaves y agrupaciones irregulares
        static void Abecedario(Transform t)
        {
            var rng = new System.Random(6232);
            Garden(t, 0, rng, new[] { "curve", "arc" }, 6, 6.2f, 12f);
            Garden(t, 0, rng, new[] { "scatter" }, 6, 6.0f, 13f);
            SoftTrees(t, 0, rng, 3, 8f, 13f); Shrubs(t, 0, rng, 7, 6.5f, 13f);
        }

        // ---- Números: parterres cuadrados con marco, cuadrículas y rombos, hileras ordenadas con escala creciente
        static void Numeros(Transform t)
        {
            int v = 1; var hub = L.HubCenter[v]; var rng = new System.Random(6233); var toApproach = Dir(hub, Pre(v, 7f)); var along = new Vector2(toApproach.y, -toApproach.x);
            string[] flowers = { "tulip", "daisy", "tulip" }; string[] petals = { "FlowerYellow", "FlowerCoral", "FlowerBlue" }; int placed = 0;
            for (int k = 0; k < 600 && placed < 3; k++)
            {
                var p = Polar(hub, R(rng, 0, 360), R(rng, 6.8f, 11f)); if (!Ok(p, 1.7f, 5f)) continue;
                int idx = placed; var go = Place(t, "Cama_Cuadrada", GetMesh($"Cama_Cuadrada_{flowers[idx]}", n => R623Meshes.SquareBed(2.6f, flowers[idx], n)), Ms("BedFrame", "BedSoil", "Stem", petals[idx], "Center"), p, Mathf.Round(Yaw(toApproach) / 90f) * 90f);
                Take(p, 1.9f); placed++;
            }
            Garden(t, 1, rng, new[] { "grid", "diamond", "line" }, 6, 6.2f, 12f);
            // Hileras de árboles: casi regulares (paso 2,8 m, desvío ±0,15 m), con la altura subiendo de 0,85 a 1,1
            for (int row = 0; row < 2; row++)
            {
                var start = hub + along * (row == 0 ? 11.5f : -11.5f) + toApproach * -6f; string[] kinds = { "poplar", "round" };
                for (int i = 0; i < 6; i++)
                {
                    var p = start + toApproach * (i * 2.8f) + new Vector2(R(rng, -0.15f, 0.15f), R(rng, -0.15f, 0.15f)); if (!Ok(p, 1.0f, 6.5f)) continue;
                    var tr = Styles[1].trees[(i + row) % 2 == 0 ? 0 : 2]; TreeAt(t, tr.kind, tr.leaf, tr.trunk, p, 0.85f + i * 0.05f, row * 90f + R(rng, -8, 8));
                }
            }
            Shrubs(t, 1, rng, 6, 6.5f, 12.5f);
        }

        // ---- Meses: cuatro árboles de estación, parterres circulares con borde, anillos de flores
        static void Meses(Transform t)
        {
            int v = 2; var hub = L.HubCenter[v]; var rng = new System.Random(6234); var st = Styles[v];
            string[] bedFlower = { "daisy", "tulip", "poppy", "daisy" }; string[] bedPetal = { "FlowerPink", "FlowerYellow", "FlowerOrange", "FlowerWhite" }; int placed = 0;
            for (int k = 0; k < 800 && placed < 4; k++)
            {
                var p = Polar(hub, R(rng, 0, 360), R(rng, 6.8f, 11.5f)); if (!Ok(p, 1.9f, 5f)) continue;
                int idx = placed; Place(t, "Cama_Redonda", GetMesh($"Cama_Redonda_{bedFlower[idx]}", n => R623Meshes.RoundBed(1.7f, bedFlower[idx], n)), Ms("Curb", "BedSoil", "Stem", bedPetal[idx], "Center"), p, R(rng, 0, 360)); Take(p, 2.0f); placed++;
            }
            // un árbol por estación: primavera (flor), verano (copa densa), otoño (naranja), invierno (pino azulado)
            (string kind, string leaf)[] seasons = { ("blossom", "BlossomPink"), ("oak", "SummerLeaf"), ("round", "AutumnOrange"), ("pine_tall", "WinterPine") };
            for (int s = 0; s < 4; s++)
                for (int k = 0; k < 300; k++)
                {
                    var p = Polar(hub, 45f + 90f * s + R(rng, -25, 25), R(rng, 9.5f, 13f)); if (!Ok(p, 1.6f, 6.5f)) continue;
                    TreeAt(t, seasons[s].kind, seasons[s].leaf, "Trunk", p, R(rng, 1.1f, 1.3f), R(rng, 0, 360));
                    for (int b = 0; b < 2; b++) { var q = Polar(p, 120f * b + 40f, 2.3f); if (Ok(q, 0.8f, 5f)) ShrubAt(t, "bush_berry", st.bushLeaf[s], q, R(rng, 0.8f, 1.0f), R(rng, 0, 360)); }
                    break;
                }
            Garden(t, 2, rng, new[] { "ring" }, 4, 6.2f, 12.5f); Garden(t, 2, rng, new[] { "scatter" }, 3, 6.0f, 13f);
            SoftTrees(t, 2, rng, 2, 8f, 13f);
        }

        // ---- Saludos: copas amplias para dar sombra, flores cálidas junto a la plaza social y césped abierto
        static void Saludos(Transform t)
        {
            int v = 3; var hub = L.HubCenter[v]; var rng = new System.Random(6235); var toApproach = Dir(hub, Pre(v, 7f)); var along = new Vector2(toApproach.y, -toApproach.x);
            for (int s = 0; s < 2; s++)
                for (int k = 0; k < 400; k++)
                {
                    var p = hub + along * (s == 0 ? 1f : -1f) * R(rng, 9f, 11.5f) + toApproach * R(rng, -4f, 4f); if (!Ok(p, 1.9f, 6.5f)) continue;
                    TreeAt(t, "oak", s == 0 ? "OliveLeaf" : "Green3", "Trunk", p, R(rng, 1.25f, 1.45f), R(rng, 0, 360)); break;
                }
            var st = Styles[v]; int placed = 0;
            for (int k = 0; k < 400 && placed < 8; k++)      // jardines pequeños pegados a la plaza (por fuera de los bancos)
            {
                var f0 = st.flowers[placed % st.flowers.Length]; var p = Polar(hub, 20f + placed * 45f + R(rng, -10, 10), R(rng, 5.5f, 6.8f)); float foot = FlowerFoot(f0.layout) * 0.7f;
                if (!Ok(p, foot, 4.5f)) continue; var go = FlowersAt(t, f0.flower, f0.layout == "arc" ? "scatter" : f0.layout, f0.petal, p, R(rng, 0, 360), rng); placed++;
            }
            Shrubs(t, 3, rng, 6, 6.5f, 13f); SoftTrees(t, 3, rng, 3, 9f, 14f);
            Garden(t, 3, rng, new[] { "scatter", "arc" }, 4, 7f, 13f);
        }

        // ================================================================== transiciones entre aldeas
        static void Grupos(Transform t)
        {
            var rng = new System.Random(6236); int groups = 0;
            for (int k = 0; k < 3000 && groups < 11; k++)
            {
                var c = new Vector2(R(rng, -41f, 41f), R(rng, -41f, 41f)); if (!Ok(c, 1.4f, 6.5f)) continue;
                int v = PickVillage(c, rng); var st = Styles[v]; int n = rng.NextDouble() < 0.35 ? 2 : (rng.NextDouble() < 0.55 ? 3 : (rng.NextDouble() < 0.6 ? 4 : 5));
                for (int i = 0; i < n; i++) for (int tries = 0; tries < 25; tries++)
                {
                    var p = i == 0 ? c : c + new Vector2(R(rng, -3.2f, 3.2f), R(rng, -3.2f, 3.2f)); if (!Ok(p, 1.0f, 6.5f)) continue;
                    var tr = st.trees[rng.Next(st.trees.Length)]; TreeAt(t, tr.kind, tr.leaf, tr.trunk, p, i == 0 ? R(rng, 0.95f, 1.2f) : R(rng, 0.65f, 1.0f), R(rng, 0, 360)); break;
                }
                for (int i = 0; i < 3; i++) { var q = c + new Vector2(R(rng, -4f, 4f), R(rng, -4f, 4f)); if (Ok(q, 0.7f, 4.5f)) ShrubAt(t, st.bushes[rng.Next(st.bushes.Length)], st.bushLeaf[rng.Next(st.bushLeaf.Length)], q, R(rng, 0.8f, 1.2f), R(rng, 0, 360)); }
                for (int i = 0; i < 3; i++) { var q = c + new Vector2(R(rng, -4f, 4f), R(rng, -4f, 4f)); if (Ok(q, 0.4f, 4f)) TuftAt(t, q, R(rng, 0.9f, 1.4f), R(rng, 0, 360)); }
                var f0 = st.flowers[rng.Next(st.flowers.Length)]; var fp = c + new Vector2(R(rng, -3.5f, 3.5f), R(rng, -3.5f, 3.5f)); string lay = f0.layout == "ring" || f0.layout == "grid" ? "scatter" : f0.layout;
                if (Ok(fp, FlowerFoot(lay), 4.5f)) FlowersAt(t, f0.flower, lay, f0.petal, fp, R(rng, 0, 360), rng);
                groups++;
            }
        }

        /// <summary>Líneas naturales que acompañan a los caminos: un árbol o un arbusto con flores cada 5–9 m, a 3–4,5 m del eje y alternando lados.</summary>
        static void Caminos(Transform t)
        {
            var rng = new System.Random(6237); int trees = 0;
            for (int pi = 0; pi < L.Paths.Count; pi++)
            {
                if (L.PathName[pi].Contains("Puente")) continue;
                var pts = Proto612Layout.Resample(L.Paths[pi], 1f).Select(q => q.Item1).ToList(); float gap = R(rng, 3f, 6f);
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    gap -= 1f; if (gap > 0) continue; gap = R(rng, 5f, 9f);
                    var tan = (pts[i + 1] - pts[i - 1]).normalized; var nrm = new Vector2(-tan.y, tan.x) * (rng.NextDouble() < 0.5 ? 1f : -1f); var p = pts[i] + nrm * R(rng, 3.0f, 4.5f);
                    int v = PickVillage(p, rng); var st = Styles[v];
                    if (trees < 14 && rng.NextDouble() < 0.5 && Ok(p, 1.0f, 6.5f)) { var tr = st.trees[rng.Next(st.trees.Length)]; string kind = tr.kind == "poplar" || tr.kind == "oak" || tr.kind == "pine_tall" ? "sapling" : tr.kind; if (kind == "sapling") tr.leaf = st.bushLeaf[0]; TreeAt(t, kind, tr.leaf, tr.trunk, p, R(rng, 0.7f, 1.0f), R(rng, 0, 360)); trees++; }
                    else if (Ok(p, 0.7f, 4.5f))
                    {
                        ShrubAt(t, st.bushes[rng.Next(st.bushes.Length)], st.bushLeaf[rng.Next(st.bushLeaf.Length)], p, R(rng, 0.8f, 1.1f), R(rng, 0, 360));
                        var q = p + nrm * R(rng, 0.9f, 1.4f) + tan * R(rng, -0.8f, 0.8f); var f0 = st.flowers[rng.Next(st.flowers.Length)]; if (Ok(q, 0.7f, 4f)) FlowersAt(t, f0.flower, "scatter", f0.petal, q, R(rng, 0, 360), rng);
                    }
                }
            }
        }

        /// <summary>Orillas del río: juncos junto al agua, flores y arbustos bajos, y algunos sauces/árboles alejados (≥ 6 m). Los puentes conservan una franja de 4 m libre a cada lado de su eje.</summary>
        static void Rio(Transform t)
        {
            var rng = new System.Random(6238); var pts = Proto612Layout.Resample(L.River, 2f).Select(q => q.Item1).ToList(); int trees = 0;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var tan = (pts[i + 1] - pts[i - 1]).normalized; var nrm = new Vector2(-tan.y, tan.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    double roll = rng.NextDouble(); var edge = pts[i] + nrm * side * R(rng, 2.5f, 3.3f); int v = PickVillage(edge, rng); var st = Styles[v];
                    if (roll < 0.30) { if (Ok(edge, 0.4f, 2.0f, 1f)) ReedsAt(t, edge, R(rng, 0.9f, 1.3f), R(rng, 0, 360)); }
                    else if (roll < 0.50) { var p = pts[i] + nrm * side * R(rng, 3.2f, 4.6f); var f0 = st.flowers[rng.Next(st.flowers.Length)]; if (Ok(p, 0.8f, 2.5f, 1f)) FlowersAt(t, f0.flower, "scatter", f0.petal, p, R(rng, 0, 360), rng); }
                    else if (roll < 0.62) { var p = pts[i] + nrm * side * R(rng, 3.6f, 5.2f); if (Ok(p, 0.8f, 2.5f, 1f)) ShrubAt(t, st.bushes[rng.Next(st.bushes.Length)], st.bushLeaf[rng.Next(st.bushLeaf.Length)], p, R(rng, 0.8f, 1.2f), R(rng, 0, 360)); }
                    else if (roll < 0.68 && trees < 9) { var p = pts[i] + nrm * side * R(rng, 6.0f, 8.0f); if (Ok(p, 1.5f, 5.5f, 1f)) { TreeAt(t, rng.NextDouble() < 0.6 ? "willow" : "birch", rng.NextDouble() < 0.5 ? "MintLeaf" : "Green3", "Trunk", p, R(rng, 0.9f, 1.2f), R(rng, 0, 360)); trees++; } }
                }
            }
        }

        /// <summary>Rellena huecos grandes con vegetación baja (hierba, flores, algún arbolito) para que el terreno no quede vacío.</summary>
        static void Relleno(Transform t)
        {
            var rng = new System.Random(6239); int placed = 0;
            for (float x = -40f; x <= 40f && placed < 26; x += 5.5f)
                for (float z = -40f; z <= 40f && placed < 26; z += 5.5f)
                {
                    var p = new Vector2(x + R(rng, -2f, 2f), z + R(rng, -2f, 2f)); bool near = false; foreach (var (q, rr) in Occ) if (Vector2.Distance(p, q) < rr + 4.5f) { near = true; break; }
                    if (near || !Ok(p, 1.0f, 4.5f)) continue; int v = PickVillage(p, rng); var st = Styles[v]; var f0 = st.flowers[rng.Next(st.flowers.Length)];
                    FlowersAt(t, f0.flower, "scatter", f0.petal, p, R(rng, 0, 360), rng);
                    for (int i = 0; i < 3; i++) { var q = p + new Vector2(R(rng, -1.6f, 1.6f), R(rng, -1.6f, 1.6f)); if (Ok(q, 0.4f, 4f)) TuftAt(t, q, R(rng, 0.9f, 1.3f), R(rng, 0, 360)); }
                    var s = p + new Vector2(R(rng, 1.2f, 2.2f), R(rng, -1f, 1f)); if (Ok(s, 0.9f, 4.5f)) { if (placed % 3 == 0) { var tr = st.trees[rng.Next(st.trees.Length)]; TreeAt(t, "sapling", st.bushLeaf[0], "Trunk", s, R(rng, 0.8f, 1.1f), R(rng, 0, 360)); } else ShrubAt(t, st.bushes[rng.Next(st.bushes.Length)], st.bushLeaf[rng.Next(st.bushLeaf.Length)], s, R(rng, 0.8f, 1.1f), R(rng, 0, 360)); }
                    placed++;
                }
        }
    }
}
