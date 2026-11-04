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
        // Tinta con algo de transparencia: se distingue de los títulos sin perder contraste sobre el crema (con 0.8 quedaba flojo).
        private static readonly Color TextSoft = new Color(0.059f, 0.227f, 0.267f, 0.92f);

        [MenuItem("Lumina/Pantallas/Construir todo")]
        public static void BuildAll()
        {
            EnsureSignCatalog();
            BuildPracticeWithShortcuts();
            BuildProfile();
            BuildSettings();
            BuildResults();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Pantallas Practice, Profile, Settings y Results construidas.");
        }

        /// <summary>Reconstruye solo Practice y vuelve a añadirle la fila de accesos a las casas. No toca las casas ni LuminaWorld.</summary>
        [MenuItem("Lumina/Pantallas/Construir solo Practice (con accesos a las casas)")]
        public static void BuildPracticeOnly()
        {
            EnsureSignCatalog();
            BuildPracticeWithShortcuts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Practice reconstruida con sus accesos a las casas.");
        }

        /// <summary>Reconstruye Profile, Settings y Results sin tocar Practice, las casas ni LuminaWorld.</summary>
        [MenuItem("Lumina/Pantallas/Construir solo Profile, Settings y Results")]
        public static void BuildProfileSettingsResults()
        {
            EnsureSignCatalog();
            BuildProfile();
            BuildSettings();
            BuildResults();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Profile, Settings y Results reconstruidas.");
        }

        /// <summary>Reconstruye solo Settings (CONFIGURACIÓN) sin tocar Profile, Results, Practice ni las casas.</summary>
        [MenuItem("Lumina/Pantallas/Construir solo Settings")]
        public static void BuildSettingsOnly()
        {
            BuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Settings reconstruida.");
        }

        // BuildPractice parte de una escena nueva, así que borra la fila «CASAS DE APRENDIZAJE» que añade el builder de Niveles.
        // AddShortcutsToPractice es idempotente (sustituye la fila anterior), por eso se vuelve a aplicar aquí.
        private static void BuildPracticeWithShortcuts()
        {
            BuildPractice();
            LuminaLevelsBuilder.AddShortcutsToPractice();
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

        private const string AlphabetPath = DataDir + "LscAlphabet.asset";
        private const string NumberTemplatesPath = DataDir + "NumberLandmarkTemplates.asset";

        /// <summary>
        /// Catálogo de Practice: los cuatro niveles con el contenido ya establecido en cada casa de aprendizaje (abecedario del
        /// asset LscAlphabet, y los números, meses y saludos de <see cref="LearningLevels"/>). Se reescribe entero cada vez: el
        /// asset no guarda contenido propio, solo refleja esas fuentes.
        /// </summary>
        private static void EnsureSignCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SignCatalog>(SignCatalogPath);
            bool isNew = catalog == null;
            if (isNew) catalog = ScriptableObject.CreateInstance<SignCatalog>();
            catalog.categories = new[]
            {
                AlphabetCategory(),
                LevelCategory("numeros", "Números", LearningLevels.Numbers),
                LevelCategory("meses", "Meses", LearningLevels.Months),
                LevelCategory("saludos", "Saludos y frases", LearningLevels.Greetings),
            };
            if (isNew) AssetDatabase.CreateAsset(catalog, SignCatalogPath);
            else EditorUtility.SetDirty(catalog);
        }

        private static SignCategory AlphabetCategory()
        {
            var alphabet = AssetDatabase.LoadAssetAtPath<Lumina.Alphabet.LscAlphabet>(AlphabetPath);
            if (alphabet == null || alphabet.letters == null)
                Debug.LogWarning("[Lumina] No se encontró " + AlphabetPath + ": la categoría Abecedario de Practice queda vacía.");
            var letters = alphabet != null && alphabet.letters != null ? alphabet.letters : new Lumina.Alphabet.LetterEntry[0];

            var entries = new SignEntry[letters.Length];
            for (int i = 0; i < letters.Length; i++)
            {
                string code = Lumina.Api.SignCodes.ForLetter(letters[i].letter);
                entries[i] = new SignEntry
                {
                    id = code, displayName = code, displayNameEn = code,
                    instruction = string.IsNullOrWhiteSpace(letters[i].instruction)
                        ? $"Es la seña de la letra {code}. Observa la forma y la posición de la mano."
                        : letters[i].instruction,
                    instructionEn = $"This is the sign for the letter {code}. Notice the shape and position of the hand."
                };
            }
            return new SignCategory { id = "alfabeto", displayName = "Abecedario", signs = entries };
        }

        private static SignCategory LevelCategory(string id, string name, LevelDefinition level)
        {
            var entries = new SignEntry[level.items.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = level.ToSignEntry(level.items[i]);
                entries[i].displayName = level.items[i].es; // fijo en español; GetName() elige el inglés en ejecución
            }
            return new SignCategory { id = id, displayName = name, signs = entries };
        }

        // ------------------------------------------------------------------ Escenas

        internal static Transform BeginScene(string title, string backLabel, out SceneHeaderUI header, out Canvas canvas, bool sena = false)
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

        internal static void Save(string path) => EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);

        internal static GameObject PlaceSena(Transform world)
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
        internal static RectTransform SideCard(Transform parent, string name, Vector2 size, out RectTransform outer)
        {
            var inner = Ui.Card(parent, name, size, out outer);
            Ui.Place(outer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, -60f), size);
            return inner;
        }

        // ------------------------------------------------------------------ Practice

        // Zona de las tarjetas a la derecha (la seña/Sena queda a la izquierda). Todo en proporciones de pantalla, no en píxeles.
        private static readonly Vector2 SideCardMin = new Vector2(0.43f, 0.07f), SideCardMax = new Vector2(0.955f, 0.80f);
        private static readonly Vector2 WideCardMin = new Vector2(0.03f, 0.07f), WideCardMax = new Vector2(0.97f, 0.80f);

        /// <summary>Tarjeta que se estira entre dos anclas: se adapta a cualquier relación de aspecto. Devuelve el interior.</summary>
        internal static RectTransform ResponsiveCard(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, out RectTransform outer)
        {
            var inner = Ui.Card(parent, name, Vector2.zero, out outer);
            outer.anchorMin = anchorMin; outer.anchorMax = anchorMax; outer.pivot = Mid;
            outer.offsetMin = outer.offsetMax = Vector2.zero;
            return inner;
        }

        internal static LayoutElement Elem(Component target, float minW = -1f, float minH = -1f, float prefW = -1f, float prefH = -1f, float flexW = -1f, float flexH = -1f)
        {
            var e = target.GetComponent<LayoutElement>();
            if (e == null) e = target.gameObject.AddComponent<LayoutElement>();
            e.minWidth = minW; e.minHeight = minH; e.preferredWidth = prefW; e.preferredHeight = prefH; e.flexibleWidth = flexW; e.flexibleHeight = flexH;
            return e;
        }

        internal static VerticalLayoutGroup VStack(Component target, float spacing, RectOffset padding, TextAnchor align = TextAnchor.UpperCenter)
        {
            var g = target.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = padding; g.childAlignment = align;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = true; g.childForceExpandHeight = false;
            return g;
        }

        internal static HorizontalLayoutGroup HStack(Component target, float spacing, RectOffset padding, TextAnchor align, bool expandWidth, bool expandHeight)
        {
            var g = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.padding = padding; g.childAlignment = align;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = expandWidth; g.childForceExpandHeight = expandHeight;
            return g;
        }

        /// <summary>El texto se reduce hasta <paramref name="min"/> para caber en su caja (con o sin saltos de línea) en lugar de desbordarla.</summary>
        internal static void FitText(TMP_Text text, float min, bool wrap)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(min, text.fontSize);
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>Botón con etiqueta autoajustable y alto fijo; el ancho lo reparte el layout padre.</summary>
        internal static Button FlexButton(Transform parent, string name, string text, Color face, Color shadow, Color textColor,
            float height, float fontSize, float minLabel, float minWidth = 110f, float prefWidth = -1f, float flexWidth = 1f)
        {
            var button = Ui.MakeButton(parent, name, text, face, shadow, textColor, new Vector2(prefWidth > 0f ? prefWidth : 200f, height), fontSize, 8f, 2f);
            FitText(button.GetComponentInChildren<TMP_Text>(true), minLabel, true);
            Elem(button, minWidth, height, prefWidth, height, flexWidth, -1f);
            return button;
        }

        /// <summary>Zona con scroll vertical (barra delgada a la derecha). <paramref name="content"/> es donde se colocan los elementos.</summary>
        private static RectTransform BuildScroll(Transform parent, string name, out RectTransform content)
        {
            var root = Ui.Rect(name, parent);
            var catcher = root.gameObject.AddComponent<Image>(); // recibe la rueda del ratón también entre botones
            catcher.color = new Color(1f, 1f, 1f, 0f);

            var viewport = Ui.Rect("Viewport", root);
            Ui.Stretch(viewport, 0f, 0f, 24f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Ui.Rect("Signs", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var track = Ui.Panel(root, "Scrollbar", Ui.Sand);
            track.raycastTarget = true;
            Ui.Place(track.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(14f, 0f));
            var handle = Ui.Panel(track.transform, "Handle", Ui.TurquoiseDark);
            handle.raycastTarget = true;
            Ui.Stretch(handle.rectTransform);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return root;
        }

        private static void BuildPractice()
        {
            var world = BeginScene("PRÁCTICA", "VOLVER", out var header, out var canvas);
            var sena = PlaceSena(world);
            var catalog = AssetDatabase.LoadAssetAtPath<SignCatalog>(SignCatalogPath); // Tras NewScene: la escena nueva descarga los assets sin usar.
            var numberTemplates = AssetDatabase.LoadAssetAtPath<Senas.AI.MediaPipe.AlphabetLandmarkTemplateSet>(NumberTemplatesPath);
            var pad = new RectOffset(36, 36, 32, 32);

            // --- Selección: pestañas de los 4 niveles, cuadrícula de señas con scroll y botón de practicar
            var selection = ResponsiveCard(canvas.transform, "SelectionPanel", SideCardMin, SideCardMax, out var selectionOuter);
            VStack(selection, 22f, pad);

            var tabs = Ui.Rect("Categories", selection);
            Elem(tabs, prefH: 84f, minH: 76f);
            HStack(tabs, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter, true, true);
            var tabTemplate = FlexButton(tabs, "CategoryTemplate", "", Ui.CreamWarm, Ui.SandDark, Ui.Ink, 84f, 24f, 14f, 110f, 200f);

            var scrollRoot = BuildScroll(selection, "SignsScroll", out var grid);
            Elem(scrollRoot, minH: 240f, prefH: 420f, flexH: 1f);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(210f, 92f); gridLayout.spacing = new Vector2(16f, 16f);
            gridLayout.padding = new RectOffset(4, 4, 4, 8);
            gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var signTemplate = Ui.MakeButton(grid, "SignTemplate", "", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(210f, 92f), 30f, 8f, 2f);
            FitText(signTemplate.GetComponentInChildren<TMP_Text>(true), 16f, true);

            var empty = Ui.Label(scrollRoot, "EmptyLabel", "", 34f, TextSoft);
            Ui.Stretch(empty.rectTransform, 24f, 24f, 24f, 24f);
            FitText(empty, 18f, true);

            var practice = Ui.MakeButton(selection, "PracticeButton", "ELIGE UNA SEÑA", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(620f, 104f), 36f, 10f, 3f);
            FitText(practice.GetComponentInChildren<TMP_Text>(true), 20f, false);
            Elem(practice, minH: 88f, prefH: 104f);

            // --- Aprendizaje
            var learning = ResponsiveCard(canvas.transform, "LearningPanel", SideCardMin, SideCardMax, out var learningOuter);
            VStack(learning, 20f, new RectOffset(48, 48, 40, 40));
            var signName = Ui.Label(learning, "SignName", "", 120f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            FitText(signName, 44f, false);
            Elem(signName, minH: 90f, prefH: 150f);
            var instruction = Ui.Label(learning, "Instruction", "", 40f, Ui.Ink);
            FitText(instruction, 22f, true);
            Elem(instruction, minH: 110f, prefH: 150f, flexH: 1f);
            var demoNotice = Ui.Label(learning, "DemoNotice", "La demostración animada llegará pronto.", 28f, Ui.TurquoiseDark);
            FitText(demoNotice, 16f, true);
            Elem(demoNotice, minH: 40f, prefH: 56f);
            var actions = Ui.Rect("Actions", learning);
            VStack(actions, 22f, new RectOffset(40, 40, 0, 0), TextAnchor.LowerCenter);
            var replay = Ui.MakeButton(actions, "ReplayButton", "VER DE NUEVO", Ui.Turquoise, Ui.TurquoiseDark, Ui.Ink, new Vector2(620f, 96f), 34f, 8f, 3f);
            var myTurn = Ui.MakeButton(actions, "MyTurnButton", "AHORA ES MI TURNO", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(620f, 118f), 38f, 10f, 3f);
            FitText(replay.GetComponentInChildren<TMP_Text>(true), 20f, false);
            FitText(myTurn.GetComponentInChildren<TMP_Text>(true), 20f, false);
            Elem(replay, minH: 80f, prefH: 96f);
            Elem(myTurn, minH: 92f, prefH: 118f);

            // --- Preparación / práctica: tarjeta ancha; la cámara ocupa el espacio sobrante y el panel de texto tiene un ancho mínimo
            var session = ResponsiveCard(canvas.transform, "SessionPanel", WideCardMin, WideCardMax, out var sessionOuter);
            HStack(session, 40f, new RectOffset(40, 40, 40, 40), TextAnchor.MiddleCenter, false, true);

            var cameraFrame = Ui.Panel(session, "CameraFrame", Ui.Ink);
            Elem(cameraFrame, minW: 360f, prefW: 760f, flexW: 1.7f);
            var aspect = Ui.Rect("Aspect", cameraFrame.transform);
            Ui.Stretch(aspect);
            var fitter = aspect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 16f / 9f;
            var view = new GameObject("CameraView", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.transform.SetParent(aspect, false);
            Ui.Stretch(view.rectTransform, 12f, 12f, 12f, 12f);
            view.color = new Color(0f, 0f, 0f, 0.25f); view.raycastTarget = false;
            var cameraStatus = Ui.Label(cameraFrame.transform, "CameraStatus", "", 30f, Ui.Cream);
            Ui.Stretch(cameraStatus.rectTransform, 28f, 28f, 28f, 28f);
            FitText(cameraStatus, 16f, true);
            var feed = cameraFrame.gameObject.AddComponent<CameraFeedUI>();
            Ui.Set(feed, "view", view);
            Ui.Set(feed, "status", cameraStatus);

            var column = Ui.Rect("Column", session);
            Elem(column, minW: 320f, prefW: 460f, flexW: 1f);
            VStack(column, 16f, new RectOffset(0, 0, 0, 0));
            var sessionSign = Ui.Label(column, "SignName", "", 96f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 3f);
            FitText(sessionSign, 36f, false);
            Elem(sessionSign, minH: 70f, prefH: 110f);
            var sessionText = Ui.Label(column, "Instruction", "", 32f, Ui.Ink);
            FitText(sessionText, 18f, true);
            Elem(sessionText, minH: 100f, prefH: 150f);
            var countdown = Ui.Label(column, "Countdown", "", 170f, Ui.Coral);
            FitText(countdown, 60f, false);
            Elem(countdown, minH: 110f, prefH: 190f, flexH: 1f);
            var status = Ui.Label(column, "Status", "", 32f, Ui.TurquoiseDark);
            FitText(status, 16f, true);
            Elem(status, minH: 70f, prefH: 120f);
            var start = Ui.MakeButton(column, "StartButton", "COMENZAR", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(440f, 108f), 40f, 10f, 3f);
            var finish = Ui.MakeButton(column, "FinishButton", "TERMINAR", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(440f, 108f), 40f, 10f, 3f);
            foreach (var b in new[] { start, finish })
            {
                FitText(b.GetComponentInChildren<TMP_Text>(true), 20f, false);
                Elem(b, minH: 88f, prefH: 108f);
            }

            var controller = canvas.gameObject.AddComponent<PracticeController>();
            Ui.Set(controller, "catalog", catalog);
            Ui.Set(controller, "numberTemplates", numberTemplates);
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

        internal static ProgressBarUI BuildBar(Transform parent, float yFromTop, float width, float height)
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

        internal static StatCardUI BuildStatCard(Transform parent, string name)
        {
            var frame = Ui.Panel(parent, name, Ui.TurquoiseSoft);
            var inner = Ui.Panel(frame.transform, "Inner", Ui.CreamWarm);
            Ui.Stretch(inner.rectTransform, 5f, 5f, 5f, 5f);
            var value = Ui.Label(inner.transform, "Value", "", 76f, Ui.Coral, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.Place(value.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-20f, 0f));
            value.rectTransform.offsetMin = new Vector2(10f, 0f); value.rectTransform.offsetMax = new Vector2(-10f, -8f);
            var caption = Ui.Label(inner.transform, "Caption", "", 22f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.FitLabel(caption, 18f);
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
            var status = Ui.Label(pill.transform, "Status", "", 22f, Ui.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            Ui.FitLabel(status, 18f);
            Ui.Stretch(status.rectTransform);
            return row.rectTransform;
        }

        // ------------------------------------------------------------------ Settings

        private static void BuildSettings()
        {
            BeginScene("CONFIGURACIÓN", "VOLVER", out var header, out var canvas);

            var card = Ui.Card(canvas.transform, "SettingsPanel", new Vector2(1560f, 800f), out var outer);
            Ui.Place(outer, Mid, Mid, Mid, new Vector2(0f, -60f), new Vector2(1560f, 800f));

            // Columna izquierda: audio, preferencias (idioma y apariencia) y personalización (cambiar de avatar).
            var left = Column(card, "LeftColumn", true);
            Section(left, "AUDIO");
            var music = SliderRow(left, "MÚSICA", out var musicValue);
            var sfx = SliderRow(left, "EFECTOS DE SONIDO", out var sfxValue);
            Section(left, "PREFERENCIAS");
            var language = SelectorRow(left, "Language", "IDIOMA");
            var theme = SelectorRow(left, "Theme", "APARIENCIA");
            Section(left, "PERSONALIZACIÓN");
            var avatarRow = Ui.LayoutRow(left, "AvatarRow", 88f);
            var changeAvatar = Ui.MakeButton(avatarRow, "ChangeAvatarButton", "CAMBIAR AVATAR", Ui.Coral, Ui.CoralDark, Ui.Cream, new Vector2(560f, 80f), 32f, 8f, 3f);
            Ui.Place((RectTransform)changeAvatar.transform, Mid, Mid, Mid, Vector2.zero, new Vector2(560f, 80f)); // centrado en la columna

            // Columna derecha: cámara y acciones.
            var right = Column(card, "RightColumn", false);
            Section(right, "CÁMARA");
            var cameraSel = SelectorRow(right, "Camera");

            var previewRow = Ui.LayoutRow(right, "Preview", 315f);
            var frame = Ui.Panel(previewRow, "CameraFrame", Ui.Ink);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(560f, 315f));
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


            var controller = canvas.gameObject.AddComponent<SettingsController>();
            Ui.Set(controller, "header", header);
            Ui.Set(controller, "musicSlider", music);
            Ui.Set(controller, "sfxSlider", sfx);
            Ui.Set(controller, "musicValue", musicValue);
            Ui.Set(controller, "sfxValue", sfxValue);
            Ui.Set(controller, "cameraSelector", cameraSel);
            Ui.Set(controller, "testCameraButton", test);
            Ui.Set(controller, "testCameraLabel", test.GetComponentInChildren<TMP_Text>());
            Ui.Set(controller, "cameraFeed", feed);
            Ui.Set(controller, "languageSelector", language);
            Ui.Set(controller, "themeSelector", theme);
            Ui.Set(controller, "restoreButton", restore);
            Ui.Set(controller, "changeAvatarButton", changeAvatar);

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
