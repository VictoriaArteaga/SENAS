#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Lumina.Auth;
using Lumina.Player;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Conductor temporal para la prueba en Play Mode de un avatar de Avatars2 sobre LuminaWorld (se añade a la escena abierta en memoria y no
    /// se guarda). Usa el PlayerAvatar REAL (instanciado desde un catálogo temporal en memoria; AvatarCatalog.asset no se toca), el
    /// FirstPersonController real y entrada inyectada en el Input System (no hay periféricos físicos).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class Avatars2PlayDriver : MonoBehaviour
    {
        public string avatar, prefabPath, outDir, tag;
        /// <summary>true = usa el AvatarCatalog.asset REAL y el PlayerAvatar de la escena (la elección se fija antes de que despierte PlayerAvatar).</summary>
        public bool realCatalog;

        private void Awake()
        {
            if (realCatalog) AuthSession.SelectAvatar(avatar);   // orden de ejecución -1000: antes del Awake de PlayerAvatar
        }

        private readonly StringBuilder _log = new StringBuilder();
        private FirstPersonController _fpc; private CharacterController _cc; private Camera _cam; private Keyboard _kb; private Mouse _mouse;
        private PlayerAvatar _pa;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            yield return Main();
            File.WriteAllText(Path.Combine(outDir, $"{tag}_playmode_resultados.txt"), _log.ToString());
            Debug.Log(_log.ToString());
            EditorApplication.Exit(0);
        }

        private IEnumerator Main()
        {
            Directory.CreateDirectory(outDir);
            _fpc = FindFirstObjectByType<FirstPersonController>(); _cc = _fpc.GetComponent<CharacterController>(); _cam = _fpc.GetComponentInChildren<Camera>();
            var settings = ScriptableObject.CreateInstance<InputSettings>();
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            _kb = Keyboard.current ?? InputSystem.AddDevice<Keyboard>(); _mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            _log.AppendLine($"Play Mode: Application.isPlaying={Application.isPlaying}; jugador='{_fpc.name}' pos={_fpc.transform.position:F2}; cámara fov={_cam.fieldOfView} altura local={_cam.transform.localPosition.y:F2}");

            GameObject prefab;
            if (realCatalog)
            {
                // Flujo real: AuthSession.AvatarId -> AvatarCatalog.asset de la escena -> PlayerAvatar de la escena.
                _pa = _fpc.GetComponent<PlayerAvatar>();
                var catalogField = (AvatarCatalog)typeof(PlayerAvatar).GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_pa);
                var entry = catalogField != null ? catalogField.Find(avatar) : null;
                _log.AppendLine($"[Catálogo real] '{(catalogField != null ? catalogField.name : "NULL")}' Find('{avatar}') -> {(entry != null ? entry.prefab.name + " (" + AssetDatabase.GetAssetPath(entry.prefab) + ")" : "NULL")}");
                prefab = entry?.prefab;
                yield return null;
            }
            else
            {
                // PlayerAvatar REAL con catálogo temporal en memoria que contiene el avatar a probar.
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                var entries = (Array)Array.CreateInstance(typeof(AvatarCatalog.Entry), 1);
                entries.SetValue(new AvatarCatalog.Entry { id = avatar, displayName = avatar, prefab = prefab }, 0);
                typeof(AvatarCatalog).GetField("avatars", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(catalog, entries);
                AuthSession.SelectAvatar(avatar);
                var holder = new GameObject("TEMP_PlayerAvatarHolder"); holder.SetActive(false);
                holder.transform.SetParent(_fpc.transform, false);
                _pa = holder.AddComponent<PlayerAvatar>();
                typeof(PlayerAvatar).GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_pa, catalog);
                holder.SetActive(true);   // Awake real de PlayerAvatar: instancia el prefab y aplica FirstPersonHiddenParts
                yield return null;
            }

            var body = _pa.Body;
            if (body == null) { _log.AppendLine($"[FALLA] PlayerAvatar no instanció el cuerpo para el id '{avatar}'"); yield break; }
            _log.AppendLine($"[PlayerAvatar] Body='{body.name}' id={_pa.CurrentId} localPos={body.transform.localPosition:F2} escala={body.transform.localScale:F2}");
            foreach (var r in body.GetComponentsInChildren<MeshRenderer>(true))
                _log.AppendLine($"   {r.name,-16} shadowCastingMode={r.shadowCastingMode,-12} enabled={r.enabled} activeInHierarchy={r.gameObject.activeInHierarchy}");
            if (prefab != null)
                _log.AppendLine("Prefab en disco (no debe haber cambiado): " + string.Join(", ", prefab.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name + "=" + r.shadowCastingMode)));
            var br = body.GetComponentsInChildren<Renderer>(true); var bb = br[0].bounds; foreach (var r in br) bb.Encapsulate(r.bounds);
            _log.AppendLine($"Cuerpo instanciado: altura={bb.size.y:F2} m ancho={bb.size.x:F2}/{bb.size.z:F2} pies y={bb.min.y:F2} (suelo y={_fpc.transform.position.y:F2}); coincide con el prefab del catálogo: {(prefab != null && PrefabUtility.GetCorrespondingObjectFromSource(body) == prefab || prefab != null && body.name == "Avatar_" + avatar)}");
            _log.AppendLine($"Colliders en el cuerpo: {body.GetComponentsInChildren<Collider>(true).Length}; Rigidbody: {body.GetComponentsInChildren<Rigidbody>(true).Length}; Animators: {body.GetComponentsInChildren<Animator>(true).Length}");

            for (int i = 0; i < 90; i++) yield return null;   // texturas a la GPU y escena estable
            if (!_fpc.CursorLocked) _fpc.SetCursorLocked(true);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            // Mirada hacia abajo con ratón inyectado (sensibilidad 0,1 grados por unidad): se corrige hasta alcanzar el ángulo.
            foreach (var target in new[] { 0f, 55f, 80f })
            {
                for (int k = 0; k < 40 && Mathf.Abs(Pitch() - target) > 0.6f; k++)
                {
                    float err = target - Pitch();
                    InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(0f, -Mathf.Clamp(err * 10f, -300f, 300f)) });
                    yield return null;
                }
                InputSystem.QueueStateEvent(_mouse, new MouseState()); yield return null; yield return null;
                foreach (var fov in new[] { 60f, 90f })
                {
                    float keep = _cam.fieldOfView; _cam.fieldOfView = fov;
                    Capture(rt, $"{outDir}/{tag}_play_pitch{(int)target:D2}_fov{(int)fov}.png");
                    _cam.fieldOfView = keep;
                }
                _log.AppendLine($"[Mirada] pitch real={Pitch():F1}° (objetivo {target})");
            }
            // Volver a mirar al frente.
            for (int k = 0; k < 40 && Mathf.Abs(Pitch()) > 0.6f; k++)
            {
                InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(0f, Mathf.Clamp(Pitch() * 10f, -300f, 300f)) });
                yield return null;
            }
            InputSystem.QueueStateEvent(_mouse, new MouseState());
            yield return null;

            // Movimiento real: W durante ~1,5 s con el cuerpo y otra vez sin él.
            yield return Walk("con cuerpo");
            Destroy(_pa.Body); yield return null; yield return null;
            yield return Walk("sin cuerpo");
        }

        private IEnumerator Walk(string label)
        {
            Vector3 p0 = _fpc.transform.position; float t0 = Time.time; int frames = 0; float sum = 0f;
            var flagsOr = CollisionFlags.None;
            while (Time.time - t0 < 1.5f)
            {
                InputSystem.QueueStateEvent(_kb, new KeyboardState(Key.W));
                yield return null; frames++; sum += Time.unscaledDeltaTime; flagsOr |= _cc.collisionFlags;
            }
            InputSystem.QueueStateEvent(_kb, new KeyboardState()); yield return null;
            Vector3 d = _fpc.transform.position - p0; d.y = 0;
            float dt = Time.time - t0;
            _log.AppendLine($"[Movimiento {label}] {d.magnitude:F2} m en {dt:F2} s = {d.magnitude / dt:F2} m/s (moveSpeed=4); y={_fpc.transform.position.y:F2}; collisionFlags={flagsOr}; fotogramas={frames}; tiempo medio de fotograma={1000f * sum / frames:F1} ms");
            // Volver al punto de partida para la siguiente medida.
            _cc.enabled = false; _fpc.transform.position = p0; _cc.enabled = true;
        }

        private float Pitch() { float x = _cam.transform.localEulerAngles.x; return x > 180f ? x - 360f : x; }

        private void Capture(RenderTexture rt, string path)
        {
            var prev = _cam.targetTexture; _cam.targetTexture = rt; _cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = old; _cam.targetTexture = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
        }
    }
}
#endif
