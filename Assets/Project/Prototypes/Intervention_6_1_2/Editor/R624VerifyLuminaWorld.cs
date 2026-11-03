using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using L = Lumina.Prototype612.R621Layout;
using H = Lumina.Prototype612.Proto612LayoutV2;
using Lumina.Prototype612;

namespace LuminaProto
{
    /// <summary>
    /// Verificación de LuminaWorld tras la integración del prototipo 6.2.3 (solo lectura: abre la escena, camina con el CharacterController real
    /// en el Editor y NO guarda la escena). No activa MediaPipe, cámara web ni reconocimiento. Resultados y capturas se escriben en el
    /// directorio indicado con -outDir (por defecto Temp/).
    /// </summary>
    public static class R624VerifyLuminaWorld
    {
        const string WorldPath = "Assets/Project/Scenes/LuminaWorld.unity";
        static readonly StringBuilder Log = new StringBuilder();
        static int _pass, _fail;
        static CharacterController _cc;
        static string _out = "Temp/";

        class Result { public string name; public bool reached; public Vector3 end; public float minY = 99, maxY = -99; public string note; }

        static void Check(bool ok, string text) { if (ok) _pass++; else _fail++; Log.AppendLine((ok ? "[PASA] " : "[FALLA] ") + text); }

        public static void RunBatch()
        {
            int code = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-outDir"); if (i >= 0 && i + 1 < args.Length) _out = args[i + 1].TrimEnd('/', '\\') + "/";
                Directory.CreateDirectory(_out);
                bool shots = !args.Contains("-noShots");
                Run(shots);
                code = _fail == 0 ? 0 : 2;
            }
            catch (Exception e) { Log.AppendLine("EXCEPCIÓN: " + e); code = 1; }
            File.WriteAllText(_out + "r624_resultados.txt", Log + $"\nRESUMEN: {_pass} pasan, {_fail} fallan\n");
            EditorApplication.Exit(code);
        }


        public static void RunDiag()
        {
            var a = Environment.GetCommandLineArgs(); int oi = Array.IndexOf(a, "-outDir"); if (oi >= 0) { _out = a[oi + 1].TrimEnd('/', '\\') + "/"; Directory.CreateDirectory(_out); }
            EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single); Physics.SyncTransforms();
            var sb = new StringBuilder();
            void At(string tag, Vector3 p, float r)
            {
                sb.AppendLine($"-- {tag} ({p.x:0.0},{p.y:0.0},{p.z:0.0}) r={r}");
                foreach (var c in Physics.OverlapSphere(p, r)) { var path = c.transform.name; var t = c.transform.parent; while (t != null) { path = t.name + "/" + path; t = t.parent; } sb.AppendLine($"   {c.GetType().Name} {path} active={c.gameObject.activeInHierarchy}"); }
            }
            var pl = GameObject.Find("Player");
            At("spawn", pl.transform.position + Vector3.up, 1.2f);
            At("blocked_abecedario_tematica", new Vector3(-14.3f, 0.5f, 22f), 1.5f);
            At("blocked_puente_norte", new Vector3(13.6f, 0.5f, 24.4f), 1.5f);
            At("blocked_puente_sur", new Vector3(13.8f, 0.5f, -25.1f), 1.5f);
            File.WriteAllText(_out + "r624_diag.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        static void Run(bool shots)
        {
            var scene = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            Structure(scene);
            Spatial();
            if (shots) Captures();
        }

        static GameObject Find(Scene s, string name)
        {
            foreach (var r in s.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.gameObject;
            return null;
        }

        // ------------------------------------------------------------------ estructura de producción
        static void Structure(Scene scene)
        {
            Log.AppendLine("== Estructura y componentes de producción ==");
            int missing = 0, total = 0;
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true)) { total++; missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject); }
            Check(missing == 0, $"scripts faltantes en {total} objetos de la escena: {missing}");

            var player = scene.GetRootGameObjects().Where(g => g.name == "Player").ToList();
            Check(player.Count == 1, "un único jugador raíz");
            var p = player[0];
            Check(p.activeInHierarchy && p.GetComponent<CharacterController>() != null && p.GetComponent<Lumina.Player.FirstPersonController>() != null && p.GetComponent<Lumina.Player.PlayerAvatar>() != null, "Player: CharacterController + FirstPersonController + PlayerAvatar presentes y activos");
            var cam = p.GetComponentInChildren<Camera>();
            Check(cam != null && cam.CompareTag("MainCamera"), "cámara principal en primera persona dentro del Player");
            Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "un solo AudioListener activo");
            Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.CompareTag("MainCamera")) == 1, "una sola cámara MainCamera activa");

            var sena = Find(scene, "Sena");
            Check(sena != null && sena.activeInHierarchy, "Sena presente y activa");
            Check(sena.GetComponent<Lumina.Interaction.ProximityInteractable>() != null && sena.GetComponent<Lumina.NPC.SenaPresenter>() != null && sena.GetComponent<Lumina.NPC.SenaWelcomeInteraction>() != null, "Sena conserva ProximityInteractable + SenaPresenter + SenaWelcomeInteraction");
            Check(sena.GetComponentsInChildren<Renderer>().Length > 0, "Sena conserva su modelo (renderers: " + sena.GetComponentsInChildren<Renderer>().Length + ")");

            Log.AppendLine("-- Las cuatro puertas --");
            var expected = new (string door, string scene, string entrada, string village, bool west, bool north)[]
            {
                ("PuertaCasaAprendizaje", "AlphabetHouse", "Entrada_AlphabetHouse", "Abecedario (NO)", true, true),
                ("PuertaCasaMeses", "MonthsHouse", "Entrada_MonthsHouse", "Meses (NE)", false, true),
                ("PuertaCasaNumeros", "NumbersHouse", "Entrada_NumbersHouse", "Números (SO)", true, false),
                ("PuertaCasaSaludos", "GreetingsHouse", "Entrada_GreetingsHouse", "Saludos (SE)", false, false),
            };
            var inBuild = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
            foreach (var e in expected)
            {
                var d = Find(scene, e.door); var ent = Find(scene, e.entrada);
                bool comps = d != null && d.activeInHierarchy && d.GetComponent<Lumina.Interaction.ProximityInteractable>() != null && d.GetComponent<Lumina.Interaction.InteractionPoint>() != null && d.GetComponent<Lumina.Interaction.SceneEntrance>() != null;
                Check(comps, $"{e.door}: ProximityInteractable + InteractionPoint + SceneEntrance activos");
                if (!comps) continue;
                string target = new SerializedObject(d.GetComponent<Lumina.Interaction.SceneEntrance>()).FindProperty("sceneName").stringValue;
                var ip = new SerializedObject(d.GetComponent<Lumina.Interaction.InteractionPoint>());
                var call = ip.FindProperty("onActivated.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0);
                bool wired = call.FindPropertyRelative("m_Target").objectReferenceValue == d.GetComponent<Lumina.Interaction.SceneEntrance>() && call.FindPropertyRelative("m_MethodName").stringValue == "Enter";
                var prompt = ip.FindProperty("prompt").objectReferenceValue;
                Check(target == e.scene && inBuild.Contains(target), $"{e.door} → '{target}' (esperada '{e.scene}'; en Build Settings: {inBuild.Contains(target)})");
                Check(wired && prompt != null, $"{e.door}: onActivated → SceneEntrance.Enter cableado y prompt asignado ({(prompt != null ? prompt.name : "NULL")})");
                Vector3 dp = d.transform.position; Vector3 ep = ent.transform.position;
                Check(Vector2.Distance(new Vector2(dp.x, dp.z), new Vector2(ep.x, ep.z)) < 0.01f, $"{e.door} en la entrada de su casa ({dp.x:0.0},{dp.z:0.0})");
                Check((dp.x < -1f) == e.west && (dp.z > 1f) == e.north, $"{e.door} en el cuadrante de la aldea de {e.village}");
                var house = Find(scene, e.scene.Replace("House", "House_Exterior"));
                Check(house != null && house.activeInHierarchy, $"casa {e.scene}_Exterior presente");
            }
            Check(Find(scene, "Environment") != null && !Find(scene, "Environment").activeSelf, "entorno antiguo desactivado (no borrado)");
            Check(Find(scene, "--- DECORACION R622 ---") != null && Find(scene, "--- PAISAJE R623 ---") != null && scene.GetRootGameObjects().Any(g => g.name.StartsWith("--- PROTOTIPO")), "grupos del prototipo presentes en LuminaWorld");
            var sunLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).ToList();
            Check(sunLights.Count == 1 && sunLights[0].GetComponent<Lumina.Core.ThemeAmbient>() != null, $"una sola luz direccional (la de producción, con ThemeAmbient): {sunLights.Count}");
        }

        // ------------------------------------------------------------------ espacial
        static Vector3 Place(Vector2 p, float dy = 0.05f) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);

        static void Teleport(Vector3 pos)
        {
            _cc.enabled = false; _cc.transform.position = pos; _cc.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 40; i++) _cc.Move(Vector3.down * 0.05f);
        }

        static Result Walk(string name, Vector3 start, params Vector2[] waypoints)
        {
            var res = new Result { name = name }; Teleport(start); float vy = 0f; const float dt = 0.02f;
            for (int w = 0; w < waypoints.Length; w++)
            {
                var target = waypoints[w]; float best = float.MaxValue; int sinceBest = 0, steps = 0;
                while (steps++ < 6000)
                {
                    var pos = _cc.transform.position; var d = new Vector3(target.x - pos.x, 0, target.y - pos.z); float dist = d.magnitude;
                    if (dist < 0.4f) break;
                    if (dist < best - 0.02f) { best = dist; sinceBest = 0; } else sinceBest++;
                    if (sinceBest > 150) { res.end = pos; res.note = $"bloqueado a {dist:0.0} m del objetivo {w}"; return Fin(res); }
                    if (_cc.isGrounded && vy < 0f) vy = -2f; vy += -20f * dt;
                    _cc.Move((d.normalized * 4f + Vector3.up * vy) * dt);
                    var p = _cc.transform.position; res.minY = Mathf.Min(res.minY, p.y); res.maxY = Mathf.Max(res.maxY, p.y);
                }
                if (steps >= 6000) { res.end = _cc.transform.position; res.note = "tiempo agotado"; return Fin(res); }
            }
            res.reached = true; return Fin(res);
        }
        static Result Fin(Result r) { r.end = _cc.transform.position; return r; }
        static string Fmt(Result r) => $"{(r.reached ? "llegó " : "bloqueado ")}{r.name} fin=({r.end.x:0.0},{r.end.y:0.00},{r.end.z:0.0}) y∈[{r.minY:0.00},{r.maxY:0.00}] {r.note}";

        static List<Vector2> Pts(string pathName, bool reverse = false, int dropLast = 0)
        {
            int idx = Array.IndexOf(L.PathName, pathName); var res = new List<Vector2>();
            foreach (var (p, _) in Proto612Layout.Resample(L.Paths[idx], 3f)) if (!L.HubCenter.Any(h => Vector2.Distance(h, p) < 3.9f)) res.Add(p);   // el centro de cada plaza temática lo ocupa un monumento (6.2.2)
            if (reverse) res.Reverse();
            if (dropLast > 0 && res.Count > dropLast) res.RemoveRange(res.Count - dropLast, dropLast);
            return res;
        }
        static List<Vector2> Join(params List<Vector2>[] lists) { var r = new List<Vector2>(); foreach (var l in lists) r.AddRange(l); return r; }

        static int BlockedAt(Vector2 p)
        {
            var pos = Place(p, 0.1f);
            return Physics.OverlapCapsule(pos + Vector3.up * 0.35f, pos + Vector3.up * 1.5f, 0.3f).Any(c => c.name != "Suelo" && c.name != "Player") ? 1 : 0;
        }

        static void Spatial()
        {
            Log.AppendLine("\n== Pruebas espaciales (Editor, CharacterController real, entrada por código; sin Play Mode) ==");
            var pl = GameObject.Find("Player"); _cc = pl.GetComponent<CharacterController>();
            var spawn = pl.transform.position;
            Check(Vector3.Distance(spawn, L.Spawn) < 0.01f, $"spawn de producción = spawn del prototipo ({spawn.x:0.0},{spawn.y:0.0},{spawn.z:0.0})");
            Check(Physics.OverlapCapsule(spawn + Vector3.up * 0.35f, spawn + Vector3.up * 1.5f, 0.3f).All(c => c.name == "Suelo" || c.transform.IsChildOf(pl.transform)), "spawn sin solapamiento con geometría (solo el suelo)");
            Check(Physics.Raycast(spawn + Vector3.up * 1f, Vector3.down, out var gh, 3f) && gh.distance < 1.3f, "suelo bajo el spawn: " + (gh.collider != null ? gh.collider.name : "ninguno"));

            var sena = Find(SceneManager.GetActiveScene(), "Sena");
            var eye = spawn + Vector3.up * 1.65f; var sp = sena.transform.position + Vector3.up * 1.3f;
            var fwd = pl.transform.forward; var to = sp - eye;
            float ang = Vector3.Angle(new Vector3(fwd.x, 0, fwd.z), new Vector3(to.x, 0, to.z)); var hits = Physics.RaycastAll(eye, to.normalized, to.magnitude).Where(h => !h.collider.transform.IsChildOf(sena.transform) && !h.collider.transform.IsChildOf(pl.transform)).OrderBy(h => h.distance).ToList(); bool blocked = hits.Count > 0; var hit = blocked ? hits[0] : default;
            Check(ang < 40f && !blocked, $"Sena a {to.magnitude:0.0} m del spawn, a {ang:0}° del eje de la cámara, línea de visión {(blocked ? "BLOQUEADA por " + hit.collider.name : "libre")}");
            Check(Physics.OverlapCapsule(sena.transform.position + Vector3.up * 0.35f, sena.transform.position + Vector3.up * 1.5f, 0.3f).All(c => c.name == "Suelo" || c.transform.IsChildOf(sena.transform)), "Sena sin solapamiento con geometría");
            var senaInt = sena.GetComponent<Lumina.Interaction.ProximityInteractable>();
            var radius = new SerializedObject(senaInt).FindProperty("interactionRadius").floatValue;
            Log.AppendLine($"  Sena: radio de interacción {radius:0.0} m (se alcanza caminando por la plaza; ver ruta spawn→Sena)");
            { var sx = new Vector2(sena.transform.position.x, sena.transform.position.z); var front = new Vector2(spawn.x, spawn.z) - sx; var stop = sx + front.normalized * 1.2f;
              var r = Walk("Spawn → junto a Sena (1,2 m)", spawn, stop);
              Check(r.reached && Vector2.Distance(new Vector2(r.end.x, r.end.z), sx) <= radius, Fmt(r) + $" | a {Vector2.Distance(new Vector2(r.end.x, r.end.z), sx):0.00} m de Sena (radio {radius:0.0})"); }

            Log.AppendLine("-- Del spawn a cada puerta real (siguiendo los caminos) y regreso a la plaza --");
            var doors = new[] { "PuertaCasaAprendizaje", "PuertaCasaMeses", "PuertaCasaNumeros", "PuertaCasaSaludos" };
            var routes = new (int v, List<Vector2> pts)[]
            {
                (0, Join(new List<Vector2>{ L.PlazaCenter }, Pts("Camino_Plaza_Abecedario", false, 2))),
                (2, Join(new List<Vector2>{ L.PlazaCenter }, Pts("Camino_Puente_CentroNorte"), Pts("Camino_CentroNorte_Meses", false, 2))),
                (1, Join(new List<Vector2>{ L.PlazaCenter }, Pts("Camino_Plaza_Numeros", false, 2))),
                (3, Join(new List<Vector2>{ L.PlazaCenter }, Pts("Camino_Puente_CentroSur"), Pts("Camino_CentroSur_Saludos", false, 2))),
            };
            var scene = SceneManager.GetActiveScene();
            for (int k = 0; k < 4; k++)
            {
                var door = Find(scene, doors[k]); var dp = new Vector2(door.transform.position.x, door.transform.position.z);
                var pts = routes[k].pts; pts.Add(dp);
                var r = Walk($"Spawn → {doors[k]}", spawn, pts.ToArray());
                var prox = new SerializedObject(door.GetComponent<Lumina.Interaction.ProximityInteractable>()).FindProperty("interactionRadius").floatValue;
                float dHoriz = Vector2.Distance(new Vector2(r.end.x, r.end.z), dp);
                Check(r.reached && dHoriz <= prox, Fmt(r) + $" | a {dHoriz:0.00} m de la puerta (radio de interacción {prox:0.0})");
                var back = new List<Vector2>(pts); back.Reverse(); back.Add(L.PlazaCenter);
                var rb = Walk($"Regreso {doors[k]} → plaza", r.end, back.ToArray());
                Check(rb.reached && Vector2.Distance(new Vector2(rb.end.x, rb.end.z), L.PlazaCenter) < 1f, Fmt(rb));
                int blockedPts = 0; var h = H.Houses[routes[k].v];
                for (float d = 0.5f; d <= 10f; d += 0.5f) blockedPts += BlockedAt(H.DoorPoint(routes[k].v, 0f) + h.Front * d);
                Check(blockedPts == 0, $"{doors[k]}: puerta y 10 m de acceso sin objetos ni colliders ({blockedPts} puntos bloqueados)");
            }

            Log.AppendLine("-- Cuatro puentes: ida y vuelta --");
            for (int i = 0; i < 4; i++)
            {
                var b = L.Bridges[i]; Vector2 East(float d) => b.center + b.axis * d;
                var r1 = Walk($"{L.BridgeName[i]}: este → oeste", Place(East(10f)), East(7f), East(0f), East(-7f), East(-10f));
                Check(r1.reached && r1.minY > -0.6f, Fmt(r1));
                var r2 = Walk($"{L.BridgeName[i]}: oeste → este", Place(East(-10f)), East(-7f), East(0f), East(7f), East(10f));
                Check(r2.reached && r2.minY > -0.6f, Fmt(r2));
            }

            Log.AppendLine("-- Caminos de extremo a extremo --");
            for (int i = 0; i < L.Paths.Count; i++)
            {
                var wps = Pts(L.PathName[i], false, L.PathName[i].EndsWith("Abecedario") || L.PathName[i].EndsWith("Numeros") || L.PathName[i].EndsWith("Meses") || L.PathName[i].EndsWith("Saludos") ? 2 : 0); if (wps.Count < 2) continue;
                var r = Walk(L.PathName[i], Place(wps[0]), wps.GetRange(1, wps.Count - 1).ToArray());
                Check(r.reached && r.minY > -0.6f, Fmt(r));
            }

            Log.AppendLine("-- El río bloquea fuera de los puentes; límites del mapa; casas sólidas --");
            var samples = Proto612Layout.Resample(L.River, 1f);
            foreach (float f in new[] { 0.18f, 0.36f, 0.54f, 0.82f })
            {
                var s = samples[Mathf.RoundToInt(f * (samples.Count - 1))]; if (L.Bridges.Any(bb => Vector2.Distance(bb.center, s.p) < 9f)) continue;
                var n = new Vector2(s.t.y, -s.t.x); if (n.x < 0) n = -n;
                var r = Walk($"Cruzar el río fuera de puente (punto {f:0.00})", Place(s.p + n * 8f), s.p - n * 8f);
                Check(!r.reached && r.minY > -0.6f, Fmt(r));
            }
            foreach (var (a, b) in new[] { (new Vector2(40, 5), new Vector2(60, 5)), (new Vector2(-40, -5), new Vector2(-60, -5)), (new Vector2(8, 40), new Vector2(8, 60)), (new Vector2(-8, -40), new Vector2(-8, -60)) })
            { var r = Walk($"Salir del mapa hacia ({b.x:0},{b.y:0})", Place(a), b); Check(!r.reached && Mathf.Abs(r.end.x) <= 45f && Mathf.Abs(r.end.z) <= 45f, Fmt(r)); }
            for (int i = 0; i < 4; i++)
            { var h = H.Houses[i]; var r = Walk("Atravesar " + h.name, Place(h.FrontFace + h.Front * 6f), h.center); Check(!r.reached, Fmt(r)); }

            Log.AppendLine("-- Colliders del entorno antiguo --");
            var env = Find(scene, "Environment");
            Check(env.GetComponentsInChildren<Collider>(false).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 0, "ningún collider activo del entorno antiguo");
            var invisible = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.enabled && c.GetComponent<Renderer>() == null && c.GetComponentInChildren<Renderer>() == null && c.GetComponentInParent<Renderer>() == null).Select(c => c.name).Distinct().ToList();
            Check(invisible.All(n => n.StartsWith("Limites") || n.StartsWith("Rio_Barrera") || n == "Player"), "colliders sin renderer: " + string.Join(", ", invisible));
            Teleport(spawn + Vector3.up * 0.05f);
        }

        // ------------------------------------------------------------------ capturas
        static void Take(string name, Vector3 pos, Vector3 look, float fov = 60f)
        {
            var go = new GameObject("TEMP_CAM"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
            go.transform.position = pos; go.transform.LookAt(look); cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            int w = 1600, h = 900; var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; cam.Render(); cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); File.WriteAllBytes(_out + "r624_" + name + ".png", tex.EncodeToPNG());
            RenderTexture.active = old; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }

        static void Captures()
        {
            Log.AppendLine("\n== Capturas (Editor, renderizado de cámaras temporales) ==");
            var scene = SceneManager.GetActiveScene();
            var pl = GameObject.Find("Player"); var sp = pl.transform.position;
            Take("01_general", new Vector3(10, 78, -60), new Vector3(0, 0, 4), 52f);
            Take("02_cenital", new Vector3(0, 105, -0.1f), Vector3.zero, 55f);
            Take("03_spawn_primera_persona", sp + Vector3.up * 1.65f, sp + Vector3.up * 1.65f + pl.transform.forward * 20f, 60f);
            var sena = Find(scene, "Sena").transform.position;
            Take("04_sena", sena + new Vector3(-4, 1.8f, 1.5f), sena + Vector3.up * 1.2f, 55f);
            string[] doors = { "PuertaCasaAprendizaje", "PuertaCasaMeses", "PuertaCasaNumeros", "PuertaCasaSaludos" };
            int[] vil = { 0, 2, 1, 3 };
            for (int k = 0; k < 4; k++)
            {
                var d = Find(scene, doors[k]).transform.position; var h = H.Houses[vil[k]];
                Take("05_puerta_" + doors[k], new Vector3(d.x, 1.65f, d.z) + new Vector3(h.Front.x, 0, h.Front.y) * 6.5f, new Vector3(d.x, 1.5f, d.z), 62f);
            }
            Take("06_aldeas_NO", new Vector3(-48, 22, 38), new Vector3(-20, 1, 20), 58f);
            Take("07_aldeas_SE", new Vector3(48, 22, -38), new Vector3(20, 1, -20), 58f);
            Take("08_plaza_y_rio", new Vector3(-30, 14, -22), new Vector3(-6, 0, -2), 60f);
        }
    }
}
