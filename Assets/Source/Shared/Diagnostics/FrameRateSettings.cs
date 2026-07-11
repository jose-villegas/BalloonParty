using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

namespace BalloonParty.Shared.Diagnostics
{
    internal enum FrameRateMode
    {
        Default60,
        MatchDisplay,
        Custom
    }

    internal sealed class FrameRateSettings : MonoBehaviour
    {
        [SerializeField] private FrameRateMode _mode = FrameRateMode.MatchDisplay;

        [ShowIf(nameof(IsCustom))]
        [SerializeField] private int _customFrameRate = 60;

        private bool IsCustom => _mode == FrameRateMode.Custom;

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                Apply();
            }
        }
#endif

        private void Apply()
        {
            QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = _mode switch
            {
                FrameRateMode.Default60 => 60,
                FrameRateMode.MatchDisplay => MatchDisplayTarget(),
                FrameRateMode.Custom => _customFrameRate,
                _ => 60
            };
        }

        private int MatchDisplayTarget()
        {
#if UNITY_EDITOR
            // The editor reports the Game View's refresh rate (typically 60) rather than the physical
            // display, so matching it would falsely cap the editor. Leave it uncapped here; a real build
            // matches the device's refresh rate.
            return -1;
#else
            // Android hands the app whatever mode it booted into (usually 60 Hz, even on a
            // 120 Hz panel) — Screen.currentResolution only reflects that, it doesn't request
            // a faster one. RequestHigherRefreshRate asks for the best available mode and
            // re-reads the actual result once the switch (if any) has taken effect.
            RequestHigherRefreshRate();
            return GetDisplayRefreshRate();
#endif
        }

#if !UNITY_EDITOR
        private void RequestHigherRefreshRate()
        {
            var currentRefreshRate = Screen.currentResolution.refreshRateRatio;
            var bestRefreshRate = currentRefreshRate;

            // Startup-only diagnostic (readable on device via the in-game debug console): shows
            // which modes the panel actually exposes, so a refused/missing 120 Hz request is
            // explainable from a device log alone.
            var modes = string.Join(", ", System.Array.ConvertAll(Screen.resolutions,
                r => $"{r.width}x{r.height}@{r.refreshRateRatio.value:F1}"));
            Debug.Log($"[FrameRateSettings] current {Screen.width}x{Screen.height}" +
                      $"@{currentRefreshRate.value:F1}; Screen.resolutions: {modes}");

            // Shop across ALL modes for refresh rate only, ignoring size: the app's rendering
            // surface is inset by the display cutout/nav area (e.g. 960x1989 vs the panel's
            // 960x2142 modes), so matching the panel modes' width/height exactly is structurally
            // impossible. The SetResolution below keeps the current surface size regardless.
            foreach (var resolution in Screen.resolutions)
            {
                if (resolution.refreshRateRatio.value > bestRefreshRate.value)
                {
                    bestRefreshRate = resolution.refreshRateRatio;
                }
            }

            if (bestRefreshRate.value > currentRefreshRate.value)
            {
                Debug.Log($"[FrameRateSettings] requesting {Screen.width}x{Screen.height}" +
                          $"@{bestRefreshRate.value:F1}");
                Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreenMode, bestRefreshRate);
            }
            else
            {
                Debug.Log("[FrameRateSettings] display exposes no refresh rate above current");
            }

            // Fire even when nothing was requested — the post-delay log re-reads reality either
            // way, so the granted-outcome line always appears in the device log.
            ReapplyTargetFrameRateAsync().Forget();
        }

        // The mode switch above is asynchronous, so the refresh rate Screen.currentResolution
        // reports right after SetResolution can still be stale. Wait a few frames, then re-read
        // the actual value and correct targetFrameRate — this self-heals whether the request
        // was honored or an OEM battery saver silently denied it.
        private async UniTaskVoid ReapplyTargetFrameRateAsync()
        {
            await UniTask.DelayFrame(10);

            if (this == null)
            {
                return;
            }

            Application.targetFrameRate = GetDisplayRefreshRate();
            Debug.Log($"[FrameRateSettings] display now {Screen.currentResolution.width}" +
                      $"x{Screen.currentResolution.height}" +
                      $"@{Screen.currentResolution.refreshRateRatio.value:F1}, " +
                      $"targetFrameRate={Application.targetFrameRate}");
        }
#endif

        private static int GetDisplayRefreshRate()
        {
            var hz = (int)Mathf.Round((float)Screen.currentResolution.refreshRateRatio.value);
            return hz > 0 ? hz : 60;
        }
    }
}
