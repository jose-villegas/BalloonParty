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
            // On adaptive-refresh Android (ARR, Pixel-class) there's no display-mode switch to
            // make: the panel already runs a high-Hz mode and Screen.currentResolution reports
            // the per-app RENDER rate Android arbitrated — which follows this app's own
            // targetFrameRate vote. Echoing that reading back would re-pin 60; target the
            // panel's best advertised rate instead.
            return RequestBestRefreshRate();
#endif
        }

#if !UNITY_EDITOR
        private int RequestBestRefreshRate()
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

                // No-op on ARR devices (the panel mode doesn't change); covers true mode-switch
                // devices where the panel genuinely idles in a 60 Hz mode.
                Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreenMode, bestRefreshRate);
            }
            else
            {
                Debug.Log("[FrameRateSettings] display exposes no refresh rate above current");
            }

            LogGrantedDisplayModeAsync().Forget();

            var bestHz = (int)Mathf.Round((float)bestRefreshRate.value);
            return bestHz > 0 ? bestHz : GetDisplayRefreshRate();
        }

        // Log-only by design: on ARR devices the reported per-app rate echoes this app's own
        // targetFrameRate vote, so down-correcting to it would re-pin the very 60 we're escaping.
        // Over-asking is harmless — frame pacing settles at whatever the display actually grants.
        // The delay lets the arbitration (or a real mode switch) settle before we snapshot it.
        private async UniTaskVoid LogGrantedDisplayModeAsync()
        {
            await UniTask.DelayFrame(10);

            if (this == null)
            {
                return;
            }

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
