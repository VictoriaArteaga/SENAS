using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Pule LuminaWorld sin reconstruirla (idempotente):
    ///  · Sena aparece justo delante del jugador al entrar (a ~2,4 m, mirándolo), no al otro lado de la plaza;
    ///  · cada casa tiene su letrero de nivel («NIVEL N») apilado sobre el cartel con el nombre, con el mismo estilo de los marcadores de aldea.
    /// Menú: Lumina &gt; Mundo &gt; Pulir LuminaWorld.
    /// </summary>
    public static class LuminaWorldPolish
    {
        private const string WorldPath = "Assets/Project/Scenes/LuminaWorld.unity";
        private const float SenaDistance = 2.4f;
        private const float SignStep = 0.65f; // misma separación que los marcadores de aldea (Tablero1 / Tablero2)

        // Orden de aprendizaje: el mismo de los accesos de Practice y del desbloqueo por XP.
        private static readonly (string sign, int level)[] Houses =
        {
            ("Cartel_Casa_AlphabetHouse", 1),
            ("Cartel_Casa_NumbersHouse", 2),
            ("Cartel_Casa_MonthsHouse", 3),
            ("Cartel_Casa_GreetingsHouse", 4),
        };

        [MenuItem("Lumina/Mundo/Pulir LuminaWorld")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);
            PlaceSenaInFront();
            foreach (var house in Houses) AddLevelSign(house.sign, house.level);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Lumina] LuminaWorld pulido: Sena delante del jugador y letreros de nivel en las casas.");
        }

        private static void PlaceSenaInFront()
        {
            var player = Object.FindFirstObjectByType<Lumina.Player.FirstPersonController>();
            var sena = GameObject.Find("Sena");
            if (player == null || sena == null) throw new System.InvalidOperationException("Faltan el jugador o Sena en LuminaWorld.");

            Vector3 forward = player.transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 position = player.transform.position + forward * SenaDistance;
            position.y = sena.transform.position.y;
            sena.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-forward));
            Debug.Log($"[Lumina] Sena colocada en {position} (jugador en {player.transform.position}).");
        }

        private static void AddLevelSign(string signName, int level)
        {
            var root = GameObject.Find(signName);
            if (root == null) { Debug.LogWarning("[Lumina] No se encontró " + signName); return; }

            foreach (var old in new[] { "Tablero_Nivel", "Texto_Nivel", "Texto_Nivel_reverso" })
            {
                var found = root.transform.Find(old);
                if (found != null) Object.DestroyImmediate(found.gameObject);
            }

            var board = root.transform.Find("Tablero1");
            var text = root.transform.Find("Texto");
            var back = root.transform.Find("Texto_reverso");
            if (board == null || text == null) { Debug.LogWarning("[Lumina] " + signName + " no tiene Tablero1/Texto."); return; }

            Vector3 lift = Vector3.up * SignStep;
            var levelBoard = Object.Instantiate(board.gameObject, root.transform, false);
            levelBoard.name = "Tablero_Nivel";
            levelBoard.transform.localPosition = board.localPosition + lift;
            levelBoard.transform.localRotation = board.localRotation;
            levelBoard.transform.localScale = board.localScale;

            Clone(text, "Texto_Nivel", lift, level);
            if (back != null) Clone(back, "Texto_Nivel_reverso", lift, level);

            // El poste (lateral) sube para que el cartel de arriba no quede flotando.
            var post = root.transform.Find("Poste");
            if (post != null)
            {
                float top = board.localPosition.y + SignStep + 0.45f;
                var s = post.localScale; s.y = top;
                post.localScale = s;
                var p = post.localPosition; p.y = top / 2f;
                post.localPosition = p;
            }
        }

        private static void Clone(Transform source, string name, Vector3 lift, int level)
        {
            var copy = Object.Instantiate(source.gameObject, source.parent, false);
            copy.name = name;
            var tmp = copy.GetComponent<TMP_Text>();
            tmp.text = "NIVEL " + level;
            var rect = (RectTransform)copy.transform;
            rect.localRotation = source.localRotation;
            rect.anchoredPosition3D = source.localPosition + lift;
            Debug.Log($"[Lumina] {name}: origen {source.localPosition} -> {rect.localPosition} (anchors {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot})");
        }

        /// <summary>Genera capturas de comprobación en LUMINA_SHOTS (por defecto Library/LuminaShots).</summary>
        public static void Capture()
        {
            EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Single);
            string dir = System.Environment.GetEnvironmentVariable("LUMINA_SHOTS") ?? Path.GetFullPath("Library/LuminaShots");
            Directory.CreateDirectory(dir);

            var player = Object.FindFirstObjectByType<Lumina.Player.FirstPersonController>();
            Shoot(dir, "spawn", player.transform.position + Vector3.up * 1.6f, player.transform.forward);

            foreach (var house in Houses)
            {
                var root = GameObject.Find(house.sign);
                if (root == null) continue;
                // El texto se lee desde -Z local del cartel.
                foreach (var c in root.GetComponentsInChildren<Transform>()) Debug.Log("[Lumina] " + house.sign + "/" + c.name + " local " + c.localPosition + " scale " + c.localScale);
                Vector3 target = root.transform.position + Vector3.up * 2.3f;
                Vector3 front = root.transform.position - root.transform.forward * 6f + Vector3.up * 1.6f;
                Vector3 behind = root.transform.position + root.transform.forward * 6f + Vector3.up * 1.6f;
                Shoot(dir, "nivel" + house.level + "_frente", front, target - front);
                Shoot(dir, "nivel" + house.level + "_detras", behind, target - behind);
            }
            Debug.Log("[Lumina] Capturas en " + dir);
        }

        private static void Shoot(string dir, string name, Vector3 position, Vector3 direction)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.transform.position = position;
            cam.transform.rotation = Quaternion.LookRotation(direction);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
