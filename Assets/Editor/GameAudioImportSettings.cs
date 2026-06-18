using UnityEditor;
using UnityEngine;

/// <summary>
/// Forces sensible import settings on the game-music clips in Resources/Audio so
/// the long ambient track streams from disk instead of being fully decompressed
/// into RAM (a 69-minute clip would otherwise use hundreds of MB of memory).
/// Runs automatically whenever Unity (re)imports those audio files.
/// </summary>
public class GameAudioImportSettings : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        string path = assetPath.Replace("\\", "/");
        if (!path.Contains("/Resources/Audio/")) return;

        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;     // stream, don't load to RAM
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.5f;
        settings.preloadAudioData = false;                   // per-platform setting now
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
    }
}
