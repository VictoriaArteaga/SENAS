using System.Collections.Generic;
using UnityEngine;
using Houses = Lumina.Prototype612.Proto612LayoutV2;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.1: cuatro aldeas alrededor de un río natural con cuatro puentes.
    ///   Noroeste = Abecedario · Noreste = Meses · Suroeste = Números · Sureste = Saludos. La plaza común está en la orilla oeste.
    /// Casas, puertas (medidas de los prefabs de producción) y utilidades geométricas se reutilizan de Proto612LayoutV2 / Proto612Layout
    /// sin modificarlas; todo lo demás (río, puentes, caminos, aldeas, reservas, señales) es nuevo y exclusivo de 6.2.1.
    /// </summary>
    public static class R621Layout
    {
        public const float Half = 45f;
        public const float WaterY = -0.35f;
        public const float PathHalfWidth = 1.25f;
        public const float PlazaRadius = 6.5f, HubRadius = 4.2f;
        public const float BridgeGap = 2.3f;
        public const float WaterHalfBase = 1.75f;       // semianchura del agua con multiplicador 1
        public static readonly Vector2 PlazaCenter = new Vector2(-10f, -2f);
        public static readonly Vector3 Spawn = new Vector3(-15.5f, 0.1f, -2.5f);
        public const float SpawnYaw = 90f;
        public static readonly Vector3 SenaPos = new Vector3(-7f, 0f, -3.3f);

        // Aldeas: índices alineados con Proto612LayoutV2.Houses (0 Alphabet NO, 1 Numbers SO, 2 Months NE, 3 Greetings SE).
        public static readonly string[] VillageName = { "Aldea del Abecedario", "Aldea de los Números", "Aldea de los Meses", "Aldea de los Saludos" };
        public static readonly string[] VillageDir = { "Noroeste", "Suroeste", "Noreste", "Sureste" };
        public static readonly string[] Theme = { "Turquoise", "Coral", "Sand", "Ink" };
        public static readonly Vector2[] HubCenter = { new Vector2(-16f, 24f), new Vector2(-16f, -24f), new Vector2(15f, 24f), new Vector2(15f, -24f) };

        // ------------------------------------------------------------------ río natural
        private static readonly Vector2[] RiverRaw =
        {
            new Vector2(2, 45), new Vector2(-2, 40), new Vector2(-5, 34), new Vector2(-6, 28), new Vector2(-4, 22), new Vector2(-1, 16), new Vector2(2, 12),
            new Vector2(5, 7), new Vector2(7, 1), new Vector2(5, -4), new Vector2(1, -9), new Vector2(-3, -13), new Vector2(-4, -18), new Vector2(-2, -23),
            new Vector2(2, -27), new Vector2(5, -32), new Vector2(7, -37), new Vector2(5, -41), new Vector2(3, -45)
        };
        public static readonly List<Vector2> River = Proto612Layout.Chaikin(RiverRaw, 3);

        /// <summary>Multiplicador de anchura del cauce a lo largo del río (0,78–1,22): el agua se ensancha y se estrecha.</summary>
        public static float Width(float along) => 1f + 0.22f * (Mathf.PerlinNoise(along * 0.045f + 3.7f, 11.3f) * 2f - 1f);
        /// <summary>Irregularidad de cada orilla (m añadidos al borde exterior de la franja de arena).</summary>
        public static float BankJitter(float along, int side) => 0.9f * (Mathf.PerlinNoise(along * 0.11f + side * 17f, 2.2f) * 2f - 1f);

        public static readonly Proto612Layout.Bridge[] Bridges;
        public static readonly string[] BridgeName = { "Puente norte", "Puente centro-norte", "Puente centro-sur", "Puente sur" };
        public static readonly float[] BridgeTargetZ = { 29f, 9f, -12f, -31f };

        public static readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
        public static readonly string[] PathName;
        public static readonly Vector2[][] ReserveRings = new Vector2[4][];     // centros de las 3 reservas por aldea

        static R621Layout()
        {
            Bridges = new Proto612Layout.Bridge[4];
            var dense = Proto612Layout.Resample(River, 0.25f);
            for (int i = 0; i < 4; i++)
            {
                int best = 0; float bd = 1e9f;
                for (int k = 0; k < dense.Count; k++) { float d = Mathf.Abs(dense[k].p.y - BridgeTargetZ[i]); if (d < bd) { bd = d; best = k; } }
                var t = dense[best].t; var n = new Vector2(t.y, -t.x); if (n.x < 0f) n = -n;
                Bridges[i] = new Proto612Layout.Bridge { center = dense[best].p, axis = n, yaw = Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg };
            }

            var names = new List<string>();
            void Add(string n, params Vector2[] pts) { names.Add(n); Paths.Add(Proto612Layout.Chaikin(pts, 2)); }
            void AddSmooth(string n, params Vector2[] pts) { names.Add(n); Paths.Add(Proto612Layout.Chaikin(pts, 3)); }     // 6.2.2: curvas más suaves en empalmes
            Vector2 Door(int i) => Houses.DoorPoint(i, 0f);
            Vector2 Pre(int i, float d) => Door(i) + Houses.Houses[i].Front * d;
            Vector2 We(int b, float d) => Bridges[b].center - Bridges[b].axis * d;     // lado oeste del puente
            Vector2 Ea(int b, float d) => Bridges[b].center + Bridges[b].axis * d;     // lado este

            // Plaza común ↔ casas del oeste (tramo final recto de 7 m perpendicular a la fachada).
            Add("Camino_Plaza_Abecedario", PlazaCenter + new Vector2(-1f, 5.5f), new Vector2(-12.6f, 10.5f), Pre(0, 7f), Pre(0, 3.5f), Door(0));
            Add("Camino_Plaza_Numeros", PlazaCenter + new Vector2(-1f, -5.5f), new Vector2(-12.6f, -11f), Pre(1, 7f), Pre(1, 3.5f), Door(1));
            // Plaza ↔ puentes centro-norte y centro-sur.
            Add("Camino_Puente_CentroNorte", PlazaCenter + new Vector2(4f, 3.5f), We(1, 9f), Bridges[1].center, Ea(1, 9f));
            // 6.2.2: el empalme este del puente centro-sur ya no es una «V». El camino sale del puente con una curva redondeada hacia Saludos (el tramo
            // Ea→Q es común) y la ribera se une al camino de Saludos en una «T» (punto P), en lugar de converger en el mismo extremo.
            var saludosDir = (Pre(3, 7f) - Ea(2, 9f)).normalized; var q = Ea(2, 9f) + saludosDir * 3f; var pJoin = Vector2.Lerp(q, Pre(3, 7f), 0.5f);
            AddSmooth("Camino_Puente_CentroSur", PlazaCenter + new Vector2(4f, -3.5f), We(2, 9f), Bridges[2].center, Ea(2, 9f), q);
            // Orilla este: de cada puente central a su casa, y una ribera que los une.
            Add("Camino_CentroNorte_Meses", Ea(1, 9f), Pre(2, 7f), Pre(2, 3.5f), Door(2));
            Add("Camino_CentroSur_Saludos", q, Pre(3, 7f), Pre(3, 3.5f), Door(3));
            AddSmooth("Camino_Ribera_Este", Ea(1, 9f), BankPoint(3f, 10f), BankPoint(-5f, 10f), pJoin);
            // Aldeas ↔ sus plazas temáticas y puentes norte/sur.
            Add("Camino_Abecedario_Plaza_Tematica", Pre(0, 7f), new Vector2(-12.5f, 20f), HubCenter[0]);
            Add("Camino_Numeros_Plaza_Tematica", Pre(1, 7f), new Vector2(-12.5f, -20f), HubCenter[1]);
            Add("Camino_Meses_Plaza_Tematica", Pre(2, 7f), new Vector2(13.5f, 19f), HubCenter[2]);
            Add("Camino_Saludos_Plaza_Tematica", Pre(3, 7f), new Vector2(13.5f, -19f), HubCenter[3]);
            Add("Camino_Puente_Norte", HubCenter[0], We(0, 8f), Bridges[0].center, Ea(0, 8f), HubCenter[2]);
            Add("Camino_Puente_Sur", HubCenter[1], We(3, 8f), Bridges[3].center, Ea(3, 8f), HubCenter[3]);
            PathName = names.ToArray();

            for (int v = 0; v < 4; v++) ReserveRings[v] = PickRings(v);
        }

        /// <summary>Punto de la orilla este a «off» metros del eje del río, a la altura Z indicada.</summary>
        static Vector2 BankPoint(float z, float off)
        {
            var dense = Proto612Layout.Resample(River, 0.25f); int best = 0; float bd = 1e9f;
            for (int k = 0; k < dense.Count; k++) { float d = Mathf.Abs(dense[k].p.y - z); if (d < bd) { bd = d; best = k; } }
            var t = dense[best].t; var n = new Vector2(t.y, -t.x); if (n.x < 0f) n = -n; return dense[best].p + n * off;
        }

        /// <summary>Tres reservas (futuros monumentos) por aldea, alrededor de su plaza temática y lejos de los caminos.</summary>
        static Vector2[] PickRings(int v)
        {
            var chosen = new List<Vector2>();
            for (int pick = 0; pick < 3; pick++)
            {
                Vector2 best = Vector2.zero; float bestScore = -1f;
                for (int deg = 0; deg < 360; deg += 15)
                {
                    float a = deg * Mathf.Deg2Rad; var q = HubCenter[v] + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 8.2f;
                    if (Mathf.Abs(q.x) > Half - 5f || Mathf.Abs(q.y) > Half - 5f) continue;
                    if (DistToRiver(q) < 9f || Vector2.Distance(q, Houses.Houses[v].center) < 9f || LaneDistRaw(q) < 5f) continue;
                    float score = Mathf.Min(LaneDistRaw(q), 8f);
                    foreach (var c in chosen) score = Mathf.Min(score, Vector2.Distance(c, q) / 2f);
                    if (score > bestScore) { bestScore = score; best = q; }
                }
                chosen.Add(best);
            }
            return chosen.ToArray();
        }

        // ------------------------------------------------------------------ distancias
        public static float DistToRiver(Vector2 p) => Proto612Layout.Nearest(p, River).dist;
        public static float DistToHouses(Vector2 p) => Houses.DistToHouses(p);

        static float LaneDistRaw(Vector2 p)
        {
            float best = 99f;
            foreach (var path in Paths) best = Mathf.Min(best, Proto612Layout.Nearest(p, path).dist);
            return best;
        }

        public static bool InDisc(Vector2 p, float extra)
        {
            if ((p - PlazaCenter).magnitude < PlazaRadius + extra) return true;
            for (int v = 0; v < 4; v++) if ((p - HubCenter[v]).magnitude < HubRadius + extra) return true;
            return false;
        }

        /// <summary>Distancia al camino transitable más cercano; las partes ocultas bajo plazas no cuentan.</summary>
        public static float LaneDist(Vector2 p)
        {
            float best = 99f;
            foreach (var path in Paths) foreach (var (q, _) in Proto612Layout.Resample(path, 0.5f)) if (!InDisc(q, 0.4f)) best = Mathf.Min(best, Vector2.Distance(p, q));
            return best;
        }

        public static float DistToPaths(Vector2 p) => LaneDistRaw(p);

        public static float DistToDoorCorridors(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++) { var a = Houses.DoorPoint(i, 0f); var b = a + Houses.Houses[i].Front * 10f; best = Mathf.Min(best, Proto612Layout.Nearest(p, new[] { a, b }).dist); }
            return best;
        }

        public static float DistToReserves(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var rr in ReserveRings) foreach (var c in rr) best = Mathf.Min(best, Vector2.Distance(p, c));
            return best;
        }

        // ------------------------------------------------------------------ altura
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Gauss(float x, float z, float cx, float cz, float sigma, float amp) { float dx = x - cx, dz = z - cz; return amp * Mathf.Exp(-(dx * dx + dz * dz) / (2f * sigma * sigma)); }
        private static float Flat(float dist, float r, float fade) { return 1f - Smooth((dist - r) / fade); }

        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = (Mathf.PerlinNoise(x * 0.05f + 9.1f, z * 0.05f + 23.7f) - 0.5f) * 2.0f;
            h += Gauss(x, z, 34f, 31f, 6f, 2.0f) + Gauss(x, z, -34f, -33f, 4.5f, 3.0f);

            float flat = Flat((p - PlazaCenter).magnitude, PlazaRadius + 3f, 6f);
            for (int v = 0; v < 4; v++) { flat = Mathf.Max(flat, Flat((p - HubCenter[v]).magnitude, HubRadius + 3f, 6f)); flat = Mathf.Max(flat, Flat((p - Houses.Houses[v].center).magnitude, 8f, 6f)); }
            for (int i = 0; i < Paths.Count; i++) flat = Mathf.Max(flat, Flat(Proto612Layout.Nearest(p, Paths[i]).dist, 2.5f, 3.5f));
            var rh = Proto612Layout.Nearest(p, River);
            float w = Width(rh.along);
            flat = Mathf.Max(flat, Flat(rh.dist, 5.4f, 4.4f));
            return h * (1f - flat) + Proto612Layout.RiverProfile(rh.dist / w);
        }

        // ------------------------------------------------------------------ objetos
        static bool Reserved(Vector2 p, float riverMin)
            => InDisc(p, 4f) || DistToHouses(p) < 11f || DistToPaths(p) < 4.5f || DistToRiver(p) < riverMin || DistToDoorCorridors(p) < 6f
               || DistToReserves(p) < 4f || (new Vector2(Spawn.x, Spawn.z) - p).magnitude < 3f || (p - new Vector2(SenaPos.x, SenaPos.z)).magnitude < 2.5f;

        public static List<Proto612Layout.Item> Trees()
        {
            var rng = new System.Random(6210);
            string[] names = { "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Pine Tree_1", "Pine Tree_2", "Pine Tree_3" };
            var list = new List<Proto612Layout.Item>(); var taken = new List<Vector2>();
            for (int attempts = 0; list.Count < 62 && attempts < 9000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                if (Reserved(p, 7.5f)) continue;
                bool close = false; foreach (var q in taken) if ((q - p).sqrMagnitude < 16f) { close = true; break; }
                if (close) continue;
                taken.Add(p);
                list.Add(new Proto612Layout.Item { prefab = names[rng.Next(names.Length)], pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 0.55f + 0.25f * (float)rng.NextDouble() });
            }
            return list;
        }

        public static List<Proto612Layout.Item> Rocks()
        {
            var rng = new System.Random(6211);
            string[] names = { "Rock_1", "Rock_2", "Rock_3", "Rock_4" };
            var list = new List<Proto612Layout.Item>();
            for (int attempts = 0; list.Count < 20 && attempts < 8000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                float dr = DistToRiver(p); bool nearBank = dr > 3.4f && dr < 7.5f;
                if (!nearBank && list.Count % 3 != 0) continue;
                if (InDisc(p, 2f) || DistToHouses(p) < 9f || DistToPaths(p) < 3f || dr < 3.4f || DistToDoorCorridors(p) < 5f || DistToReserves(p) < 2.5f) continue;
                list.Add(new Proto612Layout.Item { prefab = names[rng.Next(names.Length)], pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 0.8f + 0.9f * (float)rng.NextDouble() });
            }
            return list;
        }

        public static List<Proto612Layout.Item> Flowers()
        {
            var rng = new System.Random(6212);
            var list = new List<Proto612Layout.Item>();
            for (int attempts = 0; list.Count < 50 && attempts < 8000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                if (InDisc(p, 1.5f) || DistToHouses(p) < 6.5f || DistToPaths(p) < 1.9f || DistToRiver(p) < 3.4f || DistToDoorCorridors(p) < 3f || DistToReserves(p) < 2f) continue;
                list.Add(new Proto612Layout.Item { prefab = "Flower_" + (1 + rng.Next(5)), pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 1f });
            }
            return list;
        }

        /// <summary>Distancia mínima a los ejes de los cuatro puentes (tramo de ±12 m).</summary>
        public static float AxisDist(Vector2 q)
        {
            float best = 99f; foreach (var b in Bridges) best = Mathf.Min(best, Proto612Layout.Nearest(q, new[] { b.center - b.axis * 12f, b.center + b.axis * 12f }).dist); return best;
        }

        static Vector2 BestClear(IEnumerable<Vector2> candidates)
        {
            Vector2 best = Vector2.zero; float bestClear = -1f;
            foreach (var q in candidates) { if (AxisDist(q) < 2.8f) continue; float c = Mathf.Min(LaneDist(q), 6f) + Mathf.Min(DistToRiver(q), 5f) * 0.1f; if (c > bestClear) { bestClear = c; best = q; } }
            return best;
        }

        public static List<Proto612Layout.Item> Props()
        {
            var list = new List<Proto612Layout.Item>();
            Proto612Layout.Item At(string prefab, Vector2 p, float yaw) => new Proto612Layout.Item { prefab = prefab, pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = yaw, scale = 1f };
            float Face(Vector2 from, Vector2 to) => Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
            // Bancos en el lado oeste de la plaza (mirando al centro).
            foreach (float want in new[] { 225f, 315f })
            {
                Vector2 pick = Vector2.zero; float bestOff = 999f;
                for (int deg = 180; deg <= 350; deg += 5)
                {
                    float a = deg * Mathf.Deg2Rad; var q = PlazaCenter + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 4.4f;
                    if (AxisDist(q) < 3.2f || Vector2.Distance(q, new Vector2(Spawn.x, Spawn.z)) < 3.2f || Vector2.Distance(q, new Vector2(SenaPos.x, SenaPos.z)) < 3f) continue;
                    if (list.Count > 0 && Vector2.Distance(q, new Vector2(list[0].pos.x, list[0].pos.z)) < 5f) continue;
                    float off = Mathf.Abs(deg - want); if (off < bestOff) { bestOff = off; pick = q; }
                }
                list.Add(At("Bench", pick, Face(pick, PlazaCenter)));
            }
            // Faroles de la plaza: ángulos con más despeje respecto a las salidas.
            var used = new List<Vector2>();
            for (int k = 0; k < 4; k++)
            {
                var cands = new List<Vector2>();
                for (int deg = 0; deg < 360; deg += 10) { float a = deg * Mathf.Deg2Rad; var q = PlazaCenter + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 5.7f; bool far = true; foreach (var u in used) if (Vector2.Distance(u, q) < 6f) far = false; if (far) cands.Add(q); }
                var best = BestClear(cands); used.Add(best); list.Add(At("Lantern", best, 0f));
            }
            // Farol en cada extremo de puente, del lado con más despeje.
            for (int b = 0; b < 4; b++)
            {
                var br = Bridges[b]; var perp = new Vector2(-br.axis.y, br.axis.x);
                foreach (float along in new[] { 7.4f, -7.4f })
                {
                    var cands = new List<Vector2>(); foreach (float off in new[] { 3.3f, -3.3f, 4.3f, -4.3f }) cands.Add(br.center + br.axis * along + perp * off);
                    list.Add(At("Lantern", BestClear(cands), 0f));
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ señalización provisional
        public class Sign { public string name; public Vector2 pos; public Vector2 face; public string[] lines; public string[] accents; public bool doubleSided; }

        public static List<Sign> Signs()
        {
            var list = new List<Sign>();
            string[] label = { "ABECEDARIO", "NÚMEROS", "MESES", "SALUDOS" };
            for (int i = 0; i < 4; i++)
            {
                var h = Houses.Houses[i];
                list.Add(new Sign { name = "Cartel_Casa_" + h.name, pos = Houses.DoorPoint(i, 0f) + h.Front * 3.2f + h.Lateral * (i == 0 || i == 3 ? 2.6f : -2.6f), face = h.Front, lines = new[] { label[i] }, accents = new[] { Theme[i] } });
            }
            // Poste de orientación de la plaza: las cuatro aldeas, por punto cardinal (lo ve quien aparece en el spawn).
            // Dentro de la plaza, lo más de frente posible a la mirada del spawn, a ≥5 m de él, ≥3 m de Sena y fuera de los ejes de los puentes.
            var sp = PlazaCenter; float bestAng = 999f; var spawn2 = new Vector2(Spawn.x, Spawn.z); var sena2 = new Vector2(SenaPos.x, SenaPos.z);
            for (int deg = 0; deg < 360; deg += 10)
                foreach (float r in new[] { 2.8f, 3.8f, 4.8f })
                {
                    float a = deg * Mathf.Deg2Rad; var q = PlazaCenter + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                    if (AxisDist(q) < 2.8f || Vector2.Distance(q, spawn2) < 5f || Vector2.Distance(q, sena2) < 3f) continue;
                    float ang = Vector2.Angle(Vector2.right, q - spawn2) + Mathf.Abs(q.y - spawn2.y) * 0.01f; if (ang < bestAng) { bestAng = ang; sp = q; }
                }
            list.Add(new Sign { name = "Cartel_Plaza_Aldeas", pos = sp, face = (new Vector2(Spawn.x, Spawn.z) - sp).normalized, lines = new[] { "NO · ABECEDARIO", "NE · MESES", "SO · NÚMEROS", "SE · SALUDOS" }, accents = new[] { "Turquoise", "Sand", "Coral", "Ink" }, doubleSided = true });
            // Un cartel por puente, en el lado oeste y del lado con más despeje.
            for (int b = 0; b < 4; b++)
            {
                var br = Bridges[b]; var perp = new Vector2(-br.axis.y, br.axis.x);
                var cands = new List<Vector2>(); foreach (float off in new[] { 4f, -4f, 5f, -5f }) cands.Add(br.center - br.axis * 8.6f + perp * off);
                string acc = b == 0 ? "Turquoise" : b == 1 ? "Sand" : b == 2 ? "Ink" : "Coral";
                list.Add(new Sign { name = "Cartel_" + BridgeName[b].Replace(' ', '_').Replace('ú', 'u'), pos = BestClear(cands), face = -br.axis, lines = new[] { BridgeName[b].ToUpperInvariant() }, accents = new[] { acc }, doubleSided = true });
            }
            // Marcador provisional en cada plaza temática (aldea y futura reserva de monumentos).
            string[] reserve = { "LETRAS", "CIFRAS", "RELOJ Y CALENDARIO", "MANOS" };
            for (int v = 0; v < 4; v++)
            {
                var cands = new List<Vector2>(); for (int deg = 0; deg < 360; deg += 20) { float a = deg * Mathf.Deg2Rad; cands.Add(HubCenter[v] + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 5.4f); }
                var p = BestClear(cands);
                list.Add(new Sign { name = "Marcador_" + VillageName[v].Replace(' ', '_').Replace('é', 'e').Replace('ú', 'u'), pos = p, face = (Pre(v, 7f) - p).normalized, lines = new[] { VillageName[v].ToUpperInvariant(), "RESERVA: " + reserve[v] }, accents = new[] { Theme[v], Theme[v] }, doubleSided = true });
            }
            return list;
        }

        static Vector2 Pre(int i, float d) => Houses.DoorPoint(i, 0f) + Houses.Houses[i].Front * d;
    }
}
