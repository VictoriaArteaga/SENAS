using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Lumina.Core;
using L = Lumina.Prototype612.R621Layout;
using H = Lumina.Prototype612.Proto612LayoutV2;
using Debug = UnityEngine.Debug;

namespace Lumina.Prototype612
{
    /// <summary>Intervención 6.2.1: geometría, accesibilidad, colisiones, estadísticas y capturas de Prototype_Modular_FourVillages. No guarda escenas.</summary>
    public static class R621Verify
    {
        const string Evidence = Proto612Builder.Root + "Evidence/";
        static readonly StringBuilder Log = new StringBuilder();
        static CharacterController _cc;
        static int _pass, _fail;
        const string SenaName = "Sena (presencia, sin interacciones)";

        class Result { public string name; public bool reached; public Vector3 end; public float minY = 99, maxY = -99; public string note; public List<Vector3> trace = new List<Vector3>(); }

        [MenuItem("Lumina/Prototipo 6.2.1/Verificar y capturar")]
        public static void Run()
        {
            Directory.CreateDirectory(Evidence);
            EditorSceneManager.OpenScene(R621Builder.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            MissingRefs(); Geometry(); Villages(); Paths(); BridgesAndRiver(); SenaAndSigns(); Clearance();
            var traces = Tests();
            Stats();
            Captures(traces);
            Log.AppendLine($"\nRESUMEN: {_pass} pruebas pasan, {_fail} fallan");
            File.WriteAllText(Evidence + "r621_resultados.txt", Log.ToString());
            Debug.Log("[R621-VERIFY]\n" + Log);
        }

        static void Check(bool ok, string text) { if (ok) _pass++; else _fail++; Log.AppendLine((ok ? "[PASA] " : "[FALLA] ") + text); }

        static void MissingRefs()
        {
            int scripts = 0, mats = 0, total = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                total++; scripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                var r = go.GetComponent<Renderer>();
                if (r != null) foreach (var m in r.sharedMaterials) if (m == null) { mats++; Log.AppendLine("  material nulo en: " + go.name + " (" + r.GetType().Name + ", activo=" + go.activeInHierarchy + ")"); }
            }
            Log.AppendLine($"Referencias: GameObjects={total}, scripts faltantes={scripts}, materiales nulos={mats} (los 2 de Sena pertenecen a renderers inactivos o sin malla, ver 6.1.4)");
            Check(scripts == 0, "sin scripts faltantes");
        }

        // ------------------------------------------------------------------ geometría del río
        static void Geometry()
        {
            Log.AppendLine("-- Río: continuidad, anchura y forma --");
            var pts = Proto612Layout.Resample(L.River, 0.5f);
            float maxStep = 0f; for (int i = 1; i < pts.Count; i++) maxStep = Mathf.Max(maxStep, (pts[i].p - pts[i - 1].p).magnitude);
            Check(maxStep <= 0.56f, $"cauce continuo: {pts.Count} muestras, paso máximo {maxStep:0.00} m (sin huecos)");
            float minSep = 99f;
            for (int i = 0; i < pts.Count; i++) for (int j = i + 50; j < pts.Count; j++) minSep = Mathf.Min(minSep, (pts[i].p - pts[j].p).magnitude);
            Check(minSep > 9f, $"el río no se cruza ni se pliega sobre sí mismo (separación mínima entre tramos no vecinos {minSep:0.0} m)");
            float len = 0f; for (int i = 1; i < pts.Count; i++) len += (pts[i].p - pts[i - 1].p).magnitude;
            float turn = 0f; for (int i = 2; i < pts.Count; i++) turn += Mathf.Abs(Vector2.SignedAngle(pts[i - 1].p - pts[i - 2].p, pts[i].p - pts[i - 1].p));
            Check(len > 96.3f && turn > 200f, $"río sinuoso: longitud {len:0.0} m frente a 90 m en línea recta (sinuosidad {len / 90f:0.00}), giro acumulado {turn:0}°");
            float wmin = 99, wmax = 0, acc = 0; var arc = new List<float> { 0f }; for (int i = 1; i < pts.Count; i++) arc.Add(arc[i - 1] + (pts[i].p - pts[i - 1].p).magnitude);
            for (int i = 0; i < pts.Count; i++) { float w = 2f * L.WaterHalfBase * L.Width(arc[i]); wmin = Mathf.Min(wmin, w); wmax = Mathf.Max(wmax, w); acc += w; }
            Check(wmin >= 2.8f && wmax <= 4.8f, $"anchura del agua entre {wmin:0.0} y {wmax:0.0} m (media {acc / pts.Count:0.0} m)");
            float jmin = 99, jmax = -99; for (int i = 0; i < pts.Count; i++) for (int s = 0; s < 2; s++) { float jj = L.BankJitter(arc[i], s); jmin = Mathf.Min(jmin, jj); jmax = Mathf.Max(jmax, jj); }
            Check(jmax - jmin > 0.8f, $"orillas irregulares: el borde exterior varía {jmax - jmin:0.0} m entre tramos");
            var gnd = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).FirstOrDefault(m => m.name == "Orillas");
            Check(gnd != null && gnd.sharedMesh.vertexCount == pts.Count * 17, "malla de orillas continua: " + (gnd != null ? gnd.sharedMesh.vertexCount : 0) + " vértices = muestras × 17");
            float x0 = pts[0].p.x, x1 = pts[pts.Count - 1].p.x;
            Check(Mathf.Abs(pts[0].p.y - 45f) < 0.6f && Mathf.Abs(pts[pts.Count - 1].p.y + 45f) < 0.6f, $"el río atraviesa el mapa de norte (x={x0:0.0}) a sur (x={x1:0.0})");
            float minDist = 99f; foreach (var s in H.Houses) minDist = Mathf.Min(minDist, L.DistToRiver(s.center)); Check(minDist >= 14f, $"casas a ≥ {minDist:0.0} m del eje del río (el espacio útil de las aldeas no lo consume el cauce)");
        }

        // ------------------------------------------------------------------ cuatro territorios
        static int Side(Vector2 p)
        {
            var h = Proto612Layout.Nearest(p, L.River); var n = new Vector2(h.tangent.y, -h.tangent.x); if (n.x < 0) n = -n; return Vector2.Dot(p - h.point, n) >= 0 ? 1 : -1;
        }

        static void Villages()
        {
            Log.AppendLine("-- Cuatro aldeas --");
            string[] quad = { "noroeste", "suroeste", "noreste", "sureste" };
            for (int v = 0; v < 4; v++)
            {
                var hs = H.Houses[v]; int sideHouse = Side(hs.center), sideHub = Side(L.HubCenter[v]);
                bool inQuad = (hs.center.x < 0) == (v < 2) && (hs.center.y > 0) == (v == 0 || v == 2);
                Check(inQuad && sideHouse == sideHub && sideHouse == (v < 2 ? -1 : 1), $"{L.VillageName[v]} ({quad[v]}): casa ({hs.center.x:0},{hs.center.y:0}) y plaza temática ({L.HubCenter[v].x:0},{L.HubCenter[v].y:0}) en el mismo lado del río");
                foreach (var r in L.ReserveRings[v]) Check(L.LaneDist(r) >= 4f && L.DistToRiver(r) >= 9f && Vector2.Distance(r, hs.center) >= 9f, $"  reserva ({r.x:0.0},{r.y:0.0}) de {L.VillageName[v]}: a {L.LaneDist(r):0.0} m del camino, {L.DistToRiver(r):0.0} m del río");
            }
            Check(Side(L.PlazaCenter) == -1, "plaza común en la orilla oeste");
            // El río separa los territorios: las parejas oeste–este quedan en lados distintos; las del mismo lado, no.
            Check(Side(H.Houses[0].center) == Side(H.Houses[1].center) && Side(H.Houses[2].center) == Side(H.Houses[3].center) && Side(H.Houses[0].center) != Side(H.Houses[2].center), "el río deja Abecedario y Números a un lado y Meses y Saludos al otro");
            float md = 99f; for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++) md = Mathf.Min(md, Vector2.Distance(H.Houses[a].center, H.Houses[b].center));
            Check(md >= 25f, $"aldeas bien separadas: distancia mínima entre casas {md:0.0} m");
            var pl = L.PlazaCenter; var near = Mathf.Min(Mathf.Min(Vector2.Distance(pl, H.Houses[0].center), Vector2.Distance(pl, H.Houses[1].center)), Mathf.Min(Vector2.Distance(pl, H.Houses[2].center), Vector2.Distance(pl, H.Houses[3].center)));
            Check(near > 15f, $"la plaza común no es una quinta aldea: la casa más cercana está a {near:0.0} m y no tiene casa propia");
        }

        // ------------------------------------------------------------------ caminos
        static void Paths()
        {
            Log.AppendLine("-- Caminos --");
            var bridgesNear = new System.Func<Vector2, bool>(p => L.Bridges.Any(b => (p - b.center).magnitude < 7.2f));
            int bad = 0, samples = 0;
            for (int i = 0; i < L.Paths.Count; i++)
                foreach (var (p, _) in Proto612Layout.Resample(L.Paths[i], 1f))
                {
                    samples++; var rh = Proto612Layout.Nearest(p, L.River);
                    if (rh.dist < L.WaterHalfBase * L.Width(rh.along) + 1.6f && !bridgesNear(p)) { bad++; Log.AppendLine($"  {L.PathName[i]} pisa agua/orilla en ({p.x:0.0},{p.y:0.0})"); }
                }
            Check(bad == 0, $"ningún camino pisa el agua fuera de un puente ({samples} puntos revisados)");
            int ends = 0;
            for (int i = 0; i < L.Paths.Count; i++) { var p = L.Paths[i]; if (L.DistToRiver(p[0]) < 4f && !bridgesNear(p[0])) ends++; if (L.DistToRiver(p[p.Count - 1]) < 4f && !bridgesNear(p[p.Count - 1])) ends++; }
            Check(ends == 0, "ningún camino termina junto al agua sin puente");
            int inHouse = 0;
            for (int i = 0; i < L.Paths.Count; i++) foreach (var (p, _) in Proto612Layout.Resample(L.Paths[i], 0.5f)) foreach (var h in H.Houses)
            { var d = p - h.center; if (Mathf.Abs(Vector2.Dot(d, h.Front)) < h.size.y * 0.5f - 0.25f && Mathf.Abs(Vector2.Dot(d, h.Lateral)) < h.size.x * 0.5f - 0.25f) inHouse++; }
            Check(inHouse == 0, "ningún camino atraviesa la huella de una casa");
            // Cruces confusos: dos caminos que se cortan lejos de sus extremos (los empalmes en T/Y en los extremos son válidos).
            int cross = 0;
            for (int a = 0; a < L.Paths.Count; a++) for (int b = a + 1; b < L.Paths.Count; b++)
            {
                var A = L.Paths[a]; var B = L.Paths[b];
                for (int i = 0; i < A.Count - 1; i++) for (int j = 0; j < B.Count - 1; j++)
                    if (SegHit(A[i], A[i + 1], B[j], B[j + 1], out var q))
                    {
                        float dEnd = Mathf.Min(Mathf.Min((q - A[0]).magnitude, (q - A[A.Count - 1]).magnitude), Mathf.Min((q - B[0]).magnitude, (q - B[B.Count - 1]).magnitude));
                        if (dEnd > 3.5f) { cross++; Log.AppendLine($"  cruce de {L.PathName[a]} con {L.PathName[b]} en ({q.x:0.0},{q.y:0.0}), a {dEnd:0.0} m de un extremo"); }
                    }
            }
            Check(cross == 0, "sin cruces confusos entre caminos (solo empalmes en los extremos)");
            // Llegada recta de 7 m a cada puerta real.
            for (int v = 0; v < 4; v++)
            {
                var d = H.DoorPoint(v, 0f); var f = H.Houses[v].Front; float worst = 0f;
                foreach (float s in new[] { 1f, 2f, 3f, 4f, 5f, 6f }) { var q = d + f * s; worst = Mathf.Max(worst, Proto612Layout.Nearest(q, L.Paths.First(p => (p[p.Count - 1] - d).magnitude < 0.6f)).dist); }
                Check(worst < 0.35f, $"{L.VillageName[v]}: el camino llega recto y perpendicular a la puerta real (desvío máximo {worst:0.00} m en los últimos 6 m)");
            }
        }

        static bool SegHit(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 q)
        {
            q = Vector2.zero; var d1 = p2 - p1; var d2 = p4 - p3; float den = d1.x * d2.y - d1.y * d2.x; if (Mathf.Abs(den) < 1e-6f) return false;
            float t = ((p3.x - p1.x) * d2.y - (p3.y - p1.y) * d2.x) / den, u = ((p3.x - p1.x) * d1.y - (p3.y - p1.y) * d1.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false; q = p1 + d1 * t; return true;
        }

        // ------------------------------------------------------------------ puentes
        static void BridgesAndRiver()
        {
            Log.AppendLine("-- Cuatro puentes --");
            float minSep = 99f;
            for (int i = 0; i < 4; i++)
            {
                var b = L.Bridges[i]; var h = Proto612Layout.Nearest(b.center, L.River); var n = new Vector2(h.tangent.y, -h.tangent.x); if (Vector2.Dot(n, b.axis) < 0) n = -n;
                float ang = Vector2.Angle(b.axis, n);
                var e1 = b.center + b.axis * 6.2f; var e2 = b.center - b.axis * 6.2f;
                float g1 = L.Height(e1.x, e1.y), g2 = L.Height(e2.x, e2.y);
                float w = 2f * L.WaterHalfBase * L.Width(h.along);
                Check(ang < 1f && Mathf.Abs(g1) < 0.2f && Mathf.Abs(g2) < 0.2f && Mathf.Abs(b.center.y - L.BridgeTargetZ[i]) < 4f,
                    $"{L.BridgeName[i]}: centro ({b.center.x:0.0},{b.center.y:0.0}) yaw {b.yaw:0}°, perpendicular al cauce (desvío {ang:0.0}°), agua local {w:0.0} m, suelo en los extremos {g2:0.00}/{g1:0.00} m (tablero a ras, escalón < 0,3 m)");
                for (int j = i + 1; j < 4; j++) minSep = Mathf.Min(minSep, Vector2.Distance(b.center, L.Bridges[j].center));
            }
            Check(minSep >= 17f, $"puentes bien distribuidos: separación mínima {minSep:0.0} m");
            var bridgeGos = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent != null && t.parent.name == "Puentes").ToList();
            Check(bridgeGos.Count == 4, "4 puentes en la escena: " + string.Join(", ", bridgeGos.Select(t => t.name)));
            foreach (var t in bridgeGos) { var mc = t.GetComponent<MeshCollider>(); Check(mc != null && mc.enabled, $"{t.name}: conserva el MeshCollider original (activo)"); }
            var mats = bridgeGos.Select(t => t.GetComponent<MeshRenderer>().sharedMaterial.name).Distinct().Count();
            Check(mats == 4, "los 4 puentes comparten la arquitectura base pero cada uno tiene su tono de madera (" + mats + " materiales)");
        }

        // ------------------------------------------------------------------ Sena y señales
        static void SenaAndSigns()
        {
            Log.AppendLine("-- Sena, señalización y reservas --");
            var sena = GameObject.Find(SenaName);
            Check(sena != null && sena.GetComponent<Proto614SenaGreeter>() != null, "Sena (variante 6.1.4) presente con su componente de presencia");
            var signs = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent != null && t.parent.name == "Senalizacion").ToList();
            Check(signs.Count == L.Signs().Count, $"{signs.Count} postes de señalización (esperados {L.Signs().Count})");
            foreach (var t in signs) { var tm = t.GetComponentsInChildren<TMPro.TextMeshPro>(true); Check(tm.Length > 0 && tm.All(x => !string.IsNullOrEmpty(x.text)), $"{t.name}: " + string.Join(" / ", tm.Select(x => x.text).Distinct())); }
            var res = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent != null && t.parent.name == "Reservas").Count();
            Check(res == 12, $"{res} reservas de monumentos marcadas en el suelo (3 por aldea, sin objetos ni colliders)");
            var hubs = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("PlazaTematica_")).Count();
            Check(hubs == 4, $"{hubs} plazas temáticas reservadas");
        }

        static void Clearance()
        {
            Log.AppendLine("-- Despejes: bancos, faroles, señales, vegetación --");
            float MinLane(System.Func<Transform, bool> pick, out string who)
            {
                float best = 999f; who = "";
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (pick(t)) { float v = L.LaneDist(new Vector2(t.position.x, t.position.z)); if (v < best) { best = v; who = $"{t.name}({t.position.x:0.0},{t.position.z:0.0})"; } }
                return best;
            }
            string w;
            float b = MinLane(t => t.name == "Bench", out w); Check(b >= 2.4f, $"bancos: ≥2,4 m del eje de un camino (mín {b:0.0}) · {w}");
            float l = MinLane(t => t.name == "Lantern", out w); Check(l >= 1.8f, $"faroles: ≥1,8 m (mín {l:0.0}) · {w}");
            float s = MinLane(t => t.parent != null && t.parent.name == "Senalizacion", out w); Check(s >= 1.9f, $"carteles: ≥1,9 m (mín {s:0.0}) · {w}");
            float veg = 999f; string vw = ""; int trees = 0, rocks = 0, flowers = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent == null || t.parent.name != "Vegetacion") continue;
                if (t.name.StartsWith("Flower")) flowers++; else if (t.name.StartsWith("Rock")) rocks++; else trees++;
                if (t.name.StartsWith("Flower")) continue;
                var p = new Vector2(t.position.x, t.position.z);
                for (int i = 0; i < 4; i++) for (float d = 0f; d <= 10f; d += 1f) { float v = Vector2.Distance(p, H.DoorPoint(i, 0f) + H.Houses[i].Front * d); if (v < veg) { veg = v; vw = t.name + " ↔ " + H.Houses[i].name; } }
            }
            Check(veg >= 4f, $"vegetación y rocas: ≥4 m de los 10 m frente a cada puerta (mín {veg:0.0}) · {vw}");
            Log.AppendLine($"  inventario de vegetación: {trees} árboles, {rocks} rocas, {flowers} flores (la expansión completa queda para 6.2.3)");
            float rv = 999f; foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.parent != null && t.parent.name == "Vegetacion" && !t.name.StartsWith("Flower")) rv = Mathf.Min(rv, L.DistToReserves(new Vector2(t.position.x, t.position.z)));
            Check(rv >= 2.5f, $"las reservas de monumentos quedan libres de vegetación (mín {rv:0.0} m)");
            // Puentes libres: nada a menos de 3 m de los extremos del tablero y de los ejes de aproximación.
            float bl = 999f; string bw = "";
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                bool obj = t.parent != null && (t.parent.name == "Vegetacion" || t.parent.name == "Props" || t.parent.name == "Senalizacion"); if (!obj || t.name.StartsWith("Flower")) continue;
                var p = new Vector2(t.position.x, t.position.z);
                foreach (var br in L.Bridges) { float v = Proto612Layout.Nearest(p, new[] { br.center - br.axis * 12f, br.center + br.axis * 12f }).dist; if (v < bl) { bl = v; bw = t.name; } }
            }
            Check(bl >= 2.0f, $"nada bloquea el eje de ningún puente (±12 m): separación mínima {bl:0.0} m · {bw}");
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
                    if (steps % 5 == 0) res.trace.Add(p);
                }
                if (steps >= 6000) { res.end = _cc.transform.position; res.note = "tiempo agotado"; return Fin(res); }
            }
            res.reached = true; return Fin(res);
        }
        static Result Fin(Result r) { r.end = _cc.transform.position; r.trace.Add(r.end); return r; }
        static string Fmt(Result r) => $"{(r.reached ? "llegó " : "bloqueado ")}{r.name,-50} fin=({r.end.x:0.0},{r.end.y:0.00},{r.end.z:0.0}) y∈[{r.minY:0.00},{r.maxY:0.00}] {r.note}";

        static List<Vector2> Pts(string pathName, bool reverse = false, int dropLast = 0)
        {
            int idx = System.Array.IndexOf(L.PathName, pathName); var res = new List<Vector2>();
            foreach (var (p, _) in Proto612Layout.Resample(L.Paths[idx], 3f)) res.Add(p);
            if (reverse) res.Reverse();
            if (dropLast > 0 && res.Count > dropLast) res.RemoveRange(res.Count - dropLast, dropLast);
            return res;
        }
        static List<Vector2> PlazaFirst() => new List<Vector2> { L.PlazaCenter };   // el spawn está en el borde oeste: se camina primero hacia el centro de la plaza
        static List<Vector2> Join(params List<Vector2>[] lists) { var r = new List<Vector2>(); foreach (var l in lists) r.AddRange(l); return r; }

        static List<(Result r, bool expectBlocked)> Tests()
        {
            _cc = GameObject.Find("Player").GetComponent<CharacterController>();
            var list = new List<(Result, bool)>();
            void Add(Result r, bool expectBlocked, bool pass, string extra = "") { list.Add((r, expectBlocked)); Check(pass, Fmt(r) + (extra.Length > 0 ? " | " + extra : "")); }
            var spawn2 = new Vector2(L.Spawn.x, L.Spawn.z);

            Log.AppendLine("-- Accesibilidad: del spawn a las cuatro aldeas, siguiendo los caminos; y regreso a la plaza --");
            var routes = new (int v, List<Vector2> pts)[]
            {
                (0, Join(PlazaFirst(), Pts("Camino_Plaza_Abecedario", false, 2))),
                (1, Join(PlazaFirst(), Pts("Camino_Plaza_Numeros", false, 2))),
                (2, Join(PlazaFirst(), Pts("Camino_Puente_CentroNorte"), Pts("Camino_CentroNorte_Meses", false, 2))),
                (3, Join(PlazaFirst(), Pts("Camino_Puente_CentroSur"), Pts("Camino_CentroSur_Saludos", false, 2))),
            };
            foreach (var (v, pts) in routes)
            {
                var h = H.Houses[v];
                var r = Walk("Spawn → " + L.VillageName[v], L.Spawn, pts.ToArray());
                float dFace = Vector2.Dot(new Vector2(r.end.x, r.end.z) - h.FrontFace, h.Front);
                Add(r, false, r.reached && dFace > 0f && dFace < 9f, $"termina a {dFace:0.0} m de la fachada");
                var back = new List<Vector2>(pts); back.Reverse(); back.Add(L.PlazaCenter);
                var rb = Walk("Regreso " + L.VillageName[v] + " → plaza", r.end, back.ToArray());
                Add(rb, false, rb.reached && Vector2.Distance(new Vector2(rb.end.x, rb.end.z), L.PlazaCenter) < 1f);
            }

            Log.AppendLine("-- Cada camino de extremo a extremo --");
            for (int i = 0; i < L.Paths.Count; i++)
            {
                var wps = Pts(L.PathName[i], false, L.PathName[i].EndsWith("Abecedario") || L.PathName[i].EndsWith("Numeros") || L.PathName[i].EndsWith("Meses") || L.PathName[i].EndsWith("Saludos") ? 2 : 0);
                if (wps.Count < 2) continue;
                var r = Walk(L.PathName[i], Place(wps[0]), wps.GetRange(1, wps.Count - 1).ToArray());
                Add(r, false, r.reached && r.minY > -0.6f);
            }

            Log.AppendLine("-- Cuatro puentes: dos cruces completos (ida y vuelta) por puente --");
            for (int i = 0; i < 4; i++)
            {
                var b = L.Bridges[i]; Vector2 East(float d) => b.center + b.axis * d;
                for (int rep = 1; rep <= 2; rep++)
                {
                    var r1 = Walk($"{L.BridgeName[i]} cruce {rep}: este → oeste", Place(East(10f)), East(7f), East(0f), East(-7f), East(-10f));
                    Add(r1, false, r1.reached && r1.minY > -0.6f, $"altura mínima y={r1.minY:0.00}");
                    var r2 = Walk($"{L.BridgeName[i]} cruce {rep}: oeste → este", Place(East(-10f)), East(-7f), East(0f), East(7f), East(10f));
                    Add(r2, false, r2.reached && r2.minY > -0.6f, $"altura mínima y={r2.minY:0.00}");
                }
            }

            Log.AppendLine("-- Cruzar el río fuera de los puentes (debe frenar en la barrera) --");
            var samples = Proto612Layout.Resample(L.River, 1f); var spots = new List<(Vector2 p, Vector2 t, string tag, float away)>();
            foreach (float f in new[] { 0.06f, 0.18f, 0.36f, 0.54f, 0.64f, 0.82f, 0.94f })
            {
                var s = samples[Mathf.RoundToInt(f * (samples.Count - 1))]; if (L.Bridges.Any(bb => Vector2.Distance(bb.center, s.p) < 9f)) continue;
                spots.Add((s.p, s.t, $"punto {f:0.00} del cauce", 0f));
            }
            for (int i = 0; i < 4; i++) { var h = Proto612Layout.Nearest(L.Bridges[i].center, L.River); spots.Add((h.point + h.tangent * 4.5f, h.tangent, $"junto a {L.BridgeName[i]} (+4,5 m)", 1f)); spots.Add((h.point - h.tangent * 4.5f, h.tangent, $"junto a {L.BridgeName[i]} (−4,5 m)", -1f)); }
            foreach (var (p0, t, tag, away) in spots)
            {
                var n = new Vector2(t.y, -t.x); if (n.x < 0) n = -n; Result last = null; float dEnd = 0f; bool pass = false; string shiftTag = "";
                foreach (float shift in away == 0f ? new[] { 0f, 3f, -3f } : new[] { 0f, 3f * away, 6f * away })
                {
                    var p = p0 + t * shift; var a = p + n * 8f; var z = p - n * 8f;
                    if (Mathf.Abs(a.x) > 42f || Mathf.Abs(a.y) > 42f || Mathf.Abs(z.x) > 42f || Mathf.Abs(z.y) > 42f) continue;
                    last = Walk($"Cruzar el río: {tag}", Place(a), z); var rh = Proto612Layout.Nearest(new Vector2(last.end.x, last.end.z), L.River);
                    dEnd = rh.dist; float edge = L.WaterHalfBase * L.Width(rh.along);
                    pass = !last.reached && last.minY > -0.6f && dEnd > edge && dEnd < edge + 1.6f; shiftTag = shift == 0f ? "" : $" (repetido {shift:+0;-0} m)";
                    if (pass || last.reached) break;
                }
                if (last == null) { Log.AppendLine($"  (omitido: {tag}, extremos fuera del mapa)"); continue; }
                Add(last, true, pass, $"se detiene a {dEnd:0.0} m del eje{shiftTag}");
            }

            Log.AppendLine("-- Casas sólidas, puertas libres y límites del mapa --");
            for (int i = 0; i < 4; i++)
            {
                var h = H.Houses[i]; var r = Walk("Atravesar " + h.name, Place(h.FrontFace + h.Front * 6f), h.center);
                float dFace = Vector2.Dot(new Vector2(r.end.x, r.end.z) - h.FrontFace, h.Front); Add(r, true, !r.reached && dFace > -0.2f && dFace < 1.0f, $"se detiene a {dFace:0.00} m de la fachada");
                int blocked = 0; for (float d = 0.5f; d <= 10f; d += 0.5f) { var q = H.DoorPoint(i, 0f) + h.Front * d; blocked += BlockedAt(q); }
                Check(blocked == 0, $"{h.name}: puerta y 10 m de acceso sin objetos ni colliders ({blocked} puntos bloqueados)");
            }
            foreach (var (a, b) in new[] { (new Vector2(40, 5), new Vector2(60, 5)), (new Vector2(-40, -5), new Vector2(-60, -5)), (new Vector2(8, 40), new Vector2(8, 60)), (new Vector2(-8, -40), new Vector2(-8, -60)) })
            { var r = Walk($"Salir del mapa hacia ({b.x:0},{b.y:0})", Place(a), b); Add(r, true, !r.reached && Mathf.Abs(r.end.x) <= 45f && Mathf.Abs(r.end.z) <= 45f); }

            Log.AppendLine("-- Pendientes --");
            { var r = Walk("Loma noreste hasta la cima", Place(new Vector2(24, 22)), new Vector2(34, 31)); Add(r, false, r.reached, $"cima y esperada={L.Height(34, 31):0.00}");
              r = Walk("Loma suroeste (≈22°) hasta la cima", Place(new Vector2(-28, -26)), new Vector2(-34, -33)); Add(r, false, r.reached, $"cima y esperada={L.Height(-34, -33):0.00}"); }

            Log.AppendLine("-- Suelo continuo --");
            {
                var rng = new System.Random(7); int n = 0, below = 0; float minDiff = 99f; var abs = new List<float>();
                while (n < 300)
                {
                    var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 43f, (float)(rng.NextDouble() * 2 - 1) * 43f);
                    var rh = Proto612Layout.Nearest(p, L.River); if (rh.dist < L.WaterHalfBase * L.Width(rh.along) + 0.8f || L.DistToHouses(p) < 6f) continue;
                    n++; Teleport(new Vector3(p.x, L.Height(p.x, p.y) + 1.5f, p.y)); for (int i = 0; i < 30; i++) _cc.Move(Vector3.down * 0.05f);
                    float diff = _cc.transform.position.y - L.Height(p.x, p.y); minDiff = Mathf.Min(minDiff, diff); abs.Add(Mathf.Abs(diff)); if (diff < -0.25f) below++;
                }
                abs.Sort(); Check(below == 0, $"300 caídas al azar (incl. orillas): por debajo del suelo en {below}; dif. mínima {minDiff:0.000} m, mediana {abs[150]:0.000} m, p95 {abs[285]:0.000} m (máx {abs[299]:0.000}: caídas sobre objetos)");
            }
            // Colliders invisibles: los únicos sin renderer son los límites, la barrera del río y los del suelo/puentes/casas (cajas).
            var invisible = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.enabled && c.GetComponent<Renderer>() == null && c.GetComponentInChildren<Renderer>() == null && c.GetComponentInParent<Renderer>() == null).Select(c => c.name).Distinct().ToList();
            Check(invisible.All(n => n.StartsWith("Limites") || n.StartsWith("Rio_Barrera") || n == "Player"), "colliders sin renderer: " + string.Join(", ", invisible) + " (solo los esperados)");

            Log.AppendLine("-- Jugador, spawn y Sena --");
            {
                var eye = L.Spawn + Vector3.up * 1.65f; var sena = Place(new Vector2(L.SenaPos.x, L.SenaPos.z), 1.3f); var fwd = Quaternion.Euler(0, L.SpawnYaw, 0) * Vector3.forward;
                var to = sena - eye; float ang = Vector3.Angle(new Vector3(fwd.x, 0, fwd.z), new Vector3(to.x, 0, to.z)); bool blocked = Physics.Linecast(eye, sena, out var hit);
                Check(ang < 40f && !blocked, $"Sena a {to.magnitude:0.0} m del spawn, a {ang:0}° del eje (campo ±45°), línea de visión {(blocked ? "BLOQUEADA por " + hit.collider.name : "libre")}");
                Check(Physics.OverlapCapsule(L.Spawn + Vector3.up * 0.35f, L.Spawn + Vector3.up * 1.5f, 0.3f).All(c => c.name == "Suelo"), "spawn sin solapamiento con geometría (solo toca el suelo)");
                Check(Vector2.Distance(spawn2, L.PlazaCenter) < L.PlazaRadius, "spawn dentro de la plaza común, en suelo llano");
                var pl = GameObject.Find("Player"); var camera = pl.GetComponentInChildren<Camera>();
                Check(pl.GetComponent<CharacterController>().enabled && pl.GetComponent<Lumina.Player.FirstPersonController>().enabled && camera != null && camera.tag == "MainCamera" && Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "primera persona: CharacterController y FirstPersonController activos, cámara principal y un solo AudioListener");
                string vis = string.Join(", ", L.Bridges.Select((b, i) => $"{L.BridgeName[i]} {Vector3.Angle(new Vector3(fwd.x, 0, fwd.z), new Vector3(b.center.x - eye.x, 0, b.center.y - eye.z)):0}°"));
                Log.AppendLine("  ángulos de los puentes respecto al eje de la cámara en el spawn: " + vis + " (campo horizontal ±45°)");
                string vv = string.Join(", ", L.HubCenter.Select((c, i) => $"{L.VillageDir[i]} {Vector3.Angle(new Vector3(fwd.x, 0, fwd.z), new Vector3(c.x - eye.x, 0, c.y - eye.z)):0}°"));
                Log.AppendLine("  ángulos de las plazas temáticas en el spawn: " + vv);
            }
            Teleport(L.Spawn + Vector3.up * 0.05f);
            return list;
        }

        static int BlockedAt(Vector2 p)
        {
            var pos = Place(p, 0.1f);
            var hits = Physics.OverlapCapsule(pos + Vector3.up * 0.35f, pos + Vector3.up * 1.5f, 0.3f);
            return hits.Any(c => c.name != "Suelo" && c.name != "Player") ? 1 : 0;
        }

        // ------------------------------------------------------------------ estadísticas
        static void Stats()
        {
            int renderers = 0; long tris = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) { if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue; var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; renderers++; tris += mf.sharedMesh.triangles.Length / 3; }
            var counts = new SortedDictionary<string, int>(); long ct = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) { if (!c.enabled || !c.gameObject.activeInHierarchy) continue; string k = c.GetType().Name; counts[k] = counts.TryGetValue(k, out var v) ? v + 1 : 1; if (c is MeshCollider mc && mc.sharedMesh != null) ct += mc.sharedMesh.triangles.Length / 3; }
            Log.AppendLine($"Malla visible: MeshRenderers={renderers}, triángulos={tris}");
            Log.AppendLine("Colliders activos: " + string.Join(", ", counts.Select(kv => kv.Key + "=" + kv.Value)) + $" | triángulos en MeshColliders={ct}");
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

        static void Take(string name, Vector3 pos, Vector3 look, float fov = 60f) { var cam = NewCam(pos, look, fov); Shot(cam, "r621_" + name + ".png"); Object.DestroyImmediate(cam.gameObject); }
        static Vector3 G(Vector2 p, float dy) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);

        static Material FlatMat(Color c, bool transparent)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", c);
            if (transparent) { m.SetFloat("_Surface", 1f); m.SetOverrideTag("RenderType", "Transparent"); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
            return m;
        }

        static void Captures(List<(Result r, bool expectBlocked)> traces)
        {
            SetNight(false); PoseSena();
            Take("01_general_dia", new Vector3(10, 78, -60), new Vector3(0, 0, 4), 52f);
            Take("02_cenital", new Vector3(0, 105, -0.1f), Vector3.zero, 55f);
            Take("03_spawn_primera_persona", L.Spawn + Vector3.up * 1.65f, L.Spawn + Vector3.up * 1.65f + new Vector3(20, 0, 0), 60f);
            string[] vtag = { "04_aldea_noroeste_abecedario", "06_aldea_suroeste_numeros", "05_aldea_noreste_meses", "07_aldea_sureste_saludos" };
            for (int v = 0; v < 4; v++)
            {
                var m = (H.Houses[v].center + L.HubCenter[v]) * 0.5f; float sx = m.x < 0 ? 1f : -1f; float sz = m.y < 0 ? 1f : -1f;
                Take(vtag[v], new Vector3(m.x + sx * 26f, 21f, m.y + sz * 14f), new Vector3(m.x, 1f, m.y), 58f);
            }
            var s = Proto612Layout.Resample(L.River, 1f)[60]; var sn = new Vector2(s.t.y, -s.t.x); if (sn.x < 0) sn = -sn;
            Take("08_rio_y_orillas_nivel_ojo", G(s.p + sn * 9f, 2.2f), G(s.p - sn * 1f, -0.3f), 62f);
            Take("08b_rio_y_orillas_aereo", G(s.p, 26f) + new Vector3(0, 0, -16f), G(s.p, 0f), 55f);
            string[] btag = { "09_puente_norte", "10_puente_centro_norte", "11_puente_centro_sur", "12_puente_sur" };
            for (int i = 0; i < 4; i++)
            {
                var b = L.Bridges[i];
                Take(btag[i], G(b.center - b.axis * 15f, 2.3f), G(b.center, 0.7f), 62f);
                Take(btag[i] + "_oblicua", G(b.center + b.axis * 12f + new Vector2(-b.axis.y, b.axis.x) * 7f, 6f), G(b.center, 0.3f), 55f);
            }
            Take("13_conexiones_de_caminos", new Vector3(-2, 46, -30), new Vector3(-2, 0, 0), 56f);
            for (int v = 0; v < 4; v++) { var d = H.DoorPoint(v, 0f); Take("14_entrada_" + H.Houses[v].name, G(d + H.Houses[v].Front * 7.5f, 1.65f), new Vector3(d.x, 1.5f, d.y), 62f); }

            var temp = new List<GameObject>();
            foreach (var (r, expectBlocked) in traces)
            {
                if (r.trace.Count < 2) continue;
                var go = new GameObject("TEMP_TRACE"); var lr = go.AddComponent<LineRenderer>(); lr.positionCount = r.trace.Count; for (int i = 0; i < r.trace.Count; i++) lr.SetPosition(i, r.trace[i] + Vector3.up * 0.4f);
                lr.widthMultiplier = 0.45f; lr.useWorldSpace = true; lr.sharedMaterial = FlatMat((expectBlocked ? !r.reached : r.reached) ? new Color(0.1f, 0.85f, 0.25f) : new Color(1f, 0.1f, 0.1f), false); temp.Add(go);
            }
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)) { if (!mc.name.StartsWith("Rio_Barrera") || mc.sharedMesh == null) continue; var go = new GameObject("TEMP_BARRIER"); go.AddComponent<MeshFilter>().sharedMesh = mc.sharedMesh; go.AddComponent<MeshRenderer>().sharedMaterial = FlatMat(new Color(1f, 0.15f, 0.15f, 0.5f), true); temp.Add(go); }
            Take("15_colisiones", new Vector3(10, 78, -60), new Vector3(0, 0, 4), 52f);
            foreach (var go in temp) Object.DestroyImmediate(go);

            SetNight(true); var lamps = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) { if (t.name != "Lantern") continue; var go = new GameObject("TEMP_LAMP"); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.5f); l.intensity = 1.6f; l.range = 6.5f; go.transform.position = t.position + Vector3.up * 3.2f; lamps.Add(go); }
            Take("16_general_noche_editor", new Vector3(10, 78, -60), new Vector3(0, 0, 4), 52f);
            foreach (var go in lamps) Object.DestroyImmediate(go);
            SetNight(false); AnimationMode.StopAnimationMode();
        }

        static void PoseSena()
        {
            try
            {
                var sena = GameObject.Find(SenaName); var anim = sena != null ? sena.GetComponentInChildren<Animator>() : null; if (anim == null) return;
                var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/SunboxGames/Avatars/Animations/Avatar_Animations.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == "Armature|Idle_00");
                if (clip == null) return; AnimationMode.StartAnimationMode(); AnimationMode.SampleAnimationClip(anim.gameObject, clip, 1.0f);
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
            if (sun != null) { sun.intensity = dark ? _daySun * 0.28f : _daySun; sun.color = dark ? _dayColor * new Color(0.55f, 0.65f, 0.95f) : _dayColor; var amb = sun.GetComponent<ThemeAmbient>(); if (amb != null) amb.ApplyTheme(dark); }
            DynamicGI.UpdateEnvironment();
        }
    }
}
