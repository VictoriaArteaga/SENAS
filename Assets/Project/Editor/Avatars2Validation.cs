using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Lumina.Auth;
using Lumina.Player;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Validación de un avatar de Avatars2 en condiciones reales (solo lectura: abre las escenas en memoria y NO las guarda).
    ///   Studio : vistas de estudio (frente, perfil izquierdo/derecho, espalda, tres cuartos, rostro).
    ///   World  : LuminaWorld con el PlayerAvatar real (catálogo temporal en memoria, sin tocar AvatarCatalog.asset): primera persona,
    ///            mirada hacia abajo, vistas externas, sombras, colisiones y movimiento con el CharacterController real.
    ///   Auth   : RegisterScene (selector): el prefab sobre la plataforma, con el pivote y la cámara reales.
    ///   Perf   : memoria de texturas, materiales, renderers y triángulos.
    /// Uso: -executeMethod Lumina.EditorTools.Avatars2Validation.{Studio|World|Auth|Perf} -avatar carol -out {carpeta}
    /// </summary>
    public static class Avatars2Validation
    {
        private const string PrefabDir = "Assets/Project/Prefabs/PlayerAvatars/";

        public static void Studio() => Drive(StudioRoutine());
        public static void World() => Drive(WorldRoutine());
        public static void Auth() => Drive(AuthRoutine());
        public static void MatLab() => Drive(MatLabRoutine());
        public static void Perf() { try { PerfReport(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogError(e); EditorApplication.Exit(1); } }

        // ───────────────────────── Estudio ─────────────────────────
        private static IEnumerator StudioRoutine()
        {
            string id = Arg("-avatar"), outDir = Arg("-out");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = (GameObject)UnityEngine.Object.Instantiate(Load(id));
            Sun(35f, 160f, 1.3f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            var cam = MakeCamera(700, 1000, out var rt);
            cam.orthographic = true; cam.nearClipPlane = 8f; cam.farClipPlane = 12f; cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);

            for (int i = 0; i < 120; i++) { cam.orthographicSize = 1f; cam.Render(); yield return null; }

            void Shot(string name, float yaw, Vector3 target, float height)
            {
                cam.orthographicSize = height / 2f;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                cam.transform.SetPositionAndRotation(target + rot * new Vector3(0f, 0f, 10f), rot * Quaternion.Euler(0f, 180f, 0f));
                Save(cam, rt, $"{outDir}/{Tag(id)}_unity_{name}.png");
            }
            var body = new Vector3(0f, 0.85f, 0f);
            Shot("front", 0f, body, 1.95f);
            Shot("left", 90f, body, 1.95f);
            Shot("right", -90f, body, 1.95f);
            Shot("back", 180f, body, 1.95f);
            Shot("threeq", 35f, body, 1.95f);
            Shot("face", 0f, new Vector3(0f, 1.48f, 0f), 0.5f);
        }

        // ───────────────────────── LuminaWorld + PlayerAvatar ─────────────────────────
        private static IEnumerator WorldRoutine()
        {
            string id = Arg("-avatar"), outDir = Arg("-out");
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/Project/Scenes/LuminaWorld.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            var fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            var pa = fpc.GetComponent<PlayerAvatar>();
            var cc = fpc.GetComponent<CharacterController>();
            var playerCam = fpc.GetComponentInChildren<Camera>();
            log.AppendLine($"Jugador '{fpc.name}' pos={fpc.transform.position:F2} rot={fpc.transform.eulerAngles:F1} | CC h={cc.height} r={cc.radius} centro={cc.center} | cámara local={playerCam.transform.localPosition:F2} fov={playerCam.fieldOfView} near={playerCam.nearClipPlane}");

            // Línea base de movimiento sin cuerpo.
            float baseDistance = Walk(cc, fpc.transform);
            fpc.transform.position -= fpc.transform.forward * 0f;

            // PlayerAvatar REAL: catálogo temporal en memoria con 'carol' y AuthSession.AvatarId = carol (no se guarda nada).
            var catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            var entries = (Array)Array.CreateInstance(typeof(AvatarCatalog.Entry), 1);
            entries.SetValue(new AvatarCatalog.Entry { id = id, displayName = id, prefab = Load(id) }, 0);
            typeof(AvatarCatalog).GetField("avatars", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(catalog, entries);
            typeof(PlayerAvatar).GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pa, catalog);
            AuthSession.SelectAvatar(id);
            typeof(PlayerAvatar).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pa, null);

            var bodyGo = pa.Body;
            if (Arg("-hide") != null && bodyGo != null)   // EXPERIMENTO en memoria (no se guarda): solo sombra para las partes indicadas
                foreach (var r in bodyGo.GetComponentsInChildren<MeshRenderer>(true))
                    if (Arg("-hide").Split(',').Any(n => r.name.EndsWith("_" + n, StringComparison.OrdinalIgnoreCase)))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            if (bodyGo == null) { log.AppendLine("[FALLA] PlayerAvatar no instanció el cuerpo."); Flush(outDir, "world_resultados.txt", log); yield break; }
            var b = Bounds(bodyGo);
            log.AppendLine($"[PlayerAvatar] CurrentId={pa.CurrentId} Body='{bodyGo.name}' localPos={bodyGo.transform.localPosition:F2} localRot={bodyGo.transform.localEulerAngles:F1} localScale={bodyGo.transform.localScale:F2}");
            log.AppendLine($"  Bounds mundo: min={b.min:F2} max={b.max:F2} tamaño={b.size:F2}; suelo del jugador y={fpc.transform.position.y:F2} (pies del modelo y={b.min.y:F2})");
            log.AppendLine($"  Cámara a la altura {playerCam.transform.position.y - fpc.transform.position.y:F2} m; coronilla del modelo {b.max.y - fpc.transform.position.y:F2} m; frente del rostro respecto a la cámara (m, + = delante): {(playerCam.transform.position - (bodyGo.transform.position)).z:F2} (en local z cuerpo={bodyGo.transform.localPosition.z:F2})");
            log.AppendLine("  Renderers de la instancia: " + string.Join(", ", bodyGo.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name + "=" + r.shadowCastingMode)));
            log.AppendLine("  Prefab en disco (debe seguir todo On): " + string.Join(", ", Load(id).GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name + "=" + r.shadowCastingMode)));
            var colliders = bodyGo.GetComponentsInChildren<Collider>(true);
            log.AppendLine($"  Colliders en el cuerpo: {colliders.Length} (activos: {colliders.Count(c => c.enabled)}); Animators: {bodyGo.GetComponentsInChildren<Animator>(true).Length}; Rigidbody: {bodyGo.GetComponentsInChildren<Rigidbody>(true).Length}");
            Physics.SyncTransforms();
            float withBody = Walk(cc, fpc.transform);
            log.AppendLine($"  Movimiento con el CharacterController (20 pasos de 0,1 m hacia delante): sin cuerpo {baseDistance:F2} m, con cuerpo {withBody:F2} m");

            // Cámara real del jugador.
            var fpCam = playerCam;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var saveTarget = fpCam.targetTexture; fpCam.targetTexture = rt;
            var savedRot = fpCam.transform.localRotation;
            for (int i = 0; i < 120; i++) { fpCam.Render(); yield return null; }
            foreach (var pitch in new[] { 0f, 30f, 55f, 80f })
            {
                fpCam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                Save(fpCam, rt, $"{outDir}/{Tag(id)}_world_fp_pitch{(int)pitch:D2}.png");
            }
            // Mirada hacia abajo con FOV más abierto para ver si los brazos asoman por los lados.
            float fov = fpCam.fieldOfView;
            foreach (var pitch in new[] { 55f, 80f })
            {
                fpCam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                fpCam.fieldOfView = 90f; Save(fpCam, rt, $"{outDir}/{Tag(id)}_world_fp_pitch{(int)pitch:D2}_fov90.png"); fpCam.fieldOfView = fov;
            }
            fpCam.transform.localRotation = savedRot; fpCam.targetTexture = saveTarget;

            // Vistas externas con una cámara auxiliar (la del jugador no se toca).
            var ext = MakeCamera(1280, 720, out var rt2);
            ext.fieldOfView = 35f; ext.nearClipPlane = 0.1f; ext.farClipPlane = 200f;
            var urp = ext.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urp == null) urp = ext.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            ext.clearFlags = playerCam.clearFlags; ext.backgroundColor = playerCam.backgroundColor;
            Vector3 feet = new Vector3(b.center.x, fpc.transform.position.y, b.center.z);
            Vector3 fwd = fpc.transform.forward, right = fpc.transform.right;
            for (int i = 0; i < 30; i++) { ext.Render(); yield return null; }
            void Ext(string name, Vector3 pos, Vector3 target)
            {
                ext.transform.position = pos; ext.transform.LookAt(target); Save(ext, rt2, $"{outDir}/{Tag(id)}_world_ext_{name}.png");
            }
            Ext("front", feet + fwd * 5f + Vector3.up * 1.3f, feet + Vector3.up * 0.9f);
            Ext("side", feet + right * 5f + Vector3.up * 1.3f, feet + Vector3.up * 0.9f);
            Ext("back", feet - fwd * 5f + Vector3.up * 1.3f, feet + Vector3.up * 0.9f);
            Ext("face", feet + fwd * 1.4f + Vector3.up * 1.5f, feet + Vector3.up * 1.45f);
            Ext("top_shadow", feet + Vector3.up * 8f - fwd * 2f, feet);       // sombra sobre el suelo
            Ext("over_shoulder", feet - fwd * 0.9f + Vector3.up * 2.2f, feet + fwd * 2f + Vector3.up * 0.5f);

            log.AppendLine($"  Sombras: MeshRenderer.shadowCastingMode={bodyGo.GetComponentsInChildren<MeshRenderer>().Select(r => r.shadowCastingMode.ToString()).Distinct().Aggregate((a, c) => a + "/" + c)}, receiveShadows={bodyGo.GetComponentsInChildren<MeshRenderer>().All(r => r.receiveShadows)}");
            Flush(outDir, "world_resultados.txt", log);
        }

        private static float Walk(CharacterController cc, Transform t)
        {
            Vector3 start = t.position;
            for (int i = 0; i < 20; i++) { cc.Move(t.forward * 0.1f); Physics.SyncTransforms(); }
            float d = Vector3.Distance(new Vector3(start.x, 0, start.z), new Vector3(t.position.x, 0, t.position.z));
            t.position = start; Physics.SyncTransforms();
            return d;
        }

        // ───────────────────────── Selector (RegisterScene) ─────────────────────────
        private static IEnumerator AuthRoutine()
        {
            string id = Arg("-avatar"), outDir = Arg("-out");
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/Project/Scenes/RegisterScene.unity", OpenSceneMode.Single);
            var pivot = GameObject.Find("AvatarPivot").transform;
            var cam = Camera.main;
            log.AppendLine($"Pivote '{pivot.name}' pos={pivot.position:F2} escala={pivot.lossyScale:F2}; cámara {cam.name} pos={cam.transform.position:F2} rot={cam.transform.eulerAngles:F1} fov={cam.fieldOfView}");
            foreach (Transform c in pivot) log.AppendLine($"  opción existente: {c.name} bounds={Bounds(c.gameObject).size:F2}");

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            foreach (Transform c in pivot) c.gameObject.SetActive(false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(id), pivot);
            instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity;
            var bounds = Bounds(instance);
            log.AppendLine($"  {id} instanciado como el selector (mismo prefab): tamaño {bounds.size:F2}, base y={bounds.min.y:F2}");
            log.AppendLine("  Renderers en el selector: " + string.Join(", ", instance.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name + "=" + r.shadowCastingMode + (r.enabled ? "" : " (desactivado)"))));
            for (int i = 0; i < 120; i++) { cam.Render(); yield return null; }
            foreach (var yaw in new[] { 165f, 90f, 0f, 255f })
            {
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Save(cam, rt, $"{outDir}/{Tag(id)}_auth_yaw{(int)yaw}.png");
            }
            // Referencia: un avatar actual (luna) en el mismo encuadre.
            instance.SetActive(false);
            var luna = pivot.Find("luna"); if (luna != null)
            {
                luna.gameObject.SetActive(true); pivot.localRotation = Quaternion.Euler(0f, 165f, 0f);
                for (int i = 0; i < 5; i++) { cam.Render(); yield return null; }
                Save(cam, rt, $"{outDir}/luna_auth_yaw165_referencia.png");
                log.AppendLine($"  referencia luna: tamaño {Bounds(luna.gameObject).size:F2}");
            }
            cam.targetTexture = null;
            Flush(outDir, "auth_resultados.txt", log);
        }


        // ───────────────────────── Laboratorio de material (solo diagnóstico, en memoria) ─────────────────────────
        /// <summary>Renderiza el rostro con variantes del material aplicadas a una INSTANCIA (renderer.material): no se guarda nada.</summary>
        private static IEnumerator MatLabRoutine()
        {
            string id = Arg("-avatar"), outDir = Arg("-out");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = (GameObject)UnityEngine.Object.Instantiate(Load(id));
            Sun(35f, 160f, 1.3f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            var cam = MakeCamera(700, 700, out var rt);
            cam.orthographic = true; cam.nearClipPlane = 8f; cam.farClipPlane = 12f; cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            var mat = go.GetComponentInChildren<Renderer>().material;   // instancia
            var original = new Material(mat);
            for (int i = 0; i < 120; i++) { cam.orthographicSize = 1f; cam.Render(); yield return null; }
            float baseSmooth = mat.GetFloat("_Smoothness");

            void Face(string name)
            {
                cam.orthographicSize = 0.25f;
                cam.transform.SetPositionAndRotation(new Vector3(0f, 1.52f, 0f) + new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f));
                Save(cam, rt, $"{outDir}/{Tag(id)}_lab_{name}.png");
            }
            void Reset() { mat.CopyPropertiesFromMaterial(original); mat.shaderKeywords = original.shaderKeywords; }

            Face("0_actual");
            mat.SetFloat("_Smoothness", 0f); yield return null; Face("1_smoothness0"); Reset();
            mat.SetFloat("_Metallic", 0f); yield return null; Face("2_metallic0"); Reset();
            mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); yield return null; Face("3_sin_reflejos_entorno"); Reset();
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); yield return null; Face("4_sin_brillos_especulares"); Reset();
            mat.SetTexture("_BumpMap", null); mat.DisableKeyword("_NORMALMAP"); yield return null; Face("5_sin_normal"); Reset();
            mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f); mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); yield return null; Face("6_todo_apagado"); Reset();
            mat.SetFloat("_Smoothness", baseSmooth * 0.5f); yield return null; Face("7_smoothness_mitad"); Reset();
            File.WriteAllText($"{outDir}/{Tag(id)}_lab.txt", $"smoothness actual={baseSmooth}; metallic={mat.GetFloat("_Metallic")}; keywords={string.Join(",", mat.shaderKeywords)}");
        }

        // ───────────────────────── Rendimiento ─────────────────────────
        private static void PerfReport()
        {
            string id = Arg("-avatar"), outDir = Arg("-out");
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            var prefab = Load(id);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var mats = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            int tris = 0, verts = 0;
            foreach (var r in renderers) { var m = r.GetComponent<MeshFilter>().sharedMesh; tris += m.triangles.Length / 3; verts += m.vertexCount; }
            log.AppendLine($"Renderers={renderers.Length} materiales={mats.Length} triángulos={tris} vértices={verts} (submeshes={renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.subMeshCount)})");
            long total = 0; long meshMem = 0;
            foreach (var r in renderers) meshMem += Profiler.GetRuntimeMemorySizeLong(r.GetComponent<MeshFilter>().sharedMesh);
            var textures = mats.SelectMany(m => new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" }.Select(p => (m, p, t: m.GetTexture(p)))).Where(x => x.t != null).ToArray();
            log.AppendLine("Texturas importadas (formato, mipmaps, memoria de ejecución):");
            foreach (var x in textures)
            {
                var t2 = (Texture2D)x.t;
                long mem = Profiler.GetRuntimeMemorySizeLong(t2); total += mem;
                log.AppendLine($"  {x.m.name,-18} {x.p,-18} {t2.width}x{t2.height} {t2.format} mips={t2.mipmapCount} {mem / 1048576f:F1} MB");
            }
            log.AppendLine($"MEMORIA TEXTURAS TOTAL = {total / 1048576f:F1} MB; mallas = {meshMem / 1048576f:F1} MB");
            File.WriteAllText(outDir + "/perf_resultados.txt", log.ToString());
            Debug.Log(log.ToString());
        }

        // ───────────────────────── Utilidades ─────────────────────────
        private static GameObject Load(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}{Arg("-prefab") ?? "Avatar_" + id}.prefab");
        private static string Tag(string id) => Arg("-tag") ?? id;

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        private static void Sun(float x, float y, float intensity)
        {
            var g = new GameObject("Luz"); var l = g.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = intensity; l.shadows = LightShadows.Soft; g.transform.rotation = Quaternion.Euler(x, y, 0f);
        }

        private static Camera MakeCamera(int w, int h, out RenderTexture rt)
        {
            var g = new GameObject("CamValidacion"); var cam = g.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt; return cam;
        }

        private static void Save(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void Flush(string dir, string file, StringBuilder log)
        {
            File.WriteAllText(Path.Combine(dir, file), log.ToString());
            Debug.Log(log.ToString());
        }

        private static void Drive(IEnumerator routine)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                try { if (routine.MoveNext()) return; EditorApplication.update -= step; EditorApplication.Exit(0); }
                catch (Exception e) { Debug.LogError(e); EditorApplication.update -= step; EditorApplication.Exit(1); }
            };
            EditorApplication.update += step;
        }

        private static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
