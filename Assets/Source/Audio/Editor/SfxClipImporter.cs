using System;
using System.IO;
using System.Threading;
using BalloonParty.Audio;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace BalloonParty.Audio.Editor
{
    // Downloads a candidate's preview mp3 into the project, imports it with the SFX import settings
    // the plan wants (ADPCM / Decompress-On-Load / 22 kHz), and returns the imported AudioClip.
    internal static class SfxClipImporter
    {
        private const string FetchedFolder = "Assets/Audio/Fetched";

        public static async UniTask<AudioClip> ImportAsync(SfxCandidate candidate, GameSoundId soundId,
            CancellationToken cancellationToken)
        {
            byte[] bytes;
            using (var webRequest = UnityWebRequest.Get(candidate.PreviewUrl))
            {
                try
                {
                    await webRequest.SendWebRequest().WithCancellation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SFX Fetch] preview download failed: {e.Message}");
                    return null;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[SFX Fetch] preview download {webRequest.responseCode}: {webRequest.error}");
                    return null;
                }

                bytes = webRequest.downloadHandler.data;
            }

            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            Directory.CreateDirectory(FetchedFolder);
            var assetPath = $"{FetchedFolder}/{soundId}__{candidate.ProviderId}.mp3";
            File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            ApplyImportSettings(assetPath);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static void ApplyImportSettings(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not AudioImporter importer)
            {
                return;
            }

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 22050u;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
