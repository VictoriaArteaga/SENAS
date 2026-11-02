using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Postprocesado del mundo 3D de Lúmina. Un perfil por pipeline: el nivel de calidad del juego ya elige entre PC_RPAsset y
    /// Mobile_RPAsset, y cada asset tiene un perfil de volumen por defecto (<c>volumeProfile</c>) que URP apila sobre el global.
    /// Así el postprocesado de cada plataforma llega solo por el pipeline: no hay componentes Volume en las escenas 3D ni un
    /// detector de plataforma propio. Solo actúa en las cámaras 3D con "Post Processing" activado.
    /// Menú: Lumina > Postprocesado. Todo es idempotente y solo toca los assets Lumina_World_* y el campo del pipeline.
    /// </summary>
    public static class LuminaPostProcessSetup
    {
        public const string PcProfilePath = "Assets/Settings/Lumina_World_PC.asset";
        public const string MobileProfilePath = "Assets/Settings/Lumina_World_Mobile.asset";
        public const string UiProfilePath = "Assets/Settings/Lumina_UI_2D.asset";
        private const string UiTemplatePath = "Assets/Settings/SampleSceneProfile.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string MobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Lumina/Postprocesado/Crear perfiles del mundo (PC y Mobile)")]
        public static void CreateProfiles()
        {
            // PC: Bloom discreto, algo de contraste y saturación, viñeta casi imperceptible.
            Build(PcProfilePath, bloom: true);
            // Mobile: lo mismo sin Bloom (coste de una pasada extra a resolución de render con FSR y HDR).
            Build(MobileProfilePath, bloom: false);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Lumina/Postprocesado/Asignar perfiles a los pipelines")]
        public static void AssignToPipelines()
        {
            Assign(PcPipelinePath, PcProfilePath);
            Assign(MobilePipelinePath, MobileProfilePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Perfil del Global Volume de las pantallas 2D (MainMenu, Login, Register, Practice, Profile, Settings y Results).
        /// Los perfiles del mundo viven en el pipeline, que es la capa BAJO los volúmenes de escena: todo parámetro que
        /// definen y que el volumen de una pantalla no sobrescribe se filtra a esas pantallas (se comprobó: contraste +8,
        /// saturación +10 y smoothness 0.4 de la viñeta aparecían también en el menú). Este perfil es una copia de
        /// SampleSceneProfile (el que usaban esas pantallas; no se modifica) más una sobrescritura explícita con los valores
        /// que tenían antes de esos parámetros: Color Adjustments neutro y smoothness de la viñeta 0.2.
        /// </summary>
        public static VolumeProfile EnsureUiProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UiProfilePath);
            if (profile == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UiTemplatePath);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = System.IO.Path.GetFileNameWithoutExtension(UiProfilePath);
                AssetDatabase.CreateAsset(profile, UiProfilePath);
                foreach (var source in template.components)
                {
                    var copy = Object.Instantiate(source);
                    copy.name = source.name;
                    profile.components.Add(copy);
                    AssetDatabase.AddObjectToAsset(copy, profile);
                }
            }

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(0f);
            color.saturation.Override(0f);
            color.hueShift.Override(0f);
            color.colorFilter.Override(Color.white);

            var vignette = Get<Vignette>(profile);
            vignette.smoothness.Override(0.2f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        [MenuItem("Lumina/Postprocesado/Crear perfil de las pantallas 2D")]
        public static void CreateUiProfile() => EnsureUiProfile();

        /// <summary>Crea (o actualiza) los perfiles, los asigna a sus pipelines y prepara el perfil de las pantallas 2D.</summary>
        public static void Apply()
        {
            EnsureUiProfile();
            CreateProfiles();
            AssignToPipelines();
            Debug.Log("[Lumina] Postprocesado: perfiles creados y asignados a PC_RPAsset y Mobile_RPAsset.");
        }

        private static void Build(string path, bool bloom)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(profile, path);
            }

            // Sin Tonemapping a propósito: se comparó Neutral con «ninguno» sobre el mundo y las casas, y Neutral comprime el
            // cielo HDR (queda pálido y las nubes grisáceas) y apaga el coral y el turquesa de los interiores. Sin él se
            // conserva la paleta; contraste, saturación y viñeta aportan el acabado. (Las pantallas 2D usan su propio perfil.)
            if (profile.TryGet(out Tonemapping oldTonemapping))
            {
                profile.Remove<Tonemapping>();
                Object.DestroyImmediate(oldTonemapping, true);
            }

            if (bloom)
            {
                var b = Get<Bloom>(profile);
                b.threshold.Override(1f);
                b.intensity.Override(0.2f);
                b.scatter.Override(0.5f);
                b.highQualityFiltering.Override(false);
            }

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(10f);   // el filtro de color queda neutro a propósito

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.4f);

            EditorUtility.SetDirty(profile);
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) { EditorUtility.SetDirty(component); return component; }
            component = profile.Add<T>(false);
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
            return component;
        }

        private static void Assign(string pipelinePath, string profilePath)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (pipeline == null || profile == null) throw new System.InvalidOperationException("Falta " + pipelinePath + " o " + profilePath);
            if (pipeline.volumeProfile == profile) return;
            pipeline.volumeProfile = profile;
            EditorUtility.SetDirty(pipeline);
        }
    }
}
