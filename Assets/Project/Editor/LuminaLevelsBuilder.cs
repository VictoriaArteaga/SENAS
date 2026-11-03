using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Lumina.Core;
using Lumina.Interaction;
using Lumina.Signs;
using Lumina.UI;
using Ui = Lumina.EditorTools.LuminaUiKit;
using AuthBuilder = Lumina.EditorTools.LuminaAuthSceneBuilder;
using Screens = Lumina.EditorTools.LuminaScreensBuilder;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Construye las casas de aprendizaje NumbersHouse, MonthsHouse y GreetingsHouse a partir de AlphabetHouse (que no se
    /// modifica): se copia la escena para conservar su estructura, ventanas, luces, jugador, Sena, puerta de salida e
    /// indicadores, y se sustituye la decoración del abecedario por la de cada casa. Añade además las puertas en LuminaWorld
    /// y los accesos desde Practice, sin reconstruir esas escenas. Menú: Lumina &gt; Niveles.
    /// </summary>
    public static class LuminaLevelsBuilder
    {
        private const string SourceScene = AuthBuilder.ScenesDir + "AlphabetHouse.unity";
        private const string WorldScene = AuthBuilder.ScenesDir + "LuminaWorld.unity";
        private const string MaterialDir = "Assets/Project/Materials/House/Lumina_";
        private const string RootName = "--- CASA DE APRENDIZAJE ---";

        private static readonly string[] ScenePaths =
        {
            AuthBuilder.ScenesDir + "NumbersHouse.unity", AuthBuilder.ScenesDir + "MonthsHouse.unity", AuthBuilder.ScenesDir + "GreetingsHouse.unity"
        };

        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        [MenuItem("Lumina/Niveles/Construir las tres casas")]
        public static void BuildAll()
        {
            BuildHouse(LevelKind.Numbers, ScenePaths[0]);
            BuildHouse(LevelKind.Months, ScenePaths[1]);
            BuildHouse(LevelKind.Greetings, ScenePaths[2]);
            AddDoorsToWorld();
            AddShortcutsToPractice();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Casas de aprendizaje construidas, puertas añadidas a LuminaWorld y accesos a Practice.");
        }

        [MenuItem("Lumina/Niveles/Construir solo Casa de Números (0–30)")]
        public static void BuildNumbersHouseOnly()
        {
            BuildHouse(LevelKind.Numbers, ScenePaths[0]);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Casa de Números reconstruida con el rango 0–30.");
        }

        private static void RegisterInBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in ScenePaths)
            {
                list.RemoveAll(s => s.path == path);
                list.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ Casa

        private static void BuildHouse(LevelKind kind, string path)
        {
            // La copia conserva las referencias internas de AlphabetHouse (Sena ↔ burbuja ↔ indicador, jugador, puerta).
            var metaPath = path + ".meta";
            var existingMeta = File.Exists(metaPath) ? File.ReadAllText(metaPath) : null;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(SourceScene, path)) throw new System.InvalidOperationException("No se pudo copiar " + SourceScene);
            // Mantiene referencias externas a la escena cuando se vuelve a generar una casa existente.
            if (!string.IsNullOrEmpty(existingMeta))
            {
                File.WriteAllText(metaPath, existingMeta);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var root = GameObject.Find(RootName).transform;
            foreach (var name in new[] { "Estanteria", "EscenarioMano", "AtrilAbecedario", "MesaLectura", "Cuadros" })
                DestroyChild(root, name);
            DestroyChild(root, "PuntosDeInteraccion/PuntoLibroAbecedario");
            foreach (var name in new[] { "FocoMano", "RellenoMano" }) DestroyChild(root, "Luces/" + name);
            foreach (var name in new[] { "LearningCamera", "AlphabetLevelUI", "FondoVista" }) DestroyRoot(name);

            // Los indicadores del abecedario se quedan para los objetos de aprendizaje. Fuera del tema (orden ≥ 1000) para que
            // el velo oscuro de la interfaz no oscurezca la casa entera.
            var itemsPrompt = GameObject.Find("PromptAbecedario");
            itemsPrompt.name = "PromptObjetos";
            foreach (var promptName in new[] { "PromptSena", "PromptObjetos", "PromptSalida" })
                GameObject.Find(promptName).GetComponent<Canvas>().sortingOrder = 1000;

            // El cielo se ve por el tema: ThemeSkybox cambia día/noche y la luz ambiente igual que en el resto del juego.
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(AuthBuilder.WorldSkyPath);
            // Si la plantilla trae ThemeAmbient (ambiente en gradiente por tema), se guarda su aspecto de día en lugar de forzar
            // Skybox: así la escena generada conserva Trilight y coincide con lo que ThemeAmbient aplica al jugar.
            var ambient = root.GetComponent<ThemeAmbient>();

            if (ambient != null)
            {
                ambient.ApplyTheme(false);
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }
            RenderSettings.ambientIntensity = 0.6f;

            // Las macetas de la pared norte taparían pizarras y calendario.
            var plants = root.Find("Plantas");
            for (int i = plants.childCount - 1; i >= 0; i--)
                if (plants.GetChild(i).position.z > 7f) Object.DestroyImmediate(plants.GetChild(i).gameObject);

            var decor = new GameObject("Decoracion").transform;
            decor.SetParent(root, false);
            var sena = GameObject.Find("Sena");
            switch (kind)
            {
                case LevelKind.Numbers: BuildNumbersDecor(decor, root, sena); break;
                case LevelKind.Months: BuildMonthsDecor(decor, root, sena); break;
                default: BuildGreetingsDecor(decor, root, sena); break;
            }

            BuildPanelAndWire(kind, itemsPrompt.GetComponentInChildren<InteractionPrompt>(true));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void DestroyChild(Transform root, string path)
        {
            var child = root.Find(path);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void DestroyRoot(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        private static void PlaceSena(GameObject sena, Vector3 position, string[] lines)
        {
            // La burbuja de bienvenida es un objeto aparte (no hijo de Sena): se mueve con ella.
            var delta = position - sena.transform.position;
            sena.transform.position = position;
            var bubble = GameObject.Find("WelcomeBubble");
            if (bubble != null) bubble.transform.position += delta;
            var so = new SerializedObject(sena.GetComponent<Lumina.NPC.SenaWelcomeInteraction>());
            var array = so.FindProperty("welcomeLines");
            array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) array.GetArrayElementAtIndex(i).stringValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Recolor(Transform root, string prefix, Material material)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith(prefix)) r.sharedMaterial = material;
        }

        private static void RecolorRug(Transform root, params Material[] rings)
        {
            var rug = root.Find("Alfombra");
            for (int i = 0; i < rings.Length && i < rug.childCount; i++) rug.GetChild(i).GetComponent<Renderer>().sharedMaterial = rings[i];
        }

        // ------------------------------------------------------------------ Casa de los Números (aula)

        private static void BuildNumbersDecor(Transform decor, Transform root, GameObject sena)
        {
            PlaceSena(sena, new Vector3(2.0f, 0f, 5.6f), new[]
            {
                "¡Bienvenido a la Casa de los Números!",
                "Aquí contamos con las manos, del cero al treinta.",
                "Acércate a un bloque de la estantería y pulsa E para ver su seña."
            });
            // Las luces de las ventanas oeste quedaban pegadas a la estantería y quemaban los bloques.
            foreach (var light in root.Find("Ventanas").GetComponentsInChildren<Light>(true))
                if (light.transform.position.x < 0f) light.transform.position += new Vector3(1.3f, 0f, 0f);
            Recolor(root, "Zocalo", M("TurquoiseDeep"));
            Recolor(root, "Moldura", M("TurquoiseSoft"));
            RecolorRug(root, M("TurquoiseDeep"), M("Cream"), M("TurquoiseSoft"), M("CreamDeep"));

            // Pizarrón con los números y su cantidad en puntos (decoración física, no interactiva).
            var board = Group(decor, "Pizarron");
            Box(board, "Marco", new Vector3(0f, 1.9f, 7.93f), new Vector3(6.9f, 2.5f, 0.1f), M("WoodDark"), true);
            Box(board, "Superficie", new Vector3(0f, 1.9f, 7.87f), new Vector3(6.6f, 2.2f, 0.04f), M("Ink"));
            Box(board, "Repisa", new Vector3(0f, 0.72f, 7.78f), new Vector3(6.9f, 0.06f, 0.22f), M("WoodLight"));
            for (int i = 0; i < 4; i++) Box(board, "Tiza", new Vector3(-1.4f + i * 0.32f, 0.77f, 7.78f), new Vector3(0.2f, 0.04f, 0.04f), M(i % 2 == 0 ? "Cream" : "CoralSoft"));
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 11; column++)
                {
                    int number = row * 11 + column;
                    if (number > 30) continue;
                    float x = -3.0f + column * 0.6f;
                    float y = 2.55f - row * 0.68f;
                    Text(board, "Numero" + number, number.ToString(), new Vector3(x, y, 7.84f), 0.29f,
                        number % 2 == 0 ? Ui.Cream : Ui.Coral, 0f, 0.42f);
                }
            }
            Text(board, "Titulo", "DEL 0 AL 30", new Vector3(0f, 0.83f, 7.84f), 0.26f,
                new Color(1f, 0.953f, 0.863f, 0.8f), 0f, 3f);

            // Estantería de números: 31 bloques interactivos repartidos en cuatro baldas.
            var shelf = Group(decor, "EstanteriaNumeros");
            Box(shelf, "Fondo", new Vector3(-4.46f, 1.4f, 4.0f), new Vector3(0.06f, 2.5f, 5.8f), M("TurquoiseSoft"), true);
            Box(shelf, "Techo", new Vector3(-4.2f, 2.58f, 4.0f), new Vector3(0.6f, 0.08f, 5.8f), M("WoodMid"));
            foreach (float z in new[] { 1.05f, 6.95f }) Box(shelf, "Lateral", new Vector3(-4.2f, 1.3f, z), new Vector3(0.6f, 2.5f, 0.08f), M("WoodMid"), true);
            string[] blockMaterials = { "Coral", "TurquoiseDeep", "Coral", "TurquoiseDeep", "Coral" };
            for (int tier = 0; tier < 4; tier++)
            {
                float plankY = 0.38f + tier * 0.53f;
                Box(shelf, "Balda" + tier, new Vector3(-4.2f, plankY, 4.0f), new Vector3(0.6f, 0.07f, 5.8f), M("WoodMid"), true);
                for (int i = 0; i < 8; i++)
                {
                    int n = tier * 8 + i;
                    if (n > 30) continue;
                    float z = 1.55f + i * 0.7f;
                    var block = Group(decor, "Numero_" + n);
                    block.position = new Vector3(-4.16f, plankY + 0.035f + 0.25f, z);
                    Box(block, "Bloque", Vector3.zero, new Vector3(0.44f, 0.5f, 0.5f), M(blockMaterials[i % blockMaterials.Length]), true, localSpace: true);
                    Ball(block, "Sello", new Vector3(0.2f, 0.28f, 0f), 0.09f, M("CreamDeep"), localSpace: true);
                    var digit = Text(block, "Digito", n.ToString(), new Vector3(0.225f, 0f, 0f), 0.32f, Ui.Cream, -90f, 0.52f, localSpace: true);
                    var small = Text(block, "Palabra", "", new Vector3(0.225f, -0.18f, 0f), 0.065f, Ui.Cream, -90f, 0.5f, localSpace: true);
                    MakeTarget(block, LevelKind.Numbers, n, (digit, ItemLabelFormat.Digit), (small, ItemLabelFormat.Word));
                }
            }

            // Mesa de conteo con ábaco y cubos.
            var table = Group(decor, "MesaConteo");
            Box(table, "Tapa", new Vector3(2.9f, 0.78f, 3.0f), new Vector3(1.9f, 0.08f, 1.0f), M("WoodLight"), true);
            foreach (var p in new[] { new Vector2(2.0f, 2.6f), new Vector2(3.8f, 2.6f), new Vector2(2.0f, 3.4f), new Vector2(3.8f, 3.4f) })
                Box(table, "Pata", new Vector3(p.x, 0.37f, p.y), new Vector3(0.08f, 0.74f, 0.08f), M("WoodMid"));
            Box(table, "AbacoBase", new Vector3(2.55f, 0.86f, 3.0f), new Vector3(0.9f, 0.05f, 0.5f), M("WoodDark"));
            Box(table, "AbacoMarcoI", new Vector3(2.12f, 1.2f, 3.0f), new Vector3(0.05f, 0.7f, 0.5f), M("WoodDark"));
            Box(table, "AbacoMarcoD", new Vector3(2.98f, 1.2f, 3.0f), new Vector3(0.05f, 0.7f, 0.5f), M("WoodDark"));
            for (int rod = 0; rod < 4; rod++)
            {
                float y = 0.98f + rod * 0.16f;
                Box(table, "Varilla", new Vector3(2.55f, y, 3.0f), new Vector3(0.84f, 0.02f, 0.02f), M("Ink"));
                for (int b = 0; b < 5; b++)
                    Ball(table, "Cuenta", new Vector3(2.2f + b * 0.09f + (b >= 3 ? 0.28f : 0f), y, 3.0f), 0.08f, M(rod % 2 == 0 ? "Coral" : "Turquoise"));
            }
            for (int c = 0; c < 5; c++) Box(table, "CuboConteo", new Vector3(3.5f, 0.86f + c * 0.1f, 2.85f), new Vector3(0.1f, 0.1f, 0.1f), M(c % 2 == 0 ? "Coral" : "TurquoiseSoft"), false, 15f * c);
            for (int c = 0; c < 3; c++) Box(table, "CuboSuelto", new Vector3(3.3f + c * 0.14f, 0.86f, 3.3f), new Vector3(0.1f, 0.1f, 0.1f), M("CreamDeep"), false, 20f * c);

            // Tarjetas de cantidades en la pared este y cubos con números colgando del techo.
            for (int c = 0; c < 3; c++)
            {
                var card = Group(decor, "TarjetaCantidad" + (c + 2));
                float z = 3.7f + c * 0.95f;
                Box(card, "Carton", new Vector3(4.44f, 1.9f, z), new Vector3(0.04f, 0.7f, 0.6f), M("CreamDeep"));
                for (int d = 0; d < c + 2; d++) Ball(card, "Punto", new Vector3(4.41f, 2.1f - d * 0.15f, z), 0.1f, M(d % 2 == 0 ? "Coral" : "TurquoiseDeep"));
            }
            for (int h = 0; h < 3; h++)
            {
                var cube = Group(decor, "CuboColgante" + (h + 1));
                float x = -1.0f + h * 1.4f, z = 3.8f + h * 0.7f, y = 2.45f + (h % 2) * 0.15f;
                float cableLength = 3.2f - (y + 0.2f);
                Box(cube, "Cable", new Vector3(x, y + 0.2f + cableLength / 2f, z), new Vector3(0.015f, cableLength, 0.015f), M("Ink"));
                Box(cube, "Cubo", new Vector3(x, y, z), new Vector3(0.36f, 0.36f, 0.36f), M(h == 1 ? "TurquoiseDeep" : "Coral"));
                foreach (float yaw in new[] { 0f, 180f })
                    Text(cube, "Digito", (h + 1).ToString(), new Vector3(x, y, z + (yaw == 0f ? -0.19f : 0.19f)), 0.27f, Ui.Cream, yaw, 0.4f);
            }
        }

        // ------------------------------------------------------------------ Casa de los Meses (calendario)

        private static void BuildMonthsDecor(Transform decor, Transform root, GameObject sena)
        {
            PlaceSena(sena, new Vector3(2.9f, 0f, 3.3f), new[]
            {
                "¡Bienvenido a la Casa de los Meses!",
                "Aquí aprenderás los doce meses del año.",
                "Mira el calendario de la pared: apunta a un mes y pulsa E para ver su seña."
            });
            Recolor(root, "Zocalo", M("WoodMid"));
            Recolor(root, "Moldura", M("CoralSoft"));
            RecolorRug(root, M("WoodLight"), M("Cream"), M("CoralSoft"), M("CreamDeep"));

            // Calendario de pared: 12 hojas, una por mes. Son los objetos interactivos.
            var wall = Group(decor, "CalendarioPared");
            Box(wall, "Marco", new Vector3(0f, 2.0f, 7.93f), new Vector3(7.7f, 2.25f, 0.1f), M("WoodDark"), true);
            Box(wall, "Fondo", new Vector3(0f, 2.0f, 7.87f), new Vector3(7.5f, 2.05f, 0.04f), M("Cream"));
            Box(wall, "Cabecera", new Vector3(0f, 2.88f, 7.85f), new Vector3(7.5f, 0.34f, 0.04f), M("Coral"));
            foreach (float x in new[] { -3.2f, -1.6f, 0f, 1.6f, 3.2f }) Box(wall, "Argolla", new Vector3(x, 3.1f, 7.83f), new Vector3(0.1f, 0.1f, 0.14f), M("Ink"));
            Ball(wall, "Sol", new Vector3(-3.3f, 2.88f, 7.8f), 0.24f, M("LampGlow"));
            Ball(wall, "Luna", new Vector3(3.3f, 2.88f, 7.8f), 0.24f, M("CreamDeep"));
            string[] rowMaterials = { "TurquoiseSoft", "CreamDeep", "CoralSoft" };
            for (int m = 0; m < 12; m++)
            {
                int row = m / 4, col = m % 4;
                var sheet = Group(decor, "Mes_" + (m + 1));
                sheet.position = new Vector3(-2.7f + col * 1.8f, 2.48f - row * 0.52f, 7.82f);
                Box(sheet, "Hoja", Vector3.zero, new Vector3(1.66f, 0.44f, 0.05f), M(rowMaterials[row]), true, localSpace: true);
                var name = Text(sheet, "Mes", "", new Vector3(0.14f, 0f, -0.03f), 0.165f, Ui.Ink, 0f, 1.3f, localSpace: true);
                var number = Text(sheet, "Numero", "", new Vector3(-0.7f, 0f, -0.03f), 0.13f, new Color(0.812f, 0.29f, 0.247f), 0f, 0.3f, localSpace: true);
                MakeTarget(sheet, LevelKind.Months, m, (name, ItemLabelFormat.Word), (number, ItemLabelFormat.MonthNumber));
            }

            // Reloj de pared (oeste) con agujas que siguen la hora real.
            var clock = Group(decor, "RelojPared");
            Cyl(clock, "Esfera", new Vector3(-4.43f, 2.1f, 4.0f), 1.0f, 0.04f, M("Cream"), euler: new Vector3(0f, 0f, 90f));
            Cyl(clock, "Aro", new Vector3(-4.46f, 2.1f, 4.0f), 1.12f, 0.05f, M("WoodDark"), euler: new Vector3(0f, 0f, 90f));
            for (int t = 0; t < 12; t++)
            {
                float a = t * 30f * Mathf.Deg2Rad;
                Box(clock, "Marca", new Vector3(-4.40f, 2.1f + Mathf.Cos(a) * 0.4f, 4.0f + Mathf.Sin(a) * 0.4f), new Vector3(0.02f, t % 3 == 0 ? 0.1f : 0.06f, 0.02f), M("Ink"), false, 0f, localSpace: false, rotX: t * 30f);
            }
            var hour = Group(clock, "AgujaHora"); hour.position = new Vector3(-4.38f, 2.1f, 4.0f);
            Box(hour, "Cuerpo", new Vector3(0f, 0.12f, 0f), new Vector3(0.025f, 0.24f, 0.04f), M("Ink"), localSpace: true);
            var minute = Group(clock, "AgujaMinuto"); minute.position = new Vector3(-4.36f, 2.1f, 4.0f);
            Box(minute, "Cuerpo", new Vector3(0f, 0.17f, 0f), new Vector3(0.02f, 0.34f, 0.03f), M("Coral"), localSpace: true);
            Ball(clock, "Centro", new Vector3(-4.35f, 2.1f, 4.0f), 0.07f, M("Ink"));
            // Las agujas giran en el plano Y-Z: se mira desde +X, el eje del giro es −X.
            var wallClock = clock.gameObject.AddComponent<WallClock>();
            hour.localEulerAngles = minute.localEulerAngles = Vector3.zero;
            SetRef(wallClock, "hourHand", hour);
            SetRef(wallClock, "minuteHand", minute);
            SetVector(wallClock, "axis", Vector3.left);

            // Escritorio con libros, agenda, hojas sueltas, reloj de arena y lámpara.
            var desk = Group(decor, "Escritorio");
            Box(desk, "Tapa", new Vector3(-3.55f, 0.78f, 1.7f), new Vector3(1.0f, 0.07f, 1.9f), M("WoodLight"), true);
            foreach (var p in new[] { new Vector2(-3.95f, 0.85f), new Vector2(-3.15f, 0.85f), new Vector2(-3.95f, 2.55f), new Vector2(-3.15f, 2.55f) })
                Box(desk, "Pata", new Vector3(p.x, 0.37f, p.y), new Vector3(0.07f, 0.74f, 0.07f), M("WoodMid"));
            Box(desk, "Silla", new Vector3(-2.5f, 0.45f, 1.7f), new Vector3(0.5f, 0.06f, 0.5f), M("Turquoise"), true);
            Box(desk, "RespaldoSilla", new Vector3(-2.27f, 0.8f, 1.7f), new Vector3(0.06f, 0.7f, 0.5f), M("Turquoise"));
            string[] bookColors = { "Coral", "TurquoiseDeep", "CreamDeep", "CoralSoft" };
            for (int b = 0; b < 4; b++) Box(desk, "Libro", new Vector3(-3.55f, 0.84f + b * 0.07f, 0.95f), new Vector3(0.5f - b * 0.03f, 0.07f, 0.34f), M(bookColors[b]), false, 12f * b);
            Box(desk, "AgendaIzq", new Vector3(-3.75f, 0.835f, 1.75f), new Vector3(0.28f, 0.02f, 0.4f), M("Cream"));
            Box(desk, "AgendaDer", new Vector3(-3.46f, 0.835f, 1.75f), new Vector3(0.28f, 0.02f, 0.4f), M("Cream"));
            for (int s = 0; s < 6; s++) Box(desk, "HojaCalendario", new Vector3(-3.55f, 0.82f + s * 0.012f, 2.35f), new Vector3(0.36f, 0.01f, 0.28f), M(s % 2 == 0 ? "Cream" : "CreamDeep"), false, 4f * s);
            Cyl(desk, "ArenaBase", new Vector3(-3.35f, 0.83f, 2.3f), 0.2f, 0.02f, M("WoodDark"));
            Cyl(desk, "ArenaTope", new Vector3(-3.35f, 1.14f, 2.3f), 0.2f, 0.02f, M("WoodDark"));
            Ball(desk, "ArenaInferior", new Vector3(-3.35f, 0.92f, 2.3f), 0.17f, M("CreamDeep"));
            Ball(desk, "ArenaSuperior", new Vector3(-3.35f, 1.05f, 2.3f), 0.17f, M("Cream"));
            Cyl(desk, "LamparaPie", new Vector3(-3.9f, 0.84f, 2.35f), 0.14f, 0.04f, M("WoodDark"));
            Cyl(desk, "LamparaBrazo", new Vector3(-3.9f, 1.0f, 2.35f), 0.03f, 0.3f, M("WoodDark"));
            Ball(desk, "LamparaBombilla", new Vector3(-3.9f, 1.2f, 2.35f), 0.2f, M("LampGlow"));

            // Paso del tiempo: sol y luna colgando del techo, con cuatro estaciones orbitando.
            var sky = Group(decor, "SolYLuna");
            Box(sky, "Cable", new Vector3(0f, 3.0f, 3.2f), new Vector3(0.015f, 0.6f, 0.015f), M("Ink"));
            Ball(sky, "Sol", new Vector3(0f, 2.55f, 3.2f), 0.5f, M("LampGlow"));
            for (int r = 0; r < 8; r++)
            {
                float a = r * 45f * Mathf.Deg2Rad;
                Box(sky, "Rayo", new Vector3(Mathf.Cos(a) * 0.42f, 2.55f + Mathf.Sin(a) * 0.42f, 3.2f), new Vector3(0.05f, 0.18f, 0.05f), M("LampGlow"), false, 0f, localSpace: false, rotZ: r * 45f - 90f);
            }
            Ball(sky, "Luna", new Vector3(1.3f, 2.3f, 3.0f), 0.34f, M("CreamDeep"));
            string[] seasons = { "TurquoiseSoft", "Coral", "WoodLight", "Turquoise" };
            for (int s = 0; s < 4; s++)
            {
                float a = s * 90f * Mathf.Deg2Rad + 0.6f;
                Ball(sky, "Estacion" + s, new Vector3(Mathf.Cos(a) * 1.0f, 2.1f + Mathf.Sin(a) * 0.15f, 3.2f + Mathf.Sin(a) * 0.8f), 0.2f, M(seasons[s]));
            }

            // Estante bajo de organización (este): libros, cajas de archivo y un globo.
            var low = Group(decor, "EstanteOrganizacion");
            Box(low, "Mueble", new Vector3(4.15f, 0.45f, 5.3f), new Vector3(0.6f, 0.9f, 2.0f), M("WoodMid"), true);
            Box(low, "Tapa", new Vector3(4.15f, 0.93f, 5.3f), new Vector3(0.66f, 0.05f, 2.06f), M("WoodDark"));
            for (int b = 0; b < 9; b++) Box(low, "Libro", new Vector3(4.15f, 1.12f, 4.5f + b * 0.09f), new Vector3(0.3f, 0.36f - (b % 3) * 0.04f, 0.07f), M(bookColors[b % 4]));
            for (int c = 0; c < 2; c++) Box(low, "CajaArchivo", new Vector3(4.15f, 1.06f, 5.7f + c * 0.35f), new Vector3(0.34f, 0.26f, 0.28f), M(c == 0 ? "Turquoise" : "Coral"));
            Cyl(low, "GloboPie", new Vector3(4.15f, 0.98f, 6.4f), 0.2f, 0.04f, M("WoodDark"));
            Ball(low, "Globo", new Vector3(4.15f, 1.2f, 6.4f), 0.34f, M("TurquoiseDeep"));
        }

        // ------------------------------------------------------------------ Casa de los Saludos (conversación)

        private static void BuildGreetingsDecor(Transform decor, Transform root, GameObject sena)
        {
            PlaceSena(sena, new Vector3(-2.9f, 0f, 6.0f), new[]
            {
                "¡Bienvenido a la Casa de los Saludos!",
                "Aquí aprenderás a saludar y a conversar con tus manos.",
                "Mira las burbujas de la pared: apunta a una y pulsa E para ver su seña."
            });
            Recolor(root, "Zocalo", M("Coral"));
            Recolor(root, "Moldura", M("CoralSoft"));
            RecolorRug(root, M("CoralSoft"), M("Cream"), M("Coral"), M("CreamDeep"));

            // Dos pizarras de burbujas en la pared norte: SALUDOS (5) y CONVERSACIÓN (6). Son los objetos interactivos.
            var greetings = LevelsOf(LevelKind.Greetings);
            BuildBubbleBoard(decor, "Pizarra_Saludos", -2.45f, "levels.groupGreetings", greetings, 0, 5, "TurquoiseSoft");
            BuildBubbleBoard(decor, "Pizarra_Conversacion", 2.45f, "levels.groupTalk", greetings, 5, 6, "CoralSoft");

            // Dos sofás frente a frente con una mesa baja: el espacio de la conversación.
            var nook = Group(decor, "RinconConversacion");
            BuildSofa(nook, "SofaOeste", -1.9f, 4.3f, 90f, "Coral");
            BuildSofa(nook, "SofaEste", 1.9f, 4.3f, -90f, "TurquoiseDeep");
            Cyl(nook, "MesaBaja", new Vector3(0f, 0.38f, 4.3f), 0.95f, 0.06f, M("WoodLight"), collider: true);
            Cyl(nook, "MesaPie", new Vector3(0f, 0.19f, 4.3f), 0.14f, 0.38f, M("WoodDark"));
            Cyl(nook, "TazaA", new Vector3(-0.2f, 0.46f, 4.3f), 0.1f, 0.08f, M("Cream"));
            Cyl(nook, "TazaB", new Vector3(0.22f, 0.46f, 4.15f), 0.1f, 0.08f, M("CoralSoft"));
            Ball(nook, "Burbuja", new Vector3(0f, 0.62f, 4.45f), 0.16f, M("Cream"));
            // Marcas en el suelo: dónde se coloca cada interlocutor.
            Cyl(nook, "PuestoSena", new Vector3(-2.9f, 0.015f, 6.0f), 1.0f, 0.03f, M("Coral"));
            Cyl(nook, "PuestoJugador", new Vector3(2.9f, 0.015f, 6.0f), 1.0f, 0.03f, M("TurquoiseDeep"));

            // Cuadros de personas conversando (oeste y este), con su burbuja.
            BuildConversationPicture(decor, "CuadroOeste", new Vector3(-4.44f, 2.0f, 4.3f), -90f);
            BuildConversationPicture(decor, "CuadroEste", new Vector3(4.44f, 2.0f, 4.3f), 90f);

            // Burbujas de diálogo colgantes (decoración).
            var hanging = Group(decor, "BurbujasColgantes");
            for (int h = 0; h < 6; h++)
            {
                float x = (h % 2 == 0 ? -3.3f : 3.3f), z = 2.6f + (h / 2) * 1.8f, y = 2.4f + (h % 3) * 0.12f;
                Box(hanging, "Hilo", new Vector3(x, (3.2f + y) / 2f, z), new Vector3(0.012f, 3.2f - y, 0.012f), M("Ink"));
                var bubble = Ball(hanging, "Burbuja", new Vector3(x, y, z), 0.5f, M(h % 2 == 0 ? "Cream" : "CoralSoft"));
                bubble.transform.localScale = new Vector3(0.42f, 0.32f, 0.14f);
                Ball(hanging, "Cola", new Vector3(x + 0.1f, y - 0.2f, z), 0.08f, M(h % 2 == 0 ? "Cream" : "CoralSoft"));
            }
        }

        private static LevelItem[] LevelsOf(LevelKind kind) => LearningLevels.Get(kind).items;

        private static void BuildBubbleBoard(Transform decor, string name, float centerX, string titleKey, LevelItem[] items, int first, int count, string bubbleMaterial)
        {
            var board = Group(decor, name);
            Box(board, "Marco", new Vector3(centerX, 1.95f, 7.93f), new Vector3(3.9f, 2.5f, 0.1f), M("WoodDark"), true);
            Box(board, "Superficie", new Vector3(centerX, 1.95f, 7.87f), new Vector3(3.7f, 2.3f, 0.04f), M("Cream"));
            var title = Text(board, "Titulo", "", new Vector3(centerX, 2.85f, 7.82f), 0.2f, new Color(0.812f, 0.29f, 0.247f), 0f, 3.4f);
            LocalizedText.Bind(title, titleKey);
            for (int i = 0; i < count; i++)
            {
                int index = first + i;
                int row = i / 2, col = i % 2;
                var bubble = Group(decor, "Frase_" + (index + 1));
                bubble.position = new Vector3(centerX - 0.9f + col * 1.8f, 2.35f - row * 0.62f, 7.8f);
                var body = Ball(bubble, "Globo", Vector3.zero, 1f, M(bubbleMaterial), localSpace: true);
                body.transform.localScale = new Vector3(1.66f, 0.52f, 0.08f);
                var collider = body.GetComponent<Collider>(); if (collider == null) body.AddComponent<BoxCollider>();
                Ball(bubble, "Cola", new Vector3(col == 0 ? -0.6f : 0.6f, -0.3f, 0f), 0.1f, M(bubbleMaterial), localSpace: true);
                var text = Text(bubble, "Frase", "", new Vector3(0f, 0f, -0.05f), 0.15f, Ui.Ink, 0f, 1.6f, localSpace: true);
                MakeTarget(bubble, LevelKind.Greetings, index, (text, ItemLabelFormat.Phrase));
            }
        }

        private static void BuildSofa(Transform parent, string name, float x, float z, float yaw, string material)
        {
            var sofa = Group(parent, name);
            sofa.position = new Vector3(x, 0f, z);
            sofa.rotation = Quaternion.Euler(0f, yaw, 0f);
            // Local: el sofá mira hacia +Z.
            Box(sofa, "Asiento", new Vector3(0f, 0.3f, 0f), new Vector3(1.9f, 0.3f, 0.8f), M(material), true, localSpace: true);
            Box(sofa, "Respaldo", new Vector3(0f, 0.62f, -0.36f), new Vector3(1.9f, 0.55f, 0.18f), M(material), true, localSpace: true);
            foreach (float s in new[] { -1f, 1f }) Box(sofa, "Brazo", new Vector3(s * 0.98f, 0.45f, 0f), new Vector3(0.16f, 0.5f, 0.8f), M(material), false, localSpace: true);
            Box(sofa, "Cojin", new Vector3(-0.55f, 0.55f, -0.15f), new Vector3(0.4f, 0.4f, 0.12f), M("CreamDeep"), false, 12f, localSpace: true);
            foreach (float s in new[] { -0.8f, 0.8f }) Box(sofa, "Pata", new Vector3(s, 0.07f, 0.3f), new Vector3(0.08f, 0.14f, 0.08f), M("WoodDark"), localSpace: true);
        }

        private static void BuildConversationPicture(Transform decor, string name, Vector3 position, float yaw)
        {
            var picture = Group(decor, name);
            picture.position = position;
            picture.rotation = Quaternion.Euler(0f, yaw, 0f);
            // Local: el cuadro mira hacia +Z (se ve desde −Z → se coloca de espaldas a la pared).
            Box(picture, "Marco", new Vector3(0f, 0f, 0.03f), new Vector3(2.0f, 1.3f, 0.06f), M("WoodDark"), localSpace: true);
            Box(picture, "Lienzo", new Vector3(0f, 0f, -0.01f), new Vector3(1.84f, 1.14f, 0.04f), M("TurquoiseSoft"), localSpace: true);
            foreach (float s in new[] { -0.55f, 0.55f })
            {
                Ball(picture, "Cabeza", new Vector3(s, 0.12f, -0.05f), 0.28f, M(s < 0 ? "Coral" : "TurquoiseDeep"), localSpace: true);
                Box(picture, "Cuerpo", new Vector3(s, -0.28f, -0.05f), new Vector3(0.4f, 0.5f, 0.06f), M(s < 0 ? "Coral" : "TurquoiseDeep"), localSpace: true);
            }
            var bubble = Ball(picture, "Burbuja", new Vector3(0f, 0.38f, -0.06f), 1f, M("Cream"), localSpace: true);
            bubble.transform.localScale = new Vector3(0.5f, 0.28f, 0.04f);
        }

        // ------------------------------------------------------------------ Primitivas y texto

        private static Material M(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");
            if (material == null) throw new System.InvalidOperationException("Falta el material Lumina_" + name);
            return material;
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider, Quaternion rotation, bool localSpace)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (localSpace) { go.transform.localPosition = position; go.transform.localRotation = rotation; }
            else { go.transform.position = position; go.transform.rotation = rotation; }
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Caja: posición del centro (mundo o local) y tamaño en metros. <paramref name="yaw"/> gira alrededor de Y.</summary>
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = false,
            float yaw = 0f, bool localSpace = false, float rotX = 0f, float rotZ = 0f) =>
            Prim(PrimitiveType.Cube, parent, name, position, size, material, collider, Quaternion.Euler(rotX, yaw, rotZ), localSpace);

        /// <summary>Esfera de diámetro <paramref name="diameter"/>.</summary>
        private static GameObject Ball(Transform parent, string name, Vector3 position, float diameter, Material material, bool localSpace = false) =>
            Prim(PrimitiveType.Sphere, parent, name, position, Vector3.one * diameter, material, false, Quaternion.identity, localSpace);

        /// <summary>Cilindro de diámetro y altura en metros (el cilindro de Unity mide 2 de alto).</summary>
        private static GameObject Cyl(Transform parent, string name, Vector3 position, float diameter, float height, Material material, bool collider = false, Vector3 euler = default) =>
            Prim(PrimitiveType.Cylinder, parent, name, position, new Vector3(diameter, height / 2f, diameter), material, collider, Quaternion.Euler(euler), false);

        /// <summary>Texto 3D (TextMeshPro) de altura de cuerpo <paramref name="em"/> en metros, mirando según <paramref name="yaw"/>.</summary>
        private static TextMeshPro Text(Transform parent, string name, string text, Vector3 position, float em, Color color, float yaw, float width, bool localSpace = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (localSpace) { go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); }
            else { go.transform.position = position; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); }
            var t = go.AddComponent<TextMeshPro>();
            t.font = Ui.Font;
            t.text = text;
            t.fontSize = em * 10f;
            t.color = color;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(width, em * 1.4f);
            return t;
        }

        private static void MakeTarget(Transform root, LevelKind kind, int index, params (TMP_Text label, ItemLabelFormat format)[] labels)
        {
            var target = root.gameObject.AddComponent<LearningItemTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("index").intValue = index;
            var array = so.FindProperty("labels");
            array.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").objectReferenceValue = labels[i].label;
                element.FindPropertyRelative("format").enumValueIndex = (int)labels[i].format;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Object target, string property, Object value) => Ui.Set(target, property, value);

        private static void SetVector(Object target, string property, Vector3 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Panel del elemento (interfaz existente)

        private static void BuildPanelAndWire(LevelKind kind, InteractionPrompt itemsPrompt)
        {
            var canvas = AuthBuilder.MakeCanvas("LevelPanelCanvas");
            Ui.EnsureEventSystem();
            var outerSize = new Vector2(840f, 760f);
            var card = Ui.Card(canvas.transform, "LevelPanel", outerSize, out var outer);
            Ui.Place(outer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), outerSize);

            var bar = Screens.BuildBar(card, 22f, 700f, 44f);
            var position = Ui.Label(card, "Position", "", 26f, Ui.TurquoiseDark, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            Ui.TopCenter(position.rectTransform, 78f, 760f, 38f);

            TMP_Text big, nameLabel = null, group = null, senaSays = null;
            Image[] dots = new Image[0];
            float instructionY;

            switch (kind)
            {
                case LevelKind.Numbers:
                    big = Ui.Label(card, "Number", "1", 150f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
                    Ui.TopCenter(big.rectTransform, 116f, 600f, 170f);
                    nameLabel = Ui.Label(card, "Word", "", 48f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 8f);
                    Ui.TopCenter(nameLabel.rectTransform, 288f, 700f, 54f);
                    var dotsRow = Ui.Rect("CountDots", card);
                    Ui.TopCenter(dotsRow, 346f, 520f, 36f);
                    var dotsLayout = dotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    dotsLayout.spacing = 10f; dotsLayout.childAlignment = TextAnchor.MiddleCenter;
                    dotsLayout.childControlWidth = dotsLayout.childControlHeight = false;
                    dotsLayout.childForceExpandWidth = dotsLayout.childForceExpandHeight = false;
                    var list = new List<Image>();
                    for (int i = 0; i < LearningLevels.Numbers.items.Length; i++)
                    {
                        var dot = Ui.Panel(dotsRow, "Dot" + (i + 1), Ui.Sand, Ui.Circle);
                        dot.rectTransform.sizeDelta = new Vector2(34f, 34f);
                        list.Add(dot);
                    }
                    dots = list.ToArray();
                    instructionY = 394f;
                    break;

                case LevelKind.Months:
                    var page = Ui.Panel(card, "CalendarPage", Ui.CreamWarm);
                    Ui.TopCenter(page.rectTransform, 126f, 640f, 214f);
                    var outline = Ui.Panel(page.transform, "Outline", Ui.SandDark);
                    Ui.Stretch(outline.rectTransform); outline.transform.SetAsFirstSibling();
                    var sheet = Ui.Panel(page.transform, "Sheet", Ui.CreamWarm);
                    Ui.Stretch(sheet.rectTransform, 4f, 4f, 4f, 4f);
                    var band = Ui.Panel(sheet.transform, "Band", Ui.Coral);
                    Ui.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 76f));
                    nameLabel = Ui.Label(band.transform, "Month", "", 48f, Ui.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 8f);
                    Ui.Stretch(nameLabel.rectTransform, 20f, 4f, 20f, 4f);
                    foreach (float x in new[] { -200f, 200f })
                    {
                        var ring = Ui.Panel(page.transform, "Ring", Ui.Ink, Ui.Circle);
                        Ui.Place(ring.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Mid, new Vector2(x, -4f), new Vector2(28f, 28f));
                    }
                    big = Ui.Label(sheet.transform, "MonthNumber", "01", 100f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
                    Ui.Place(big.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Mid, Vector2.zero, Vector2.zero);
                    big.rectTransform.offsetMin = new Vector2(10f, 6f); big.rectTransform.offsetMax = new Vector2(-10f, -80f);
                    instructionY = 360f;
                    break;

                default:
                    var chip = Ui.Panel(card, "GroupChip", Ui.TurquoiseSoft);
                    Ui.TopCenter(chip.rectTransform, 122f, 340f, 42f);
                    group = Ui.Label(chip.transform, "Group", "", 22f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
                    Ui.Stretch(group.rectTransform);
                    var frame = Ui.Panel(card, "PhraseBubble", Ui.Turquoise);
                    Ui.TopCenter(frame.rectTransform, 172f, 700f, 150f);
                    var inner = Ui.Panel(frame.transform, "Inner", Ui.CreamWarm);
                    Ui.Stretch(inner.rectTransform, 6f, 6f, 6f, 6f);
                    big = Ui.Label(inner.transform, "Phrase", "", 58f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
                    Ui.Stretch(big.rectTransform, 20f, 6f, 20f, 6f);
                    senaSays = Ui.Label(card, "SenaSays", "", 24f, Ui.TurquoiseDark, FontStyles.Bold, TextAlignmentOptions.Center);
                    Ui.TopCenter(senaSays.rectTransform, 330f, 740f, 40f);
                    instructionY = 380f;
                    break;
            }

            var instruction = Ui.Label(card, "Instruction", "", 26f, Ui.Ink, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.TopCenter(instruction.rectTransform, instructionY, 760f, 80f);
            var demoNotice = Ui.Label(card, "DemoNotice", "La demostración animada llegará pronto.", 24f, Ui.TurquoiseDark, FontStyles.Bold);
            Ui.TopCenter(demoNotice.rectTransform, instructionY + 82f, 760f, 32f);

            // Dos filas de acciones: VER SEÑA · PRACTICAR  /  ANTERIOR · VOLVER · SIGUIENTE
            var rowA = Ui.Rect("ActionsMain", card);
            Ui.Place(rowA, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(760f, 92f));
            var learn = Ui.MakeButton(rowA, "LearnButton", "VER SEÑA", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(370f, 88f), 32f, 8f, 3f);
            Ui.Place((RectTransform)learn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(370f, 88f));
            var practice = Ui.MakeButton(rowA, "PracticeButton", "PRACTICAR", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(370f, 88f), 34f, 8f, 3f);
            Ui.Place((RectTransform)practice.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(370f, 88f));

            var rowB = Ui.Rect("ActionsNav", card);
            Ui.Place(rowB, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(760f, 92f));
            var prev = Ui.MakeButton(rowB, "PreviousButton", "ANTERIOR", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(230f, 84f), 26f, 8f, 2f);
            Ui.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(230f, 84f));
            var close = Ui.MakeButton(rowB, "CloseButton", "VOLVER", Ui.Cream, Ui.SandDark, Ui.Ink, new Vector2(230f, 84f), 26f, 8f, 2f);
            Ui.Place((RectTransform)close.transform, Mid, Mid, Mid, Vector2.zero, new Vector2(230f, 84f));
            var next = Ui.MakeButton(rowB, "NextButton", "SIGUIENTE", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(230f, 84f), 26f, 8f, 2f);
            Ui.Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(230f, 84f));

            var houseGo = new GameObject("LearningHouse");
            var controller = houseGo.AddComponent<LearningLevelController>();
            SetEnum(controller, "kind", (int)kind);
            Ui.Set(controller, "panelRoot", canvas.gameObject);
            Ui.Set(controller, "player", Object.FindFirstObjectByType<Lumina.Player.FirstPersonController>());
            Ui.Set(controller, "bigLabel", big);
            Ui.Set(controller, "nameLabel", nameLabel);
            Ui.Set(controller, "positionLabel", position);
            Ui.Set(controller, "groupLabel", group);
            Ui.Set(controller, "senaSaysLabel", senaSays);
            Ui.Set(controller, "instructionLabel", instruction);
            Ui.Set(controller, "demoNotice", demoNotice);
            Ui.SetArray(controller, "countDots", dots);
            Ui.Set(controller, "previousButton", prev);
            Ui.Set(controller, "nextButton", next);
            Ui.Set(controller, "learnButton", learn);
            Ui.Set(controller, "practiceButton", practice);
            Ui.Set(controller, "closeButton", close);
            Ui.Set(controller, "progressBar", bar);

            var selector = houseGo.AddComponent<LearningItemSelector>();
            Ui.Set(selector, "level", controller);
            Ui.Set(selector, "prompt", itemsPrompt);
            Ui.Set(selector, "promptLabel", PromptLabel(itemsPrompt));
        }

        private static TMP_Text PromptLabel(InteractionPrompt prompt)
        {
            foreach (var label in prompt.GetComponentsInChildren<TMP_Text>(true)) if (label.name == "Label") return label;
            throw new System.InvalidOperationException("El indicador no tiene un rótulo «Label».");
        }

        // ------------------------------------------------------------------ Puertas en LuminaWorld

        private static readonly (string scene, string name, string key, string es, Vector3 position, float faceX, string signKey, string signEs)[] Doors =
        {
            (LearningLevels.NumbersScene, "PuertaCasaNumeros", "levels.enterNumbers", "Entrar a la Casa de los Números", new Vector3(-13.6f, 0f, 4.77f), 1f, "title.numbers", "NÚMEROS"),
            (LearningLevels.MonthsScene, "PuertaCasaMeses", "levels.enterMonths", "Entrar a la Casa de los Meses", new Vector3(13.6f, 0f, 5.23f), -1f, "title.months", "MESES"),
            (LearningLevels.GreetingsScene, "PuertaCasaSaludos", "levels.enterGreetings", "Entrar a la Casa de los Saludos", new Vector3(-12.6f, 0f, -15.23f), 1f, "title.greetings", "SALUDOS Y FRASES"),
        };

        /// <summary>Añade a LuminaWorld una puerta por casa, copiando la de AlphabetHouse (mismo prompt, mismo radio, misma entrada).</summary>
        private static void AddDoorsToWorld()
        {
            var scene = EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Single);
            var template = GameObject.Find("PuertaCasaAprendizaje");
            var promptCanvasTemplate = GameObject.Find("DoorPromptCanvas");
            if (template == null || promptCanvasTemplate == null) throw new System.InvalidOperationException("No se encontró la puerta de la casa del abecedario en LuminaWorld.");

            foreach (var door in Doors)
            {
                DestroyRoot(door.name);
                DestroyRoot("Prompt_" + door.name);
                DestroyRoot("Cartel_" + door.name);

                var canvasCopy = Object.Instantiate(promptCanvasTemplate);
                canvasCopy.name = "Prompt_" + door.name;
                var prompt = canvasCopy.GetComponentInChildren<InteractionPrompt>(true);
                var label = PromptLabel(prompt);
                label.text = door.es;
                LocalizedText.Bind(label, door.key);

                var copy = Object.Instantiate(template);
                copy.name = door.name;
                copy.transform.position = door.position;
                SetRef(copy.GetComponent<InteractionPoint>(), "prompt", prompt);
                var so = new SerializedObject(copy.GetComponent<SceneEntrance>());
                so.FindProperty("sceneName").stringValue = door.scene;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Cartel de madera sobre la puerta: dice a qué casa se entra.
                var sign = new GameObject("Cartel_" + door.name).transform;
                sign.position = door.position + new Vector3(door.faceX * 0.5f, 1.4f, 0f);
                Box(sign, "Tabla", sign.position, new Vector3(0.1f, 0.4f, 1.7f), M("WoodMid"), false);
                var signText = Text(sign, "Rotulo", "", sign.position + new Vector3(door.faceX * 0.08f, 0f, 0f), 0.2f, Ui.Cream, door.faceX > 0 ? -90f : 90f, 1.6f);
                signText.text = door.signEs;
                LocalizedText.Bind(signText, door.signKey);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ Accesos en Practice

        /// <summary>Añade a Practice la fila «CASAS DE APRENDIZAJE». Es idempotente y no reconstruye la escena.</summary>
        internal static void AddShortcutsToPractice()
        {
            string path = AuthBuilder.ScenesDir + "Practice.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<PracticeController>();
            if (controller == null) { Debug.LogError("[Lumina] No se encontró PracticeController en Practice."); return; }

            var canvas = controller.transform;
            var old = canvas.Find("LevelShortcuts");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = Ui.Rect("LevelShortcuts", canvas);
            Ui.Place(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 34f), new Vector2(780f, 134f));
            var plate = Ui.Panel(root, "HeadingPlate", Ui.Ink);
            Ui.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(500f, 44f));
            var label = Ui.Label(plate.transform, "Heading", "CASAS DE APRENDIZAJE", 26f, Ui.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            Ui.Stretch(label.rectTransform, 10f, 0f, 10f, 0f);

            var row = Ui.Rect("Row", root);
            Ui.Place(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 88f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f; layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var numbers = Ui.MakeButton(row, "NumbersButton", "NÚMEROS", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(230f, 80f), 28f, 8f, 2f);
            var months = Ui.MakeButton(row, "MonthsButton", "MESES", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(200f, 80f), 28f, 8f, 2f);
            var greetings = Ui.MakeButton(row, "GreetingsButton", "SALUDOS Y FRASES", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(320f, 80f), 26f, 8f, 2f);

            var component = root.gameObject.AddComponent<LevelShortcutsUI>();
            Ui.Set(component, "numbersButton", numbers);
            Ui.Set(component, "monthsButton", months);
            Ui.Set(component, "greetingsButton", greetings);
            Ui.Set(controller, "levelShortcuts", root.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
