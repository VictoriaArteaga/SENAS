using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Renderiza un prefab de Avatars2 (frente, perfil, espalda y primer plano de la cara) a PNG para compararlo con el GLB original.
    /// Uso por l√≠nea de comandos: -executeMethod Lumina.EditorTools.Avatars2Preview.Render -avatar carol -out {carpeta}
    /// </summary>
    public static class Avatars2Preview
    {
        public static void Render()
        {
            string id = Arg("-avatar"), outDir = Arg("-out");
            Directory.CreateDirectory(outDir);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Prefabs/PlayerAvatars/Avatar_{id}.prefab");
            var go = (GameObject)Object.Instantiate(prefab);

            var lightGo = new GameObject("Luz");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.nearClipPlane = 8f; cam.farClipPlane = 12f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            var rt = new RenderTexture(700, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;

            // Las texturas se suben a la GPU de forma asÌncrona: se renderiza durante varios ciclos del editor antes de capturar.
            int ticks = 0;
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                cam.orthographicSize = 1f; cam.Render();
                if (++ticks < 120) return;
                EditorApplication.update -= step;
                Capture(cam, rt, outDir, id);
                EditorApplication.Exit(0);
            };
            EditorApplication.update += step;
        }

        private static void Capture(Camera cam, RenderTexture rt, string outDir, string id)
        {
            Shot(cam, rt, outDir, id, "front", 0f, new Vector3(0f, 0.85f, 0f), 1.95f);
            Shot(cam, rt, outDir, id, "side", 90f, new Vector3(0f, 0.85f, 0f), 1.95f);
            Shot(cam, rt, outDir, id, "back", 180f, new Vector3(0f, 0.85f, 0f), 1.95f);
            Shot(cam, rt, outDir, id, "face", 0f, new Vector3(0f, 1.48f, 0f), 0.5f);
            Debug.Log($"[Avatars2Preview] Renders en {outDir}");
        }

        private static void Shot(Camera cam, RenderTexture rt, string dir, string id, string name, float yaw, Vector3 target, float height)
        {
            cam.orthographicSize = height / 2f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.SetPositionAndRotation(target + rot * new Vector3(0f, 0f, 10f), rot * Quaternion.Euler(0f, 180f, 0f));   // mira hacia el modelo desde +Z (su frente)
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{dir}/{id}_unity_{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static string Arg(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
