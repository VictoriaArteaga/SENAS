using System.Text;
using UnityEditor;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>Solo lectura: mide los prefabs candidatos y comprueba si los recursos de Holotna cargan en Unity 6.</summary>
    public static class Proto612Measure
    {
        const string Village = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        const string Holotna = "Assets/Models/Holotna/Mountain/";

        public static void Run()
        {
            var sb = new StringBuilder("[P612-MEASURE]\n");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Village.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Describe(sb, go, path);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Holotna + "Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Describe(sb, go, path);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { Holotna.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                sb.AppendLine($"TERRAINLAYER {path} cargado={(l != null)} difusa={(l != null && l.diffuseTexture != null ? l.diffuseTexture.name : "null")} tamano={(l != null ? l.tileSize.ToString() : "-")}");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:TerrainData", new[] { Holotna.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var d = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                sb.AppendLine($"TERRAINDATA {path} cargado={(d != null)} tam={(d != null ? d.size.ToString() : "-")} hm={(d != null ? d.heightmapResolution.ToString() : "-")} capas={(d != null ? d.terrainLayers.Length.ToString() : "-")}");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { Holotna.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                sb.AppendLine($"SHADER {path} cargado={(s != null)} soportado={(s != null && s.isSupported)}");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Holotna + "Materials" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                sb.AppendLine($"MATERIAL {path} shader={(m != null && m.shader != null ? m.shader.name : "null")} soportado={(m != null && m.shader != null && m.shader.isSupported)}");
            }
            Debug.Log(sb.ToString());
        }

        static void Describe(StringBuilder sb, GameObject go, string path)
        {
            if (go == null) { sb.AppendLine("NULO " + path); return; }
            Bounds b = default; bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            var cols = go.GetComponentsInChildren<Collider>(true);
            var kinds = new System.Collections.Generic.List<string>();
            foreach (var c in cols)
            {
                if (c is MeshCollider mc) kinds.Add("Mesh(convex=" + mc.convex + ",tris=" + (mc.sharedMesh != null ? mc.sharedMesh.triangles.Length / 3 : -1) + ")");
                else kinds.Add(c.GetType().Name);
            }
            int tris = 0; foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            sb.AppendLine($"{System.IO.Path.GetFileName(path),-34} centro=({b.center.x:0.00},{b.center.y:0.00},{b.center.z:0.00}) tam=({b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}) min=({b.min.x:0.00},{b.min.y:0.00},{b.min.z:0.00}) tris={tris} colliders=[{string.Join(";", kinds)}]");
        }
    }
}
