using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>Diagnóstico de solo lectura de la escena Terrain (no guarda nada).</summary>
    public static class Proto612Debug
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(Proto612Builder.Root + "Scenes/Prototype_Terrain.unity", OpenSceneMode.Single);
            var t = Object.FindFirstObjectByType<Terrain>();
            var d = t.terrainData;
            var sb = new System.Text.StringBuilder("[P612-DEBUG]\n");
            sb.AppendLine($"capas={d.terrainLayers.Length} alphamapLayers={d.alphamapLayers} alphamapTextures={d.alphamapTextureCount} res={d.alphamapResolution}");
            for (int i = 0; i < d.terrainLayers.Length; i++) sb.AppendLine($"capa{i}: {(d.terrainLayers[i] != null ? d.terrainLayers[i].name + " tex=" + (d.terrainLayers[i].diffuseTexture != null ? d.terrainLayers[i].diffuseTexture.name : "null") : "null")}");
            foreach (var p in new[] { new Vector2(0, 0), new Vector2(0, 9), new Vector2(-6, 0), new Vector2(-30, 0) })
            {
                int x = Mathf.Clamp((int)((p.x + 45f) / 90f * d.alphamapResolution), 0, d.alphamapResolution - 1);
                int z = Mathf.Clamp((int)((p.y + 45f) / 90f * d.alphamapResolution), 0, d.alphamapResolution - 1);
                var a = d.GetAlphamaps(x, z, 1, 1);
                sb.AppendLine($"alpha en {p}: " + a[0, 0, 0].ToString("0.00") + " " + a[0, 0, 1].ToString("0.00") + " " + a[0, 0, 2].ToString("0.00") + " " + a[0, 0, 3].ToString("0.00"));
            }
            sb.AppendLine($"materialTemplate={(t.materialTemplate != null ? t.materialTemplate.shader.name : "null")} basemapDistance={t.basemapDistance} drawInstanced={t.drawInstanced}");
            Debug.Log(sb.ToString());
        }
    }
}
