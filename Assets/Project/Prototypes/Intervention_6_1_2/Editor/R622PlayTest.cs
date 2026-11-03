using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = Lumina.Prototype612.R621Layout;
using H = Lumina.Prototype612.Proto612LayoutV2;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Intervención 6.2.2: prueba automatizada en Play Mode sobre Prototype_Modular_FourVillages con la decoración temática. Reutiliza el conductor de 6.1.4
    /// (Proto612PlayDriver, sin modificarlo): entrada de teclado y ratón INYECTADA en el Input System (no son periféricos físicos).
    /// El driver temporal no se guarda; la escena tampoco.
    /// </summary>
    public static class R622PlayTest
    {
        static Vector2 Pre(int v, float d) => H.DoorPoint(v, 0f) + H.Houses[v].Front * d;

        public static void Run()
        {
            EditorSceneManager.OpenScene(R621Builder.ScenePath, OpenSceneMode.Single);
            var pts = new List<Vector2>(); var caps = new List<string>(); var labs = new List<string>(); var faces = new List<Vector2>();

            void Walk(string name, bool reverse, string captureMid, string captureEnd, string tag, int dropLast = 0, int faceHouse = -1)
            {
                int idx = System.Array.IndexOf(L.PathName, name); if (idx < 0) { Debug.LogError("[R621] camino desconocido " + name); return; }
                var list = new List<Vector2>(); foreach (var (p, _) in Proto612Layout.Resample(L.Paths[idx], 3f)) if (!L.HubCenter.Any(h => Vector2.Distance(h, p) < 3.9f)) list.Add(p);   // 6.2.2: el centro de cada plaza temática es un monumento
                if (reverse) list.Reverse();
                if (dropLast > 0 && !reverse) list.RemoveRange(list.Count - dropLast, dropLast);
                for (int i = 0; i < list.Count; i++)
                {
                    pts.Add(list[i]); labs.Add(tag + " " + (i + 1) + "/" + list.Count);
                    string c = ""; if (i == list.Count / 2 && !string.IsNullOrEmpty(captureMid)) c = captureMid; if (i == list.Count - 1 && !string.IsNullOrEmpty(captureEnd)) c = captureEnd;
                    caps.Add(c); faces.Add(i == list.Count - 1 && faceHouse >= 0 && !string.IsNullOrEmpty(captureEnd) ? H.DoorPoint(faceHouse, 0f) : Vector2.zero);
                }
            }
            void Point(Vector2 p, string tag) { pts.Add(p); labs.Add(tag); caps.Add(""); faces.Add(Vector2.zero); }

            Point(L.PlazaCenter, "centro de la plaza");
            Walk("Camino_Puente_CentroNorte", false, "puente_centro_norte_oeste_este", null, "Puente centro-norte O-E");
            Walk("Camino_CentroNorte_Meses", false, null, "entrada_Meses", "a Meses", 2, 2);
            Walk("Camino_CentroNorte_Meses", true, null, null, "desde Meses");
            Walk("Camino_Ribera_Este", false, null, null, "ribera este");
            Walk("Camino_CentroSur_Saludos", false, null, "entrada_Saludos", "a Saludos", 2, 3);
            Walk("Camino_CentroSur_Saludos", true, null, null, "desde Saludos");
            Walk("Camino_Puente_CentroSur", true, "puente_centro_sur_este_oeste", null, "Puente centro-sur E-O");
            Walk("Camino_Plaza_Numeros", false, null, "entrada_Numeros", "a Numeros", 2, 1);
            Walk("Camino_Plaza_Numeros", true, null, null, "desde Numeros");
            Walk("Camino_Plaza_Abecedario", false, null, "entrada_Abecedario", "a Abecedario", 2, 0);
            Walk("Camino_Abecedario_Plaza_Tematica", false, null, null, "a plaza tematica NO");
            Walk("Camino_Puente_Norte", false, "puente_norte_oeste_este", "llegada_plaza_tematica_NE", "Puente norte O-E");
            Walk("Camino_Puente_Norte", true, "puente_norte_este_oeste", null, "Puente norte E-O");
            Walk("Camino_Abecedario_Plaza_Tematica", true, null, null, "desde plaza tematica NO");
            Walk("Camino_Plaza_Abecedario", true, null, null, "regreso a la plaza");
            Walk("Camino_Plaza_Numeros", false, null, null, "a Numeros (de paso)", 3);
            Walk("Camino_Numeros_Plaza_Tematica", false, null, null, "a plaza tematica SO");
            Walk("Camino_Puente_Sur", false, "puente_sur_oeste_este", "llegada_plaza_tematica_SE", "Puente sur O-E");
            Walk("Camino_Puente_Sur", true, "puente_sur_este_oeste", null, "Puente sur E-O");

            Vector3 G(Vector2 p, float dy) => new Vector3(p.x, L.Height(p.x, p.y) + dy, p.y);
            var sp = new Vector2(L.SenaPos.x, L.SenaPos.z); var toSpawn = (new Vector2(L.Spawn.x, L.Spawn.z) - sp).normalized; var senaHead = G(sp, 1.45f);
            var plazaCam = new Vector3(-17.5f, L.Height(-17.5f, -7f) + 2.4f, -7f);
            var day = new[]
            {
                new Proto612PlayDriver.ShotSpec { name = "sena_saludo", position = G(sp + toSpawn * 2.8f, 1.55f), lookAt = senaHead + Vector3.down * 0.15f, fov = 50f },
                new Proto612PlayDriver.ShotSpec { name = "sena_idle", position = G(sp + toSpawn * 2.8f, 1.55f), lookAt = senaHead + Vector3.down * 0.15f, fov = 50f },
                new Proto612PlayDriver.ShotSpec { name = "plaza_sena", position = plazaCam, lookAt = senaHead, fov = 62f },
                new Proto612PlayDriver.ShotSpec { name = "spawn_primera_persona", fromPlayer = true },
                new Proto612PlayDriver.ShotSpec { name = "aldea_abecedario_plaza", position = G(L.HubCenter[0] + (L.HubCenter[0] - H.Houses[0].center).normalized * 6.3f, 1.65f), lookAt = G(L.HubCenter[0], 1.8f), fov = 66f },
                new Proto612PlayDriver.ShotSpec { name = "aldea_meses_reloj", position = G(L.HubCenter[2] + (Pre(2, 7f) - L.HubCenter[2]).normalized * 7.5f, 1.7f), lookAt = G(L.HubCenter[2], 3.3f), fov = 55f },
                new Proto612PlayDriver.ShotSpec { name = "aldea_saludos_manos", position = G(L.HubCenter[3] + (Pre(3, 7f) - L.HubCenter[3]).normalized * 5.5f, 1.8f), lookAt = G(L.HubCenter[3], 2.0f), fov = 55f },
            };
            var b1 = L.Bridges[1]; var ah = H.Houses[0];
            var night = new[]
            {
                new Proto612PlayDriver.ShotSpec { name = "noche_general", position = new Vector3(10, 78, -60), lookAt = new Vector3(0, 0, 4), fov = 52f },
                new Proto612PlayDriver.ShotSpec { name = "noche_plaza", position = plazaCam, lookAt = senaHead, fov = 62f },
                new Proto612PlayDriver.ShotSpec { name = "noche_puente_centro_norte", position = G(b1.center - b1.axis * 15f, 2.3f), lookAt = G(b1.center, 0.6f), fov = 60f },
                new Proto612PlayDriver.ShotSpec { name = "noche_reloj_meses", position = G(L.HubCenter[2] + (Pre(2, 7f) - L.HubCenter[2]).normalized * 7.5f, 1.7f), lookAt = G(L.HubCenter[2], 3.3f), fov = 55f },
                new Proto612PlayDriver.ShotSpec { name = "noche_manos_saludos", position = G(L.HubCenter[3] + (Pre(3, 7f) - L.HubCenter[3]).normalized * 5.5f, 1.8f), lookAt = G(L.HubCenter[3], 2.0f), fov = 55f },
                new Proto612PlayDriver.ShotSpec { name = "noche_entrada_Abecedario", position = G(H.DoorPoint(0, 0f) + ah.Front * 7.5f, 1.65f), lookAt = new Vector3(H.DoorPoint(0, 0f).x, 1.5f, H.DoorPoint(0, 0f).y), fov = 62f },
            };

            var go = new GameObject("TEMP_PlayDriver"); var d = go.AddComponent<Proto612PlayDriver>();
            d.points = pts.ToArray(); d.captureAt = caps.ToArray(); d.labels = labs.ToArray(); d.faceAt = faces.ToArray(); d.dayShots = day; d.nightShots = night; d.prefix = "r622_play_";
            d.evidenceDir = System.IO.Path.GetFullPath(Proto612Builder.Root + "Evidence/");
            d.resultPath = System.IO.Path.GetFullPath(Proto612Builder.Root + "Evidence/r622_playmode.txt");
            Debug.Log("[R621] puntos de ruta: " + pts.Count);
            EditorApplication.EnterPlaymode();
        }
    }
}
