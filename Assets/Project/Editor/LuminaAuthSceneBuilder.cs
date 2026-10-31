using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lumina.UI;
using Sunbox.Avatars;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Construye (de forma repetible) LoginScene, RegisterScene y la ventana de cierre de sesión del menú principal,
    /// reutilizando el cielo, los prefabs de aldea, el avatar y la interfaz de Lúmina. No hay autenticación real:
    /// todo funciona contra AuthenticationMockService.
    /// Menú: Lumina > Autenticación > Construir todo.
    /// </summary>
    public static class LuminaAuthSceneBuilder
    {
        private const string ScenesDir = "Assets/Project/Scenes/";
        private const string LoginPath = ScenesDir + "LoginScene.unity";
        private const string RegisterPath = ScenesDir + "RegisterScene.unity";
        private const string MainMenuPath = ScenesDir + "MainMenu.unity";
        private const string WorldPath = ScenesDir + "LuminaWorld.unity";

        private const string PrefabDir = "Assets/Project/Prefabs/";
        private const string AvatarPrefabDir = PrefabDir + "Avatars/";
        private const string BubblePrefabPath = PrefabDir + "SenaSpeechBubble.prefab";
        private const string LogoutPrefabPath = PrefabDir + "LogoutModal.prefab";
        private const string LogoutStagePrefabPath = PrefabDir + "LogoutSenaStage.prefab";
        private const string SenaPrefabPath = PrefabDir + "Sena.prefab";
        private const string AvatarBasePath = "Assets/Models/SunboxGames/Avatars/Prefabs/Avatar.prefab";
        private const string AvatarResources = "Assets/Models/SunboxGames/Avatars/Resources/";
        private const string VillageDir = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
        private const string MaterialDir = "Assets/Project/Materials/Auth/";
        private const string GroundMaterialPath = "Assets/Project/Materials/Lumina_Ground.mat";

        // Cielo y perfil de postproceso del menú principal (se buscan por GUID para no depender de su ruta).
        private const string WorldSkyPath = "Assets/Models/Fantasy Skybox FREE/Panoramics/FS002/FS002_Day.mat";
        private const string MenuVolumeGuid = "10fc4df2da32a41aaa32d77bc913491c";

        private struct AvatarSpec
        {
            public string Id, Name; public AvatarCustomization.AvatarGender Gender;
            public int Skin, Hair, HairMaterial, FacialHair, FacialHairMaterial, BottomVariation;
            public string Top, Bottom, Shoes, Glasses; public int TopVariation;
        }

        private static readonly AvatarSpec[] Avatars =
        {
            new AvatarSpec { Id = "luna", Name = "Luna", Gender = AvatarCustomization.AvatarGender.Female, Skin = 1, Hair = 6, HairMaterial = 1, TopVariation = 2,
                Top = "Tops/ClothingItem_Dress_02", Bottom = "Empty/ClothingItem_Bottom_Empty", Shoes = "Shoes/ClothingItem_Flats", Glasses = "Empty/ClothingItem_Glasses_Empty" },
            new AvatarSpec { Id = "nube", Name = "Nube", Gender = AvatarCustomization.AvatarGender.Female, Skin = 2, Hair = 5, HairMaterial = 2, TopVariation = 2, BottomVariation = 1,
                Top = "Tops/ClothingItem_TShirt", Bottom = "Bottoms/ClothingItem_Pants_Jeans", Shoes = "Shoes/ClothingItem_Basketball_Shoes", Glasses = "Empty/ClothingItem_Glasses_Empty" },
            new AvatarSpec { Id = "rio", Name = "Río", Gender = AvatarCustomization.AvatarGender.Male, Skin = 0, Hair = 1, HairMaterial = 0, FacialHair = 0, BottomVariation = 1,
                Top = "Tops/ClothingItem_Shirt", Bottom = "Bottoms/ClothingItem_Pants_Jeans", Shoes = "Shoes/ClothingItem_Suit_Shoes", Glasses = "Empty/ClothingItem_Glasses_Empty" },
            new AvatarSpec { Id = "sol", Name = "Sol", Gender = AvatarCustomization.AvatarGender.Male, Skin = 1, Hair = 7, HairMaterial = 3, FacialHair = 0, TopVariation = 1, BottomVariation = 1,
                Top = "Tops/ClothingItem_TShirt", Bottom = "Bottoms/ClothingItem_Pants_Cargo_Shorts", Shoes = "Shoes/ClothingItem_Basketball_Shoes", Glasses = "Glasses/Clear/ClothingItem_Glasses_02_Clear" }
        };

        // ------------------------------------------------------------------ Punto de entrada

        [MenuItem("Lumina/Autenticación/Construir todo")]
        public static void BuildAll()
        {
            EnsureFolders();
            var bubble = EnsureBubblePrefab();
            var avatarPrefabs = EnsureAvatarPrefabs();
            BuildLogin(bubble);
            BuildRegister(avatarPrefabs);
            BuildLogoutModal(bubble);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Lumina] Escenas de autenticación construidas.");
        }

        /// <summary>Reconstruye solo LoginScene y RegisterScene con los prefabs ya generados (no regenera avatares ni toca MainMenu).</summary>
        [MenuItem("Lumina/Autenticación/Reconstruir Login y Register")]
        public static void RebuildLoginAndRegister()
        {
            EnsureFolders();
            var bubble = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
            var avatarPrefabs = new GameObject[Avatars.Length];
            for (int i = 0; i < Avatars.Length; i++)
                avatarPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabDir + "Avatar_" + Avatars[i].Id + ".prefab");
            if (bubble == null || System.Array.Exists(avatarPrefabs, p => p == null))
                throw new System.InvalidOperationException("Faltan prefabs de la burbuja o de los avatares; ejecuta primero 'Construir todo'.");
            BuildLogin(bubble);
            BuildRegister(avatarPrefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lumina] LoginScene y RegisterScene reconstruidas.");
        }

        [MenuItem("Lumina/Autenticación/Registrar escenas en Build Settings")]
        public static void RegisterInBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == LoginPath || s.path == RegisterPath);
            list.Insert(0, new EditorBuildSettingsScene(RegisterPath, true));
            list.Insert(0, new EditorBuildSettingsScene(LoginPath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { AvatarPrefabDir, MaterialDir })
                Directory.CreateDirectory(Path.GetFullPath(dir.TrimEnd('/')));
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ Prefabs reutilizados

        // Copia la burbuja de diálogo de Sena que ya existe en LuminaWorld a un prefab (sin tocar esa escena) y le
        // oculta el botón y el contador, que no se usan en las pantallas de acceso.
        private static GameObject EnsureBubblePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
            if (existing != null) return existing;

            var scene = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Additive);
            try
            {
                GameObject source = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    source = FindDeep(root.transform, "WelcomeBubble")?.gameObject;
                    if (source != null) break;
                }
                if (source == null) throw new System.InvalidOperationException("No se encontró 'WelcomeBubble' en LuminaWorld.");
                PrefabUtility.SaveAsPrefabAsset(source, BubblePrefabPath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            var contents = PrefabUtility.LoadPrefabContents(BubblePrefabPath);
            try
            {
                FindDeep(contents.transform, "ButtonRow")?.gameObject.SetActive(false);
                FindDeep(contents.transform, "Progress")?.gameObject.SetActive(false);
                contents.name = "SenaSpeechBubble";
                contents.transform.localPosition = new Vector3(0f, 1.85f, 0f);
                PrefabUtility.SaveAsPrefabAsset(contents, BubblePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
        }

        private static GameObject[] EnsureAvatarPrefabs()
        {
            var baseAvatar = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarBasePath);
            var result = new GameObject[Avatars.Length];
            for (int i = 0; i < Avatars.Length; i++)
            {
                var spec = Avatars[i];
                string path = AvatarPrefabDir + "Avatar_" + spec.Id + ".prefab";

                var temp = (GameObject)PrefabUtility.InstantiatePrefab(baseAvatar);
                temp.name = "Avatar_" + spec.Id;
                var custom = temp.GetComponent<AvatarCustomization>();

                var so = new SerializedObject(custom);
                so.FindProperty("CurrentGender").enumValueIndex = (int)spec.Gender;
                so.FindProperty("SkinMaterialIndex").intValue = spec.Skin;
                so.FindProperty("HairStyleIndex").intValue = spec.Hair;
                so.FindProperty("HairMaterialIndex").intValue = spec.HairMaterial;
                so.FindProperty("FacialHairStyleIndex").intValue = spec.FacialHair;
                so.FindProperty("FacialHairMaterialIndex").intValue = 0;
                so.FindProperty("ClothingItemTop").objectReferenceValue = LoadClothing(spec.Top);
                so.FindProperty("ClothingItemTopVariationIndex").intValue = spec.TopVariation;
                so.FindProperty("ClothingItemBottom").objectReferenceValue = LoadClothing(spec.Bottom);
                so.FindProperty("ClothingItemBottomVariationIndex").intValue = spec.BottomVariation;
                so.FindProperty("ClothingItemShoes").objectReferenceValue = LoadClothing(spec.Shoes);
                so.FindProperty("ClothingItemGlasses").objectReferenceValue = LoadClothing(spec.Glasses);
                so.FindProperty("ClothingItemHat").objectReferenceValue = LoadClothing("Empty/ClothingItem_Hat_Empty");
                so.ApplyModifiedPropertiesWithoutUndo();

                try
                {
                    custom.SetGender(spec.Gender, true);
                    custom.UpdateCustomization();
                    custom.UpdateClothing();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Lumina] No se pudo aplicar la personalización en modo editor (se aplicará en Play): " + e.Message);
                }

                result[i] = PrefabUtility.SaveAsPrefabAsset(temp, path);
                Object.DestroyImmediate(temp);
            }
            return result;
        }

        private static ClothingItem LoadClothing(string relativePath)
        {
            var item = AssetDatabase.LoadAssetAtPath<ClothingItem>(AvatarResources + "ClothingItems/" + relativePath + ".asset");
            if (item == null) Debug.LogWarning("[Lumina] No se encontró la prenda: " + relativePath);
            return item;
        }

        // ------------------------------------------------------------------ LoginScene

        private static void BuildLogin(GameObject bubblePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupSceneBasics(new Vector3(0f, 0.72f, -5.0f), new Vector3(-5f, 0f, 0f), 36f, WorldSkyPath);

            var world = new GameObject("--- LOGIN WORLD ---").transform;
            var env = Child(world, "Environment");
            BuildGround(env, 600f);
            BuildVillageBackdrop(env, mirror: false);
            BuildLanternsAndFlowers(env, new Vector3(-1.3f, 0f, 0f));
            BuildFloatingDecor(world, new Vector3(-1.2f, 0f, 2.5f), 11, 31);
            BuildMotes(world, new Vector3(-1.2f, 2.2f, 3.5f), new Vector3(13f, 4.5f, 10f));

            // Sena (modelo original del juego) con su burbuja de diálogo.
            var senaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SenaPrefabPath);
            var sena = new GameObject("Sena");
            sena.transform.SetParent(world, false);
            sena.transform.SetPositionAndRotation(new Vector3(-1.3f, 0f, 0f), Quaternion.Euler(0f, 160f, 0f));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(senaPrefab, sena.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var bubble = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab, sena.transform);
            bubble.name = "SpeechBubble";
            LuminaUiKit.SetFloat(bubble.GetComponent<DialogueBubble>(), "referenceDistance", 5.9f);
            var speaker = sena.AddComponent<SenaSpeaker>();
            LuminaUiKit.Set(speaker, "bubble", bubble.GetComponent<DialogueBubble>());
            LuminaUiKit.Set(speaker, "senaModel", model);

            // Interfaz.
            var canvas = MakeCanvas("AuthCanvas");
            // Rótulo de marca en la zona izquierda, como el subtítulo del menú principal.
            var brand = LuminaUiKit.Panel(canvas.transform, "BrandPill", LuminaUiKit.Coral);
            LuminaUiKit.Place(brand.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(530f, 34f), new Vector2(720f, 62f));
            var brandLabel = LuminaUiKit.Label(brand.transform, "Label", "SEÑAS · EL MUNDO DE LAS MANOS", 28f, LuminaUiKit.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 5f);
            LuminaUiKit.Stretch(brandLabel.rectTransform);

            var inner = LuminaUiKit.Card(canvas.transform, "AuthPanel", new Vector2(760f, 910f), out var outer);
            LuminaUiKit.Place(outer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(760f, 910f));
            var group = outer.gameObject.AddComponent<CanvasGroup>();

            LuminaUiKit.ShadowedTitle(inner, "Title", "LÚMINA", 104f, LuminaUiKit.Coral, LuminaUiKit.TurquoiseSoft, new Vector2(5f, 6f), 22f, 640f, 120f, 6f);
            LuminaUiKit.Pill(inner, "SubtitlePill", "INICIAR SESIÓN", 26f, LuminaUiKit.Coral, LuminaUiKit.Cream, 148f, 360f, 54f, 6f);

            // Los campos, la fila de opciones y el estado van en un layout vertical: un mensaje de error empuja hacia abajo lo que sigue.
            var form = LuminaUiKit.MakeForm(inner, "Form", 214f, 620f, 10f);
            var email = LuminaUiKit.MakeField(form, "EmailField", "Correo electrónico", "nombre@correo.com", false, 0f, 620f, 28f, 66f, 28f, false, 28f, true);
            var password = LuminaUiKit.MakeField(form, "PasswordField", "Contraseña", "Tu contraseña", true, 0f, 620f, 28f, 66f, 28f, true, 28f, true);

            var options = LuminaUiKit.LayoutRow(form, "OptionsRow", 44f);
            var remember = LuminaUiKit.MakeToggle(options, "RememberToggle", "Recordar sesión", 24f, new Vector2(300f, 44f));
            LuminaUiKit.Place((RectTransform)remember.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(300f, 44f));
            var forgot = LuminaUiKit.MakeLink(options, "ForgotButton", "¿Olvidé mi contraseña?", 22f, LuminaUiKit.CoralDark, new Vector2(300f, 44f), TextAlignmentOptions.MidlineRight);
            LuminaUiKit.Place((RectTransform)forgot.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(300f, 44f));

            var statusLabel = LuminaUiKit.Label(form, "StatusLabel", string.Empty, 24f, LuminaUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            statusLabel.enableAutoSizing = true; statusLabel.fontSizeMin = 16f; statusLabel.fontSizeMax = 24f;
            LuminaUiKit.FixHeight(statusLabel, 52f);
            var status = statusLabel.gameObject.AddComponent<StatusMessage>();
            LuminaUiKit.Set(status, "label", statusLabel);

            var login = LuminaUiKit.MakeButton(inner, "LoginButton", "INICIAR SESIÓN", LuminaUiKit.Coral, LuminaUiKit.CoralDark, LuminaUiKit.Cream, new Vector2(620f, 92f), 34f, 9f, 4f);
            LuminaUiKit.TopCenter((RectTransform)login.transform, 606f, 620f, 92f);
            var loading = LuminaUiKit.MakeLoadingDots(login.transform.Find("Face"), LuminaUiKit.Cream);

            var hint = LuminaUiKit.Label(inner, "RegisterHint", "¿Es tu primera vez en Lúmina?", 24f, LuminaUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            LuminaUiKit.TopCenter(hint.rectTransform, 716f, 620f, 34f);
            hint.color = new Color(LuminaUiKit.Ink.r, LuminaUiKit.Ink.g, LuminaUiKit.Ink.b, 0.75f);
            var register = LuminaUiKit.MakeButton(inner, "RegisterButton", "CREAR CUENTA", LuminaUiKit.Turquoise, LuminaUiKit.TurquoiseDark, LuminaUiKit.Ink, new Vector2(620f, 80f), 30f, 8f, 4f);
            LuminaUiKit.TopCenter((RectTransform)register.transform, 756f, 620f, 80f);

            var footer = LuminaUiKit.Label(inner, "Footer", "Modo de prueba · todavía no hay cuentas reales", 20f, LuminaUiKit.Ink, FontStyles.Normal, TextAlignmentOptions.Center);
            LuminaUiKit.TopCenter(footer.rectTransform, 850f, 620f, 30f);
            footer.color = new Color(LuminaUiKit.Ink.r, LuminaUiKit.Ink.g, LuminaUiKit.Ink.b, 0.55f);

            var controller = new GameObject("LoginController").AddComponent<LoginUIController>();
            LuminaUiKit.Set(controller, "emailField", email);
            LuminaUiKit.Set(controller, "passwordField", password);
            LuminaUiKit.Set(controller, "rememberToggle", remember);
            LuminaUiKit.Set(controller, "loginButton", login);
            LuminaUiKit.Set(controller, "loginLabel", login.GetComponentInChildren<TMP_Text>());
            LuminaUiKit.Set(controller, "registerButton", register);
            LuminaUiKit.Set(controller, "forgotButton", forgot);
            LuminaUiKit.Set(controller, "status", status);
            LuminaUiKit.Set(controller, "loading", loading);
            LuminaUiKit.Set(controller, "panel", outer);
            LuminaUiKit.Set(controller, "panelGroup", group);
            LuminaUiKit.Set(controller, "sena", speaker);

            LuminaUiKit.EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, LoginPath);
        }

        // ------------------------------------------------------------------ RegisterScene

        private static void BuildRegister(GameObject[] avatarPrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupSceneBasics(new Vector3(0f, 0.42f, -5.6f), new Vector3(-5f, 0f, 0f), 36f, WorldSkyPath);

            var world = new GameObject("--- REGISTER WORLD ---").transform;
            var env = Child(world, "Environment");
            BuildGround(env, 600f);
            BuildVillageBackdrop(env, mirror: true);
            Vector3 stageCenter = new Vector3(-1.65f, 0f, 0f);
            BuildLanternsAndFlowers(env, stageCenter);
            BuildFloatingDecor(world, new Vector3(-1.2f, 0f, 2.5f), 13, 77);
            BuildMotes(world, new Vector3(-1.2f, 2.2f, 3.0f), new Vector3(13f, 4.5f, 9f));

            // Plataforma de exhibición en tres anillos (turquesa, crema y coral) con un foco cálido.
            var stage = Child(world, "ExhibitionStage");
            stage.position = stageCenter;
            Cylinder(stage, "RingCoral", 3.5f, 0.10f, Lit("Lumina_Stage_Coral", LuminaUiKit.Coral), 0.05f);
            Cylinder(stage, "RingCream", 3.1f, 0.16f, Lit("Lumina_Stage_Cream", LuminaUiKit.Cream), 0.10f);
            Cylinder(stage, "Top", 2.6f, 0.24f, Lit("Lumina_Stage_Turquoise", LuminaUiKit.Turquoise), 0.16f);

            var spotGo = new GameObject("StageSpot");
            spotGo.transform.SetParent(stage, false);
            spotGo.transform.localPosition = new Vector3(0.6f, 4.2f, -2.4f);
            spotGo.transform.LookAt(stage.position + new Vector3(0f, 0.9f, 0f));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot; spot.color = new Color(1f, 0.93f, 0.8f); spot.intensity = 90f; spot.range = 12f; spot.spotAngle = 52f;
            spot.shadows = LightShadows.Soft;

            // Avatares: todos bajo un pivote que gira; solo se ve el elegido.
            var pivot = Child(stage, "AvatarPivot");
            pivot.localPosition = new Vector3(0f, 0.24f, 0f);
            var options = new List<AvatarOption>();
            for (int i = 0; i < avatarPrefabs.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefabs[i], pivot);
                instance.name = Avatars[i].Id;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                options.Add(new AvatarOption { id = Avatars[i].Id, displayName = Avatars[i].Name, model = instance });
            }

            // Interfaz.
            var canvas = MakeCanvas("AuthCanvas");
            var inner = LuminaUiKit.Card(canvas.transform, "AuthPanel", new Vector2(760f, 990f), out var outer);
            LuminaUiKit.Place(outer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(760f, 990f));
            var group = outer.gameObject.AddComponent<CanvasGroup>();

            LuminaUiKit.ShadowedTitle(inner, "Title", "CREA TU CUENTA", 66f, LuminaUiKit.Coral, LuminaUiKit.TurquoiseSoft, new Vector2(4f, 5f), 18f, 680f, 84f, 4f);
            LuminaUiKit.Pill(inner, "SubtitlePill", "TU AVENTURA EMPIEZA AQUÍ", 22f, LuminaUiKit.Coral, LuminaUiKit.Cream, 108f, 460f, 46f, 5f);

            const float w = 620f;
            var form = LuminaUiKit.MakeForm(inner, "Form", 168f, w, 6f);
            var nameField = LuminaUiKit.MakeField(form, "NameField", "Nombre del jugador", "Como te llamas", false, 0f, w, 24f, 56f, 24f, false, 26f, true);
            var userField = LuminaUiKit.MakeField(form, "UsernameField", "Nombre de usuario", "tu_usuario", false, 0f, w, 24f, 56f, 24f, false, 26f, true);
            var emailField = LuminaUiKit.MakeField(form, "EmailField", "Correo electrónico", "nombre@correo.com", false, 0f, w, 24f, 56f, 24f, false, 26f, true);
            var passField = LuminaUiKit.MakeField(form, "PasswordField", "Contraseña", "Mínimo 6 caracteres", true, 0f, w, 24f, 56f, 24f, true, 26f, true);
            var confirmField = LuminaUiKit.MakeField(form, "ConfirmField", "Confirmar contraseña", "Repite tu contraseña", true, 0f, w, 24f, 56f, 24f, false, 26f, true);

            var statusLabel = LuminaUiKit.Label(form, "StatusLabel", string.Empty, 22f, LuminaUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            statusLabel.enableAutoSizing = true; statusLabel.fontSizeMin = 14f; statusLabel.fontSizeMax = 22f;
            LuminaUiKit.FixHeight(statusLabel, 44f);
            var status = statusLabel.gameObject.AddComponent<StatusMessage>();
            LuminaUiKit.Set(status, "label", statusLabel);

            var create = LuminaUiKit.MakeButton(inner, "CreateButton", "CREAR CUENTA", LuminaUiKit.Coral, LuminaUiKit.CoralDark, LuminaUiKit.Cream, new Vector2(w, 86f), 32f, 9f, 4f);
            LuminaUiKit.TopCenter((RectTransform)create.transform, 788f, w, 86f);
            var loading = LuminaUiKit.MakeLoadingDots(create.transform.Find("Face"), LuminaUiKit.Cream);

            var back = LuminaUiKit.MakeButton(inner, "BackButton", "VOLVER AL INICIO DE SESIÓN", LuminaUiKit.Sand, LuminaUiKit.SandDark, LuminaUiKit.Ink, new Vector2(w, 66f), 24f, 7f, 3f);
            LuminaUiKit.TopCenter((RectTransform)back.transform, 890f, w, 66f);

            // Selector de avatar (zona izquierda).
            var left = LuminaUiKit.Rect("AvatarSelectorUI", canvas.transform);
            LuminaUiKit.Stretch(left);
            var hintLabel = LuminaUiKit.Label(left, "Hint", "Arrastra para girar a tu avatar", 26f, LuminaUiKit.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 1f);
            LuminaUiKit.Place(hintLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(470f, -52f), new Vector2(760f, 40f));
            var chooseTitle = LuminaUiKit.Label(left, "ChooseTitle", "ELIGE A TU AVATAR", 54f, LuminaUiKit.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            var chooseShadow = LuminaUiKit.Label(left, "ChooseTitleShadow", "ELIGE A TU AVATAR", 54f, LuminaUiKit.CoralDark, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            LuminaUiKit.Place(chooseShadow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(474f, -104f), new Vector2(800f, 70f));
            LuminaUiKit.Place(chooseTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(470f, -98f), new Vector2(800f, 70f));
            chooseShadow.transform.SetSiblingIndex(chooseTitle.transform.GetSiblingIndex());

            var prev = LuminaUiKit.MakeButton(left, "PreviousButton", "<", LuminaUiKit.Turquoise, LuminaUiKit.TurquoiseDark, LuminaUiKit.Ink, new Vector2(96f, 96f), 52f, 8f, 0f);
            LuminaUiKit.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110f, 30f), new Vector2(96f, 96f));
            var next = LuminaUiKit.MakeButton(left, "NextButton", ">", LuminaUiKit.Turquoise, LuminaUiKit.TurquoiseDark, LuminaUiKit.Ink, new Vector2(96f, 96f), 52f, 8f, 0f);
            LuminaUiKit.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(830f, 30f), new Vector2(96f, 96f));

            var badge = LuminaUiKit.Panel(left, "SelectedBadge", LuminaUiKit.Turquoise);
            LuminaUiKit.Place(badge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(470f, 292f), new Vector2(230f, 44f));
            var badgeText = LuminaUiKit.Label(badge.transform, "Label", "TU AVATAR", 22f, LuminaUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center, 4f);
            LuminaUiKit.Stretch(badgeText.rectTransform);
            badge.gameObject.SetActive(false);

            var namePill = LuminaUiKit.Panel(left, "NamePill", LuminaUiKit.Coral);
            LuminaUiKit.Place(namePill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(470f, 236f), new Vector2(320f, 64f));
            var nameLabel = LuminaUiKit.Label(namePill.transform, "Label", "LUNA", 38f, LuminaUiKit.Cream, FontStyles.Bold, TextAlignmentOptions.Center, 8f);
            LuminaUiKit.Stretch(nameLabel.rectTransform);

            var dots = new List<Image>();
            for (int i = 0; i < avatarPrefabs.Length; i++)
            {
                var dot = LuminaUiKit.Panel(left, "Dot" + (i + 1), LuminaUiKit.TurquoiseSoft, LuminaUiKit.Circle);
                float x = 470f + (i - (avatarPrefabs.Length - 1) * 0.5f) * 40f;
                LuminaUiKit.Place(dot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 184f), new Vector2(20f, 20f));
                dots.Add(dot);
            }

            var select = LuminaUiKit.MakeButton(left, "SelectButton", "SELECCIONAR", LuminaUiKit.Coral, LuminaUiKit.CoralDark, LuminaUiKit.Cream, new Vector2(330f, 82f), 28f, 8f, 3f);
            LuminaUiKit.Place((RectTransform)select.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(470f, 104f), new Vector2(330f, 82f));

            var selector = left.gameObject.AddComponent<AvatarSelectorController>();
            LuminaUiKit.Set(selector, "pivot", pivot);
            SetAvatarOptions(selector, options);
            LuminaUiKit.Set(selector, "previousButton", prev);
            LuminaUiKit.Set(selector, "nextButton", next);
            LuminaUiKit.Set(selector, "selectButton", select);
            LuminaUiKit.Set(selector, "selectLabel", select.GetComponentInChildren<TMP_Text>());
            LuminaUiKit.Set(selector, "nameLabel", nameLabel);
            LuminaUiKit.Set(selector, "selectedBadge", badge.gameObject);
            LuminaUiKit.SetArray(selector, "dots", dots.ToArray());

            // Bienvenida tras crear la cuenta de prueba.
            var overlay = LuminaUiKit.Panel(canvas.transform, "WelcomeOverlay", LuminaUiKit.Dim, null, false);
            LuminaUiKit.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            var overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            var welcomeInner = LuminaUiKit.Card(overlay.transform, "WelcomeCard", new Vector2(860f, 520f), out var welcomeOuter);
            LuminaUiKit.Place(welcomeOuter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 520f));
            var wTitle = LuminaUiKit.Label(welcomeInner, "Title", "¡Bienvenido a Lúmina!", 52f, LuminaUiKit.CoralDark, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
            LuminaUiKit.TopCenter(wTitle.rectTransform, 56f, 760f, 140f);
            var wBody = LuminaUiKit.Label(welcomeInner, "Body", string.Empty, 28f, LuminaUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            LuminaUiKit.TopCenter(wBody.rectTransform, 214f, 740f, 120f);
            var cont = LuminaUiKit.MakeButton(welcomeInner, "ContinueButton", "IR AL MENÚ PRINCIPAL", LuminaUiKit.Coral, LuminaUiKit.CoralDark, LuminaUiKit.Cream, new Vector2(560f, 92f), 32f, 9f, 4f);
            LuminaUiKit.TopCenter((RectTransform)cont.transform, 366f, 560f, 92f);
            overlay.gameObject.SetActive(false);

            var controller = new GameObject("RegisterController").AddComponent<RegisterUIController>();
            LuminaUiKit.Set(controller, "nameField", nameField);
            LuminaUiKit.Set(controller, "usernameField", userField);
            LuminaUiKit.Set(controller, "emailField", emailField);
            LuminaUiKit.Set(controller, "passwordField", passField);
            LuminaUiKit.Set(controller, "confirmField", confirmField);
            LuminaUiKit.Set(controller, "createButton", create);
            LuminaUiKit.Set(controller, "createLabel", create.GetComponentInChildren<TMP_Text>());
            LuminaUiKit.Set(controller, "backButton", back);
            LuminaUiKit.Set(controller, "status", status);
            LuminaUiKit.Set(controller, "loading", loading);
            LuminaUiKit.Set(controller, "avatars", selector);
            LuminaUiKit.Set(controller, "panel", outer);
            LuminaUiKit.Set(controller, "panelGroup", group);
            LuminaUiKit.Set(controller, "welcomeGroup", overlayGroup);
            LuminaUiKit.Set(controller, "welcomeTitle", wTitle);
            LuminaUiKit.Set(controller, "welcomeBody", wBody);
            LuminaUiKit.Set(controller, "continueButton", cont);

            LuminaUiKit.EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, RegisterPath);
        }

        private static void SetAvatarOptions(Object target, List<AvatarOption> options)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty("options");
            p.arraySize = options.Count;
            for (int i = 0; i < options.Count; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = options[i].id;
                e.FindPropertyRelative("displayName").stringValue = options[i].displayName;
                e.FindPropertyRelative("model").objectReferenceValue = options[i].model;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ LogoutModal (en MainMenu)

        private static void BuildLogoutModal(GameObject bubblePrefab)
        {
            var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

            // Repetible: se eliminan las piezas creadas en una ejecución anterior.
            foreach (var name in new[] { "LogoutButton", "LogoutModal", "LogoutSenaStage" })
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    var found = FindDeep(go.transform, name);
                    if (found != null) Object.DestroyImmediate(found.gameObject);
                }
            }

            Canvas canvas = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = FindDeep(root.transform, "MainMenuCanvas");
                if (c != null) { canvas = c.GetComponent<Canvas>(); break; }
            }
            if (canvas == null) throw new System.InvalidOperationException("No se encontró MainMenuCanvas en MainMenu.");

            // Escenario de Sena (lejos del menú), con cámara propia que dibuja en una textura. Es su propio prefab.
            var stage = new GameObject("LogoutSenaStage").transform;
            stage.position = new Vector3(600f, 0f, 0f);
            var senaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SenaPrefabPath);
            var sena = (GameObject)PrefabUtility.InstantiatePrefab(senaPrefab, stage);
            sena.name = "Sena";
            sena.transform.localPosition = Vector3.zero;
            sena.transform.localRotation = Quaternion.Euler(0f, 175f, 0f);
            var camGo = new GameObject("SenaCamera");
            camGo.transform.SetParent(stage, false);
            camGo.transform.localPosition = new Vector3(0f, 1.05f, -3.5f);
            camGo.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 30f; cam.enabled = false;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var fill = new GameObject("StageFill").AddComponent<Light>();
            fill.transform.SetParent(stage, false);
            fill.transform.localPosition = new Vector3(0.8f, 2.4f, -2.2f);
            fill.type = LightType.Point; fill.color = new Color(1f, 0.95f, 0.85f); fill.intensity = 14f; fill.range = 9f;
            var stageComponent = stage.gameObject.AddComponent<LogoutSenaStage>();
            LuminaUiKit.Set(stageComponent, "stageCamera", cam);
            LuminaUiKit.Set(stageComponent, "senaAnimator", sena.GetComponentInChildren<Animator>());
            PrefabUtility.SaveAsPrefabAssetAndConnect(stage.gameObject, LogoutStagePrefabPath, InteractionMode.AutomatedAction);

            // Ventana.
            var root2 = LuminaUiKit.Rect("LogoutModal", canvas.transform);
            LuminaUiKit.Stretch(root2);

            // Botón de entrada, en la esquina superior derecha (el menú no cambia de estructura). Forma parte del prefab.
            var logout = LuminaUiKit.MakeButton(root2, "LogoutButton", "CERRAR SESIÓN", LuminaUiKit.Sand, LuminaUiKit.SandDark, LuminaUiKit.Ink, new Vector2(300f, 72f), 24f, 7f, 3f);
            LuminaUiKit.Place((RectTransform)logout.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -40f), new Vector2(300f, 72f));
            var visual = LuminaUiKit.Rect("Visual", root2);
            LuminaUiKit.Stretch(visual);
            var visualGroup = visual.gameObject.AddComponent<CanvasGroup>();
            var dim = visual.gameObject.AddComponent<Image>();
            dim.color = LuminaUiKit.Dim; dim.raycastTarget = true;

            var inner = LuminaUiKit.Card(visual, "Card", new Vector2(1060f, 640f), out var cardOuter);
            LuminaUiKit.Place(cardOuter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 640f));

            var senaFrame = LuminaUiKit.Panel(inner, "SenaFrame", LuminaUiKit.CreamWarm);
            LuminaUiKit.Place(senaFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(380f, 560f));
            var raw = new GameObject("SenaView", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(senaFrame.transform, false);
            LuminaUiKit.Stretch(raw.rectTransform, 6f, 6f, 6f, 6f);
            raw.raycastTarget = false;

            var title = LuminaUiKit.Label(inner, "Title", "¿Te vas de Lúmina?", 50f, LuminaUiKit.CoralDark, FontStyles.Bold, TextAlignmentOptions.Left, 1f);
            LuminaUiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -70f), new Vector2(560f, 150f));
            var message = LuminaUiKit.Label(inner, "Message", "Sena te espera para continuar la aventura cuando regreses.", 30f, LuminaUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            LuminaUiKit.Place(message.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -232f), new Vector2(540f, 150f));

            var confirm = LuminaUiKit.MakeButton(inner, "ConfirmButton", "SÍ, CERRAR SESIÓN", LuminaUiKit.Sand, LuminaUiKit.SandDark, LuminaUiKit.Ink, new Vector2(540f, 78f), 28f, 8f, 3f);
            LuminaUiKit.Place((RectTransform)confirm.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(440f, 126f), new Vector2(540f, 78f));
            var cancel = LuminaUiKit.MakeButton(inner, "CancelButton", "CONTINUAR JUGANDO", LuminaUiKit.Coral, LuminaUiKit.CoralDark, LuminaUiKit.Cream, new Vector2(540f, 92f), 32f, 9f, 3f);
            LuminaUiKit.Place((RectTransform)cancel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(440f, 24f), new Vector2(540f, 92f));

            var controller = root2.gameObject.AddComponent<LogoutModalController>();
            LuminaUiKit.Set(controller, "visualRoot", visual.gameObject);
            LuminaUiKit.Set(controller, "group", visualGroup);
            LuminaUiKit.Set(controller, "card", cardOuter);
            LuminaUiKit.Set(controller, "titleLabel", title);
            LuminaUiKit.Set(controller, "messageLabel", message);
            LuminaUiKit.Set(controller, "confirmButton", confirm);
            LuminaUiKit.Set(controller, "cancelButton", cancel);
            LuminaUiKit.Set(controller, "senaView", raw);
            LuminaUiKit.Set(controller, "openButton", logout);

            // La ventana se guarda como prefab reutilizable; todas sus referencias son internas (no hay overrides de escena).
            var prefabInstance = PrefabUtility.SaveAsPrefabAssetAndConnect(root2.gameObject, LogoutPrefabPath, InteractionMode.AutomatedAction);
            // Que la ventana (y su botón) queden por encima del resto del menú.
            prefabInstance.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ Entorno 3D compartido

        private static void SetupSceneBasics(Vector3 cameraPosition, Vector3 cameraEuler, float fov, string skyPath)
        {
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.fog = false;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraEuler));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;

            var sunGo = new GameObject("Directional Light");
            sunGo.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, 0.91f, 0.76f); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(MenuVolumeGuid));
            DynamicGI.UpdateEnvironment();
        }

        private static void BuildGround(Transform parent, float size)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = new Vector3(0f, -0.02f, 20f);
            plane.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            plane.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(plane.GetComponent<Collider>());
        }

        // Aldea y montañas al fondo. 'mirror' cambia la disposición para que cada escena tenga personalidad propia.
        private static void BuildVillageBackdrop(Transform parent, bool mirror)
        {
            float m = mirror ? -1f : 1f;
            var back = Child(parent, "Backdrop");

            // Camino de piedra que se aleja hacia la aldea.
            var path = Child(back, "Path");
            var tile = Village("Path_2");
            for (int i = 0; i < 9; i++)
                Place(tile, path, new Vector3(m * (-1.0f + Mathf.Sin(i * 0.8f) * 0.9f), 0f, 0.4f + i * 2.4f), i % 2 == 0 ? 0f : 90f, 0.55f, "PathTile");

            Place(Village("House_1"), back, new Vector3(m * -8.5f, 0f, 12f), mirror ? 240f : 120f, 0.5f, "House_1");
            Place(Village("House_3"), back, new Vector3(m * 7f, 0f, 15f), mirror ? 140f : 215f, 0.5f, "House_3");
            Place(Village("House_2"), back, new Vector3(m * -0.5f, 0f, 27f), 180f, 0.55f, "House_2");
            Place(Village("Bench"), back, new Vector3(m * -4.4f, 0f, 3.8f), mirror ? -70f : 70f, 1f, "Bench");
            Place(Village("Fence"), back, new Vector3(4.6f, 0f, 6.4f), 90f, 1f, "Fence");
            Place(Village("Fence"), back, new Vector3(4.6f, 0f, 9.6f), 90f, 1f, "Fence");

            // Las montañas del paquete miden ~100 m de ancho: se usan pequeñas y lejos como colinas de fondo.
            var mountains = Child(back, "Mountains");
            Place(Village("Mountain_1"), mountains, new Vector3(-85f, -1f, 125f), 15f, 0.7f, "Mountain");
            Place(Village("Mountain_3"), mountains, new Vector3(-5f, -1f, 140f), -20f, 0.8f, "Mountain");
            Place(Village("Mountain_2"), mountains, new Vector3(80f, -1f, 130f), 160f, 0.7f, "Mountain");

            // Árboles que enmarcan la escena sin tapar a los personajes.
            var trees = Child(back, "Trees");
            string[] kinds = { "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Pine Tree_1", "Pine Tree_2", "Pine Tree_3" };
            var rng = new System.Random(mirror ? 11 : 7);
            for (int i = 0; i < 22; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (6.5f + (float)rng.NextDouble() * 12f);
                float z = -1f + (float)rng.NextDouble() * 24f;
                if (Mathf.Abs(x) < 6f) x = side * 6.5f;
                Place(Village(kinds[rng.Next(kinds.Length)]), trees, new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f,
                    0.7f * Mathf.Lerp(0.85f, 1.25f, (float)rng.NextDouble()), "Tree");
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -26f + i * 6.5f + (float)rng.NextDouble() * 3f;
                Place(Village(kinds[rng.Next(kinds.Length)]), trees, new Vector3(x, 0f, 34f + (float)rng.NextDouble() * 10f), (float)rng.NextDouble() * 360f, 1.1f, "FarTree");
            }
        }

        private static void BuildLanternsAndFlowers(Transform parent, Vector3 center)
        {
            var props = Child(parent, "Props");
            var lantern = Village("Lantern");
            foreach (var offset in new[] { new Vector3(-5.4f, 0f, 8.5f), new Vector3(5.2f, 0f, 7.4f) })
            {
                var l = Place(lantern, props, center + offset, 0f, 1.1f, "Lantern");
                var lightGo = new GameObject("LanternLight");
                lightGo.transform.SetParent(l.transform, false);
                lightGo.transform.localPosition = new Vector3(0.45f, 2.55f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point; light.color = new Color(1f, 0.82f, 0.55f); light.intensity = 9f; light.range = 5.5f;
            }

            var flowers = Child(props, "Flowers");
            var rng = new System.Random(5);
            string[] kinds = { "Flower_1", "Flower_2", "Flower_3", "Flower_4", "Flower_5" };
            for (int i = 0; i < 7; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = Mathf.Lerp(2.3f, 3.8f, (float)rng.NextDouble());
                var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius * 0.8f);
                Place(Village(kinds[rng.Next(kinds.Length)]), flowers, pos, (float)rng.NextDouble() * 360f, Mathf.Lerp(0.28f, 0.42f, (float)rng.NextDouble()), "Flower");
            }
            Place(Village("Rock_1"), props, center + new Vector3(-2.7f, 0f, 0.6f), 40f, 0.9f, "Rock");
            Place(Village("Rock_2"), props, center + new Vector3(2.4f, 0f, -0.2f), 200f, 0.7f, "Rock");
        }

        // Esferas pastel que flotan y giran; mismo lenguaje de puntos de colores del menú principal.
        private static void BuildFloatingDecor(Transform parent, Vector3 center, int count, int seed)
        {
            var root = Child(parent, "FloatingDecor");
            var mats = new[]
            {
                Unlit("Lumina_Dot_Coral", LuminaUiKit.Coral),
                Unlit("Lumina_Dot_Turquoise", LuminaUiKit.Turquoise),
                Unlit("Lumina_Dot_Cream", LuminaUiKit.CreamWarm)
            };
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Dot";
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(root, false);
                float x = center.x + ((float)rng.NextDouble() - 0.5f) * 11f;
                float y = 0.7f + (float)rng.NextDouble() * 3.4f;
                float z = center.z + ((float)rng.NextDouble() - 0.4f) * 8f;
                sphere.transform.localPosition = new Vector3(x, y, z);
                sphere.transform.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.26f, (float)rng.NextDouble());
                sphere.GetComponent<Renderer>().sharedMaterial = mats[i % mats.Length];
                var floater = sphere.AddComponent<AmbientFloater>();
                LuminaUiKit.SetFloat(floater, "phase", (float)rng.NextDouble() * 10f);
                LuminaUiKit.SetFloat(floater, "amplitude", Mathf.Lerp(0.08f, 0.22f, (float)rng.NextDouble()));
                LuminaUiKit.SetFloat(floater, "frequency", Mathf.Lerp(0.2f, 0.45f, (float)rng.NextDouble()));
            }
        }

        // Partículas suaves que suben despacio (motas de luz).
        private static void BuildMotes(Transform parent, Vector3 center, Vector3 area)
        {
            var go = new GameObject("FloatingMotes");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(LuminaUiKit.CreamWarm, LuminaUiKit.TurquoiseSoft);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission; emission.rateOverTime = 11f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = area;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MoteMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ Utilidades

        private static Canvas MakeCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameObject Village(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageDir + name + ".prefab");
            if (prefab == null) Debug.LogError("[Lumina] Prefab de aldea no encontrado: " + name);
            return prefab;
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale, string name)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            string category = TintCategory(prefab.name);
            if (category != null) Tint(go, category);
            return go;
        }

        // El paquete de aldea referencia una textura de paleta inexistente y sus modelos salen blancos; LuminaWorld los tiñe
        // con copias de Color.mat (Lumina_Village_*). Aquí se reutilizan esas mismas copias para que las escenas coincidan.
        private const string VillageParentMaterial = "Assets/Models/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat";
        private static readonly Dictionary<string, Color> VillagePalette = new Dictionary<string, Color>
        {
            { "House", new Color(0.93f, 0.85f, 0.70f) }, { "Tree", new Color(0.36f, 0.62f, 0.30f) }, { "Pine", new Color(0.17f, 0.44f, 0.28f) },
            { "Path", new Color(0.74f, 0.71f, 0.64f) }, { "Wood", new Color(0.55f, 0.38f, 0.22f) }, { "Metal", new Color(0.26f, 0.26f, 0.30f) },
            { "Rock", new Color(0.52f, 0.52f, 0.54f) }, { "Flower", new Color(0.93f, 0.48f, 0.60f) }, { "Terracotta", new Color(0.76f, 0.42f, 0.30f) },
            { "Mountain", new Color(0.42f, 0.62f, 0.66f) }
        };

        private static string TintCategory(string prefabName)
        {
            if (prefabName.StartsWith("House")) return "House";
            if (prefabName.StartsWith("Pine Tree")) return "Pine";
            if (prefabName.StartsWith("Tree")) return "Tree";
            if (prefabName.StartsWith("Path")) return "Path";
            if (prefabName.StartsWith("Fence") || prefabName.StartsWith("Bench") || prefabName.StartsWith("Crate")) return "Wood";
            if (prefabName.StartsWith("Lantern")) return "Metal";
            if (prefabName.StartsWith("Rock")) return "Rock";
            if (prefabName.StartsWith("Flower Pot")) return "Terracotta";
            if (prefabName.StartsWith("Flower")) return "Flower";
            if (prefabName.StartsWith("Mountain") || prefabName.StartsWith("Cliff")) return "Mountain";
            return null;
        }

        private static void Tint(GameObject instance, string category)
        {
            var parent = AssetDatabase.LoadAssetAtPath<Material>(VillageParentMaterial);
            string path = "Assets/Project/Materials/Lumina_Village_" + category + ".mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (variant == null)
            {
                variant = new Material(parent) { name = "Lumina_Village_" + category };
                variant.SetColor("_BaseColor", VillagePalette[category]);
                AssetDatabase.CreateAsset(variant, path);
            }

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = renderer.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == parent) { mats[i] = variant; dirty = true; }
                if (!dirty) continue;
                renderer.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // Disco bajo (el cilindro de Unity mide 2 de alto): 'height' es el alto real y descansa sobre el suelo.
        private static void Cylinder(Transform parent, string name, float diameter, float height, Material material, float unused = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Lit(string name, Color color)
        {
            string path = MaterialDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Unlit(string name, Color color)
        {
            string path = MaterialDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material MoteMaterial()
        {
            const string name = "Lumina_Mote";
            string path = MaterialDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/UI/Sprites/Circle.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
