using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Lumina.Prototype612
{
    /// <summary>Intervención 6.1.4, solo lectura: diagnostica el Animator, las animaciones y los materiales nulos del prefab de Sena.</summary>
    public static class Proto614SenaInspect
    {
        public static void Run()
        {
            var sb = new StringBuilder("[P614-SENA]\n");
            var sena = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Sena.prefab");
            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SunboxGames/Avatars/Prefabs/Avatar.prefab");
            foreach (var (label, go) in new[] { ("Sena.prefab", sena), ("Avatar.prefab (original del paquete)", avatar) })
            {
                sb.AppendLine("== " + label);
                foreach (var a in go.GetComponentsInChildren<Animator>(true))
                    sb.AppendLine($"Animator en '{a.name}': controller={(a.runtimeAnimatorController != null ? AssetDatabase.GetAssetPath(a.runtimeAnimatorController) : "null")} avatar={(a.avatar != null ? a.avatar.name + " humanoide=" + a.avatar.isHuman + " valido=" + a.avatar.isValid : "null")} rootMotion={a.applyRootMotion} cullingMode={a.cullingMode}");
                foreach (var c in go.GetComponents<MonoBehaviour>()) if (c != null) sb.AppendLine("  componente raíz: " + c.GetType().FullName);
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mats = r.sharedMaterials; int nulls = 0; for (int i = 0; i < mats.Length; i++) if (mats[i] == null) nulls++;
                    var names = new System.Collections.Generic.List<string>(); for (int i = 0; i < mats.Length; i++) names.Add(mats[i] != null ? mats[i].name : "NULO");
                    sb.AppendLine($"  SMR '{r.name}' activoPropio={r.gameObject.activeSelf} activoEnJerarquia={r.gameObject.activeInHierarchy} enabled={r.enabled} malla={(r.sharedMesh != null ? r.sharedMesh.name + " sub=" + r.sharedMesh.subMeshCount : "null")} ranuras={mats.Length} nulos={nulls} [{string.Join(", ", names)}]");
                }
            }

            string[] controllers = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/Models/SunboxGames", "Assets/Project" });
            foreach (var g in controllers)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                sb.AppendLine("== Controller " + path);
                foreach (var p in ctrl.parameters) sb.AppendLine($"  parametro {p.name} ({p.type}) por defecto: float={p.defaultFloat} int={p.defaultInt} bool={p.defaultBool}");
                foreach (var layer in ctrl.layers)
                {
                    sb.AppendLine($"  capa '{layer.name}' peso={layer.defaultWeight} mascara={(layer.avatarMask != null ? layer.avatarMask.name : "ninguna")} estado por defecto={(layer.stateMachine.defaultState != null ? layer.stateMachine.defaultState.name : "ninguno")}");
                    foreach (var st in layer.stateMachine.states)
                    {
                        string motion = st.state.motion != null ? st.state.motion.name + " (" + st.state.motion.GetType().Name + ")" : "sin motion";
                        sb.AppendLine("    estado '" + st.state.name + "' -> " + motion);
                        if (st.state.motion is BlendTree bt) foreach (var ch in bt.children) sb.AppendLine($"       hijo {(ch.motion != null ? ch.motion.name : "null")} umbral={ch.threshold}");
                    }
                }
            }

            foreach (var f in new[] { "Assets/Models/SunboxGames/Avatars/Animations/Avatar_Animations.fbx", "Assets/Models/SunboxGames/Avatars/Animations/Avatar_Animations_Refference.fbx" })
            {
                sb.AppendLine("== Clips en " + f);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(f)) if (o is AnimationClip c && !c.name.StartsWith("__preview")) sb.AppendLine($"  clip '{c.name}' {c.length:0.00}s loop={c.isLooping} humanoide={c.isHumanMotion} fps={c.frameRate}");
                var imp = AssetImporter.GetAtPath(f) as ModelImporter;
                if (imp != null) sb.AppendLine("  importador: animationType=" + imp.animationType + " avatarSetup=" + imp.avatarSetup);
            }
            Debug.Log(sb.ToString());
        }
    }
}
