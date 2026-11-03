using System.Collections.Generic;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.1.3: distribución con el río atravesando el centro del pueblo de norte a sur. El río separa dos sectores
    /// con dos casas cada uno: oeste (AlphabetHouse, NumbersHouse) y este (MonthsHouse, GreetingsHouse). La plaza está en la
    /// orilla oeste junto al puente principal, y una pequeña explanada recibe ese puente en la orilla este.
    /// Reutiliza las utilidades geométricas de <see cref="Proto612Layout"/> (la versión anterior no se modifica).
    /// </summary>
    public static class Proto612LayoutV2
    {
        public const float Half = 45f;
        public const float WaterY = -0.35f;
        public const float PathHalfWidth = 1.25f;
        public const float RiverWaterHalfWidth = 2.0f;
        public const float BarrierOffset = 2.2f;
        public const float BridgeGap = 2.3f;           // semianchura del hueco de la barrera en cada puente (el tablero mide 4,24 m)

        public const float PlazaRadius = 6.5f, EsplanadeRadius = 4.2f;
        public static readonly Vector2 PlazaCenter = new Vector2(-13f, 0f);
        public static Vector2 EsplanadeCenter;

        public static readonly Vector3 Spawn = new Vector3(-18f, 0.1f, -1.5f);
        public const float SpawnYaw = 90f;                                  // mira al este: plaza, Sena, puente y río
        public static readonly Vector3 SenaPos = new Vector3(-10.5f, 0f, 2.2f);

        // Río de norte a sur: curvas suaves, casi vertical en los puentes para que crucen en perpendicular.
        private static readonly Vector2[] RiverRaw =
        {
            new Vector2(2, 45), new Vector2(-2, 36), new Vector2(-4, 26), new Vector2(-4, 14), new Vector2(-1, 2),
            new Vector2(1, -8), new Vector2(4, -20), new Vector2(4, -32), new Vector2(0, -45)
        };
        public static readonly List<Vector2> River = Proto612Layout.Chaikin(RiverRaw, 3);

        // ------------------------------------------------------------------ casas
        public class House
        {
            public string name, prefab; public Vector2 center; public float yaw;
            public float doorLateral;               // posición lateral de la puerta respecto al centro de la fachada (medida de las puertas de producción)
            public Vector2 localCenter, size;     // centro y tamaño de la huella (ya a escala 0,5), en el espacio local del prefab
            public Vector2 Front => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
            public Vector2 Lateral => new Vector2(Mathf.Cos(yaw * Mathf.Deg2Rad), -Mathf.Sin(yaw * Mathf.Deg2Rad));
            /// <summary>Posición del pivote del prefab para que el centro de su huella caiga en «center».</summary>
            public Vector2 Pivot => center - (Lateral * localCenter.x + Front * localCenter.y);
            public Vector2 FrontFace => center + Front * (size.y * 0.5f);
        }

        // Medidas de los prefabs de la aldea (Proto612Measure), a escala 0,5.
        private static House Make(string name, string prefab, Vector2 center, float yaw, Vector2 lc, Vector2 size, float doorLateral)
            => new House { name = name, prefab = prefab, center = center, yaw = yaw, localCenter = lc * 0.5f, size = size * 0.5f, doorLateral = doorLateral };

        public static readonly House[] Houses =
        {
            Make("AlphabetHouse", "House_3", new Vector2(-22, 14), 90f, new Vector2(-3.97f, -1.39f), new Vector2(12.53f, 13.48f), -2.48f),
            Make("NumbersHouse", "House_1", new Vector2(-22, -14), 90f, new Vector2(0.78f, -0.06f), new Vector2(11.43f, 14.89f), -0.38f),
            Make("MonthsHouse", "House_2", new Vector2(22, 14), -90f, new Vector2(-0.08f, 0.60f), new Vector2(11.22f, 11.72f), -0.77f),
            Make("GreetingsHouse", "House_2", new Vector2(22, -14), -90f, new Vector2(-0.08f, 0.60f), new Vector2(11.22f, 11.72f), -0.77f),
        };

        // ------------------------------------------------------------------ puentes y caminos
        public static readonly Proto612Layout.Bridge[] Bridges;
        public static readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
        public static readonly string[] PathName;

        static Proto612LayoutV2()
        {
            Bridges = new[] { MakeBridge(new Vector2(-0.6f, 0f)), MakeBridge(new Vector2(-4f, 20f)) };
            var b1 = Bridges[0]; var b2 = Bridges[1];
            EsplanadeCenter = b1.center + b1.axis * (6.2f + 3.0f);

            var names = new List<string>();
            void Add(string n, params Vector2[] pts) { names.Add(n); Paths.Add(Proto612Layout.Chaikin(pts, 2)); }
            // 6.1.4: los dos últimos tramos de cada camino de casa llegan en línea recta y perpendicular a la fachada (7 m), de modo que
            // quien camina por él mira de frente la puerta sin que haya que forzar la cámara.
            Vector2 Door(int i, float lateral) => DoorPoint(i, lateral);
            Vector2 Pre(int i, float lateral, float d) => DoorPoint(i, lateral) + Houses[i].Front * d;

            // Plaza ↔ casas del sector oeste.
            Add("Camino_Plaza_Alphabet", new Vector2(-14.5f, 5.5f), new Vector2(-12.6f, 10f), Pre(0, 0f, 7f), Pre(0, 0f, 3.5f), Door(0, 0f));
            Add("Camino_Plaza_Numbers", new Vector2(-14.5f, -5.5f), new Vector2(-12.6f, -10f), Pre(1, 0f, 7f), Pre(1, 0f, 3.5f), Door(1, 0f));
            // Explanada este ↔ casas del sector este.
            Add("Camino_Explanada_Months", EsplanadeCenter + new Vector2(2.5f, 3f), new Vector2(12.6f, 9f), Pre(2, 0f, 7f), Pre(2, 0f, 3.5f), Door(2, 0f));
            Add("Camino_Explanada_Greetings", EsplanadeCenter + new Vector2(2.5f, -3f), new Vector2(12.6f, -9f), Pre(3, 0f, 7f), Pre(3, 0f, 3.5f), Door(3, 0f));
            // Puente principal: de la plaza a la explanada.
            Add("Camino_Puente1", b1.center - b1.axis * 9f, b1.center, b1.center + b1.axis * 9f);
            // Puente norte: une los caminos de AlphabetHouse y MonthsHouse sin pasar por la plaza.
            Add("Camino_Puente2", Pre(0, 0f, 7f), new Vector2(-11.3f, 17.5f), b2.center - b2.axis * 8f, b2.center, b2.center + b2.axis * 8f, new Vector2(11.5f, 17.5f), Pre(2, 0f, 7f));
            PathName = names.ToArray();
        }

        /// <summary>Punto del suelo frente a la puerta real de la casa (0,15 m dentro de la fachada); «lateral» es un desplazamiento adicional a lo largo de la fachada.</summary>
        public static Vector2 DoorPoint(int i, float lateral) => Houses[i].FrontFace - Houses[i].Front * 0.15f + Houses[i].Lateral * (Houses[i].doorLateral + lateral);

        private static Proto612Layout.Bridge MakeBridge(Vector2 nominal)
        {
            var hit = Proto612Layout.Nearest(nominal, River);
            var t = hit.tangent; var n = new Vector2(t.y, -t.x);
            if (n.x < 0f) n = -n;
            return new Proto612Layout.Bridge { center = hit.point, axis = n, yaw = Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg };
        }

        // ------------------------------------------------------------------ altura
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Gauss(float x, float z, float cx, float cz, float sigma, float amp) { float dx = x - cx, dz = z - cz; return amp * Mathf.Exp(-(dx * dx + dz * dz) / (2f * sigma * sigma)); }
        private static float Flat(float dist, float r, float fade) { return 1f - Smooth((dist - r) / fade); }

        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = (Mathf.PerlinNoise(x * 0.05f + 9.1f, z * 0.05f + 23.7f) - 0.5f) * 2.0f;
            h += Gauss(x, z, 34f, 31f, 6f, 2.0f) + Gauss(x, z, -34f, -33f, 4.5f, 3.0f);       // dos lomas en las esquinas (≈9° y ≈22°)

            float flat = Flat((p - PlazaCenter).magnitude, PlazaRadius + 3f, 6f);
            flat = Mathf.Max(flat, Flat((p - EsplanadeCenter).magnitude, EsplanadeRadius + 3f, 6f));
            for (int i = 0; i < Houses.Length; i++) flat = Mathf.Max(flat, Flat((p - Houses[i].center).magnitude, 8f, 6f));
            for (int i = 0; i < Paths.Count; i++) flat = Mathf.Max(flat, Flat(Proto612Layout.Nearest(p, Paths[i]).dist, 2.5f, 3.5f));
            float dr = Proto612Layout.Nearest(p, River).dist;
            flat = Mathf.Max(flat, Flat(dr, 4.6f, 4.4f));
            return h * (1f - flat) + Proto612Layout.RiverProfile(dr);
        }

        // ------------------------------------------------------------------ objetos
        public static float DistToPaths(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Paths.Count; i++) best = Mathf.Min(best, Proto612Layout.Nearest(p, Paths[i]).dist);
            return best;
        }
        public static float DistToHouses(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Houses.Length; i++) best = Mathf.Min(best, (p - Houses[i].center).magnitude);
            return best;
        }
        public static float DistToRiver(Vector2 p) => Proto612Layout.Nearest(p, River).dist;
        /// <summary>Distancia al pasillo de 10 m que parte de cada puerta hacia fuera (debe quedar libre de vegetación y rocas).</summary>
        public static float DistToDoorCorridors(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Houses.Length; i++) { var a = DoorPoint(i, 0f); var b = a + Houses[i].Front * 10f; best = Mathf.Min(best, Proto612Layout.Nearest(p, new[] { a, b }).dist); }
            return best;
        }
        static bool InCivic(Vector2 p, float extra)
            => (p - PlazaCenter).magnitude < PlazaRadius + extra || (p - EsplanadeCenter).magnitude < EsplanadeRadius + extra
               || ((new Vector2(Spawn.x, Spawn.z)) - p).magnitude < 3f || (p - new Vector2(SenaPos.x, SenaPos.z)).magnitude < 2.5f;

        public static List<Proto612Layout.Item> Trees()
        {
            var rng = new System.Random(6130);
            string[] names = { "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Pine Tree_1", "Pine Tree_2", "Pine Tree_3" };
            var list = new List<Proto612Layout.Item>(); var taken = new List<Vector2>();
            for (int attempts = 0; list.Count < 70 && attempts < 8000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                if (InCivic(p, 7f) || DistToHouses(p) < 11f || DistToPaths(p) < 4.5f || DistToRiver(p) < 6.5f || DistToDoorCorridors(p) < 6f) continue;
                bool close = false; foreach (var q in taken) if ((q - p).sqrMagnitude < 16f) { close = true; break; }
                if (close) continue;
                taken.Add(p);
                list.Add(new Proto612Layout.Item { prefab = names[rng.Next(names.Length)], pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 0.55f + 0.25f * (float)rng.NextDouble() });
            }
            return list;
        }

        public static List<Proto612Layout.Item> Rocks()
        {
            var rng = new System.Random(6131);
            string[] names = { "Rock_1", "Rock_2", "Rock_3", "Rock_4" };
            var list = new List<Proto612Layout.Item>();
            for (int attempts = 0; list.Count < 18 && attempts < 6000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                float dr = DistToRiver(p);
                bool nearBank = dr > 2.7f && dr < 7f;
                if (!nearBank && list.Count % 3 != 0) continue;            // la mayoría, junto a las orillas
                if (InCivic(p, 3f) || DistToHouses(p) < 9f || DistToPaths(p) < 3f || dr < 2.7f || DistToDoorCorridors(p) < 5f) continue;
                list.Add(new Proto612Layout.Item { prefab = names[rng.Next(names.Length)], pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 0.8f + 0.9f * (float)rng.NextDouble() });
            }
            return list;
        }

        public static List<Proto612Layout.Item> Flowers()
        {
            var rng = new System.Random(6132);
            var list = new List<Proto612Layout.Item>();
            for (int attempts = 0; list.Count < 55 && attempts < 6000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                if (InCivic(p, 1.5f) || DistToHouses(p) < 6.5f || DistToPaths(p) < 1.9f || DistToRiver(p) < 2.8f || DistToDoorCorridors(p) < 3f) continue;
                list.Add(new Proto612Layout.Item { prefab = "Flower_" + (1 + rng.Next(5)), pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 1f });
            }
            return list;
        }

        public static List<Proto612Layout.Item> Props()
        {
            var list = new List<Proto612Layout.Item>();
            Proto612Layout.Item At(string prefab, Vector2 p, float yaw) => new Proto612Layout.Item { prefab = prefab, pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = yaw, scale = 1f };
            float Face(Vector2 from, Vector2 to) => Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
            // Bancos en el lado oeste de la plaza (mirando al centro): la unión con el puente y los caminos quedan libres.
            foreach (float deg in new[] { 215f, 325f })
            {
                float a = deg * Mathf.Deg2Rad; var p = PlazaCenter + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 4.2f; list.Add(At("Bench", p, Face(p, PlazaCenter)));
            }
            // Bancos de la explanada, en el lado del río (mirando al agua y al puente), lejos del arranque de los caminos.
            foreach (float sz in new[] { 3.5f, -3.5f }) { var p = EsplanadeCenter + new Vector2(-1.2f, sz); list.Add(At("Bench", p, Face(p, EsplanadeCenter + new Vector2(0f, sz * 0.2f)))); }
            // Faroles: plaza (4), un farol por extremo de puente (4) y dos en las entradas de las casas más alejadas.
            var lamps = new List<Vector2>();
            foreach (float deg in new[] { 20f, 160f, 235f, 305f }) { float a = deg * Mathf.Deg2Rad; lamps.Add(PlazaCenter + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 5.6f); }
            foreach (var b in Bridges)
            {
                var perp = new Vector2(-b.axis.y, b.axis.x);
                foreach (float along in new[] { 7.4f, -7.4f })
                {
                    // Candidatos a ambos lados del extremo del puente; se elige el de mayor despeje respecto a los caminos.
                    Vector2 best = Vector2.zero; float bestClear = -1f;
                    foreach (float off in new[] { 3.2f, -3.2f, 4.2f, -4.2f })
                    {
                        var q = b.center + b.axis * along + perp * off; float clear = DistToPaths(q);
                        if (clear > bestClear) { bestClear = clear; best = q; }
                    }
                    lamps.Add(best);
                }
            }
            foreach (var p in lamps) list.Add(At("Lantern", p, 0f));
            return list;
        }

        // ------------------------------------------------------------------ señalización (6.1.4)
        public class Sign
        {
            public string name; public Vector2 pos; public Vector2 face;     // «face»: hacia dónde mira el cartel (donde está quien lo lee)
            public string[] lines; public string[] accents; public bool doubleSided;
        }

        public static List<Sign> Signs()
        {
            var list = new List<Sign>();
            string[] accent = { "Turquoise", "Coral", "Sand", "Turquoise" };
            string[] label = { "ABECEDARIO", "NÚMEROS", "MESES", "SALUDOS" };
            for (int i = 0; i < 4; i++)
            {
                var h = Houses[i];
                // A 4,5 m de la fachada, junto al tramo recto de llegada y del lado contrario a la curva del camino; mira hacia quien se acerca.
                list.Add(new Sign { name = "Cartel_" + h.name, pos = DoorPoint(i, 0f) + h.Front * 4.5f + h.Lateral * (i == 0 || i == 3 ? -2.6f : 2.6f), face = h.Front, lines = new[] { label[i] }, accents = new[] { accent[i] } });
            }
            var b1 = Bridges[0]; var b2 = Bridges[1];
            // Poste de la plaza y del sector oeste: lo ve quien aparece en el spawn y quien vuelve del puente.
            list.Add(new Sign { name = "Cartel_Plaza_SectorOeste", pos = new Vector2(-8.3f, -5.2f), face = (new Vector2(Spawn.x, Spawn.z) - new Vector2(-8.3f, -5.2f)).normalized, lines = new[] { "PLAZA CENTRAL", "SECTOR OESTE" }, accents = new[] { "Turquoise", "Coral" }, doubleSided = true });
            // Poste del sector este: en la bifurcación de la explanada, de frente a quien cruza el puente principal.
            list.Add(new Sign { name = "Cartel_SectorEste", pos = EsplanadeCenter + new Vector2(3.6f, -0.4f), face = -b1.axis, lines = new[] { "SECTOR ESTE", "MESES  ·  SALUDOS" }, accents = new[] { "Sand", "Turquoise" }, doubleSided = true });
            list.Add(new Sign { name = "Cartel_PuentePrincipal", pos = b1.center - b1.axis * 4.2f + new Vector2(-b1.axis.y, b1.axis.x) * -3.9f, face = -b1.axis, lines = new[] { "PUENTE PRINCIPAL" }, accents = new[] { "Coral" }, doubleSided = true });
            list.Add(new Sign { name = "Cartel_PuenteNorte", pos = b2.center - b2.axis * 9.6f + new Vector2(-b2.axis.y, b2.axis.x) * 3.2f, face = -b2.axis, lines = new[] { "PUENTE NORTE" }, accents = new[] { "Coral" }, doubleSided = true });
            return list;
        }
    }
}
