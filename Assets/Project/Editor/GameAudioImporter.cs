using UnityEditor;
using UnityEngine;

namespace Lumina.EditorTools
{
    /// <summary>Ajustes de importación de Resources/Audio: el fondo se lee en streaming (pesa ~6 MB); los efectos se descomprimen al reproducir.</summary>
    public sealed class GameAudioImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.EndsWith("/fondo.mp3");
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.6f : 0.8f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
        }
    }
}
