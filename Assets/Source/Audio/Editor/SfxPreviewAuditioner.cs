using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace BalloonParty.Audio.Editor
{
    // Best-effort in-editor audition of a candidate's mp3 preview: streams it to a throwaway AudioClip
    // and plays it through UnityEditor's internal AudioUtil via reflection. AudioUtil's method names
    // shift across Unity versions, so this tries the known ones and no-ops if none match — never a
    // hard failure (reflection is compile-safe; a version miss just means no sound).
    internal static class SfxPreviewAuditioner
    {
        public static async UniTask PlayAsync(string previewUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(previewUrl))
            {
                return;
            }

            using var request = UnityWebRequestMultimedia.GetAudioClip(previewUrl, AudioType.MPEG);
            try
            {
                await request.SendWebRequest().WithCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SFX Fetch] preview stream failed: {e.Message}");
                return;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SFX Fetch] preview stream {request.responseCode}: {request.error}");
                return;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null)
            {
                PlayClip(clip);
            }
        }

        public static void Stop()
        {
            if (!Invoke("StopAllPreviewClips"))
            {
                Invoke("StopAllClips");
            }
        }

        private static void PlayClip(AudioClip clip)
        {
            var audioUtil = GetAudioUtilType();
            if (audioUtil == null)
            {
                return;
            }

            // Unity 2020+: PlayPreviewClip(AudioClip, int startSample, bool loop). Older: PlayClip(AudioClip).
            var preview = audioUtil.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (preview != null)
            {
                preview.Invoke(null, new object[] { clip, 0, false });
                return;
            }

            var legacy = audioUtil.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(AudioClip) }, null);
            legacy?.Invoke(null, new object[] { clip });
        }

        private static bool Invoke(string methodName)
        {
            var method = GetAudioUtilType()?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            if (method == null)
            {
                return false;
            }

            method.Invoke(null, null);
            return true;
        }

        private static Type GetAudioUtilType()
        {
            return typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        }
    }
}
