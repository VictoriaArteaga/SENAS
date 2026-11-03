using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Lumina.Core;
using Lumina.Player;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Prueba automatizada en Play Mode (6.1.4): conduce al jugador real (FirstPersonController, sin modificarlo) inyectando eventos de
    /// teclado (W) y de ratón (delta) en el Input System, comprueba la animación real de Sena, cruza ambos puentes, visita las
    /// cuatro casas y repite capturas con el tema nocturno (GameSettings.DarkMode, que activa ThemeSkybox/ThemeAmbient/ThemeLights).
    /// NO es una prueba con periféricos físicos. Solo existe como objeto temporal que crea Proto612PlayTest; no se guarda.
    /// </summary>
    public class Proto612PlayDriver : MonoBehaviour
    {
        [System.Serializable] public class ShotSpec { public string name; public Vector3 position; public Vector3 lookAt; public float fov = 60f; public bool fromPlayer; }

        public Vector2[] points;
        public string[] captureAt;
        public string[] labels;
        public Vector2[] faceAt;            // si no es (0,0): al llegar se gira hacia ese punto (la fachada) antes de capturar
        public ShotSpec[] dayShots;
        public ShotSpec[] nightShots;
        public string prefix = "play_";
        public string evidenceDir;
        public string resultPath;

        FirstPersonController _fpc; CharacterController _cc; Keyboard _kb; Mouse _mouse; Camera _cam;
        float _t0; float _minY = 99, _maxY = -99; readonly StringBuilder _log = new StringBuilder();
        int _frames; bool _finished;

        void Start()
        {
            _fpc = FindFirstObjectByType<FirstPersonController>(); _cc = _fpc.GetComponent<CharacterController>(); _cam = Camera.main;
            // Los dispositivos creados por script no «corren en segundo plano»: se usa una copia temporal de los ajustes (el asset no se toca).
            var s = ScriptableObject.CreateInstance<InputSettings>();
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = s;
            _kb = Keyboard.current ?? InputSystem.AddDevice<Keyboard>(); _mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            _t0 = Time.realtimeSinceStartup;
            _log.AppendLine("Play Mode: teclado=" + _kb.name + " raton=" + _mouse.name + "; entrada inyectada via Input System (NO son perifericos fisicos)");
            _log.AppendLine("Controlador: FirstPersonController enabled=" + _fpc.enabled + ", CharacterController enabled=" + _cc.enabled + ", camara=" + (_cam != null ? _cam.name : "NULA") + ", AudioListeners=" + FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            _log.AppendLine("Inicio en (" + F(_cc.transform.position.x) + "," + F(_cc.transform.position.y, 2) + "," + F(_cc.transform.position.z) + "), yaw " + _cc.transform.eulerAngles.y.ToString("0") + " grados");
            StartCoroutine(Main());
        }

        static string F(float v, int d = 1) => v.ToString(d == 1 ? "0.0" : "0.00");

        void Update()
        {
            if (_finished) return;
            _frames++;
            if (!_fpc.CursorLocked) _fpc.SetCursorLocked(true);
            var p = _cc.transform.position; _minY = Mathf.Min(_minY, p.y); _maxY = Mathf.Max(_maxY, p.y);
            if (Time.realtimeSinceStartup - _t0 > 480f) Finish("TIEMPO TOTAL AGOTADO");
        }

        IEnumerator Main()
        {
            // 1) Sena: ¿el Animator real evalúa un estado y la postura deja de ser la T-pose?
            yield return new WaitForSeconds(0.8f);
            Proof("t=0,8 s (el jugador aparece a ~8 m de Sena)");
            yield return new WaitForSeconds(0.9f);
            foreach (var sh in dayShots) if (sh.name.StartsWith("sena_saludo")) Capture(sh);
            Proof("t=1,7 s");
            yield return new WaitForSeconds(6.3f);
            Proof("t=8 s (el saludo dura 3,8 s: debe haber vuelto a Idle)");
            foreach (var sh in dayShots) if (!sh.name.StartsWith("sena_saludo")) Capture(sh);

            // 2) Ruta completa por los caminos, con teclado y ratón inyectados.
            int i = 0; float leg = Time.realtimeSinceStartup;
            while (i < points.Length)
            {
                var pos = _cc.transform.position; var target = points[i];
                var d = new Vector3(target.x - pos.x, 0, target.y - pos.z); float dist = d.magnitude;
                if (dist < 0.8f)
                {
                    _log.AppendLine("  [" + (i + 1) + "/" + points.Length + "] " + labels[i] + ": llego en " + F(Time.realtimeSinceStartup - leg) + "s, pos=(" + F(pos.x) + "," + F(pos.y, 2) + "," + F(pos.z) + "), yaw " + _cc.transform.eulerAngles.y.ToString("0"));
                    if (!string.IsNullOrEmpty(captureAt[i]))
                    {
                        if (faceAt != null && i < faceAt.Length && faceAt[i] != Vector2.zero)
                        {
                            // Como haría una persona al llegar: se detiene y gira con el ratón hasta tener la fachada de frente.
                            InputSystem.QueueStateEvent(_kb, new KeyboardState());
                            float t0 = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - t0 < 2.5f)
                            {
                                var dd = new Vector3(faceAt[i].x - _cc.transform.position.x, 0, faceAt[i].y - _cc.transform.position.z);
                                float e2 = Vector3.SignedAngle(_cc.transform.forward, dd.normalized, Vector3.up);
                                if (Mathf.Abs(e2) < 3f) break;
                                InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(Mathf.Clamp(e2 * 10f, -150f, 150f), 0f) });
                                yield return null;
                            }
                            yield return null; yield return null;
                            _log.AppendLine("     giro final hacia la fachada: yaw " + _cc.transform.eulerAngles.y.ToString("0") + " grados");
                        }
                        Capture(new ShotSpec { name = captureAt[i], fromPlayer = true });
                    }
                    i++; leg = Time.realtimeSinceStartup; yield return null; continue;
                }
                if (Time.realtimeSinceStartup - leg > 45f) { Finish("BLOQUEADO en el punto " + (i + 1) + " (" + labels[i] + ") a " + F(dist) + " m, pos=(" + F(pos.x) + "," + F(pos.y, 2) + "," + F(pos.z) + ")"); yield break; }
                float err = Vector3.SignedAngle(_cc.transform.forward, d.normalized, Vector3.up);
                InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(Mathf.Clamp(err * 10f, -300f, 300f), 0f) });   // el controlador usa 0,1 grados por unidad
                InputSystem.QueueStateEvent(_kb, Mathf.Abs(err) < 20f ? new KeyboardState(Key.W) : new KeyboardState());
                yield return null;
            }
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            _log.AppendLine("RUTA COMPLETA (" + points.Length + " puntos)");

            // 3) Noche con el sistema de temas real.
            GameSettings.DarkMode = true;
            yield return new WaitForSeconds(2f);
            int lights = 0, lanterns = 0; foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name == "Lantern") { lanterns++; var tl = t.Find("ThemeLight"); var l = tl != null ? tl.GetComponent<Light>() : null; if (l != null && l.enabled && tl.gameObject.activeInHierarchy && l.intensity > 0f) lights++; }   // las luces de ThemeLights son objetos ocultos (DontSave): se cuentan desde cada farol
            _log.AppendLine("Noche: DarkMode=" + GameSettings.DarkMode + ", luces de farol activas creadas por ThemeLights=" + lights + " de " + lanterns + " faroles");
            foreach (var sh in nightShots) Capture(sh);
            Finish(null);
        }

        void Proof(string when)
        {
            var greeter = FindFirstObjectByType<Proto614SenaGreeter>();
            if (greeter == null) { _log.AppendLine("  Sena [" + when + "]: SIN componente Proto614SenaGreeter"); return; }
            var anim = greeter.GetComponentInChildren<Animator>();
            if (anim == null) { _log.AppendLine("  Sena [" + when + "]: SIN Animator activo"); return; }
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips); var hand = anim.GetBoneTransform(HumanBodyBones.LeftHand); var rhand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            string state = info.IsName("Locomotion") ? "Locomotion (Idle)" : info.IsName("Wave") ? "Wave" : "otro";
            float lh = hand != null && hips != null ? hand.position.y - hips.position.y : float.NaN;
            float rh = rhand != null && hips != null ? rhand.position.y - hips.position.y : float.NaN;
            float spread = hand != null && rhand != null ? Vector3.Distance(hand.position, rhand.position) : float.NaN;
            _log.AppendLine("  Sena [" + when + "]: estado=" + state + " normalizedTime=" + info.normalizedTime.ToString("0.00") + ", mano izq. a " + F(lh, 2) + " m de la cadera (altura), mano der. " + F(rh, 2) + ", separacion entre manos " + F(spread, 2) + " m (en T-pose ~1,5 m con las manos a la altura de los hombros), animator.enabled=" + anim.enabled);
        }

        void Capture(ShotSpec sh)
        {
            if (string.IsNullOrEmpty(evidenceDir)) return;
            Camera cam = _cam; GameObject temp = null;
            if (!sh.fromPlayer)
            {
                temp = new GameObject("TEMP_SHOT"); cam = temp.AddComponent<Camera>(); cam.fieldOfView = sh.fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
                temp.transform.position = sh.position; temp.transform.LookAt(sh.lookAt);
                var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? temp.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
            }
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(evidenceDir, prefix + sh.name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = old; cam.targetTexture = null; Destroy(tex); rt.Release(); Destroy(rt);
            if (temp != null) Destroy(temp);
        }

        void Finish(string failure)
        {
            if (_finished) return;
            _finished = true;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            var p = _cc.transform.position;
            _log.AppendLine(failure == null ? "RESULTADO: PRUEBA COMPLETA" : "RESULTADO: FALLO - " + failure);
            _log.AppendLine("Altura minima y=" + F(_minY, 2) + ", maxima y=" + F(_maxY, 2) + "; tiempo real " + F(Time.realtimeSinceStartup - _t0) + "s en " + _frames + " fotogramas; fin en (" + F(p.x) + "," + F(p.y, 2) + "," + F(p.z) + ")");
            File.WriteAllText(resultPath, _log.ToString());
            Debug.Log("[P612-PLAY]\n" + _log);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(failure == null ? 0 : 3);
#endif
        }
    }
}
