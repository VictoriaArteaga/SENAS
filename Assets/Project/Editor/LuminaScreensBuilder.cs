using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lumina.Player;
using Lumina.Signs;
using Lumina.UI;
using Ui = Lumina.EditorTools.LuminaUiKit;
using AuthBuilder = Lumina.EditorTools.LuminaAuthSceneBuilder;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Construye (de forma repetible) las pantallas Practice, Profile («MI PROGRESO»), Settings y Results con las piezas
    /// de LuminaUiKit y el mismo fondo de aldea que Login. Menú: Lumina > Pantallas > Construir todo.
    /// </summary>
    public static class LuminaScreensBuilder
    {
        private const string DataDir = "Assets/Project/Data/";
        private const string SignCatalogPath = DataDir + "SignCatalog.asset";
        private const string AvatarCatalogPath = DataDir + "AvatarCatalog.asset";

        private static readonly string[] ScenePaths =
        {
            AuthBuilder.ScenesDir + "Practice.unity", AuthBuilder.ScenesDir + "Profile.unity",
            AuthBuilder.ScenesDir + "Settings.unity", AuthBuilder.ScenesDir + "Results.unity"
        };

        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
        private static readonly Color TextSoft = new Color(0.059f, 0.227f, 0.267f, 0.8f);

        [MenuItem("Lumina/Pantallas/Construir todo")]
        public static void BuildAll()
        {
            EnsureSignCatalog();
            BuildPractice();
            BuildProfile();
            BuildSettings();
            BuildResults();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Pantallas Practice, Profile, Settings y Results construidas.");
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

        // ------------------------------------------------------------------ Datos

        private const string GenericText = "Observa cómo se realiza la seña.";

        private static string NumberEs(string n) => $"Es la seña del número {n}. Fíjate en qué dedos se usan y cómo se colocan.";
        private static string NumberEn(string n) => $"This is the sign for the number {n}. Notice which fingers are used and how they are placed.";
        private static string ColorEs(string c) => $"Es la seña del color {c}. Observa dónde se coloca la mano y cómo se mueve.";
        private static string ColorEn(string c) => $"This is the sign for the color {c}. Notice where the hand is placed and how it moves.";

        private static void EnsureSignCatalog()
        {
            var defaults = ScriptableObject.CreateInstance<SignCatalog>();
            defaults.categories = new[]
            {
                Category("saludos", "Saludos",
                    ("Hola", "Es la seña para saludar. Observa con calma cómo se mueve la mano.", "This is the sign for greeting. Watch carefully how the hand moves."),
                    ("Gracias", "Es la seña para agradecer. Fíjate en la mano y en hacia dónde se mueve.", "This is the sign for saying thank you. Notice the hand and where it moves."),
                    ("Por favor", "Es la seña para pedir algo con cortesía. Observa la forma y el movimiento de la mano.", "This is the sign for asking politely. Watch the shape and movement of the hand."),
                    ("Adiós", "Es la seña para despedirte. Observa cómo se mueve la mano.", "This is the sign for saying goodbye. Watch how the hand moves.")),
                Category("numeros", "Números",
                    ("Uno", NumberEs("1"), NumberEn("1")), ("Dos", NumberEs("2"), NumberEn("2")),
                    ("Tres", NumberEs("3"), NumberEn("3")), ("Cuatro", NumberEs("4"), NumberEn("4"))),
                Category("colores", "Colores",
                    ("Rojo", ColorEs("rojo"), ColorEn("red")), ("Azul", ColorEs("azul"), ColorEn("blue")),
                    ("Verde", ColorEs("verde"), ColorEn("green")), ("Amarillo", ColorEs("amarillo"), ColorEn("yellow"))),
                Category("personas", "Personas",
                    ("Yo", "Es la seña para hablar de ti. Observa dónde se coloca la mano.", "This is the sign for talking about yourself. Notice where the hand is placed."),
                    ("Tú", "Es la seña para referirte a otra persona. Observa dónde se coloca la mano.", "This is the sign for referring to another person. Notice where the hand is placed."),
                    ("Amigo", "Es la seña para hablar de un amigo. Observa las manos y su movimiento.", "This is the sign for talking about a friend. Watch both hands and their movement."),
                    ("Familia", "Es la seña para hablar de tu familia. Observa las manos y su movimiento.", "This is the sign for talking about your family. Watch both hands and their movement.")),
            };

            var catalog = AssetDatabase.LoadAssetAtPath<SignCatalog>(SignCatalogPath);
            if (catalog == null)
            {
                AssetDatabase.CreateAsset(defaults, SignCatalogPath);
                return;
            }

            // Solo se rellenan los textos vacíos o genéricos: lo escrito a mano en el asset no se pisa.
            foreach (var category in catalog.categories)
                foreach (var sign in category.signs)
                    foreach (var d in defaults.categories)
                        foreach (var ds in d.signs)
                        {
                            if (ds.id != sign.id) continue;
                            if (string.IsNullOrWhiteSpace(sign.instruction) || sign.instruction == GenericText) sign.instruction = ds.instruction;
                            if (string.IsNullOrWhiteSpace(sign.instructionEn)) sign.instructionEn = ds.instructionEn;
                        }
            EditorUtility.SetDirty(catalog);
            Object.DestroyImmediate(defaults);
        }

        private static SignCategory Category(string id, string name, params (string name, string es, string en)[] signs)
        {
            var entries = new SignEntry[signs.Length];
            for (int i = 0; i < signs.Length; i++)
                entries[i] = new SignEntry
                {
                    id = id + "." + signs[i].name.ToLowerInvariant().Replace(' ', '_'), displayName = signs[i].name,
                    instruction = signs[i].es, instructionEn = signs[i].en
                };
            return new SignCategory { id = id, displayName = name, signs = entries };
        }

        // ------------------------------------------------------------------ Escenas

        private static Transform BeginScene(string title, string backLabel, out SceneHeaderUI header, out Canvas canvas, bool sena = false)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AuthBuilder.SetupSceneBasics(new Vector3(0f, 0.72f, -5.0f), new Vector3(-5f, 0f, 0f), 36f, AuthBuilder.WorldSkyPath);

            var world = new GameObject("--- WORLD ---").transform;
            var env = AuthBuilder.Child(world, "Environment");
            AuthBuilder.BuildGround(env, 600f);
            AuthBuilder.BuildVillageBackdrop(env, mirror: false);
            AuthBuilder.BuildLanternsAndFlowers(env, new Vector3(-1.3f, 0f, 0f));
            AuthBuilder.BuildFloatingDecor(world, new Vector3(-1.2f, 0f, 2.5f), 11, 31);
            AuthBuilder.BuildMotes(world, new Vector3(-1.2f, 2.2f, 3.5f), new Vector3(13f, 4.5f, 10f));

            canvas = AuthBuilder.MakeCanvas("ScreenCanvas");
            Ui.EnsureEventSystem();
            header = BuildHeader(canvas.transform, title, backLabel);
            return world;
        }

        private static SceneHeaderUI BuildHeader(Transform canvas, string title, string backLabel)
        {
            var root = Ui.Rect("Header", canvas);
            Ui.Stretch(root);
            var face = Ui.ShadowedTitle(root, "Title", title, 96f, Ui.Coral, Ui.TurquoiseSoft, new Vector2(5f, 6f), 36f, 1100f, 120f, 6f);
            var shadow = root.Find("TitleShadow").GetComponent<TMP_Text>();

            var back = Ui.MakeButton(root, "BackButton", backLabel, Ui.Cream, Ui.SandDark, Ui.Ink, new Vector2(260f, 88f), 32f, 8f, 3f);
            Ui.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -44f), new Vector2(260f, 88f));

            var header = root.gameObject.AddComponent<SceneHeaderUI>();
            Ui.Set(header, "titleLabel", face);
            Ui.Set(header, "titleShadow", shadow);
            Ui.Set(header, "backButton", back);
            return header;
        }

        private static void Save(string path) => EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);

        private static GameObject PlaceSena(Transform world)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AuthBuilder.SenaPrefabPath);
            var sena = new GameObject("Sena");
            sena.transform.SetParent(world, false);
            sena.transform.SetPositionAndRotation(new Vector3(-1.3f, 0f, 0f), Quaternion.Euler(0f, 160f, 0f));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sena.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            return sena;
        }

        /// <summary>Tarjeta anclada: pivote y ancla en el mismo punto, para que el tamaño no dependa de la resolución.</summary>
        private static RectTransform SideCard(Transform parent, string name, Vector2 size, out RectTransform outer)
        {
            var inner = Ui.Card(parent, name, size, out outer);
            Ui.Place(outer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, -60f), size);
            return inner;
        }

        // ------------------------------------------------------------------ Practice

        private static void BuildPractice()
        {
            var world = BeginScene("PRÁCTICA", "VOLVER", out var header, out var canvas);
            var sena = PlaceSena(world);
            var catalog = AssetDatabase.LoadAssetAtPath<SignCatalog>(SignCatalogPath); // Tras NewScene: la escena nueva descarga los assets sin usar.

            // --- Selección
            var selection = SideCard(canvas.transform, "SelectionPanel", new Vector2(1000f, 780f), out var selectionOuter);
            var tabs = Ui.Rect("Categories", selection);
            Ui.Place(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-80f, 84f));
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 14f; tabsLayout.childAlignment = TextAnchor.MiddleCenter;
            tabsLayout.childControlWidth = tabsLayout.childControlHeight = false;
            tabsLayout.childForceExpandWidth = tabsLayout.childForceExpandHeight = false;

            var grid = Ui.Rect("Signs", selection);
            Ui.Stretch(grid, 40f, 170f, 40f, 160f);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(280f, 100f); gridLayout.spacing = new Vector2(20f, 24f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gridLayout.constraintCount = 3;
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            var tabTemplate = Ui.MakeButton(tabs, "CategoryTemplate", "", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(200f, 76f), 24f, 6f, 2f);
            var signTemplate = Ui.MakeButton(grid, "SignTemplate", "", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(280f, 100f), 32f, 8f, 2f);
            var empty = Ui.Label(selection, "EmptyLabel", "", 34f, TextSoft);
            Ui.Stretch(empty.rectTransform, 60f, 170f, 60f, 160f);
            var practice = Ui.MakeButton(selection, "PracticeButton", "ELIGE UNA SEÑA", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(620f, 104f), 36f, 10f, 3f);
            Ui.Place((RectTransform)practice.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(620f, 104f));

            // --- Aprendizaje
            var learning = SideCard(canvas.transform, "LearningPanel", new Vector2(1000f, 780f), out var learningOuter);
            var signName = Ui.Label(learning, "SignName", "", 120f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            Ui.TopCenter(signName.rectTransform, 60f, 900f, 150f);
            var instruction = Ui.Label(learning, "Instruction", "", 40f, Ui.Ink);
            Ui.TopCenter(instruction.rectTransform, 240f, 760f, 140f);
            var demoNotice = Ui.Label(learning, "DemoNotice", "La demostración animada llegará pronto.", 28f, Ui.TurquoiseDark);
            Ui.TopCenter(demoNotice.rectTransform, 400f, 760f, 60f);
            var stack = Ui.Rect("Actions", learning);
            Ui.Place(stack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(620f, 260f));
            var stackLayout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            stackLayout.spacing = 26f; stackLayout.childAlignment = TextAnchor.LowerCenter;
            stackLayout.childControlWidth = stackLayout.childControlHeight = false;
            stackLayout.childForceExpandWidth = stackLayout.childForceExpandHeight = false;
            var replay = Ui.MakeButton(stack, "ReplayButton", "VER DE NUEVO", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(620f, 96f), 34f, 8f, 3f);
            var myTurn = Ui.MakeButton(stack, "MyTurnButton", "AHORA ES MI TURNO", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(620f, 118f), 38f, 10f, 3f);

            // --- Preparación / práctica (tarjeta ancha y centrada: la cámara necesita espacio)
            var session = Ui.Card(canvas.transform, "SessionPanel", new Vector2(1440f, 780f), out var sessionOuter);
            Ui.Place(sessionOuter, Mid, Mid, Mid, new Vector2(0f, -60f), new Vector2(1440f, 780f));

            var cameraFrame = Ui.Panel(session, "CameraFrame", Ui.Ink);
            Ui.Place(cameraFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(830f, 470f));
            var view = new GameObject("CameraView", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.transform.SetParent(cameraFrame.transform, false);
            Ui.Stretch(view.rectTransform, 16f, 16f, 16f, 16f);
            view.color = new Color(0f, 0f, 0f, 0.25f); view.raycastTarget = false;
            var cameraStatus = Ui.Label(cameraFrame.transform, "CameraStatus", "", 30f, Ui.Cream);
            Ui.Stretch(cameraStatus.rectTransform, 30f, 30f, 30f, 30f);
            var feed = cameraFrame.gameObject.AddComponent<CameraFeedUI>();
            Ui.Set(feed, "view", view);
            Ui.Set(feed, "status", cameraStatus);

            var column = Ui.Rect("Column", session);
            Ui.Place(column, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(480f, 680f));
            var sessionSign = Ui.Label(column, "SignName", "", 96f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 3f);
            Ui.TopCenter(sessionSign.rectTransform, 0f, 480f, 120f);
            var sessionText = Ui.Label(column, "Instruction", "", 32f, Ui.Ink);
            Ui.TopCenter(sessionText.rectTransform, 130f, 460f, 140f);
            var countdown = Ui.Label(column, "Countdown", "", 170f, Ui.Coral);
            Ui.Place(countdown.rectTransform, Mid, Mid, Mid, new Vector2(0f, -20f), new Vector2(300f, 200f));
            var status = Ui.Label(column, "Status", "", 32f, Ui.TurquoiseDark);
            Ui.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(480f, 90f));
            var start = Ui.MakeButton(column, "StartButton", "COMENZAR", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(440f, 108f), 40f, 10f, 3f);
            var finish = Ui.MakeButton(column, "FinishButton", "TERMINAR", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(440f, 108f), 40f, 10f, 3f);
            foreach (var b in new[] { start, finish })
                Ui.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(440f, 108f));

            var controller = canvas.gameObject.AddComponent<PracticeController>();
            Ui.Set(controller, "catalog", catalog);
            Ui.Set(controller, "header", header);
            Ui.Set(controller, "selectionPanel", selectionOuter.gameObject);
            Ui.Set(controller, "categoryContainer", tabs);
            Ui.Set(controller, "signContainer", grid);
            Ui.Set(controller, "categoryTemplate", tabTemplate);
            Ui.Set(controller, "signTemplate", signTemplate);
            Ui.Set(controller, "emptyLabel", empty);
            Ui.Set(controller, "practiceButton", practice);
            Ui.Set(controller, "demoNotice", demoNotice);
            Ui.Set(controller, "learningPanel", learningOuter.gameObject);
            Ui.Set(controller, "demoStage", sena);
            Ui.Set(controller, "learningSignLabel", signName);
            Ui.Set(controller, "learningInstructionLabel", instruction);
            Ui.Set(controller, "replayButton", replay);
            Ui.Set(controller, "myTurnButton", myTurn);
            Ui.Set(controller, "sessionPanel", sessionOuter.gameObject);
            Ui.Set(controller, "sessionSignLabel", sessionSign);
            Ui.Set(controller, "sessionInstructionLabel", sessionText);
            Ui.Set(controller, "countdownLabel", countdown);
            Ui.Set(controller, "statusLabel", status);
            Ui.Set(controller, "cameraFeed", feed);
            Ui.Set(controller, "cameraView", view);
            Ui.Set(controller, "startButton", start);
            Ui.Set(controller, "finishButton", finish);

            Save(ScenePaths[0]);
        }

        // ------------------------------------------------------------------ Profile (MI PROGRESO)

        private static void BuildProfile()
        {
            var world = BeginScene("MI PROGRESO", "VOLVER", out var header, out var canvas);
            var anchor = AuthBuilder.Child(world, "AvatarAnchor");
            anchor.SetPositionAndRotation(new Vector3(-1.3f, 0f, 0f), Quaternion.Euler(0f, 165f, 0f));

            var namePill = Ui.Panel(canvas.transform, "NamePill", Ui.Coral);
            Ui.Place(namePill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(530f, 34f), new Vector2(560f, 62f));
            var nameLabel = Ui.Label(namePill.transform, "Label", "", 28f, Ui.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 5f);
            Ui.Stretch(nameLabel.rectTransform);

            var card = Ui.Card(canvas.transform, "ProgressPanel", new Vector2(1000f, 860f), out var outer);
            Ui.Place(outer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, -70f), new Vector2(1000f, 860f));

            var level = Ui.Label(card, "Level", "", 90f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            Ui.TopCenter(level.rectTransform, 24f, 900f, 104f);
            var levelTitle = Ui.Label(card, "LevelTitle", "", 36f, Ui.TurquoiseDark, FontStyles.Bold, TextAlignmentOptions.Center, 5f);
            Ui.TopCenter(levelTitle.rectTransform, 130f, 900f, 50f);

            var bar = BuildBar(card, 196f, 820f, 48f);

            var statsRow = Ui.Rect("Stats", card);
            Ui.Place(statsRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(880f, 170f));
            var statsLayout = statsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            statsLayout.spacing = 20f; statsLayout.childAlignment = TextAnchor.MiddleCenter;
            statsLayout.childControlWidth = statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = statsLayout.childForceExpandHeight = true;
            var signsCard = BuildStatCard(statsRow, "SignsCard");
            var precisionCard = BuildStatCard(statsRow, "PrecisionCard");
            var starsCard = BuildStatCard(statsRow, "StarsCard");

            var hint = Ui.Label(card, "EmptyHint", "Aún no hay registros. ¡Practica tu primera seña!", 24f, TextSoft, FontStyles.Normal);
            Ui.TopCenter(hint.rectTransform, 442f, 840f, 30f);
            var heading = Ui.Label(card, "LevelsHeading", "NIVELES DE APRENDIZAJE", 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, 4f);
            Ui.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -482f), new Vector2(840f, 40f));

            var list = Ui.Rect("Levels", card);
            Ui.Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(840f, 300f));
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10f; listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlWidth = true; listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true; listLayout.childForceExpandHeight = false;
            var row = BuildLevelRow(list);

            var controller = canvas.gameObject.AddComponent<ProfileController>();
            Ui.Set(controller, "header", header);
            Ui.Set(controller, "avatarCatalog", AssetDatabase.LoadAssetAtPath<AvatarCatalog>(AvatarCatalogPath));
            Ui.Set(controller, "avatarAnchor", anchor);
            Ui.Set(controller, "playerNameLabel", nameLabel);
            Ui.Set(controller, "levelLabel", level);
            Ui.Set(controller, "levelTitleLabel", levelTitle);
            Ui.Set(controller, "xpBar", bar);
            Ui.Set(controller, "signsCard", signsCard);
            Ui.Set(controller, "precisionCard", precisionCard);
            Ui.Set(controller, "starsCard", starsCard);
            Ui.Set(controller, "emptyHint", hint);
            Ui.Set(controller, "levelsContainer", list);
            Ui.Set(controller, "levelRowTemplate", row);

            Save(ScenePaths[1]);
        }

        private static ProgressBarUI BuildBar(Transform parent, float yFromTop, float width, float height)
        {
            var track = Ui.Panel(parent, "XpBar", Ui.Sand);
            Ui.TopCenter(track.rectTransform, yFromTop, width, height);
            var outline = Ui.Panel(track.transform, "Outline", Ui.SandDark);
            Ui.Stretch(outline.rectTransform);
            var inner = Ui.Panel(track.transform, "Track", Ui.Cream);
            Ui.Stretch(inner.rectTransform, 4f, 4f, 4f, 4f);
            var fill = Ui.Panel(inner.transform, "Fill", Ui.Turquoise);
            Ui.Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            var label = Ui.Label(track.transform, "Label", "", 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.Stretch(label.rectTransform);

            var bar = track.gameObject.AddComponent<ProgressBarUI>();
            Ui.Set(bar, "fill", fill.rectTransform);
            Ui.Set(bar, "label", label);
            return bar;
        }

        private static StatCardUI BuildStatCard(Transform parent, string name)
        {
            var frame = Ui.Panel(parent, name, Ui.TurquoiseSoft);
            var inner = Ui.Panel(frame.transform, "Inner", Ui.CreamWarm);
            Ui.Stretch(inner.rectTransform, 5f, 5f, 5f, 5f);
            var value = Ui.Label(inner.transform, "Value", "", 76f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.Place(value.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-20f, 0f));
            value.rectTransform.offsetMin = new Vector2(10f, 0f); value.rectTransform.offsetMax = new Vector2(-10f, -8f);
            var caption = Ui.Label(inner.transform, "Caption", "", 20f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.38f), Mid, Vector2.zero, Vector2.zero);
            caption.rectTransform.offsetMin = new Vector2(8f, 6f); caption.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var card = frame.gameObject.AddComponent<StatCardUI>();
            Ui.Set(card, "valueLabel", value);
            Ui.Set(card, "captionLabel", caption);
            return card;
        }

        private static RectTransform BuildLevelRow(Transform parent)
        {
            var row = Ui.Panel(parent, "LevelRowTemplate", Ui.CreamWarm);
            Ui.FixHeight(row, 64f);
            row.gameObject.AddComponent<CanvasGroup>();

            var badge = Ui.Panel(row.transform, "Badge", Ui.Sand, Ui.Circle);
            Ui.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(46f, 46f));
            var badgeLabel = Ui.Label(badge.transform, "Label", "", 22f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            Ui.Stretch(badgeLabel.rectTransform);

            var title = Ui.Label(row.transform, "Title", "", 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, 2f);
            Ui.Stretch(title.rectTransform, 80f, 0f, 270f, 0f);

            var pill = Ui.Panel(row.transform, "StatusPill", Ui.Sand);
            Ui.Place(pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(240f, 42f));
            var status = Ui.Label(pill.transform, "Status", "", 20f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.Stretch(status.rectTransform);
            return row.rectTransform;
        }

        // ------------------------------------------------------------------ Settings

        private static void BuildSettings()
        {
            BeginScene("CONFIGURACIÓN", "VOLVER", out var header, out var canvas);

            var card = Ui.Card(canvas.transform, "SettingsPanel", new Vector2(1560f, 800f), out var outer);
            Ui.Place(outer, Mid, Mid, Mid, new Vector2(0f, -60f), new Vector2(1560f, 800f));

            // Columna izquierda: audio, gráficos y preferencias (idioma y apariencia).
            var left = Column(card, "LeftColumn", true);
            Section(left, "AUDIO");
            var music = SliderRow(left, "MÚSICA", out var musicValue);
            var sfx = SliderRow(left, "EFECTOS DE SONIDO", out var sfxValue);
            Section(left, "GRÁFICOS");
            var quality = SelectorRow(left, "Quality");
            Section(left, "PREFERENCIAS");
            var language = SelectorRow(left, "Language", "IDIOMA");
            var theme = SelectorRow(left, "Theme", "APARIENCIA");

            // Columna derecha: cámara, acciones y accesibilidad.
            var right = Column(card, "RightColumn", false);
            Section(right, "CÁMARA");
            var cameraSel = SelectorRow(right, "Camera");

            var previewRow = Ui.LayoutRow(right, "Preview", 252f);
            var frame = Ui.Panel(previewRow, "CameraFrame", Ui.Ink);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(448f, 252f));
            var view = new GameObject("CameraView", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.transform.SetParent(frame.transform, false);
            Ui.Stretch(view.rectTransform, 14f, 14f, 14f, 14f);
            view.color = new Color(0f, 0f, 0f, 0.25f); view.raycastTarget = false;
            var viewStatus = Ui.Label(frame.transform, "Status", "", 24f, Ui.Cream);
            Ui.Stretch(viewStatus.rectTransform, 20f, 20f, 20f, 20f);
            var feed = frame.gameObject.AddComponent<CameraFeedUI>();
            Ui.Set(feed, "view", view);
            Ui.Set(feed, "status", viewStatus);

            var actions = Ui.LayoutRow(right, "Actions", 88f);
            var test = Ui.MakeButton(actions, "TestCameraButton", "PROBAR", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(270f, 80f), 32f, 8f, 3f);
            Ui.Place((RectTransform)test.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(270f, 80f));
            var restore = Ui.MakeButton(actions, "RestoreButton", "RESTAURAR", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(270f, 80f), 30f, 8f, 3f);
            Ui.Place((RectTransform)restore.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(270f, 80f));

            Section(right, "ACCESIBILIDAD");
            // TODO(accesibilidad): añadir aquí las opciones (tamaño de texto, contraste, etc.) cuando se definan.
            var note = Ui.Label(right, "AccessibilityNote", "Pronto habrá más opciones para adaptar el juego a ti.", 24f, TextSoft, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Ui.FixHeight(note, 40f);

            var controller = canvas.gameObject.AddComponent<SettingsController>();
            Ui.Set(controller, "header", header);
            Ui.Set(controller, "musicSlider", music);
            Ui.Set(controller, "sfxSlider", sfx);
            Ui.Set(controller, "musicValue", musicValue);
            Ui.Set(controller, "sfxValue", sfxValue);
            Ui.Set(controller, "qualitySelector", quality);
            Ui.Set(controller, "cameraSelector", cameraSel);
            Ui.Set(controller, "testCameraButton", test);
            Ui.Set(controller, "testCameraLabel", test.GetComponentInChildren<TMP_Text>());
            Ui.Set(controller, "cameraFeed", feed);
            Ui.Set(controller, "languageSelector", language);
            Ui.Set(controller, "themeSelector", theme);
            Ui.Set(controller, "restoreButton", restore);

            Save(ScenePaths[2]);
        }

        private static RectTransform Column(Transform card, string name, bool left)
        {
            var column = Ui.Rect(name, card);
            float anchor = left ? 0f : 1f;
            Ui.Place(column, new Vector2(anchor, 0f), new Vector2(anchor, 1f), new Vector2(anchor, 0.5f),
                new Vector2(left ? 50f : -50f, 0f), new Vector2(660f, -60f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true; layout.childControlHeight = true; // Las alturas fijas salen de LayoutElement (FixHeight).
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return column;
        }

        private static void Section(Transform parent, string title)
        {
            var label = Ui.Label(parent, "Section_" + title, title, 32f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.BottomLeft, 5f);
            Ui.FixHeight(label, 56f);
            var rule = Ui.Panel(parent, "Rule", Ui.TurquoiseSoft);
            Ui.FixHeight(rule, 4f);
        }

        private static Slider SliderRow(Transform parent, string title, out TMP_Text value)
        {
            var row = Ui.LayoutRow(parent, "Row_" + title, 64f);
            var label = Ui.Label(row, "Label", title, 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Ui.Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, 0f));

            var rt = Ui.Rect("Slider", row);
            Ui.Place(rt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 44f));
            rt.offsetMin = new Vector2(260f, -22f); rt.offsetMax = new Vector2(-90f, 22f);

            // Zona de clic transparente a toda la altura: sin ella solo se podía arrastrar el tirador, no pulsar la barra.
            var hit = Ui.Panel(rt, "Hit", new Color(1f, 1f, 1f, 0f));
            Ui.Stretch(hit.rectTransform);
            hit.raycastTarget = true;
            var bg = Ui.Panel(rt, "Background", Ui.Sand);
            Ui.Place(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Mid, Vector2.zero, new Vector2(0f, 16f));
            var fillArea = Ui.Rect("Fill Area", rt);
            Ui.Place(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Mid, Vector2.zero, new Vector2(0f, 16f));
            var fill = Ui.Panel(fillArea, "Fill", Ui.Turquoise);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Ui.Rect("Handle Slide Area", rt);
            Ui.Stretch(handleArea, 22f, 0f, 22f, 0f);
            var handle = Ui.Panel(handleArea, "Handle", Ui.Coral, Ui.Circle);
            handle.raycastTarget = true;
            Ui.Place(handle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Mid, Vector2.zero, new Vector2(44f, 44f));
            handle.preserveAspect = true;

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.8f;
            slider.transition = Selectable.Transition.ColorTint;
            slider.colors = new ColorBlock
            {
                normalColor = Color.white, highlightedColor = new Color(1f, 0.88f, 0.82f), pressedColor = new Color(0.8f, 0.7f, 0.66f),
                selectedColor = new Color(0.62f, 0.9f, 0.88f), disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f),
                colorMultiplier = 1f, fadeDuration = 0.08f
            };
            // Anillo visible al enfocar con teclado: la forma cambia, no solo el color.
            var ring = Ui.Panel(handle.transform, "Ring", new Color(0.059f, 0.227f, 0.267f, 0.9f), Ui.Circle);
            Ui.Place(ring.rectTransform, Mid, Mid, Mid, Vector2.zero, new Vector2(14f, 14f));

            value = Ui.Label(row, "Value", "", 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Ui.Place(value.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(80f, 0f));
            return slider;
        }

        /// <summary>Selector «‹ valor ›». Con <paramref name="inlineTitle"/> lleva la etiqueta a su izquierda; sin ella ocupa todo el ancho.</summary>
        private static OptionSelectorUI SelectorRow(Transform parent, string name, string inlineTitle = null)
        {
            if (inlineTitle == null)
                return BuildSelector(Ui.LayoutRow(parent, "Selector_" + name, 72f), 72f);

            var row = Ui.LayoutRow(parent, "Row_" + name, 64f);
            var label = Ui.Label(row, "Label", inlineTitle, 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Ui.Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(230f, 0f));
            var holder = Ui.Rect("Selector", row);
            Ui.Place(holder, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(400f, 0f));
            return BuildSelector(holder, 64f);
        }

        private static OptionSelectorUI BuildSelector(RectTransform row, float size)
        {
            var prev = Ui.MakeButton(row, "Previous", "<", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(size, size), 34f, 6f, 0f);
            Ui.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size, size));
            var next = Ui.MakeButton(row, "Next", ">", Ui.CreamWarm, Ui.SandDark, Ui.Ink, new Vector2(size, size), 34f, 6f, 0f);
            Ui.Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(size, size));
            var box = Ui.Panel(row, "ValueBox", Ui.Sand);
            Ui.Stretch(box.rectTransform, size + 14f, 6f, size + 14f, 6f);
            var value = Ui.Label(box.transform, "Value", "", 28f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 1f);
            Ui.Stretch(value.rectTransform, 12f, 0f, 12f, 0f);
            value.overflowMode = TextOverflowModes.Ellipsis;

            var selector = row.gameObject.AddComponent<OptionSelectorUI>();
            Ui.Set(selector, "previousButton", prev);
            Ui.Set(selector, "nextButton", next);
            Ui.Set(selector, "valueLabel", value);
            return selector;
        }

        // ------------------------------------------------------------------ Results

        private static void BuildResults()
        {
            var world = BeginScene("RESULTADOS", "MENÚ", out var header, out var canvas);
            PlaceSena(world);

            var card = SideCard(canvas.transform, "ResultsPanel", new Vector2(1000f, 780f), out _);
            var sign = Ui.Label(card, "SignName", "", 120f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            Ui.TopCenter(sign.rectTransform, 60f, 900f, 150f);
            var row = Ui.Rect("Stats", card);
            Ui.TopCenter(row, 240f, 880f, 190f);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 20f; rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            var stat = BuildStatCard(row, "PrecisionCard");
            var stars = BuildStatCard(row, "StarsCard");
            var xp = BuildStatCard(row, "XpCard");
            foreach (var c in new[] { stat, stars, xp }) ((RectTransform)c.transform).sizeDelta = new Vector2(280f, 190f);
            var message = Ui.Label(card, "Message", "", 36f, Ui.Ink);
            Ui.TopCenter(message.rectTransform, 470f, 760f, 110f);
            var again = Ui.MakeButton(card, "PracticeAgainButton", "SEGUIR PRACTICANDO", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(620f, 118f), 38f, 10f, 3f);
            Ui.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(620f, 118f));

            var controller = canvas.gameObject.AddComponent<ResultsController>();
            Ui.Set(controller, "header", header);
            Ui.Set(controller, "signLabel", sign);
            Ui.Set(controller, "precisionCard", stat);
            Ui.Set(controller, "starsCard", stars);
            Ui.Set(controller, "xpCard", xp);
            Ui.Set(controller, "messageLabel", message);
            Ui.Set(controller, "practiceAgainButton", again);

            Save(ScenePaths[3]);
        }
    }
}
