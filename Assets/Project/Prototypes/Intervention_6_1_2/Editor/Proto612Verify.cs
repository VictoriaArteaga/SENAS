using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Lumina.Core;
using Debug = UnityEngine.Debug;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Verificación del prototipo 6.1.2 sobre las dos escenas: pruebas de colisión con un CharacterController real (mismos
    /// parámetros que el jugador de Lúmina), estadísticas y capturas. Nunca guarda escenas.
    /// </summary>
    public static class Proto612Verify
    {
        const string Evidence = Proto612Builder.Root + "Evidence/";
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Lumina/Prototipo 6.1.2/Verificar y capturar")]
        public static void Run()
        {
            Directory.CreateDirectory(Evidence);
            foreach (var (kind, scenePath) in new[] { ("modular", "Scenes/Prototype_Modular.unity"), ("terrain", "Scenes/Prototype_Terrain.unity") })
            {
                Log.AppendLine("==================== " + kind.ToUpper() + " ====================");
                var scene = EditorSceneManager.OpenScene(Proto612Builder.Root + scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                Missing();
                var traces = RunTests();
                Stats(kind);
                Captures(kind, traces);
                Log.AppendLine();
            }
            File.WriteAllText(Evidence + "resultados.txt", Log.ToString());
            Debug.Log("[P612-VERIFY]\n" + Log);
        }

        // ------------------------------------------------------------------ referencias faltantes

        static void Missing()
        {
            int missingScripts = 0, missingMats = 0, total = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                total++;
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                var r = go.GetComponent<Renderer>();
                if (r != null) foreach (var m in r.sharedMaterials) if (m == null) { missingMats++; Log.AppendLine("  material nulo en: " + go.name + " (" + r.GetType().Name + ", " + r.sharedMaterials.Length + " ranuras)"); }
            }
            Log.AppendLine($"Referencias: GameObjects={total}, scripts faltantes={missingScripts}, materiales nulos={missingMats}");
        }

        // ------------------------------------------------------------------ pruebas de movimiento

        class Result { public string name; public bool reached; public int blockedAt = -1; public Vector3 end; public float minY = 99, maxY = -99; public List<Vector3> trace = new List<Vector3>(); public string note; }

        static CharacterController _cc;

        static Vector3 Place(Vector2 p, float yOffset = 0.05f)
        {
            return new Vector3(p.x, Proto612Layout.Height(p.x, p.y) + yOffset, p.y);
        }

        static void Teleport(Vector3 pos)
        {
            _cc.enabled = false; _cc.transform.position = pos; _cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 40; i++) _cc.Move(Vector3.down * 0.05f);
        }

        /// <summary>Reproduce el movimiento de FirstPersonController: 4 m/s, gravedad -20, isGrounded → -2.</summary>
        static Result Walk(string name, Vector3 start, params Vector2[] waypoints)
        {
            var res = new Result { name = name };
            Teleport(start);
            float vy = 0f; const float dt = 0.02f; const float speed = 4f;
            for (int w = 0; w < waypoints.Length; w++)
            {
                var target = waypoints[w]; float best = float.MaxValue; int sinceBest = 0, steps = 0;
                while (steps++ < 4000)
                {
                    var pos = _cc.transform.position;
                    var d = new Vector3(target.x - pos.x, 0, target.y - pos.z);
                    float dist = d.magnitude;
                    if (dist < 0.4f) break;
                    if (dist < best - 0.02f) { best = dist; sinceBest = 0; } else sinceBest++;
                    if (sinceBest > 150) { res.blockedAt = w; res.end = pos; res.note = $"bloqueado a {dist:0.0} m del objetivo {w}"; return Finish(res); }
                    if (_cc.isGrounded && vy < 0f) vy = -2f;
                    vy += -20f * dt;
                    _cc.Move((d.normalized * speed + Vector3.up * vy) * dt);
                    var p = _cc.transform.position;
                    res.minY = Mathf.Min(res.minY, p.y); res.maxY = Mathf.Max(res.maxY, p.y);
                    if (steps % 5 == 0) res.trace.Add(p);
                }
                if (steps >= 4000) { res.blockedAt = w; res.end = _cc.transform.position; res.note = "tiempo agotado"; return Finish(res); }
            }
            res.reached = true; res.end = _cc.transform.position;
            return Finish(res);
        }

        static Result Finish(Result r) { r.end = _cc.transform.position; r.trace.Add(r.end); return r; }

        static string Fmt(Result r) =>
            $"{(r.reached ? "OK " : "-- ")}{r.name,-44} fin=({r.end.x:0.0},{r.end.y:0.00},{r.end.z:0.0}) y∈[{r.minY:0.00},{r.maxY:0.00}] {r.note}";

        static List<(Result r, bool expectBlocked)> RunTests()
        {
            var player = GameObject.Find("Player");
            _cc = player.GetComponent<CharacterController>();
            var list = new List<(Result, bool)>();
            void T(Result r, bool expectBlocked, bool pass, string extra = "")
            {
                list.Add((r, expectBlocked));
                Log.AppendLine((pass ? "[PASA] " : "[FALLA] ") + Fmt(r) + (extra.Length > 0 ? " | " + extra : ""));
            }

            Log.AppendLine("-- Acceso a las cuatro zonas (desde el spawn, a pie, 4 m/s) --");
            for (int i = 0; i < 4; i++)
            {
                var r = Walk("Spawn → " + Proto612Layout.HouseName[i], Proto612Layout.Spawn, new Vector2(0, -3f), Proto612Layout.HouseFront[i]);
                T(r, false, r.reached);
            }

            Log.AppendLine("-- Puentes: ida y vuelta --");
            for (int i = 0; i < 2; i++)
            {
                var b = Proto612Layout.Bridges[i];
                var east = b.center + b.axis * 10f; var west = b.center - b.axis * 10f;   // dentro del mapa (los caminos llegan a ±12)
                var r1 = Walk($"Puente {i + 1}: este → oeste", Place(east), b.center + b.axis * 7f, b.center, b.center - b.axis * 7f, west);
                T(r1, false, r1.reached && r1.minY > -0.6f, $"profundidad mínima y={r1.minY:0.00} (el lecho está a -1.2)");
                var r2 = Walk($"Puente {i + 1}: oeste → este", Place(west), b.center - b.axis * 7f, b.center, b.center + b.axis * 7f, east);
                T(r2, false, r2.reached && r2.minY > -0.6f, $"profundidad mínima y={r2.minY:0.00}");
            }

            Log.AppendLine("-- El río no se puede atravesar fuera de los puentes --");
            foreach (var nominal in new[] { new Vector2(-30, 2), new Vector2(-24, -10), new Vector2(-37, 17) })
            {
                var h = Proto612Layout.Nearest(nominal, Proto612Layout.River);
                var n = new Vector2(h.tangent.y, -h.tangent.x); if (n.x < 0) n = -n;
                var r = Walk($"Cruzar el río en ({h.point.x:0},{h.point.y:0})", Place(h.point + n * 8f), h.point - n * 8f);
                float dEnd = Proto612Layout.Nearest(new Vector2(r.end.x, r.end.z), Proto612Layout.River).dist;
                T(r, true, !r.reached && r.minY > -0.6f && dEnd > 1.9f, $"distancia al eje al detenerse={dEnd:0.0} m");
            }

            Log.AppendLine("-- Límites del mapa --");
            foreach (var (a, b) in new[] { (new Vector2(40, 0), new Vector2(60, 0)), (new Vector2(0, 40), new Vector2(0, 60)), (new Vector2(30, -40), new Vector2(30, -60)) })
            {
                var r = Walk($"Salir del mapa hacia ({b.x:0},{b.y:0})", Place(a), b);
                T(r, true, !r.reached && Mathf.Abs(r.end.x) <= 45f && Mathf.Abs(r.end.z) <= 45f);
            }

            Log.AppendLine("-- Colisión con las casas --");
            {
                var r = Walk("Atravesar la casa norte", Place(new Vector2(0, 5)), new Vector2(0, 20));
                T(r, true, !r.reached && r.end.z < 13.4f, "no debe pasar de la fachada (z≈13.3)");
                r = Walk("Atravesar la casa oeste", Place(new Vector2(-5, 0)), new Vector2(-25, 0));
                T(r, true, !r.reached && r.end.x > -12.4f);
                r = Walk("Atravesar la casa este", Place(new Vector2(5, 0)), new Vector2(25, 0));
                T(r, true, !r.reached && r.end.x < 12.9f);
                r = Walk("Atravesar la casa sur", Place(new Vector2(0, -5)), new Vector2(0, -25));
                T(r, true, !r.reached && r.end.z > -12.9f);
            }

            Log.AppendLine("-- Pendientes --");
            {
                var r = Walk("Loma noreste (≈11°) hasta la cima", Place(new Vector2(14, 14)), new Vector2(28, 26));
                T(r, false, r.reached, $"cima y esperada={Proto612Layout.Height(28, 26):0.00}");
                r = Walk("Loma sureste (≈22°) hasta la cima", Place(new Vector2(22, -22)), new Vector2(30, -28));
                T(r, false, r.reached, $"cima y esperada={Proto612Layout.Height(30, -28):0.00}");
                r = Walk("Descenso de la loma sureste", Place(new Vector2(30, -28), 0.5f), new Vector2(18, -18));
                T(r, false, r.reached);
            }

            // Alineación suelo ↔ función de altura: ¿el jugador atraviesa el suelo? (la altura se compara con el suelo teórico)
            {
                var rng = new System.Random(7); int n = 0, below = 0; float minDiff = 99f; var abs = new List<float>();
                while (n < 200)
                {
                    var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 42f, (float)(rng.NextDouble() * 2 - 1) * 42f);
                    if (Proto612Layout.Nearest(p, Proto612Layout.River).dist < 4f || Proto612Layout.DistToHouses(p) < 5f) continue;
                    n++;
                    Teleport(new Vector3(p.x, Proto612Layout.Height(p.x, p.y) + 1.5f, p.y));
                    for (int i = 0; i < 30; i++) _cc.Move(Vector3.down * 0.05f);
                    float diff = _cc.transform.position.y - Proto612Layout.Height(p.x, p.y);
                    minDiff = Mathf.Min(minDiff, diff); abs.Add(Mathf.Abs(diff));
                    if (diff < -0.25f) below++;
                }
                abs.Sort();
                Log.AppendLine($"{(below == 0 ? "[PASA]" : "[FALLA]")} 200 caídas al azar: por debajo del suelo teórico (>0.25 m) en {below}; diferencia mínima {minDiff:0.000} m; mediana |y-H| {abs[100]:0.000} m; p95 {abs[190]:0.000} m; máx {abs[199]:0.000} m (los máximos positivos son caídas sobre rocas, flores o árboles)");
            }
            Teleport(Proto612Layout.Spawn + Vector3.up * 0.05f);
            return list;
        }

        // ------------------------------------------------------------------ estadísticas

        static void Stats(string kind)
        {
            int renderers = 0; long tris = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                renderers++; tris += mf.sharedMesh.triangles.Length / 3;
            }
            var counts = new SortedDictionary<string, int>(); long colliderTris = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                string k = c.GetType().Name; counts[k] = counts.TryGetValue(k, out var v) ? v + 1 : 1;
                if (c is MeshCollider mc && mc.sharedMesh != null) colliderTris += mc.sharedMesh.triangles.Length / 3;
            }
            var parts = new List<string>(); foreach (var kv in counts) parts.Add(kv.Key + "=" + kv.Value);
            Log.AppendLine($"Malla visible: MeshRenderers={renderers}, triángulos={tris}");
            Log.AppendLine("Colliders activos: " + string.Join(", ", parts) + $" | triángulos en MeshColliders={colliderTris}");
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                int res = terrain.terrainData.heightmapResolution;
                Log.AppendLine($"Terrain: heightmap {res}x{res} (celda {terrain.terrainData.size.x / (res - 1):0.00} m), alphamap {terrain.terrainData.alphamapResolution}, capas={terrain.terrainData.terrainLayers.Length}, triángulos a máxima resolución≈{(res - 1) * (res - 1) * 2}");
            }
        }

        // ------------------------------------------------------------------ capturas

        static Camera NewCam(Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("TEMP_CAM"); var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
            go.transform.position = pos; go.transform.LookAt(lookAt);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return cam;
        }

        static void Shot(Camera cam, string file, int w = 1600, int h = 900)
        {
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt; cam.Render(); cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(Evidence + file, tex.EncodeToPNG());
            RenderTexture.active = old; cam.targetTexture = null;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }

        static void Captures(string kind, List<(Result r, bool expectBlocked)> traces)
        {
            var b1 = Proto612Layout.Bridges[0];
            Vector3 Up(Vector2 p, float dy) => new Vector3(p.x, Proto612Layout.Height(p.x, p.y) + dy, p.y);
            var riverPt = Proto612Layout.Nearest(new Vector2(-29f, -6f), Proto612Layout.River);
            var rn = new Vector2(riverPt.tangent.y, -riverPt.tangent.x); if (rn.x < 0) rn = -rn;
            var side = new Vector2(-b1.axis.y, b1.axis.x);

            void Take(string name, Vector3 pos, Vector3 look, float fov = 60f)
            {
                var cam = NewCam(pos, look, fov); Shot(cam, kind + "_" + name + ".png"); Object.DestroyImmediate(cam.gameObject);
            }

            SetNight(false);
            Take("01_general", new Vector3(0, 66, -66), new Vector3(-4, 0, 2), 48f);
            Take("02_camino", Up(new Vector2(-5.5f, 6.0f), 2.0f), Up(new Vector2(-20f, 11f), 0.8f));
            Take("03_rio_orillas", Up(riverPt.point + rn * 8.5f, 2.6f), Up(riverPt.point - rn * 1.5f, -0.4f), 55f);
            Take("04_puente", Up(b1.center + b1.axis * 15f + side * 3f, 2.1f), Up(b1.center, 0.4f), 55f);
            Take("05_primera_persona", Proto612Layout.Spawn + new Vector3(0, 1.65f, 0), Proto612Layout.Spawn + new Vector3(0, 1.65f, 20f), 60f);
            Take("07_dia", Proto612Layout.Spawn + new Vector3(0, 1.65f, 0), new Vector3(-6, 6f, 14f), 70f);
            // Recorrido en primera persona sobre el puente.
            Take("05b_primera_persona_puente", Up(b1.center + b1.axis * 8.5f, 1.65f + 0.1f), Up(b1.center - b1.axis * 8f, 1.4f), 70f);

            // Colisiones: recorridos de las pruebas (verde = llegó, rojo = bloqueado) y las barreras invisibles en rojo translúcido.
            var temp = new List<GameObject>();
            foreach (var (r, expectBlocked) in traces)
            {
                if (r.trace.Count < 2) continue;
                var go = new GameObject("TEMP_TRACE"); var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = r.trace.Count; for (int i = 0; i < r.trace.Count; i++) lr.SetPosition(i, r.trace[i] + Vector3.up * 0.4f);
                lr.widthMultiplier = 0.45f; lr.useWorldSpace = true;
                bool ok = expectBlocked ? !r.reached : r.reached;
                lr.sharedMaterial = FlatMat(ok ? new Color(0.1f, 0.85f, 0.25f) : new Color(1f, 0.1f, 0.1f), false);
                temp.Add(go);
            }
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (!mc.name.StartsWith("Rio_Barrera") || mc.sharedMesh == null) continue;
                var go = new GameObject("TEMP_BARRIER"); go.transform.SetPositionAndRotation(mc.transform.position, mc.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mc.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = FlatMat(new Color(1f, 0.15f, 0.15f, 0.5f), true);
                temp.Add(go);
            }
            Take("06_colisiones", new Vector3(0, 70, -52), new Vector3(-4, 0, 0), 48f);
            foreach (var go in temp) Object.DestroyImmediate(go);

            // Noche: cielo nocturno, sol atenuado, ambiente nocturno y luces de farol (solo para la captura).
            SetNight(true);
            var lamps = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Lantern")) continue;
                var go = new GameObject("TEMP_LAMP"); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.45f); l.intensity = 3.5f; l.range = 9f;
                go.transform.position = t.position + Vector3.up * 3.2f; lamps.Add(go);
            }
            Take("08_noche", Proto612Layout.Spawn + new Vector3(0, 1.65f, 0), new Vector3(-6, 6f, 14f), 70f);
            Take("08b_noche_general", new Vector3(0, 66, -66), new Vector3(-4, 0, 2), 48f);
            foreach (var go in lamps) Object.DestroyImmediate(go);
            SetNight(false);

            // Tiempo de CPU de Camera.Render en el Editor (orientativo; no es FPS de juego).
            var pcam = NewCam(Proto612Layout.Spawn + new Vector3(0, 1.65f, 0), Proto612Layout.Spawn + new Vector3(0, 1.65f, 20f), 60f);
            var rt = new RenderTexture(1280, 720, 24); pcam.targetTexture = rt;
            for (int i = 0; i < 10; i++) pcam.Render();
            var sw = Stopwatch.StartNew(); for (int i = 0; i < 40; i++) pcam.Render(); sw.Stop();
            Log.AppendLine($"Camera.Render 1280x720 en el Editor (batchmode, 40 renders): {sw.Elapsed.TotalMilliseconds / 40.0:0.0} ms/render [orientativo; no es rendimiento de juego ni de móvil]");
            pcam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pcam.gameObject);
        }

        static Material FlatMat(Color c, bool transparent)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetOverrideTag("RenderType", "Transparent"); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            }
            return m;
        }

        // Réplica de ThemeSkybox/ThemeAmbient para el Editor (los scripts de producción no se tocan).
        static Material _daySky; static float _daySun = -1; static Color _dayColor;
        static void SetNight(bool dark)
        {
            var sun = RenderSettings.sun;
            if (_daySky == null || RenderSettings.skybox == _daySky || _daySun < 0)
            {
                if (_daySun < 0) { _daySky = RenderSettings.skybox; _daySun = sun != null ? sun.intensity : 1.1f; _dayColor = sun != null ? sun.color : Color.white; }
            }
            var set = Resources.Load<ThemeSkyboxSet>("ThemeSkyboxSet");
            var night = set != null ? set.NightFor(_daySky) : null;
            RenderSettings.skybox = dark && night != null ? night : _daySky;
            if (sun != null)
            {
                sun.intensity = dark ? _daySun * 0.28f : _daySun;
                sun.color = dark ? _dayColor * new Color(0.55f, 0.65f, 0.95f) : _dayColor;
                var amb = sun.GetComponent<ThemeAmbient>(); if (amb != null) amb.ApplyTheme(dark);
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
