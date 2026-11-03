using System.Collections.Generic;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Distribución espacial común a las dos alternativas (mallas modulares y Unity Terrain): casas, plaza, caminos, río, puentes
    /// y la función de altura. Las dos escenas sacan de aquí exactamente la misma forma de terreno, de modo que la comparación
    /// es entre tecnologías y no entre diseños. Solo existe dentro del prototipo 6.1.2.
    /// </summary>
    public static class Proto612Layout
    {
        public const float Half = 45f;            // el mundo mide 90 x 90 m
        public const float WaterY = -0.35f;       // nivel del agua
        public const float PlazaRadius = 7f;
        public const float PathHalfWidth = 1.2f;
        public const float RiverWaterHalfWidth = 2.0f;
        public const float BarrierOffset = 2.2f;  // distancia del eje del río a la que se corta el paso

        // Casas: norte, oeste, este y sur. Las entradas miran a la plaza (el frente del prefab es +Z local).
        public static readonly string[] HouseName = { "AlphabetHouse", "NumbersHouse", "MonthsHouse", "GreetingsHouse" };
        public static readonly string[] HousePrefab = { "House_3", "House_1", "House_2", "House_2" };
        public static readonly Vector2[] HouseCenter = { new Vector2(0, 16), new Vector2(-16, 0), new Vector2(16, 0), new Vector2(0, -16) };
        public static readonly float[] HouseYaw = { 180f, 90f, -90f, 0f };
        public static readonly Vector2[] HouseFront = { new Vector2(0, 10.6f), new Vector2(-10.6f, 0), new Vector2(10.6f, 0), new Vector2(0, -11.4f) };

        /// <summary>Altura a la que se coloca el puente para que su tablero quede a ras de las orillas (se mide en la construcción).</summary>
        public static float BridgeY;

        public static readonly Vector3 Spawn = new Vector3(0f, 0.1f, -11f);
        public static readonly Vector3 SenaPos = new Vector3(2f, 0f, 1f);

        // Río: puntos de referencia del encargo, desplazados hacia el oeste donde la referencia rozaba la casa de los números.
        private static readonly Vector2[] RiverRaw =
        {
            new Vector2(-44, 30), new Vector2(-34, 14), new Vector2(-30, 0), new Vector2(-26, -14), new Vector2(-20, -30), new Vector2(-13, -45)
        };
        public static readonly List<Vector2> River = Chaikin(RiverRaw, 3);

        public struct Bridge { public Vector2 center; public Vector2 axis; public float yaw; }
        public static readonly Bridge[] Bridges;

        public static readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
        public static readonly string[] PathName;

        static Proto612Layout()
        {
            Bridges = new[] { MakeBridge(new Vector2(-33f, 10f)), MakeBridge(new Vector2(-23f, -22f)) };

            var b1 = Bridges[0]; var b2 = Bridges[1];
            var names = new List<string>();
            void Add(string n, params Vector2[] pts) { names.Add(n); Paths.Add(Chaikin(pts, 2)); }
            Add("Camino_Norte", new Vector2(0, 5.5f), new Vector2(0, 12.2f));
            Add("Camino_Oeste", new Vector2(-5.5f, 0), new Vector2(-12.2f, 0));
            Add("Camino_Este", new Vector2(5.5f, 0), new Vector2(12.2f, 0));
            Add("Camino_Sur", new Vector2(0, -5.5f), new Vector2(0, -12.2f));
            Add("Camino_Puente1", new Vector2(-4.5f, 4.5f), new Vector2(-12f, 10f), b1.center + b1.axis * 12f, b1.center, b1.center - b1.axis * 12f);
            Add("Camino_Puente2", new Vector2(-4.5f, -4.5f), new Vector2(-9f, -13f), b2.center + b2.axis * 12f, b2.center, b2.center - b2.axis * 12f);
            PathName = names.ToArray();
        }

        private static Bridge MakeBridge(Vector2 nominal)
        {
            var hit = Nearest(nominal, River);
            var t = hit.tangent;
            var n = new Vector2(t.y, -t.x);
            if (n.x < 0f) n = -n;
            return new Bridge { center = hit.point, axis = n, yaw = Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg };
        }

        // ------------------------------------------------------------------ geometría 2D

        public struct Hit { public float dist; public Vector2 point; public Vector2 tangent; public float along; }

        public static Hit Nearest(Vector2 p, IList<Vector2> pts)
        {
            var best = new Hit { dist = float.MaxValue };
            float acc = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var ab = b - a; float len = ab.magnitude;
                float t = len > 1e-5f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / (len * len)) : 0f;
                var q = a + ab * t; float d = (p - q).magnitude;
                if (d < best.dist) best = new Hit { dist = d, point = q, tangent = len > 1e-5f ? ab / len : Vector2.up, along = acc + len * t };
                acc += len;
            }
            return best;
        }

        public static List<Vector2> Chaikin(IList<Vector2> pts, int iterations)
        {
            var cur = new List<Vector2>(pts);
            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector2> { cur[0] };
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.75f));
                }
                next.Add(cur[cur.Count - 1]);
                cur = next;
            }
            return cur;
        }

        /// <summary>Puntos cada <paramref name="step"/> metros a lo largo de la polilínea, con su tangente.</summary>
        public static List<(Vector2 p, Vector2 t)> Resample(IList<Vector2> pts, float step)
        {
            var res = new List<(Vector2, Vector2)>();
            float carry = 0f;
            res.Add((pts[0], (pts[1] - pts[0]).normalized));
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1]; float len = (b - a).magnitude; var dir = (b - a) / Mathf.Max(len, 1e-5f);
                float d = step - carry;
                while (d <= len) { res.Add((a + dir * d, dir)); d += step; }
                carry = len - (d - step);
            }
            var last = pts[pts.Count - 1];
            if ((res[res.Count - 1].Item1 - last).sqrMagnitude > 1e-4f) res.Add((last, (last - pts[pts.Count - 2]).normalized));
            return res;
        }

        // ------------------------------------------------------------------ altura

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Gauss(float x, float z, float cx, float cz, float sigma, float amp)
        {
            float dx = x - cx, dz = z - cz; return amp * Mathf.Exp(-(dx * dx + dz * dz) / (2f * sigma * sigma));
        }
        /// <summary>1 dentro de «r», 0 a partir de «r + fade», transición suave entre ambos.</summary>
        private static float Flat(float dist, float r, float fade) { return 1f - Smooth((dist - r) / fade); }

        public static float RiverProfile(float d)
        {
            if (d <= 1.0f) return -1.2f;
            if (d <= 1.75f) return Mathf.Lerp(-1.2f, WaterY, Smooth((d - 1.0f) / 0.75f));
            if (d <= 3.5f) return Mathf.Lerp(WaterY, 0f, Smooth((d - 1.75f) / 1.75f));
            return 0f;
        }

        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = (Mathf.PerlinNoise(x * 0.05f + 17.3f, z * 0.05f + 41.7f) - 0.5f) * 2.0f;
            h += Gauss(x, z, 28f, 26f, 7f, 2.2f) + Gauss(x, z, 30f, -28f, 4.5f, 3.0f);     // lomas de prueba de pendientes (~11° y ~22°)

            float flat = Flat(p.magnitude, 11f, 6f);
            for (int i = 0; i < HouseCenter.Length; i++) flat = Mathf.Max(flat, Flat((p - HouseCenter[i]).magnitude, 8f, 6f));
            for (int i = 0; i < Paths.Count; i++) flat = Mathf.Max(flat, Flat(Nearest(p, Paths[i]).dist, 2.5f, 3.5f));
            float dr = Nearest(p, River).dist;
            flat = Mathf.Max(flat, Flat(dr, 4.6f, 4.4f));

            return h * (1f - flat) + RiverProfile(dr);
        }

        // ------------------------------------------------------------------ vegetación y objetos (misma lista para ambas escenas)

        public struct Item { public string prefab; public Vector3 pos; public float yaw; public float scale; }

        public static float DistToPaths(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Paths.Count; i++) best = Mathf.Min(best, Nearest(p, Paths[i]).dist);
            return best;
        }

        public static float DistToHouses(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < HouseCenter.Length; i++) best = Mathf.Min(best, (p - HouseCenter[i]).magnitude);
            return best;
        }

        public static List<Item> Trees()
        {
            var rng = new System.Random(612);
            string[] names = { "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Pine Tree_1", "Pine Tree_2", "Pine Tree_3" };
            var list = new List<Item>(); var taken = new List<Vector2>();
            for (int attempts = 0; list.Count < 70 && attempts < 6000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                if (p.magnitude < 14f || DistToHouses(p) < 11f || DistToPaths(p) < 4.5f || Nearest(p, River).dist < 6f) continue;
                bool close = false; foreach (var q in taken) if ((q - p).sqrMagnitude < 16f) { close = true; break; }
                if (close) continue;
                taken.Add(p);
                list.Add(new Item { prefab = names[rng.Next(names.Length)], pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 0.55f + 0.25f * (float)rng.NextDouble() });
            }
            return list;
        }

        public static List<Item> Rocks()
        {
            var rng = new System.Random(6121);
            string[] names = { "Rock_1", "Rock_2", "Rock_3", "Rock_4" };
            var list = new List<Item>();
            for (int attempts = 0; list.Count < 16 && attempts < 4000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                float dr = Nearest(p, River).dist;
                bool nearBank = dr > 2.7f && dr < 6f;
                if (!nearBank && list.Count % 2 == 0) continue; // la mitad, junto a las orillas
                if (p.magnitude < 9f || DistToHouses(p) < 9f || DistToPaths(p) < 3f || dr < 2.7f) continue;
                list.Add(new Item { prefab = names[rng.Next(names.Length)], pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 0.8f + 0.9f * (float)rng.NextDouble() });
            }
            return list;
        }

        public static List<Item> Flowers()
        {
            var rng = new System.Random(6122);
            var list = new List<Item>();
            for (int attempts = 0; list.Count < 50 && attempts < 4000; attempts++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (Half - 3f), (float)(rng.NextDouble() * 2 - 1) * (Half - 3f));
                if (p.magnitude < PlazaRadius + 1.5f || DistToHouses(p) < 6.5f || DistToPaths(p) < 1.9f || Nearest(p, River).dist < 2.8f) continue;
                list.Add(new Item { prefab = "Flower_" + (1 + rng.Next(5)), pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = (float)(rng.NextDouble() * 360), scale = 1f });
            }
            return list;
        }

        public static List<Item> Props()
        {
            var list = new List<Item>();
            // Bancos en las diagonales de la plaza, mirando al centro.
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad; var p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 5.3f;
                list.Add(new Item { prefab = "Bench", pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg, scale = 1f });
            }
            // Faroles junto a las entradas de la plaza y a los extremos de los puentes.
            var lamps = new List<Vector2> { new Vector2(2.3f, 7.8f), new Vector2(-2.3f, -7.8f), new Vector2(7.8f, -2.3f), new Vector2(-7.8f, 2.3f) };
            foreach (var b in Bridges)
            {
                var side = new Vector2(-b.axis.y, b.axis.x) * 2.8f;
                lamps.Add(b.center + b.axis * 7.2f + side); lamps.Add(b.center - b.axis * 7.2f - side);
            }
            foreach (var p in lamps) list.Add(new Item { prefab = "Lantern", pos = new Vector3(p.x, Height(p.x, p.y), p.y), yaw = 0f, scale = 1f });
            return list;
        }
    }
}
