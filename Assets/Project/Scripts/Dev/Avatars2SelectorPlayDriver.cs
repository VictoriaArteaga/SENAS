#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Auth;
using Lumina.UI;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Conductor temporal para probar en Play Mode el selector REAL de RegisterScene (se añade a la escena en memoria; no se guarda):
    /// recorre las opciones con el botón SIGUIENTE, comprueba qué modelo hay activo, su tamaño y su nombre, captura la vista y pulsa SELECCIONAR
    /// en cada una comprobando que AuthSession.AvatarId recibe el id (la misma cadena que luego usa PlayerAvatar).
    /// </summary>
    public class Avatars2SelectorPlayDriver : MonoBehaviour
    {
        public string outDir;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outDir);
            yield return Main();
            File.WriteAllText(Path.Combine(outDir, "selector_playmode_resultados.txt"), _log.ToString());
            Debug.Log(_log.ToString());
            EditorApplication.Exit(0);
        }

        private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private IEnumerator Main()
        {
            var selector = FindFirstObjectByType<AvatarSelectorController>();
            var pivot = Field<Transform>(selector, "pivot");
            var next = Field<Button>(selector, "nextButton");
            var select = Field<Button>(selector, "selectButton");
            var nameLabel = Field<TMP_Text>(selector, "nameLabel");
            var dots = Field<Image[]>(selector, "dots");
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _log.AppendLine($"Play Mode={Application.isPlaying}. Selector: enabled={selector.enabled} Count={selector.Count} puntos={dots.Length}; AuthSession.AvatarId inicial='{AuthSession.AvatarId}'");
            for (int i = 0; i < 40; i++) yield return null;   // el selector oculta los demás tras dos fotogramas

            int fails = 0;
            var seen = new System.Collections.Generic.List<string>();
            for (int step = 0; step < selector.Count + 1; step++)
            {
                var active = pivot.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToArray();
                string names = string.Join(", ", active.Select(t => t.name));
                var renderers = active.SelectMany(t => t.GetComponentsInChildren<Renderer>()).ToArray();
                var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
                int activeDots = dots.Count(d => d.transform.localScale.x > 1.2f);
                _log.AppendLine($"[Opción {step}] etiqueta='{nameLabel.text}' modelos activos={active.Length} ({names}) altura={b.size.y:F2} m ancho máx={Mathf.Max(b.size.x, b.size.z):F2} m base y={b.min.y - pivot.position.y:F2} punto resaltado={activeDots}");
                if (active.Length != 1) { fails++; _log.AppendLine("   [FALLA] debe haber exactamente un modelo activo"); }
                if (step < selector.Count) seen.Add(active[0].name);
                if (step < selector.Count)
                {
                    // captura sin UI (render de la cámara a textura)
                    var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render();
                    var old = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                    RenderTexture.active = old; cam.targetTexture = prev;
                    File.WriteAllBytes($"{outDir}/selector_opcion{step}_{active[0].name}.png", tex.EncodeToPNG()); Destroy(tex);

                    // SELECCIONAR y comprobar el id que recibe AuthSession
                    select.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.5f);   // la animación de 'pop' usa tiempo real
                    bool ok = selector.HasSelection && selector.SelectedId == active[0].name && AuthSession.AvatarId == active[0].name;
                    _log.AppendLine($"   SELECCIONAR -> SelectedId='{selector.SelectedId}' SelectedName='{selector.SelectedName}' AuthSession.AvatarId='{AuthSession.AvatarId}' {(ok ? "[OK]" : "[FALLA]")}");
                    if (!ok) fails++;
                }
                next.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.7f);
            }
            _log.AppendLine($"Recorrido: {string.Join(" -> ", seen)}; la opción {selector.Count} (tras la última) es la primera otra vez: {(_log.ToString().Contains($"[Opción {selector.Count}] etiqueta='CAROL'") ? "sí" : "NO")}");
            _log.AppendLine(fails == 0 ? "RESULTADO: todas las comprobaciones del selector pasan" : $"RESULTADO: {fails} FALLOS");
        }
    }
}
#endif
