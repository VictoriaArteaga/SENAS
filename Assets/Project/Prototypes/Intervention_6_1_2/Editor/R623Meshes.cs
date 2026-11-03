using System.Collections.Generic;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.3: mallas procedurales low-poly de paisajismo (árboles, arbustos, hierba, juncos, flores y parterres).
    /// Reutiliza las primitivas de R622Meshes sin modificarlas. Submallas: árboles [tronco, hoja]; arbustos [hoja, fruto]; flores [tallo, pétalo, centro].
    /// </summary>
    public static class R623Meshes
    {
        static Matrix4x4 T(Vector3 p, float yaw = 0f) => Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
        static Matrix4x4 S(Vector3 p, Vector3 s) => Matrix4x4.TRS(p, Quaternion.identity, s);
        static void Ball(MB mb, float x, float y, float z, float sx, float sy, float sz, int sub = 0) => R622Meshes.Ico(mb, 1, S(new Vector3(x, y, z), new Vector3(sx, sy, sz)), sub);

        static void ShrubBall(MB mb, float x, float y, float z, float sx, float sy, float sz, int sub = 0) => R622Meshes.Ico(mb, 0, S(new Vector3(x, y, z), new Vector3(sx, sy, sz)), sub);      // arbustos: la hoja es la submalla 0 (el fruto, la 1)

        // ================================================================== árboles
        public static Mesh Tree(string kind, string name)
        {
            var mb = new MB(2);
            switch (kind)
            {
                case "oak":        // copa irregular y ancha, tronco grueso
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.27f, 1.7f, 6);
                    Ball(mb, 0f, 2.7f, 0f, 1.4f, 1.0f, 1.3f, 1); Ball(mb, 0.95f, 2.25f, 0.35f, 0.85f, 0.75f, 0.8f); Ball(mb, -0.85f, 2.35f, -0.4f, 0.9f, 0.8f, 0.85f); Ball(mb, 0.1f, 3.3f, -0.55f, 0.8f, 0.7f, 0.75f); Ball(mb, -0.3f, 2.1f, 0.9f, 0.7f, 0.6f, 0.7f);
                    break;
                case "poplar":     // alto y estrecho: punto de referencia visual
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.17f, 2.3f, 6);
                    Ball(mb, 0f, 3.5f, 0f, 0.72f, 1.55f, 0.72f, 1); Ball(mb, 0.12f, 4.9f, 0.05f, 0.5f, 0.95f, 0.5f);
                    break;
                case "pine_tall":  // pino de cuatro pisos
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.21f, 1.1f, 6);
                    R622Meshes.Frustum(mb, 1, T(new Vector3(0, 0.7f, 0)), 1.5f, 0.1f, 1.7f, 8); R622Meshes.Frustum(mb, 1, T(new Vector3(0, 1.7f, 0), 22f), 1.2f, 0.08f, 1.5f, 8);
                    R622Meshes.Frustum(mb, 1, T(new Vector3(0, 2.6f, 0), 45f), 0.9f, 0.06f, 1.4f, 8); R622Meshes.Frustum(mb, 1, T(new Vector3(0, 3.4f, 0)), 0.6f, 0f, 1.35f, 8);
                    break;
                case "sapling":    // árbol pequeño
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.09f, 0.95f, 6); Ball(mb, 0f, 1.45f, 0f, 0.62f, 0.58f, 0.62f, 1); Ball(mb, 0.3f, 1.2f, 0.1f, 0.32f, 0.3f, 0.32f);
                    break;
                case "blossom":    // copa de nube con bolas alrededor (floración)
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.2f, 1.35f, 6);
                    Ball(mb, 0f, 2.1f, 0f, 0.95f, 0.8f, 0.95f, 1);
                    for (int i = 0; i < 5; i++) { float a = i * 72f * Mathf.Deg2Rad + 0.3f; Ball(mb, Mathf.Sin(a) * 0.95f, 1.85f + (i % 2) * 0.35f, Mathf.Cos(a) * 0.95f, 0.55f, 0.5f, 0.55f); }
                    break;
                case "willow":     // sauce: copa ancha con ramas colgantes (para el río)
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.23f, 1.9f, 6); Ball(mb, 0f, 2.95f, 0f, 1.6f, 0.8f, 1.6f, 1);
                    for (int i = 0; i < 6; i++) { float a = i * 60f * Mathf.Deg2Rad; Ball(mb, Mathf.Sin(a) * 1.35f, 2.05f, Mathf.Cos(a) * 1.35f, 0.34f, 0.95f, 0.34f); }
                    break;
                case "birch":      // abedul esbelto
                    R622Meshes.Cylinder(mb, 0, T(Vector3.zero), 0.12f, 2.7f, 6); Ball(mb, 0f, 3.3f, 0f, 0.85f, 1.1f, 0.85f, 1); Ball(mb, 0.45f, 2.65f, 0.2f, 0.48f, 0.55f, 0.48f);
                    break;
            }
            return mb.Build(name);
        }

        // ================================================================== arbustos, hierba y juncos
        public static Mesh Shrub(string kind, string name)
        {
            var mb = new MB(2);
            switch (kind)
            {
                case "bush_low": ShrubBall(mb, 0f, 0.28f, 0f, 0.75f, 0.45f, 0.7f, 1); ShrubBall(mb, 0.55f, 0.22f, 0.25f, 0.5f, 0.34f, 0.5f); ShrubBall(mb, -0.5f, 0.2f, -0.3f, 0.45f, 0.32f, 0.45f); ShrubBall(mb, 0.1f, 0.2f, -0.6f, 0.4f, 0.3f, 0.4f); break;
                case "bush_tall": ShrubBall(mb, 0f, 0.55f, 0f, 0.62f, 0.62f, 0.62f, 1); ShrubBall(mb, 0.45f, 0.4f, 0.15f, 0.48f, 0.5f, 0.48f); ShrubBall(mb, -0.4f, 0.45f, -0.2f, 0.45f, 0.55f, 0.45f); break;
                case "bush_berry":
                    ShrubBall(mb, 0f, 0.3f, 0f, 0.7f, 0.5f, 0.7f, 1); ShrubBall(mb, 0.5f, 0.24f, 0.2f, 0.45f, 0.34f, 0.45f);
                    for (int i = 0; i < 7; i++) { float a = i * 51f * Mathf.Deg2Rad; var m = S(new Vector3(Mathf.Sin(a) * 0.62f, 0.38f + (i % 3) * 0.1f, Mathf.Cos(a) * 0.62f), Vector3.one * 0.09f); R622Meshes.Ico(mb, 1, m, 0); }
                    break;
                case "tuft":     // mata de hierba: hojas finas inclinadas
                    for (int i = 0; i < 7; i++) { float a = i * 51f; float tilt = 8f + (i % 3) * 7f; var m = Matrix4x4.TRS(new Vector3(Mathf.Sin(a) * 0.07f, 0, Mathf.Cos(a) * 0.07f), Quaternion.Euler(tilt, a, 0), Vector3.one); R622Meshes.TaperBox(mb, 0, m, 0.07f, 0.03f, 0.01f, 0.01f, 0.32f + (i % 3) * 0.1f); }
                    break;
                case "reeds":    // juncos con espiga (para la orilla)
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a) * 0.14f, 0, Mathf.Cos(a) * 0.14f); float h = 1.0f + (i % 3) * 0.28f; var m = Matrix4x4.TRS(p, Quaternion.Euler(5f + (i % 2) * 5f, i * 60f, 0), Vector3.one);
                        R622Meshes.TaperBox(mb, 0, m, 0.05f, 0.05f, 0.025f, 0.025f, h); if (i % 2 == 0) R622Meshes.Cylinder(mb, 1, m * Matrix4x4.TRS(new Vector3(0, h - 0.12f, 0), Quaternion.identity, Vector3.one), 0.045f, 0.26f, 5);
                    }
                    break;
            }
            return mb.Build(name);
        }

        // ================================================================== flores
        static void Flower(MB mb, string kind, Vector3 pos, System.Random rng, int smStem, int smPetal, int smCenter)
        {
            float r = (float)rng.NextDouble(); float yaw = (float)rng.NextDouble() * 60f;
            float h = kind == "spike" ? 0.42f + 0.2f * r : kind == "tulip" ? 0.34f + 0.14f * r : 0.28f + 0.2f * r;
            R622Meshes.BevelBox(mb, smStem, T(pos + new Vector3(0, h * 0.5f, 0)), new Vector3(0.05f, h, 0.05f), 0.01f);
            switch (kind)
            {
                case "tulip": R622Meshes.Frustum(mb, smPetal, T(pos + new Vector3(0, h - 0.06f, 0), yaw), 0.05f, 0.14f, 0.22f, 6); R622Meshes.Cylinder(mb, smCenter, T(pos + new Vector3(0, h + 0.15f, 0)), 0.05f, 0.03f, 5); break;
                case "spike": for (int i = 0; i < 3; i++) R622Meshes.Frustum(mb, smPetal, T(pos + new Vector3(0, h + i * 0.1f, 0), yaw), 0.085f - i * 0.02f, 0.06f - i * 0.015f, 0.12f, 5); R622Meshes.Cylinder(mb, smCenter, T(pos + new Vector3(0, h + 0.3f, 0)), 0.02f, 0.05f, 4); break;
                case "poppy": R622Meshes.Cylinder(mb, smPetal, T(pos + new Vector3(0, h, 0), yaw), 0.21f, 0.04f, 8); R622Meshes.Cylinder(mb, smCenter, T(pos + new Vector3(0, h + 0.04f, 0)), 0.08f, 0.05f, 6); break;
                default: R622Meshes.Cylinder(mb, smPetal, T(pos + new Vector3(0, h, 0), yaw), 0.16f, 0.05f, 6); R622Meshes.Cylinder(mb, smCenter, T(pos + new Vector3(0, h + 0.05f, 0)), 0.055f, 0.05f, 5); break;
            }
        }

        static List<Vector2> Layout(string layout, int count, float size, System.Random rng)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                float u = count > 1 ? i / (float)(count - 1) : 0.5f;
                switch (layout)
                {
                    case "ring": { float a = (i + (float)rng.NextDouble() * 0.3f) / count * Mathf.PI * 2f; pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size); break; }
                    case "arc": { float a = Mathf.Lerp(-70f, 70f, u) * Mathf.Deg2Rad; pts.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a) - 0.6f) * size + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.14f); break; }
                    case "curve": { float x = (u - 0.5f) * size; pts.Add(new Vector2(x, Mathf.Sin(u * Mathf.PI * 2f) * size * 0.16f + ((float)rng.NextDouble() - 0.5f) * 0.14f)); break; }
                    case "line": pts.Add(new Vector2((u - 0.5f) * size, ((float)rng.NextDouble() - 0.5f) * 0.12f)); break;
                    case "grid": { int cols = Mathf.CeilToInt(Mathf.Sqrt(count)); pts.Add(new Vector2((i % cols - (cols - 1) * 0.5f) * size, (i / cols - (Mathf.CeilToInt(count / (float)cols) - 1) * 0.5f) * size)); break; }
                    case "diamond": { int cols = Mathf.CeilToInt(Mathf.Sqrt(count)); var g = new Vector2((i % cols - (cols - 1) * 0.5f) * size, (i / cols - (Mathf.CeilToInt(count / (float)cols) - 1) * 0.5f) * size); pts.Add(new Vector2(g.x - g.y, g.x + g.y) * 0.72f); break; }
                    default: { float a = (float)rng.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rng.NextDouble()) * size; pts.Add(new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr)); break; }
                }
            }
            return pts;
        }

        /// <summary>Grupo de flores: kind = daisy|tulip|spike|poppy; layout = scatter|ring|arc|curve|line|grid|diamond. Submallas [tallo, pétalo, centro].</summary>
        public static Mesh Flowers(string kind, string layout, int count, float size, int seed, string name)
        {
            var mb = new MB(3); var rng = new System.Random(seed);
            foreach (var p in Layout(layout, count, size, rng)) Flower(mb, kind, new Vector3(p.x, 0, p.y), rng, 0, 1, 2);
            return mb.Build(name);
        }

        // ================================================================== parterres con borde
        /// <summary>Parterre cuadrado con marco bajo y flores en cuadrícula. Submallas [marco, tierra, tallo, pétalo, centro].</summary>
        public static Mesh SquareBed(float side, string flower, string name)
        {
            var mb = new MB(5); var rng = new System.Random(2623); float h = 0.3f, t = 0.22f, half = side * 0.5f;
            R622Meshes.BevelBox(mb, 0, T(new Vector3(0, h * 0.5f, half - t * 0.5f)), new Vector3(side, h, t), 0.05f); R622Meshes.BevelBox(mb, 0, T(new Vector3(0, h * 0.5f, -half + t * 0.5f)), new Vector3(side, h, t), 0.05f);
            R622Meshes.BevelBox(mb, 0, T(new Vector3(half - t * 0.5f, h * 0.5f, 0)), new Vector3(t, h, side - 2 * t), 0.05f); R622Meshes.BevelBox(mb, 0, T(new Vector3(-half + t * 0.5f, h * 0.5f, 0)), new Vector3(t, h, side - 2 * t), 0.05f);
            R622Meshes.BevelBox(mb, 1, T(new Vector3(0, 0.07f, 0)), new Vector3(side - 2 * t, 0.14f, side - 2 * t), 0.02f);
            int n = 4; float step = (side - 2 * t - 0.4f) / (n - 1);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) Flower(mb, flower, new Vector3(-half + t + 0.2f + i * step, 0.12f, -half + t + 0.2f + j * step), rng, 2, 3, 4);
            return mb.Build(name);
        }

        /// <summary>Parterre circular con borde de piedra y anillo de flores. Submallas [borde, tierra, tallo, pétalo, centro].</summary>
        public static Mesh RoundBed(float radius, string flower, string name)
        {
            var mb = new MB(5); var rng = new System.Random(3623); int seg = 14;
            for (int i = 0; i < seg; i++) { float a = i * 360f / seg; var dir = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad)); R622Meshes.BevelBox(mb, 0, Matrix4x4.TRS(dir * radius + new Vector3(0, 0.15f, 0), Quaternion.Euler(0, a, 0), Vector3.one), new Vector3(radius * 2f * Mathf.PI / seg * 1.02f, 0.3f, 0.22f), 0.05f); }
            R622Meshes.Cylinder(mb, 1, T(Vector3.zero), radius - 0.1f, 0.14f, 14);
            for (int i = 0; i < 12; i++) { float a = i * 30f * Mathf.Deg2Rad; Flower(mb, flower, new Vector3(Mathf.Sin(a), 0.12f, Mathf.Cos(a)) * radius * 0.68f, rng, 2, 3, 4); }
            for (int i = 0; i < 5; i++) { float a = i * 72f * Mathf.Deg2Rad; Flower(mb, flower, new Vector3(Mathf.Sin(a), 0.12f, Mathf.Cos(a)) * radius * 0.32f, rng, 2, 3, 4); }
            return mb.Build(name);
        }
    }
}
