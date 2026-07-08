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

        private static int MatchDisplayTarget()
        {
#if UNITY_EDITOR
            // The editor reports the Game View's refresh rate (typically 60) rather than the physical
            // display, so matching it would falsely cap the editor. Leave it uncapped here; a real build
            // matches the device's refresh rate.
            return -1;
#else
            return GetDisplayRefreshRate();
#endif
        }

        private static int GetDisplayRefreshRate()
        {
            var hz = (int)Mathf.Round((float)Screen.currentResolution.refreshRateRatio.value);
            return hz > 0 ? hz : 60;
        }
    }
}
