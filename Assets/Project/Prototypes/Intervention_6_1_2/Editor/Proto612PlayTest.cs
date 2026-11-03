using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = Lumina.Prototype612.Proto612LayoutV2;

namespace Lumina.Prototype612
{
    /// <summary>Prepara y lanza la prueba automatizada de Play Mode (el driver temporal no se guarda; la escena no se guarda).</summary>
    public static class Proto612PlayTest
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(Proto612BuilderV2.ScenePath, OpenSceneMode.Single);
            var pts = new List<Vector2>(); var caps = new List<string>(); var labs = new List<string>(); var faces = new List<Vector2>();

            void Walk(string name, bool reverse, string captureMid, string captureEnd, string tag, int dropLast = 0, int faceHouse = -1)
            {
                int idx = System.Array.IndexOf(L.PathName, name); var list = new List<Vector2>();
                foreach (var (p, _) in Proto612Layout.Resample(L.Paths[idx], 3f)) list.Add(p);
                if (reverse) list.Reverse();
                if (dropLast > 0 && !reverse) list.RemoveRange(list.Count - dropLast, dropLast);
                for (int i = 0; i < list.Count; i++)
                {
                    pts.Add(list[i]); labs.Add(tag + " " + (i + 1) + "/" + list.Count);
                    string c = "";
                    if (i == list.Count / 2 && !string.IsNullOrEmpty(captureMid)) c = captureMid;
                    if (i == list.Count - 1 && !string.IsNullOrEmpty(captureEnd)) c = captureEnd;
                    caps.Add(c);
                    faces.Add(i == list.Count - 1 && faceHouse >= 0 && !string.IsNullOrEmpty(captureEnd) ? L.DoorPoint(faceHouse, 0f) : Vector2.zero);
                }
            }

            // Spawn → puente 1 (O→E) → Months → Greetings → puente 1 (E→O) → Numbers → Alphabet → puente 2 (O→E) → puente 2 (E→O).
            Walk("Camino_Puente1", false, "puente_principal_oeste_este", null, "Puente 1 O-E");
            Walk("Camino_Explanada_Months", false, null, "entrada_MonthsHouse", "a Months", 2, 2);
            Walk("Camino_Explanada_Months", true, null, null, "desde Months");
            Walk("Camino_Explanada_Greetings", false, null, "entrada_GreetingsHouse", "a Greetings", 2, 3);
            Walk("Camino_Explanada_Greetings", true, null, null, "desde Greetings");
            Walk("Camino_Puente1", true, "puente_principal_este_oeste", null, "Puente 1 E-O");
            Walk("Camino_Plaza_Numbers", false, null, "entrada_NumbersHouse", "a Numbers", 2, 1);
            Walk("Camino_Plaza_Numbers", true, null, null, "desde Numbers");
            Walk("Camino_Plaza_Alphabet", false, null, "entrada_AlphabetHouse", "a Alphabet", 2, 0);
            Walk("Camino_Puente2", false, "puente_norte_oeste_este", "llegada_MonthsHouse_por_puente_norte", "Puente 2 O-E", 1, 2);
            Walk("Camino_Puente2", true, "puente_norte_este_oeste", "llegada_AlphabetHouse_por_puente_norte", "Puente 2 E-O", 0, 0);

            Vector3 G(Vector2 p, float dy) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);
            var sp = new Vector2(L.SenaPos.x, L.SenaPos.z); var toSpawn = (new Vector2(L.Spawn.x, L.Spawn.z) - sp).normalized;
            var b1 = L.Bridges[0]; var ah = L.Houses[0];
            var senaHead = G(sp, 1.45f);
            var day = new[]
            {
                new Proto612PlayDriver.ShotSpec { name = "sena_saludo", position = G(sp + toSpawn * 2.8f, 1.55f), lookAt = senaHead + Vector3.down * 0.15f, fov = 50f },
                new Proto612PlayDriver.ShotSpec { name = "sena_idle", position = G(sp + toSpawn * 2.8f, 1.55f), lookAt = senaHead + Vector3.down * 0.15f, fov = 50f },
                new Proto612PlayDriver.ShotSpec { name = "plaza_sena", position = new Vector3(-19.5f, L.Height(-19.5f, -5.2f) + 2.3f, -5.2f), lookAt = senaHead, fov = 62f },
                new Proto612PlayDriver.ShotSpec { name = "spawn_primera_persona", fromPlayer = true },
            };
            var night = new[]
            {
                new Proto612PlayDriver.ShotSpec { name = "noche_general", position = new Vector3(8, 74, -62), lookAt = new Vector3(0, 0, 4), fov = 50f },
                new Proto612PlayDriver.ShotSpec { name = "noche_plaza", position = new Vector3(-19.5f, L.Height(-19.5f, -5.2f) + 2.3f, -5.2f), lookAt = senaHead, fov = 62f },
                new Proto612PlayDriver.ShotSpec { name = "noche_puente_principal", position = G(b1.center - b1.axis * 15f, 2.2f), lookAt = G(b1.center, 0.6f), fov = 60f },
                new Proto612PlayDriver.ShotSpec { name = "noche_entrada_AlphabetHouse", position = G(L.DoorPoint(0, 0f) + ah.Front * 7.5f, 1.65f), lookAt = new Vector3(L.DoorPoint(0, 0f).x, 1.5f, L.DoorPoint(0, 0f).y), fov = 62f },
            };

            var go = new GameObject("TEMP_PlayDriver");
            var d = go.AddComponent<Proto612PlayDriver>();
            d.points = pts.ToArray(); d.captureAt = caps.ToArray(); d.labels = labs.ToArray(); d.faceAt = faces.ToArray(); d.dayShots = day; d.nightShots = night; d.prefix = "r4_play_";
            d.evidenceDir = System.IO.Path.GetFullPath(Proto612Builder.Root + "Evidence/");
            d.resultPath = System.IO.Path.GetFullPath(Proto612Builder.Root + "Evidence/r4_playmode.txt");
            Debug.Log("[P612-PLAY] puntos de ruta: " + pts.Count);
            EditorApplication.EnterPlaymode();
        }
    }
}
