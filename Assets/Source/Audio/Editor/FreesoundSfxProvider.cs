using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BalloonParty.Audio.Editor
{
    internal sealed class FreesoundSfxProvider : ISfxProvider
    {
        private readonly FreesoundTokenSource _tokenSource;

        public FreesoundSfxProvider(FreesoundTokenSource tokenSource)
        {
            _tokenSource = tokenSource;
        }

        public async UniTask<IReadOnlyList<SfxCandidate>> FetchAsync(SfxFetchRequest request,
            CancellationToken cancellationToken)
        {
            if (!_tokenSource.TryResolve(out var token, out _))
            {
                Debug.LogWarning("[SFX Fetch] No Freesound token — set FREESOUND_API_TOKEN or paste one in the sound-bank inspector.");
                return Array.Empty<SfxCandidate>();
            }

            var url = FreesoundQueryBuilder.BuildSearchUrl(request);
            using var webRequest = UnityWebRequest.Get(url);
            webRequest.SetRequestHeader("Authorization", "Token " + token);

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
                Debug.LogWarning($"[SFX Fetch] Freesound request failed: {e.Message}");
                return Array.Empty<SfxCandidate>();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SFX Fetch] Freesound returned {webRequest.responseCode}: {webRequest.error}");
                return Array.Empty<SfxCandidate>();
            }

            return FreesoundResponseParser.Parse(webRequest.downloadHandler.text, request.AllowedLicenses);
        }
    }
}
