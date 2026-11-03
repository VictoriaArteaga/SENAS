using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LuminaProto
{
    /// <summary>
    /// Integra el mundo del prototipo 6.2.3 (Prototype_Modular_FourVillages) en la escena de producción LuminaWorld.
    /// No se reconstruye nada: los grupos raíz del prototipo se MUEVEN a LuminaWorld (se conservan GUID, prefabs, materiales y mallas)
    /// y la escena prototipo se cierra SIN guardar. De producción se conservan Player, Sena, luz, EventSystem, canvases de
    /// interacción y las cuatro puertas (ProximityInteractable + InteractionPoint + SceneEntrance); el entorno antiguo se desactiva.
    /// No toca nada de reconocimiento de señas ni modelos de señas.
    /// </summary>
    public static class R624IntegrateIntoLuminaWorld
    {
        const string WorldPath = "Assets/Project/Scenes/LuminaWorld.unity";
        const string ProtoPath = "Assets/Project/Prototypes/Intervention_6_1_2/Scenes/Prototype_Modular_FourVillages.unity";

        // puerta de producción -> marcador de entrada del prototipo (1,2 m delante de la puerta real de cada casa)
        static readonly (string door, string entrada)[] Doors =
        {
            ("PuertaCasaAprendizaje", "Entrada_AlphabetHouse"),
            ("PuertaCasaMeses", "Entrada_MonthsHouse"),
            ("PuertaCasaNumeros", "Entrada_NumbersHouse"),
            ("PuertaCasaSaludos", "Entrada_GreetingsHouse"),
        };

        [MenuItem("Lumina/Integracion 6.2.3 en LuminaWorld")]
        public static void Run() => Execute();

        public static void RunBatch()
        {
            int code = 0;
            try { Execute(); }
            catch (Exception e) { Debug.LogError("[R624] FALLO: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static void Execute()
        {
            var world = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);
            if (world.GetRootGameObjects().Any(g => g.name.StartsWith("--- PROTOTIPO")))
                throw new Exception("LuminaWorld ya contiene el mundo del prototipo; se aborta para no duplicarlo.");

            var proto = EditorSceneManager.OpenScene(ProtoPath, OpenSceneMode.Additive);
            var roots = proto.GetRootGameObjects();
            var main = roots.Single(g => g.name.StartsWith("--- PROTOTIPO"));
            var r622 = roots.Single(g => g.name == "--- DECORACION R622 ---");
            var r623 = roots.Single(g => g.name == "--- PAISAJE R623 ---");
            var protoPlayer = roots.Single(g => g.name == "Player");

            // datos del prototipo que se trasladan a los objetos de producción
            var senaProto = main.transform.Cast<Transform>().Single(t => t.name.StartsWith("Sena (presencia"));
            Vector3 senaPos = senaProto.position; Quaternion senaRot = senaProto.rotation;
            Vector3 spawnPos = protoPlayer.transform.position; Quaternion spawnRot = protoPlayer.transform.rotation;
            var entradas = new Dictionary<string, Vector3>();
            foreach (var d in Doors)
                entradas[d.entrada] = main.transform.Find("Casas/" + d.entrada).position;

            // la Sena del prototipo es solo visual: se descarta (producción conserva la suya con todos sus componentes)
            UnityEngine.Object.DestroyImmediate(senaProto.gameObject);

            // 1) mover el mundo del prototipo a LuminaWorld
            SceneManager.MoveGameObjectToScene(main, world);
            SceneManager.MoveGameObjectToScene(r622, world);
            SceneManager.MoveGameObjectToScene(r623, world);

            // 2) producción: jugador, punto de aparición y Sena a la nueva distribución
            var player = world.GetRootGameObjects().Single(g => g.name == "Player");
            player.transform.SetPositionAndRotation(spawnPos, spawnRot);
            var spawn = FindInScene(world, "PlayerSpawn");
            spawn.transform.SetPositionAndRotation(spawnPos, spawnRot);
            var sena = FindInScene(world, "Sena");
            sena.transform.SetPositionAndRotation(senaPos, senaRot);

            // 3) producción: las cuatro puertas (se conservan sus componentes) a la entrada de su casa
            foreach (var d in Doors)
            {
                var go = world.GetRootGameObjects().Single(g => g.name == d.door);
                var p = entradas[d.entrada];
                go.transform.position = new Vector3(p.x, go.transform.position.y, p.z);
            }

            // 4) producción: el entorno antiguo (suelo, caminos, plaza, casas, vegetación y props de la distribución anterior)
            //    queda desactivado, no borrado; los carteles de puerta antiguos los cubren los Cartel_Casa_* del prototipo
            var env = FindInScene(world, "Environment");
            env.SetActive(false);
            foreach (var n in new[] { "Cartel_PuertaCasaMeses", "Cartel_PuertaCasaNumeros", "Cartel_PuertaCasaSaludos" })
                world.GetRootGameObjects().Single(g => g.name == n).SetActive(false);

            // 5) cerrar el prototipo SIN guardar y guardar solo LuminaWorld
            EditorSceneManager.CloseScene(proto, true);
            SceneManager.SetActiveScene(world);
            if (!EditorSceneManager.SaveScene(world)) throw new Exception("No se pudo guardar LuminaWorld.");
            Debug.Log("[R624] Integración completada y guardada: " + WorldPath);
        }

        static GameObject FindInScene(Scene s, string name)
        {
            foreach (var r in s.GetRootGameObjects())
            {
                if (r.name == name) return r;
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            throw new Exception("No encontrado: " + name);
        }
    }
}
