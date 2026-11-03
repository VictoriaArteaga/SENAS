using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Player;
using Lumina.UI;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Comprobaciones SIN PERSISTENCIA de los builders de avatares (LuminaAuthSceneBuilder y LuminaWorldAuthIntegration.EnsureCatalog):
    /// trabaja con copias en memoria del catálogo y con una escena nueva en memoria que no se guarda. No toca AvatarCatalog.asset ni ninguna escena.
    /// Uso: -executeMethod Lumina.EditorTools.Avatars2BuilderCheck.Run -out {archivo de resultados}
    /// </summary>
    public static class Avatars2BuilderCheck
    {
        private static readonly string[] Legacy = { "luna", "nube", "rio", "sol" };
        private static readonly string[] Approved = { "carol", "luna-crimson", "tom", "crimson", "neon", "tensy" };
        private static readonly StringBuilder Log = new StringBuilder();
        private static int _fail;

        private static void Check(bool ok, string text) { if (!ok) _fail++; Log.AppendLine((ok ? "[PASA] " : "[FALLA] ") + text); }

        public static void Run()
        {
            try { RunChecks(); }
            catch (System.Exception e) { _fail++; Log.AppendLine("EXCEPCIÓN: " + e); }
            Log.AppendLine(_fail == 0 ? "RESUMEN: todo correcto" : $"RESUMEN: {_fail} comprobaciones fallan");
            var path = System.Environment.GetEnvironmentVariable("CHECK_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, Log.ToString());
            Debug.Log(Log.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(_fail == 0 ? 0 : 2);
        }

        private static List<(string id, string name, Object prefab)> Entries(AvatarCatalog c)
        {
            var list = new SerializedObject(c).FindProperty("avatars");
            var r = new List<(string, string, Object)>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                r.Add((e.FindPropertyRelative("id").stringValue, e.FindPropertyRelative("displayName").stringValue, e.FindPropertyRelative("prefab").objectReferenceValue));
            }
            return r;
        }

        private static AvatarCatalog Clone(AvatarCatalog source, System.Func<(string id, string name, Object prefab), bool> keep = null)
        {
            var c = ScriptableObject.CreateInstance<AvatarCatalog>();   // en memoria, no es un asset
            var so = new SerializedObject(c);
            var list = so.FindProperty("avatars");
            var src = Entries(source).Where(e => keep == null || keep(e)).ToList();
            list.arraySize = src.Count;
            for (int i = 0; i < src.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = src[i].id;
                e.FindPropertyRelative("displayName").stringValue = src[i].name;
                e.FindPropertyRelative("prefab").objectReferenceValue = src[i].prefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return c;
        }

        private static void RunChecks()
        {
            // ── Roster ──
            var legacy = LuminaAvatarRoster.Legacy.Select(e => e.Id).ToArray();
            var selectable = LuminaAvatarRoster.Selectable.Select(e => e.Id).ToArray();
            var catalogIds = LuminaAvatarRoster.Catalog.Select(e => e.Id).ToArray();
            Check(legacy.SequenceEqual(Legacy), "Roster.Legacy = " + string.Join(", ", legacy));
            Check(selectable.SequenceEqual(Approved), "Roster.Selectable = " + string.Join(", ", selectable));
            Check(catalogIds.SequenceEqual(Legacy.Concat(Approved)), "Roster.Catalog (10) = " + string.Join(", ", catalogIds));
            Check(LuminaAvatarRoster.Catalog.All(e => e.LoadPrefab() != null), "los 10 prefabs del roster existen");
            LuminaAvatarRoster.RequirePrefabs();

            // ── Catálogo real (solo lectura) ──
            var real = AssetDatabase.LoadAssetAtPath<AvatarCatalog>("Assets/Project/Data/AvatarCatalog.asset");
            var realEntries = Entries(real);
            Check(realEntries.Select(e => e.id).SequenceEqual(Legacy.Concat(Approved)), "AvatarCatalog.asset real tiene las 10 entradas en orden");

            // a) catálogo completo: MergeCatalog no cambia nada
            var full = Clone(real);
            bool changed = LuminaWorldAuthIntegration.MergeCatalog(full);
            Check(!changed && Entries(full).SequenceEqual(realEntries), "a) catálogo completo -> MergeCatalog no cambia nada (idempotente)");

            // b) estado antiguo (solo 4): se completan las 6 sin tocar las 4
            var four = Clone(real, e => Legacy.Contains(e.id));
            Check(Entries(four).Count == 4, "b) copia con solo los 4 antiguos");
            changed = LuminaWorldAuthIntegration.MergeCatalog(four);
            var e4 = Entries(four);
            Check(changed && e4.Select(e => e.id).SequenceEqual(Legacy.Concat(Approved)), "b) MergeCatalog completa a 10 entradas en orden: " + string.Join(", ", e4.Select(e => e.id)));
            Check(Legacy.All(id => e4.First(e => e.id == id).prefab == realEntries.First(e => e.id == id).prefab), "b) los 4 antiguos conservan exactamente su prefab");
            Check(!LuminaWorldAuthIntegration.MergeCatalog(four) && Entries(four).Count == 10, "b) segunda ejecución: sin cambios y sin duplicados");

            // c) catálogo vacío (builder en un proyecto limpio)
            var empty = ScriptableObject.CreateInstance<AvatarCatalog>();
            LuminaWorldAuthIntegration.MergeCatalog(empty);
            Check(Entries(empty).Select(e => e.id).SequenceEqual(Legacy.Concat(Approved)), "c) catálogo vacío -> 10 entradas en orden");

            // d) entrada extra desconocida: se conserva
            var extra = Clone(real);
            var so = new SerializedObject(extra); var list = so.FindProperty("avatars");
            list.arraySize++; var added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("id").stringValue = "zzz-extra"; added.FindPropertyRelative("displayName").stringValue = "Extra";
            added.FindPropertyRelative("prefab").objectReferenceValue = realEntries[0].prefab; so.ApplyModifiedPropertiesWithoutUndo();
            changed = LuminaWorldAuthIntegration.MergeCatalog(extra);
            Check(!changed && Entries(extra).Count == 11 && Entries(extra).Last().id == "zzz-extra", "d) una entrada desconocida se conserva (11) y no se reordena");

            // e) prefab vacío en una entrada existente: se rellena sin tocar el resto
            var hole = Clone(real);
            var so2 = new SerializedObject(hole); so2.FindProperty("avatars").GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = null; so2.ApplyModifiedPropertiesWithoutUndo();
            changed = LuminaWorldAuthIntegration.MergeCatalog(hole);
            Check(changed && Entries(hole).SequenceEqual(realEntries), "e) un prefab vacío de 'luna' se rellena y el resto queda igual");

            // ── Selector (RegisterScene construido en memoria, sin guardar) ──
            string before = EditorSceneManager.GetActiveScene().path;
            var scene1 = LuminaAuthSceneBuilder.BuildRegisterInMemoryForTest();
            Check(string.IsNullOrEmpty(scene1.path), "la escena generada es nueva y sin ruta (no sobrescribe RegisterScene.unity)");
            string summary1 = Summarize();
            var scene2 = LuminaAuthSceneBuilder.BuildRegisterInMemoryForTest();
            string summary2 = Summarize();
            Check(summary1 == summary2, "idempotencia: dos construcciones en memoria producen exactamente la misma estructura");
            Log.AppendLine(summary2);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // se descarta lo construido
        }

        private static string Summarize()
        {
            var selector = Object.FindFirstObjectByType<AvatarSelectorController>();
            var so = new SerializedObject(selector);
            var pivot = (Transform)so.FindProperty("pivot").objectReferenceValue;
            var options = so.FindProperty("options");
            var ids = new List<string>(); var names = new List<string>(); var prefabsOk = true; var models = new List<GameObject>();
            for (int i = 0; i < options.arraySize; i++)
            {
                var e = options.GetArrayElementAtIndex(i);
                string id = e.FindPropertyRelative("id").stringValue;
                ids.Add(id); names.Add(e.FindPropertyRelative("displayName").stringValue);
                var model = (GameObject)e.FindPropertyRelative("model").objectReferenceValue; models.Add(model);
                var src = model != null ? PrefabUtility.GetCorrespondingObjectFromSource(model) : null;
                prefabsOk &= src != null && AssetDatabase.GetAssetPath(src) == $"Assets/Project/Prefabs/PlayerAvatars/Avatar_{id}.prefab" && model.activeSelf;
            }
            var dots = so.FindProperty("dots");
            var dotPos = new List<string>();
            for (int i = 0; i < dots.arraySize; i++) dotPos.Add(((Image)dots.GetArrayElementAtIndex(i).objectReferenceValue).rectTransform.anchoredPosition.x.ToString("0"));
            var kids = pivot.Cast<Transform>().ToList();
            var legacyKids = kids.Where(t => Legacy.Contains(t.name)).ToList();
            string summary =
                $"SELECTOR opciones={string.Join(",", ids)} nombres={string.Join("|", names)}\n" +
                $"SELECTOR cada opción es instancia del prefab aprobado (Avatar_<id> en PlayerAvatars) y está activa: {prefabsOk}\n" +
                $"SELECTOR puntos={dots.arraySize} x=[{string.Join(",", dotPos)}] y={((Image)dots.GetArrayElementAtIndex(0).objectReferenceValue).rectTransform.anchoredPosition.y}\n" +
                $"SELECTOR pivote: {kids.Count} hijos = {string.Join(",", kids.Select(t => t.name + (t.gameObject.activeSelf ? "(activo)" : "(oculto)")))}; antiguos presentes y desactivados: {legacyKids.Count == 4 && legacyKids.All(t => !t.gameObject.activeSelf)}; nombres únicos: {kids.Select(t => t.name).Distinct().Count() == kids.Count}";
            bool ok = ids.SequenceEqual(Approved) && prefabsOk && dots.arraySize == 6 && legacyKids.Count == 4 && legacyKids.All(t => !t.gameObject.activeSelf) && kids.Count == 10
                && !ids.Any(Legacy.Contains) && kids.Select(t => t.name).Distinct().Count() == kids.Count
                && dotPos.SequenceEqual(new[] { "370", "410", "450", "490", "530", "570" });
            Check(ok, "selector generado: 6 opciones aprobadas (ningún antiguo), 6 puntos 370..570, 4 antiguos desactivados, 10 hijos únicos");
            return summary;
        }
    }
}
