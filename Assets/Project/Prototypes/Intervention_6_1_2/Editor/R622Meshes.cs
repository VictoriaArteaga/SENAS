using System.Collections.Generic;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.2: generador de mallas low-poly (flat shading, un vértice por cara) exclusivo del prototipo. Todas las piezas se
    /// construyen en código y se guardan como Meshes/R622_*.asset; ningún recurso compartido se usa ni se modifica.
    /// </summary>
    public class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<int>[] t;
        public MB(int submeshes) { t = new List<int>[submeshes]; for (int i = 0; i < submeshes; i++) t[i] = new List<int>(); }

        /// <summary>Triángulo visible desde el lado hacia el que apunta «outward» (orientación calculada, así no hay que ordenar los vértices a mano).</summary>
        public void Tri(int sm, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var tmp = b; b = c; c = tmp; }
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t[sm].Add(i); t[sm].Add(i + 1); t[sm].Add(i + 2);
        }
        public void Quad(int sm, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) { Tri(sm, a, b, c, outward); Tri(sm, a, c, d, outward); }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.subMeshCount = t.Length;
            for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }

    public static class R622Meshes
    {
        static Vector3 P(Matrix4x4 m, float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));
        static Vector3 O(Matrix4x4 m) => m.MultiplyPoint3x4(Vector3.zero);

        // ------------------------------------------------------------------ primitivas
        /// <summary>Caja centrada con las caras frontal y posterior achaflanadas (borde suave): 14 caras.</summary>
        public static void BevelBox(MB mb, int sm, Matrix4x4 m, Vector3 size, float b)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f; b = Mathf.Min(b, Mathf.Min(hx, hy) - 0.001f, hz - 0.001f);
            var o = O(m); var sx = new[] { -1f, 1f, 1f, -1f }; var sy = new[] { -1f, -1f, 1f, 1f };
            Vector3[] ff = new Vector3[4], fo = new Vector3[4], bo = new Vector3[4], bb = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                ff[i] = P(m, sx[i] * (hx - b), sy[i] * (hy - b), hz); fo[i] = P(m, sx[i] * hx, sy[i] * hy, hz - b);
                bo[i] = P(m, sx[i] * hx, sy[i] * hy, -hz + b); bb[i] = P(m, sx[i] * (hx - b), sy[i] * (hy - b), -hz);
            }
            mb.Quad(sm, ff[0], ff[1], ff[2], ff[3], m.MultiplyVector(Vector3.forward));
            mb.Quad(sm, bb[0], bb[1], bb[2], bb[3], m.MultiplyVector(Vector3.back));
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                mb.Quad(sm, ff[i], ff[j], fo[j], fo[i], (ff[i] + ff[j] + fo[i] + fo[j]) * 0.25f - o);
                mb.Quad(sm, fo[i], fo[j], bo[j], bo[i], (fo[i] + fo[j] + bo[i] + bo[j]) * 0.25f - o);
                mb.Quad(sm, bo[i], bo[j], bb[j], bb[i], (bo[i] + bo[j] + bb[i] + bb[j]) * 0.25f - o);
            }
        }

        /// <summary>Tronco de cono con base en y=0 (r1 = 0 da un cono).</summary>
        public static void Frustum(MB mb, int sm, Matrix4x4 m, float r0, float r1, float h, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides, am = (a0 + a1) * 0.5f;
                var b0 = P(m, Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0); var b1 = P(m, Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                var t0 = P(m, Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1); var t1 = P(m, Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                var outSide = m.MultiplyVector(new Vector3(Mathf.Cos(am) * h, r0 - r1, Mathf.Sin(am) * h));
                if (r1 > 0.0001f) mb.Quad(sm, b0, b1, t1, t0, outSide); else mb.Tri(sm, b0, b1, t0, outSide);
                mb.Tri(sm, O(m), b0, b1, m.MultiplyVector(Vector3.down));
                if (r1 > 0.0001f) mb.Tri(sm, P(m, 0, h, 0), t0, t1, m.MultiplyVector(Vector3.up));
            }
        }
        public static void Cylinder(MB mb, int sm, Matrix4x4 m, float r, float h, int sides) => Frustum(mb, sm, m, r, r, h, sides);

        /// <summary>Caja que se estrecha: base (w0×d0) en y=0 y tapa (w1×d1) en y=h.</summary>
        public static void TaperBox(MB mb, int sm, Matrix4x4 m, float w0, float d0, float w1, float d1, float h)
        {
            var o = O(m); var sx = new[] { -1f, 1f, 1f, -1f }; var sz = new[] { -1f, -1f, 1f, 1f }; var b = new Vector3[4]; var tp = new Vector3[4];
            for (int i = 0; i < 4; i++) { b[i] = P(m, sx[i] * w0 * 0.5f, 0, sz[i] * d0 * 0.5f); tp[i] = P(m, sx[i] * w1 * 0.5f, h, sz[i] * d1 * 0.5f); }
            mb.Quad(sm, b[0], b[1], b[2], b[3], m.MultiplyVector(Vector3.down)); mb.Quad(sm, tp[0], tp[1], tp[2], tp[3], m.MultiplyVector(Vector3.up));
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; mb.Quad(sm, b[i], b[j], tp[j], tp[i], (b[i] + b[j] + tp[i] + tp[j]) * 0.25f - (o + m.MultiplyVector(Vector3.up) * h * 0.5f)); }
        }

        static Vector3[] _ico; static int[][] _icoF;
        static void InitIco()
        {
            if (_ico != null) return;
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            _ico = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < _ico.Length; i++) _ico[i].Normalize();
            _icoF = new[] { new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                            new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 } };
        }

        /// <summary>Icosfera (subdiv 0 = 20 caras, 1 = 80) de radio 1 escalada por «m».</summary>
        public static void Ico(MB mb, int sm, Matrix4x4 m, int subdiv)
        {
            InitIco(); var o = O(m);
            foreach (var f in _icoF)
            {
                Vector3 a = _ico[f[0]], b = _ico[f[1]], c = _ico[f[2]];
                if (subdiv <= 0) { mb.Tri(sm, m.MultiplyPoint3x4(a), m.MultiplyPoint3x4(b), m.MultiplyPoint3x4(c), (m.MultiplyPoint3x4((a + b + c) / 3f)) - o); continue; }
                Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                foreach (var tri in new[] { new[] { a, ab, ca }, new[] { ab, b, bc }, new[] { ca, bc, c }, new[] { ab, bc, ca } })
                    mb.Tri(sm, m.MultiplyPoint3x4(tri[0]), m.MultiplyPoint3x4(tri[1]), m.MultiplyPoint3x4(tri[2]), m.MultiplyPoint3x4((tri[0] + tri[1] + tri[2]) / 3f) - o);
            }
        }

        public static Matrix4x4 TRS(Vector3 pos, float yaw = 0f, float pitch = 0f, float roll = 0f, Vector3? scale = null)
            => Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, roll), scale ?? Vector3.one);

        // ------------------------------------------------------------------ letras y números (trazos con borde achaflanado)
        static readonly Dictionary<char, Vector2[][]> Glyphs = new Dictionary<char, Vector2[][]>
        {
            ['A'] = new[] { V(0, 0, 0.3f, 1, 0.6f, 0), V(0.12f, 0.36f, 0.48f, 0.36f) },
            ['B'] = new[] { V(0, 0, 0, 1), V(0, 1, 0.38f, 1, 0.55f, 0.88f, 0.55f, 0.63f, 0.38f, 0.5f, 0, 0.5f), V(0, 0.5f, 0.44f, 0.5f, 0.6f, 0.37f, 0.6f, 0.13f, 0.44f, 0, 0, 0) },
            ['C'] = new[] { V(0.6f, 0.84f, 0.46f, 1, 0.16f, 1, 0, 0.84f, 0, 0.16f, 0.16f, 0, 0.46f, 0, 0.6f, 0.16f) },
            ['D'] = new[] { V(0, 0, 0, 1, 0.34f, 1, 0.58f, 0.8f, 0.58f, 0.2f, 0.34f, 0, 0, 0) },
            ['E'] = new[] { V(0.56f, 1, 0, 1, 0, 0, 0.56f, 0), V(0, 0.5f, 0.45f, 0.5f) },
            ['L'] = new[] { V(0, 1, 0, 0, 0.56f, 0) },
            ['M'] = new[] { V(0, 0, 0, 1, 0.3f, 0.45f, 0.6f, 1, 0.6f, 0) },
            ['O'] = new[] { V(0.3f, 1, 0.52f, 0.9f, 0.6f, 0.65f, 0.6f, 0.35f, 0.52f, 0.1f, 0.3f, 0, 0.08f, 0.1f, 0, 0.35f, 0, 0.65f, 0.08f, 0.9f, 0.3f, 1) },
            ['Z'] = new[] { V(0, 1, 0.6f, 1, 0, 0, 0.6f, 0) },
            ['0'] = new[] { V(0.3f, 1, 0.52f, 0.9f, 0.6f, 0.65f, 0.6f, 0.35f, 0.52f, 0.1f, 0.3f, 0, 0.08f, 0.1f, 0, 0.35f, 0, 0.65f, 0.08f, 0.9f, 0.3f, 1) },
            ['1'] = new[] { V(0.12f, 0.78f, 0.34f, 1, 0.34f, 0), V(0.08f, 0, 0.58f, 0) },
            ['2'] = new[] { V(0, 0.84f, 0.16f, 1, 0.44f, 1, 0.6f, 0.84f, 0.6f, 0.64f, 0, 0, 0.6f, 0) },
            ['3'] = new[] { V(0, 0.88f, 0.14f, 1, 0.45f, 1, 0.6f, 0.86f, 0.6f, 0.64f, 0.45f, 0.52f, 0.2f, 0.52f), V(0.45f, 0.52f, 0.6f, 0.4f, 0.6f, 0.14f, 0.45f, 0, 0.14f, 0, 0, 0.12f) },
            ['4'] = new[] { V(0.44f, 0, 0.44f, 1, 0, 0.3f, 0.6f, 0.3f) },
            ['5'] = new[] { V(0.56f, 1, 0.06f, 1, 0.02f, 0.55f, 0.4f, 0.6f, 0.58f, 0.45f, 0.58f, 0.15f, 0.4f, 0, 0.12f, 0, 0, 0.1f) },
            ['7'] = new[] { V(0, 1, 0.6f, 1, 0.2f, 0) },
        };
        static Vector2[] V(params float[] xy) { var r = new Vector2[xy.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xy[2 * i], xy[2 * i + 1]); return r; }

        /// <summary>Glifo de altura «h» con base en y=0, centrado en x y en z, de «depth» de grosor; «stroke» = grosor del trazo.</summary>
        /// <summary>Los glifos se dibujan para leerse desde el lado +Z del objeto (el que mira al jugador): se invierte X y se centra.</summary>
        static Vector2 Mirror(Vector2 v, float h) => new Vector2(-(v.x * h - 0.3f * h), v.y * h);

        public static void Glyph(MB mb, int sm, Matrix4x4 m, char c, float h, float depth, float stroke)
        {
            if (!Glyphs.TryGetValue(c, out var strokes)) return;
            foreach (var line in strokes)
            {
                for (int i = 0; i < line.Length - 1; i++)
                {
                    Vector2 a = Mirror(line[i], h), b = Mirror(line[i + 1], h); var d = b - a; float len = d.magnitude + stroke * 0.04f;
                    float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; var mid = (a + b) * 0.5f;
                    BevelBox(mb, sm, m * Matrix4x4.TRS(new Vector3(mid.x, mid.y, 0), Quaternion.Euler(0, 0, ang), Vector3.one), new Vector3(len, stroke, depth), stroke * 0.28f);
                }
                foreach (var pt in line)      // nodo hexagonal: une los trazos y redondea los extremos
                {
                    var q = Mirror(pt, h);
                    Cylinder(mb, sm, m * Matrix4x4.TRS(new Vector3(q.x, q.y, -depth * 0.46f), Quaternion.Euler(90, 0, 0), Vector3.one), stroke * 0.5f, depth * 0.92f, 8);
                }
            }
        }

        public static Mesh GlyphMesh(char c, float h, float depth, float stroke, string name)
        {
            var mb = new MB(1); Glyph(mb, 0, Matrix4x4.TRS(new Vector3(0, stroke * 0.5f, 0), Quaternion.identity, Vector3.one), c, h, depth, stroke); return mb.Build(name);
        }

        // ------------------------------------------------------------------ mano decorativa (abstracta y estilizada; NO representa ninguna seña)
        public static void Hand(MB mb, int sm, Matrix4x4 m)
        {
            BevelBox(mb, sm, m * Matrix4x4.TRS(new Vector3(0, 0.15f, 0), Quaternion.identity, Vector3.one), new Vector3(0.58f, 0.34f, 0.28f), 0.05f);             // muñeca
            BevelBox(mb, sm, m * Matrix4x4.TRS(new Vector3(0, 0.78f, 0), Quaternion.identity, Vector3.one), new Vector3(1.0f, 0.95f, 0.32f), 0.1f);              // palma
            float[] xs = { -0.36f, -0.12f, 0.12f, 0.36f }, lens = { 0.62f, 0.78f, 0.72f, 0.55f }, rolls = { 8f, 3f, -3f, -8f };
            for (int i = 0; i < 4; i++)
            {
                var fm = m * Matrix4x4.TRS(new Vector3(xs[i], 1.2f, 0), Quaternion.Euler(0, 0, rolls[i]), Vector3.one);
                TaperBox(mb, sm, fm, 0.23f, 0.28f, 0.19f, 0.23f, lens[i]);
                Ico(mb, sm, fm * Matrix4x4.TRS(new Vector3(0, lens[i], 0), Quaternion.identity, new Vector3(0.105f, 0.12f, 0.125f)), 0);                      // punta redondeada
            }
            var tm = m * Matrix4x4.TRS(new Vector3(-0.46f, 0.5f, 0), Quaternion.Euler(0, 0, 50f), Vector3.one);
            TaperBox(mb, sm, tm, 0.27f, 0.3f, 0.21f, 0.25f, 0.55f); Ico(mb, sm, tm * Matrix4x4.TRS(new Vector3(0, 0.55f, 0), Quaternion.identity, new Vector3(0.12f, 0.13f, 0.14f)), 0);       // pulgar
        }
        public static Mesh HandMesh(string name) { var mb = new MB(1); Hand(mb, 0, Matrix4x4.identity); return mb.Build(name); }

        // ------------------------------------------------------------------ plataformas, anillos y formas geométricas planas
        public static Mesh Plinth(float r, float h, string name) { var mb = new MB(2); Cylinder(mb, 0, Matrix4x4.identity, r, h, 18); Cylinder(mb, 1, Matrix4x4.TRS(new Vector3(0, h, 0), Quaternion.identity, Vector3.one), r * 0.82f, h * 0.55f, 18); return mb.Build(name); }
        public static Mesh Flat(string shape, float size, float h, string name)
        {
            var mb = new MB(1);
            if (shape == "circle") Cylinder(mb, 0, Matrix4x4.identity, size * 0.5f, h, 20);
            else if (shape == "square") BevelBox(mb, 0, Matrix4x4.TRS(new Vector3(0, h * 0.5f, 0), Quaternion.Euler(90, 0, 0), Vector3.one), new Vector3(size * 0.88f, size * 0.88f, h), h * 0.3f);
            else Cylinder(mb, 0, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 90, 0), Vector3.one), size * 0.62f, h, 3);
            return mb.Build(name);
        }
        public static Mesh RingFlat(float r0, float r1, float h, int sides, string name)
        {
            var mb = new MB(1);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 i0 = new Vector3(Mathf.Cos(a0) * r0, h, Mathf.Sin(a0) * r0), i1 = new Vector3(Mathf.Cos(a1) * r0, h, Mathf.Sin(a1) * r0), o0 = new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1), o1 = new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                mb.Quad(0, i0, i1, o1, o0, Vector3.up);
            }
            return mb.Build(name);
        }

        // ------------------------------------------------------------------ árboles, arbustos y flores (submallas: 0 tronco, 1 copa)
        public static Mesh Tree(string kind, string name)
        {
            var mb = new MB(2); Matrix4x4 T(float y) => Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.identity, Vector3.one);
            Matrix4x4 S(Vector3 p, Vector3 s) => Matrix4x4.TRS(p, Quaternion.identity, s);
            switch (kind)
            {
                case "round": Cylinder(mb, 0, Matrix4x4.identity, 0.2f, 1.5f, 6); Ico(mb, 1, S(new Vector3(0, 2.3f, 0), new Vector3(1.25f, 1.05f, 1.25f)), 1); Ico(mb, 1, S(new Vector3(0.55f, 1.85f, 0.2f), new Vector3(0.7f, 0.6f, 0.7f)), 0); break;
                case "cone": Cylinder(mb, 0, Matrix4x4.identity, 0.2f, 0.9f, 6); Frustum(mb, 1, T(0.6f), 1.35f, 0.12f, 1.5f, 8); Frustum(mb, 1, T(1.5f), 1.05f, 0.08f, 1.35f, 8); Frustum(mb, 1, T(2.3f), 0.72f, 0f, 1.25f, 8); break;
                case "tall": Cylinder(mb, 0, Matrix4x4.identity, 0.18f, 2.5f, 6); Ico(mb, 1, S(new Vector3(0, 3.6f, 0), new Vector3(0.85f, 1.6f, 0.85f)), 1); break;
                case "cluster": Cylinder(mb, 0, Matrix4x4.identity, 0.19f, 1.3f, 6); Ico(mb, 1, S(new Vector3(0, 2.0f, 0), new Vector3(0.9f, 0.85f, 0.9f)), 0); Ico(mb, 1, S(new Vector3(0.75f, 1.65f, 0.1f), new Vector3(0.75f, 0.7f, 0.75f)), 0); Ico(mb, 1, S(new Vector3(-0.6f, 1.75f, -0.4f), new Vector3(0.7f, 0.65f, 0.7f)), 0); break;
                case "umbrella": Cylinder(mb, 0, Matrix4x4.identity, 0.17f, 1.7f, 6); Ico(mb, 1, S(new Vector3(0, 2.1f, 0), new Vector3(1.7f, 0.55f, 1.7f)), 1); break;
                case "shrub": Ico(mb, 1, S(new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.5f, 0.7f)), 0); Ico(mb, 1, S(new Vector3(0.5f, 0.25f, 0.15f), new Vector3(0.5f, 0.38f, 0.5f)), 0); Ico(mb, 1, S(new Vector3(-0.45f, 0.22f, -0.2f), new Vector3(0.45f, 0.34f, 0.45f)), 0); break;
                case "hedge": BevelBox(mb, 1, Matrix4x4.TRS(new Vector3(0, 0.45f, 0), Quaternion.identity, Vector3.one), new Vector3(1.3f, 0.9f, 1.3f), 0.14f); break;
            }
            return mb.Build(name);
        }

        /// <summary>Parche de flores (submallas: 0 tallos, 1 pétalos, 2 centros). «ordered» = en filas regulares; si no, dispersas con semilla.</summary>
        public static Mesh FlowerPatch(int count, float radius, int seed, bool ordered, string name)
        {
            var mb = new MB(3); var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                Vector2 p;
                if (ordered) { int cols = Mathf.CeilToInt(Mathf.Sqrt(count)); p = new Vector2((i % cols - (cols - 1) * 0.5f) * 0.42f, (i / cols - (count / cols) * 0.5f) * 0.42f); }
                else { float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * radius; p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
                float h = ordered ? 0.4f : 0.3f + 0.22f * (float)rng.NextDouble(); var pos = new Vector3(p.x, 0, p.y);
                BevelBox(mb, 0, Matrix4x4.TRS(pos + new Vector3(0, h * 0.5f, 0), Quaternion.identity, Vector3.one), new Vector3(0.05f, h, 0.05f), 0.01f);
                Cylinder(mb, 1, Matrix4x4.TRS(pos + new Vector3(0, h, 0), Quaternion.Euler(0, (float)rng.NextDouble() * 60f, 0), Vector3.one), 0.15f, 0.06f, 6);
                Cylinder(mb, 2, Matrix4x4.TRS(pos + new Vector3(0, h + 0.05f, 0), Quaternion.identity, Vector3.one), 0.055f, 0.05f, 5);
            }
            return mb.Build(name);
        }

        // ------------------------------------------------------------------ reloj y torre del reloj (Aldea de los Meses)
        /// <summary>Esfera de reloj en el plano XY mirando a +Z. Submallas: 0 aro, 1 esfera, 2 marcas y agujas.</summary>
        public static Mesh ClockFace(float r, string name)
        {
            var mb = new MB(3); var face = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one);
            Cylinder(mb, 0, face * Matrix4x4.TRS(new Vector3(0, -0.06f, 0), Quaternion.identity, Vector3.one), r, 0.18f, 20);
            Cylinder(mb, 1, face * Matrix4x4.TRS(new Vector3(0, 0.1f, 0), Quaternion.identity, Vector3.one), r * 0.86f, 0.06f, 20);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * r * 0.7f; bool quarter = i % 3 == 0;
                BevelBox(mb, 2, Matrix4x4.TRS(p + new Vector3(0, 0, 0.2f), Quaternion.Euler(0, 0, -i * 30f), Vector3.one), new Vector3(quarter ? 0.16f : 0.1f, quarter ? 0.34f : 0.22f, 0.07f), 0.02f);
            }
            BevelBox(mb, 2, Matrix4x4.TRS(new Vector3(-0.17f * r, 0.27f * r, 0.22f), Quaternion.Euler(0, 0, 35f), Vector3.one), new Vector3(0.12f, r * 0.62f, 0.07f), 0.03f);        // hora ≈ 10
            BevelBox(mb, 2, Matrix4x4.TRS(new Vector3(0.23f * r, 0.36f * r, 0.24f), Quaternion.Euler(0, 0, -30f), Vector3.one), new Vector3(0.09f, r * 0.82f, 0.07f), 0.025f);      // minutos ≈ 10
            Cylinder(mb, 2, Matrix4x4.TRS(new Vector3(0, 0, 0.2f), Quaternion.Euler(90, 0, 0), Vector3.one), 0.12f, 0.1f, 8);
            return mb.Build(name);
        }

        /// <summary>Torre (submallas: 0 cuerpo, 1 techo, 2 remates): base 2,0 m, 5,2 m de alto, techo piramidal y veleta.</summary>
        public static Mesh ClockTower(string name)
        {
            var mb = new MB(3);
            BevelBox(mb, 0, Matrix4x4.TRS(new Vector3(0, 0.25f, 0), Quaternion.identity, Vector3.one), new Vector3(2.7f, 0.5f, 2.7f), 0.1f);
            TaperBox(mb, 0, Matrix4x4.identity * Matrix4x4.TRS(new Vector3(0, 0.45f, 0), Quaternion.identity, Vector3.one), 2.0f, 2.0f, 1.7f, 1.7f, 3.9f);
            BevelBox(mb, 2, Matrix4x4.TRS(new Vector3(0, 4.45f, 0), Quaternion.identity, Vector3.one), new Vector3(2.2f, 0.22f, 2.2f), 0.06f);
            Frustum(mb, 1, Matrix4x4.TRS(new Vector3(0, 4.55f, 0), Quaternion.Euler(0, 45, 0), Vector3.one), 1.5f, 0f, 1.35f, 4);
            Cylinder(mb, 2, Matrix4x4.TRS(new Vector3(0, 5.85f, 0), Quaternion.identity, Vector3.one), 0.05f, 0.55f, 5);
            Ico(mb, 2, Matrix4x4.TRS(new Vector3(0, 6.45f, 0), Quaternion.identity, new Vector3(0.14f, 0.14f, 0.14f)), 0);
            return mb.Build(name);
        }

        // ------------------------------------------------------------------ calendario (submallas: 0 hoja, 1 cabecera, 2 anillos y postes, 3-6 estaciones)
        public static Mesh Calendar(string name)
        {
            var mb = new MB(7);
            BevelBox(mb, 2, Matrix4x4.TRS(new Vector3(-1.55f, 1.2f, -0.12f), Quaternion.identity, Vector3.one), new Vector3(0.16f, 2.4f, 0.16f), 0.03f);
            BevelBox(mb, 2, Matrix4x4.TRS(new Vector3(1.55f, 1.2f, -0.12f), Quaternion.identity, Vector3.one), new Vector3(0.16f, 2.4f, 0.16f), 0.03f);
            BevelBox(mb, 0, Matrix4x4.TRS(new Vector3(0, 1.7f, 0), Quaternion.identity, Vector3.one), new Vector3(3.2f, 2.2f, 0.16f), 0.07f);
            BevelBox(mb, 1, Matrix4x4.TRS(new Vector3(0, 2.52f, 0.06f), Quaternion.identity, Vector3.one), new Vector3(3.2f, 0.5f, 0.2f), 0.07f);
            for (int i = -1; i <= 1; i += 2) Cylinder(mb, 2, Matrix4x4.TRS(new Vector3(i * 0.9f, 2.7f, 0.06f), Quaternion.Euler(90, 0, 0), Vector3.one), 0.09f, 0.3f, 8);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                {
                    int month = row * 4 + col; int season = ((month + 1) % 12) / 3;        // dic–feb invierno, mar–may primavera, jun–ago verano, sep–nov otoño
                    BevelBox(mb, 3 + season, Matrix4x4.TRS(new Vector3(-1.2f + col * 0.8f, 1.95f - row * 0.62f, 0.1f), Quaternion.identity, Vector3.one), new Vector3(0.7f, 0.5f, 0.1f), 0.04f);
                }
            return mb.Build(name);
        }
    }
}
