using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Web-friendly audio: effects are small mono Vorbis decoded on load (instant playback),
    /// music is compressed in memory (WebGL cannot stream from disk).
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/"))
                return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith("Assets/Audio/Music/");
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.45f : 0.6f;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = music ? 44100u : 32000u;
            importer.defaultSampleSettings = settings;
        }
    }
}
