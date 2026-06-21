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
                FrameRateMode.MatchDisplay => GetDisplayRefreshRate(),
                FrameRateMode.Custom => _customFrameRate,
                _ => 60
            };
        }

        private static int GetDisplayRefreshRate()
        {
            var hz = (int)Mathf.Round((float)Screen.currentResolution.refreshRateRatio.value);
            return hz > 0 ? hz : 60;
        }
    }
}
