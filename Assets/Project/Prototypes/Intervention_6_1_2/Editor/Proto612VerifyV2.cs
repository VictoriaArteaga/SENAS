using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Lumina.Core;
using L = Lumina.Prototype612.Proto612LayoutV2;
using Debug = UnityEngine.Debug;

namespace Lumina.Prototype612
{
    /// <summary>Intervención 6.1.3: pruebas de geometría, colisión y navegación, estadísticas y capturas de la escena de río central. No guarda escenas.</summary>
    public static class Proto612VerifyV2
    {
        const string Evidence = Proto612Builder.Root + "Evidence/";
        static readonly StringBuilder Log = new StringBuilder();
        static CharacterController _cc;
        static int _pass, _fail;

        class Result { public string name; public bool reached; public Vector3 end; public float minY = 99, maxY = -99; public string note; public List<Vector3> trace = new List<Vector3>(); }

        [MenuItem("Lumina/Prototipo 6.1.2/Verificar modular con río central (6.1.3)")]
        public static void Run()
        {
            Directory.CreateDirectory(Evidence);
            EditorSceneManager.OpenScene(Proto612BuilderV2.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            MissingRefs();
            Geometry();
            SenaAndSigns();
            Clearance();
            var traces = Tests();
            Stats();
            Captures(traces);
            Log.AppendLine($"\nRESUMEN: {_pass} pruebas pasan, {_fail} fallan");
            File.WriteAllText(Evidence + "r4_resultados.txt", Log.ToString());
            Debug.Log("[P612-V2-VERIFY]\n" + Log);
        }

        static void Check(bool ok, string text) { if (ok) _pass++; else _fail++; Log.AppendLine((ok ? "[PASA] " : "[FALLA] ") + text); }

        static void MissingRefs()
        {
            int scripts = 0, mats = 0, total = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                total++; scripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                var r = go.GetComponent<Renderer>();
                if (r != null) foreach (var m in r.sharedMaterials) if (m == null) { mats++; Log.AppendLine("  material nulo en: " + go.name + " (" + r.GetType().Name + ")"); }
            }
            Log.AppendLine($"Referencias: GameObjects={total}, scripts faltantes={scripts}, materiales nulos={mats} (detalle en la sección Sena)");
            Check(scripts == 0, "sin scripts faltantes");
        }

        // ------------------------------------------------------------------ geometría: ¿se cruzan río, casas, plaza y caminos?
        static void Geometry()
        {
            Log.AppendLine("-- Geometría (sin intersecciones) --");
            foreach (var h in L.Houses)
            {
                float min = 99f;
                foreach (float sx in new[] { -0.5f, 0.5f }) foreach (float sy in new[] { -0.5f, 0.5f })
                    min = Mathf.Min(min, L.DistToRiver(h.center + h.Lateral * (h.size.x * sx) + h.Front * (h.size.y * sy)));
                Check(min >= 8f, $"{h.name} ({(h.center.x < 0 ? "oeste" : "este")}): esquina más cercana al eje del río a {min:0.0} m");
            }
            float pl = L.DistToRiver(L.PlazaCenter) - L.PlazaRadius, es = L.DistToRiver(L.EsplanadeCenter) - L.EsplanadeRadius;
            Check(pl >= 3.6f, $"plaza: borde a {pl:0.0} m del eje del río (la orilla acaba a 3,5)");
            Check(es >= 3.6f, $"explanada: borde a {es:0.0} m del eje del río");
            Check(L.PlazaCenter.x < L.Bridges[0].center.x && L.EsplanadeCenter.x > L.Bridges[0].center.x, "plaza en la orilla oeste y explanada en la este, a cada lado del puente principal");

            int bad = 0, samples = 0;
            for (int i = 0; i < L.Paths.Count; i++)
                foreach (var (p, _) in Proto612Layout.Resample(L.Paths[i], 1f))
                {
                    samples++;
                    if (L.DistToRiver(p) < 3.8f && !((p - L.Bridges[0].center).magnitude < 7f || (p - L.Bridges[1].center).magnitude < 7f)) { bad++; Log.AppendLine($"  camino {L.PathName[i]} sobre el río en {p}"); }
                }
            Check(bad == 0, $"ningún camino pisa el río fuera de un puente ({samples} puntos revisados)");
            int inHouse = 0;
            for (int i = 0; i < L.Paths.Count; i++)
                foreach (var (p, _) in Proto612Layout.Resample(L.Paths[i], 0.5f))
                    foreach (var h in L.Houses)
                    {
                        var d = p - h.center; float f = Vector2.Dot(d, h.Front), l = Vector2.Dot(d, h.Lateral);
                        if (Mathf.Abs(f) < h.size.y * 0.5f - 0.25f && Mathf.Abs(l) < h.size.x * 0.5f - 0.25f) inHouse++;
                    }
            Check(inHouse == 0, "ningún camino atraviesa la huella de una casa");
            var tilt = new float[2];
            for (int i = 0; i < 2; i++) { var b = L.Bridges[i]; var h = Proto612Layout.Nearest(b.center, L.River); var nn = new Vector2(h.tangent.y, -h.tangent.x); if (Vector2.Dot(nn, b.axis) < 0f) nn = -nn; tilt[i] = Vector2.Angle(b.axis, nn); }
            Log.AppendLine($"Puentes: centro1=({L.Bridges[0].center.x:0.0},{L.Bridges[0].center.y:0.0}) yaw={L.Bridges[0].yaw:0}°, centro2=({L.Bridges[1].center.x:0.0},{L.Bridges[1].center.y:0.0}) yaw={L.Bridges[1].yaw:0}° (perpendiculares al río, desvío {tilt[0]:0.0}° y {tilt[1]:0.0}°)");
        }

        // ------------------------------------------------------------------ movimiento
        static Vector3 Place(Vector2 p, float dy = 0.05f) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);

        static void Teleport(Vector3 pos)
        {
            _cc.enabled = false; _cc.transform.position = pos; _cc.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 40; i++) _cc.Move(Vector3.down * 0.05f);
        }

        static Result Walk(string name, Vector3 start, params Vector2[] waypoints)
        {
            var res = new Result { name = name }; Teleport(start);
            float vy = 0f; const float dt = 0.02f;
            for (int w = 0; w < waypoints.Length; w++)
            {
                var target = waypoints[w]; float best = float.MaxValue; int sinceBest = 0, steps = 0;
                while (steps++ < 5000)
                {
                    var pos = _cc.transform.position; var d = new Vector3(target.x - pos.x, 0, target.y - pos.z); float dist = d.magnitude;
                    if (dist < 0.4f) break;
                    if (dist < best - 0.02f) { best = dist; sinceBest = 0; } else sinceBest++;
                    if (sinceBest > 150) { res.end = pos; res.note = $"bloqueado a {dist:0.0} m del objetivo {w}"; return Fin(res); }
                    if (_cc.isGrounded && vy < 0f) vy = -2f;
                    vy += -20f * dt;
                    _cc.Move((d.normalized * 4f + Vector3.up * vy) * dt);
                    var p = _cc.transform.position; res.minY = Mathf.Min(res.minY, p.y); res.maxY = Mathf.Max(res.maxY, p.y);
                    if (steps % 5 == 0) res.trace.Add(p);
                }
                if (steps >= 5000) { res.end = _cc.transform.position; res.note = "tiempo agotado"; return Fin(res); }
            }
            res.reached = true; return Fin(res);
        }
        static List<Vector2> Join(List<Vector2> a, List<Vector2> b) { var r = new List<Vector2>(a); r.AddRange(b); return r; }
        static Result Fin(Result r) { r.end = _cc.transform.position; r.trace.Add(r.end); return r; }
        static string Fmt(Result r) => $"{(r.reached ? "llegó " : "bloqueado ")}{r.name,-46} fin=({r.end.x:0.0},{r.end.y:0.00},{r.end.z:0.0}) y∈[{r.minY:0.00},{r.maxY:0.00}] {r.note}";

        static List<(Result r, bool expectBlocked)> Tests()
        {
            _cc = GameObject.Find("Player").GetComponent<CharacterController>();
            var list = new List<(Result, bool)>();
            void Add(Result r, bool expectBlocked, bool pass, string extra = "") { list.Add((r, expectBlocked)); Check(pass, Fmt(r) + (extra.Length > 0 ? " | " + extra : "")); }

            List<Vector2> Pts(string pathName, bool dropLast = true)
            {
                int idx = System.Array.IndexOf(L.PathName, pathName); var res = new List<Vector2>();
                foreach (var (p, _) in Proto612Layout.Resample(L.Paths[idx], 3f)) res.Add(p);
                if (dropLast && res.Count > 1) res.RemoveAt(res.Count - 1);   // el último punto está pegado a la pared (el jugador mide 0,6 m de ancho)
                return res;
            }

            Log.AppendLine("-- Accesibilidad desde el spawn a las cuatro casas (siguiendo los caminos reales) --");
            var routes = new[]
            {
                ("AlphabetHouse", Pts("Camino_Plaza_Alphabet")),
                ("NumbersHouse", Pts("Camino_Plaza_Numbers")),
                ("MonthsHouse", Join(Pts("Camino_Puente1", false), Pts("Camino_Explanada_Months"))),
                ("GreetingsHouse", Join(Pts("Camino_Puente1", false), Pts("Camino_Explanada_Greetings"))),
            };
            foreach (var (houseName, wps) in routes)
            {
                var h = System.Array.Find(L.Houses, x => x.name == houseName);
                var r = Walk("Spawn → " + houseName + " (por camino" + (houseName.StartsWith("Months") || houseName.StartsWith("Greet") ? " y puente 1" : "") + ")", L.Spawn, wps.ToArray());
                float dFace = Vector2.Dot(new Vector2(r.end.x, r.end.z) - h.FrontFace, h.Front);
                Add(r, false, r.reached && dFace < 4f, $"termina a {dFace:0.0} m de la fachada");
            }

            Log.AppendLine("-- Caminos de extremo a extremo (sin interrupciones) --");
            for (int i = 0; i < L.Paths.Count; i++)
            {
                var wps = Pts(L.PathName[i]);
                var r = Walk(L.PathName[i], Place(wps[0]), wps.GetRange(1, wps.Count - 1).ToArray());
                Add(r, false, r.reached && r.minY > -0.6f);
            }

            Log.AppendLine("-- Puentes: dos cruces completos (ida y vuelta) por puente --");
            for (int i = 0; i < 2; i++)
            {
                var b = L.Bridges[i];
                Vector2 East(float d) => b.center + b.axis * d;
                for (int rep = 1; rep <= 2; rep++)
                {
                    var r1 = Walk($"Puente {i + 1} cruce {rep}: este → oeste", Place(East(10f)), East(7f), East(0f), East(-7f), East(-10f));
                    Add(r1, false, r1.reached && r1.minY > -0.6f, $"altura mínima y={r1.minY:0.00}");
                    var r2 = Walk($"Puente {i + 1} cruce {rep}: oeste → este", Place(East(-10f)), East(-7f), East(0f), East(7f), East(10f));
                    Add(r2, false, r2.reached && r2.minY > -0.6f, $"altura mínima y={r2.minY:0.00}");
                }
            }

            Log.AppendLine("-- Intentos de cruzar el río fuera de los puentes (deben bloquearse en la barrera) --");
            var samples = Proto612Layout.Resample(L.River, 1f);
            var spots = new List<(Vector2 p, Vector2 t, string tag, float away)>();
            foreach (float f in new[] { 0.10f, 0.20f, 0.40f, 0.62f, 0.80f, 0.92f }) { var s = samples[Mathf.RoundToInt(f * (samples.Count - 1))]; spots.Add((s.p, s.t, $"punto {f:0.00} del cauce", 0f)); }
            for (int i = 0; i < 2; i++) { var h = Proto612Layout.Nearest(L.Bridges[i].center, L.River); spots.Add((h.point + h.tangent * 4.5f, h.tangent, $"junto al puente {i + 1} (+4,5 m)", 1f)); spots.Add((h.point - h.tangent * 4.5f, h.tangent, $"junto al puente {i + 1} (−4,5 m)", -1f)); }
            foreach (var (p0, t, tag, away) in spots)
            {
                var n = new Vector2(t.y, -t.x); if (n.x < 0) n = -n;
                Result last = null; float dEnd = 0f; bool pass = false; string shiftTag = "";
                foreach (float shift in away == 0f ? new[] { 0f, 3f, -3f } : new[] { 0f, 3f * away, 6f * away })        // si un obstáculo de la orilla corta el intento, se repite desplazado (nunca hacia el puente)
                {
                    var p = p0 + t * shift; var a = p + n * 8f; var z = p - n * 8f;
                    if (Mathf.Abs(a.x) > 42f || Mathf.Abs(a.y) > 42f || Mathf.Abs(z.x) > 42f || Mathf.Abs(z.y) > 42f) continue;
                    last = Walk($"Cruzar el río: {tag}", Place(a), z); dEnd = L.DistToRiver(new Vector2(last.end.x, last.end.z));
                    pass = !last.reached && last.minY > -0.6f && dEnd > 1.9f && dEnd < 3.2f; shiftTag = shift == 0f ? "" : $" (repetido {shift:+0;-0} m)";
                    if (pass || last.reached) break;
                }
                if (last == null) { Log.AppendLine($"  (omitido: {tag}, extremos fuera del mapa)"); continue; }
                Add(last, true, pass, $"se detiene a {dEnd:0.0} m del eje{shiftTag}");
            }

            Log.AppendLine("-- Colisión con las casas (se aproxima de frente y debe detenerse en la fachada) --");
            foreach (var h in L.Houses)
            {
                var r = Walk("Atravesar " + h.name, Place(h.FrontFace + h.Front * 6f), h.center);
                float dFace = Vector2.Dot(new Vector2(r.end.x, r.end.z) - h.FrontFace, h.Front);
                Add(r, true, !r.reached && dFace > -0.2f && dFace < 1.0f, $"se detiene a {dFace:0.00} m de la fachada");
            }

            Log.AppendLine("-- Límites del mapa --");
            foreach (var (a, b) in new[] { (new Vector2(40, 5), new Vector2(60, 5)), (new Vector2(-40, -5), new Vector2(-60, -5)), (new Vector2(10, 40), new Vector2(10, 60)), (new Vector2(10, -40), new Vector2(10, -60)) })
            {
                var r = Walk($"Salir del mapa hacia ({b.x:0},{b.y:0})", Place(a), b);
                Add(r, true, !r.reached && Mathf.Abs(r.end.x) <= 45f && Mathf.Abs(r.end.z) <= 45f);
            }

            Log.AppendLine("-- Pendientes (lomas en las esquinas) --");
            {
                var r = Walk("Loma noreste hasta la cima", Place(new Vector2(24, 22)), new Vector2(34, 31));
                Add(r, false, r.reached, $"cima y esperada={L.Height(34, 31):0.00}");
                r = Walk("Loma suroeste (≈22°) hasta la cima", Place(new Vector2(-28, -26)), new Vector2(-34, -33));
                Add(r, false, r.reached, $"cima y esperada={L.Height(-34, -33):0.00}");
            }

            Log.AppendLine("-- Suelo --");
            {
                var rng = new System.Random(7); int n = 0, below = 0; float minDiff = 99f; var abs = new List<float>();
                while (n < 200)
                {
                    var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 42f, (float)(rng.NextDouble() * 2 - 1) * 42f);
                    if (L.DistToRiver(p) < 4f || L.DistToHouses(p) < 6f) continue;
                    n++; Teleport(new Vector3(p.x, L.Height(p.x, p.y) + 1.5f, p.y));
                    for (int i = 0; i < 30; i++) _cc.Move(Vector3.down * 0.05f);
                    float diff = _cc.transform.position.y - L.Height(p.x, p.y); minDiff = Mathf.Min(minDiff, diff); abs.Add(Mathf.Abs(diff)); if (diff < -0.25f) below++;
                }
                abs.Sort();
                Check(below == 0, $"200 caídas al azar: por debajo del suelo en {below}; diferencia mínima {minDiff:0.000} m, mediana |y-H| {abs[100]:0.000} m, p95 {abs[190]:0.000} m (máx {abs[199]:0.000}: caídas sobre objetos)");
            }

            Log.AppendLine("-- Sena y puntos de inicio --");
            {
                var eye = L.Spawn + Vector3.up * 1.65f; var sena = Place(new Vector2(L.SenaPos.x, L.SenaPos.z), 1.3f);
                var fwd = Quaternion.Euler(0, L.SpawnYaw, 0) * Vector3.forward;
                var to = sena - eye; float ang = Vector3.Angle(new Vector3(fwd.x, 0, fwd.z), new Vector3(to.x, 0, to.z));
                bool blocked = Physics.Linecast(eye, sena, out var hit);
                Check(ang < 40f && !blocked, $"Sena a {to.magnitude:0.0} m del spawn, a {ang:0}° del eje de la cámara (campo horizontal ±45°), línea de visión {(blocked ? "BLOQUEADA por " + hit.collider.name : "libre")}");
                var bt = new Vector3(L.Bridges[0].center.x, 0.8f, L.Bridges[0].center.y) - eye; float angB = Vector3.Angle(new Vector3(fwd.x, 0, fwd.z), new Vector3(bt.x, 0, bt.z));
                Check(angB < 40f, $"puente principal visible desde el spawn ({bt.magnitude:0.0} m, {angB:0}° del eje)");
                Check(Vector2.Distance(new Vector2(L.Spawn.x, L.Spawn.z), L.PlazaCenter) < L.PlazaRadius, "spawn dentro de la plaza (suelo llano y seguro)");
            }
            Teleport(L.Spawn + Vector3.up * 0.05f);
            return list;
        }

        // ------------------------------------------------------------------ estadísticas
        static void Stats()
        {
            int renderers = 0; long tris = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue; var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                renderers++; tris += mf.sharedMesh.triangles.Length / 3;
            }
            var counts = new SortedDictionary<string, int>(); long ct = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue; string k = c.GetType().Name; counts[k] = counts.TryGetValue(k, out var v) ? v + 1 : 1;
                if (c is MeshCollider mc && mc.sharedMesh != null) ct += mc.sharedMesh.triangles.Length / 3;
            }
            var parts = new List<string>(); foreach (var kv in counts) parts.Add(kv.Key + "=" + kv.Value);
            Log.AppendLine($"Malla visible: MeshRenderers={renderers}, triángulos={tris}");
            Log.AppendLine("Colliders activos: " + string.Join(", ", parts) + $" | triángulos en MeshColliders={ct}");
        }

        // ------------------------------------------------------------------ capturas
        static Camera NewCam(Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("TEMP_CAM"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
            go.transform.position = pos; go.transform.LookAt(look); cam.GetUniversalAdditionalCameraData().renderPostProcessing = true; return cam;
        }

        static void Shot(Camera cam, string file, int w = 1600, int h = 900)
        {
            var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; cam.Render(); cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); File.WriteAllBytes(Evidence + file, tex.EncodeToPNG());
            RenderTexture.active = old; cam.targetTexture = null; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }

        static void Take(string name, Vector3 pos, Vector3 look, float fov = 60f)
        {
            var cam = NewCam(pos, look, fov); Shot(cam, "r4_" + name + ".png"); Object.DestroyImmediate(cam.gameObject);
        }

        static Material FlatMat(Color c, bool transparent)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", c);
            if (transparent) { m.SetFloat("_Surface", 1f); m.SetOverrideTag("RenderType", "Transparent"); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
            return m;
        }

        static Vector3 Up(Vector2 p, float dy) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);

        // ------------------------------------------------------------------ 6.1.4: Sena, señales y despejes
        const string SenaName = "Sena (presencia, sin interacciones)";

        static void SenaAndSigns()
        {
            Log.AppendLine("-- Sena (animación y materiales) --");
            var sena = GameObject.Find(SenaName);
            Check(sena != null, "Sena presente en la escena (variante Sena_Proto.prefab)");
            if (sena == null) return;
            Check(sena.GetComponent<Proto614SenaGreeter>() != null, "Sena lleva Proto614SenaGreeter (giro hacia el jugador + saludo «Wave»)");
            foreach (var a in sena.GetComponentsInChildren<Animator>(true))
                Log.AppendLine($"  Animator '{a.name}': activo={a.gameObject.activeInHierarchy}, controller={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NULO")}, avatar válido={(a.avatar != null && a.avatar.isValid)}, rootMotion={a.applyRootMotion}");
            var female = sena.GetComponentInChildren<Animator>();
            Check(female != null && female.runtimeAnimatorController != null && female.avatar != null && female.avatar.isValid, "el Animator activo tiene controlador y avatar humanoide válidos");
            int violations = 0;
            foreach (var r in sena.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null) continue;
                    bool visible = r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null;
                    Log.AppendLine($"  ranura nula: '{r.name}' ranura {i} de {mats.Length} · malla={(r.sharedMesh != null ? r.sharedMesh.name : "NINGUNA")} · activo={r.gameObject.activeInHierarchy} · renderizaría={visible}");
                    if (visible) violations++;
                }
            }
            Check(violations == 0, "ninguna ranura nula pertenece a un renderer visible (activo y con malla)");
            var male = sena.transform.Find("Male_Avatar");
            Log.AppendLine("  Male_Avatar " + (male == null ? "no existe" : "activo=" + male.gameObject.activeInHierarchy));
            var h = Proto612Layout.Nearest(new Vector2(L.SenaPos.x, L.SenaPos.z), L.Paths[0]);
            float lane = 99f; foreach (var path in L.Paths) foreach (var (q, _) in Proto612Layout.Resample(path, 0.5f)) if (!InDisc(q, 0.4f)) lane = Mathf.Min(lane, Vector2.Distance(new Vector2(L.SenaPos.x, L.SenaPos.z), q));
            Check(lane > 3f, $"Sena a {lane:0.0} m del camino más cercano (no estorba)");

            Log.AppendLine("-- Señalización --");
            var signs = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name.StartsWith("Cartel_")) signs.Add(t);
            Check(signs.Count == L.Signs().Count, $"{signs.Count} postes de señalización (esperados {L.Signs().Count}): " + string.Join(", ", signs.ConvertAll(x => x.name)));
            foreach (var t in signs)
            {
                var tmps = t.GetComponentsInChildren<TMPro.TextMeshPro>(true);
                bool any = tmps.Length > 0; foreach (var tm in tmps) if (string.IsNullOrEmpty(tm.text)) any = false;
                Check(any, $"{t.name}: {tmps.Length} textos TMP ({string.Join(" / ", System.Array.ConvertAll(tmps, x => x.text))})");
            }
        }

        static bool InDisc(Vector2 p, float extra) => (p - L.PlazaCenter).magnitude < L.PlazaRadius + extra || (p - L.EsplanadeCenter).magnitude < L.EsplanadeRadius + extra;

        static float Lane(Vector2 p)
        {
            float best = 99f;
            foreach (var path in L.Paths) foreach (var (q, _) in Proto612Layout.Resample(path, 0.5f)) if (!InDisc(q, 0.4f)) best = Mathf.Min(best, Vector2.Distance(p, q));
            return best;
        }

        static void Clearance()
        {
            Log.AppendLine("-- Despejes (bancos, faroles, señales, vegetación) --");
            float Min(string prefix, System.Func<Transform, bool> pick, System.Func<Vector2, float> f, out string who)
            {
                float best = 999f; who = "";
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (pick(t)) { float v = f(new Vector2(t.position.x, t.position.z)); if (v < best) { best = v; who = t.name + $"({t.position.x:0.0},{t.position.z:0.0})"; } }
                return best;
            }
            string w;
            float b = Min("Bench", t => t.name == "Bench", Lane, out w); Check(b >= 2.4f, $"bancos: separación mínima al eje de un camino {b:0.0} m (≥2,4) · {w}");
            float l = Min("Lantern", t => t.name == "Lantern", Lane, out w); Check(l >= 1.6f, $"faroles: separación mínima {l:0.0} m (≥1,6) · {w}");
            float sgn = Min("Cartel", t => t.name.StartsWith("Cartel_") && t.parent != null && t.parent.name == "Senalizacion", Lane, out w); Check(sgn >= 1.9f, $"carteles: separación mínima {sgn:0.0} m (≥1,9) · {w}");
            // Vegetación y rocas lejos de las fachadas: ninguna a menos de 4 m del tramo de 10 m frente a cada puerta.
            float veg = 999f; string vw = "";
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent == null || t.parent.name != "Vegetacion" || t.name.StartsWith("Flower")) continue;
                var p = new Vector2(t.position.x, t.position.z);
                for (int i = 0; i < 4; i++) for (float d = 0f; d <= 10f; d += 1f) { float v = Vector2.Distance(p, L.DoorPoint(i, 0f) + L.Houses[i].Front * d); if (v < veg) { veg = v; vw = t.name + " ↔ " + L.Houses[i].name; } }
            }
            Check(veg >= 4f, $"vegetación/rocas: distancia mínima a los 10 m frente a una puerta {veg:0.0} m (≥4) · {vw}");
            int trees = 0, rocks = 0, flowers = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.parent != null && t.parent.name == "Vegetacion") { if (t.name.StartsWith("Flower")) flowers++; else if (t.name.StartsWith("Rock")) rocks++; else trees++; }
            Log.AppendLine($"  inventario: {trees} árboles, {rocks} rocas, {flowers} flores");
        }

        // ------------------------------------------------------------------ capturas
        static void Captures(List<(Result r, bool expectBlocked)> traces)
        {
            SetNight(false);
            PoseSena();
            var b1 = L.Bridges[0]; var b2 = L.Bridges[1];
            Vector3 G(Vector2 p, float dy) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);
            var sp = new Vector2(L.SenaPos.x, L.SenaPos.z);

            Take("01_general_dia", new Vector3(8, 74, -62), new Vector3(0, 0, 4), 50f);
            Take("01b_general_cenital", new Vector3(0, 100, -0.1f), Vector3.zero, 55f);
            Take("05_sector_oeste", new Vector3(8, 13, -1), new Vector3(-20, 1.5f, 0), 82f);
            Take("06_sector_este", new Vector3(-6, 13, -1), new Vector3(20, 1.5f, 0), 82f);
            Take("07_puente_principal", G(b1.center - b1.axis * 15f, 2.2f), G(b1.center, 0.7f), 62f);
            Take("08_puente_norte", G(b2.center - b2.axis * 15f, 2.2f), G(b2.center, 0.7f), 62f);
            for (int i = 0; i < 4; i++)
            {
                var h = L.Houses[i]; var f = L.DoorPoint(i, 0f);
                Take("09_entrada_" + h.name, G(f + h.Front * 7.5f, 1.65f), new Vector3(f.x, 1.5f, f.y), 62f);
            }
            Take("13_caminos_y_bancos", new Vector3(-2, 27, -23), new Vector3(-2, 0, 2), 50f);
            Take("14_primera_persona_spawn", L.Spawn + Vector3.up * 1.65f, L.Spawn + Vector3.up * 1.65f + new Vector3(20, 0, 0), 60f);
            Take("15_cartel_plaza_sector_oeste", G(L.Signs().Find(x => x.name == "Cartel_Plaza_SectorOeste").pos + new Vector2(-4.5f, 0.6f), 1.7f), G(L.Signs().Find(x => x.name == "Cartel_Plaza_SectorOeste").pos, 1.9f), 50f);
            Take("16_cartel_sector_este", G(L.Signs().Find(x => x.name == "Cartel_SectorEste").pos - b1.axis * 6f, 1.7f), G(L.Signs().Find(x => x.name == "Cartel_SectorEste").pos, 1.9f), 50f);
            Take("17_cartel_casa_" + L.Houses[1].name, G(L.Signs().Find(x => x.name == "Cartel_NumbersHouse").pos + L.Houses[1].Front * 5f, 1.7f), G(L.Signs().Find(x => x.name == "Cartel_NumbersHouse").pos, 1.9f), 50f);

            // Colisiones: recorridos de las pruebas (verde = resultado esperado) y barrera invisible en rojo translúcido.
            var temp = new List<GameObject>();
            foreach (var (r, expectBlocked) in traces)
            {
                if (r.trace.Count < 2) continue;
                var go = new GameObject("TEMP_TRACE"); var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = r.trace.Count; for (int i = 0; i < r.trace.Count; i++) lr.SetPosition(i, r.trace[i] + Vector3.up * 0.4f);
                lr.widthMultiplier = 0.45f; lr.useWorldSpace = true; lr.sharedMaterial = FlatMat((expectBlocked ? !r.reached : r.reached) ? new Color(0.1f, 0.85f, 0.25f) : new Color(1f, 0.1f, 0.1f), false); temp.Add(go);
            }
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (!mc.name.StartsWith("Rio_Barrera") || mc.sharedMesh == null) continue;
                var go = new GameObject("TEMP_BARRIER"); go.AddComponent<MeshFilter>().sharedMesh = mc.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = FlatMat(new Color(1f, 0.15f, 0.15f, 0.5f), true); temp.Add(go);
            }
            Take("18_colisiones", new Vector3(8, 74, -62), new Vector3(0, 0, 4), 50f);
            foreach (var go in temp) Object.DestroyImmediate(go);

            // Noche en el Editor (luces de farol temporales; las reales de ThemeLights se capturan en Play Mode).
            SetNight(true);
            var lamps = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "Lantern") continue;
                var go = new GameObject("TEMP_LAMP"); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.5f); l.intensity = 1.6f; l.range = 6.5f; go.transform.position = t.position + Vector3.up * 3.2f; lamps.Add(go);
            }
            Take("02_general_noche_editor", new Vector3(8, 74, -62), new Vector3(0, 0, 4), 50f);
            foreach (var go in lamps) Object.DestroyImmediate(go);
            SetNight(false);
            AnimationMode.StopAnimationMode();
        }

        /// <summary>En el Editor sin ejecutar, el Animator no evalúa y Sena se ve en T-pose: se muestrea el clip Idle solo para las capturas (no se guarda).</summary>
        static void PoseSena()
        {
            try
            {
                var sena = GameObject.Find(SenaName); var anim = sena != null ? sena.GetComponentInChildren<Animator>() : null; if (anim == null) return;
                const string fbx = "Assets/Models/SunboxGames/Avatars/Animations/Avatar_Animations.fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Armature|Idle_00");
                if (clip == null) { Log.AppendLine("  (no se encontró el clip Armature|Idle_00)"); return; }
                AnimationMode.StartAnimationMode(); AnimationMode.SampleAnimationClip(anim.gameObject, clip, 1.0f);
                Log.AppendLine("Capturas del Editor: pose Idle_00 muestreada en Sena (solo visual; no se guarda).");
            }
            catch (System.Exception e) { Log.AppendLine("  (no se pudo muestrear Idle en el Editor: " + e.Message + ")"); }
        }

        static Material _daySky; static float _daySun = -1; static Color _dayColor;
        static void SetNight(bool dark)
        {
            var sun = RenderSettings.sun;
            if (_daySun < 0) { _daySky = RenderSettings.skybox; _daySun = sun != null ? sun.intensity : 1.1f; _dayColor = sun != null ? sun.color : Color.white; }
            var set = Resources.Load<ThemeSkyboxSet>("ThemeSkyboxSet"); var night = set != null ? set.NightFor(_daySky) : null;
            RenderSettings.skybox = dark && night != null ? night : _daySky;
            if (sun != null)
            {
                sun.intensity = dark ? _daySun * 0.28f : _daySun; sun.color = dark ? _dayColor * new Color(0.55f, 0.65f, 0.95f) : _dayColor;
                var amb = sun.GetComponent<ThemeAmbient>(); if (amb != null) amb.ApplyTheme(dark);
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
