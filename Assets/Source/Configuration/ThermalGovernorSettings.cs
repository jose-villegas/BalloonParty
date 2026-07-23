using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Thermal Governor Settings", fileName = "ThermalGovernorSettings")]
    internal class ThermalGovernorSettings : ScriptableObject, IThermalGovernorSettings
    {
        [Header("Toggle")]
        [Tooltip("When off the governor never changes the frame rate; the boot vote stands.")]
        [SerializeField] private bool _enabled = true;

        [Header("Rate ladder (fastest first)")]
        [Tooltip("Frame-rate rungs. The Pixel 9 panel natively supports 80 Hz, which divides its 240 Hz vsync base cleanly.")]
        [SerializeField] private int[] _rateLadder = { 120, 80, 60 };

        [Header("Thresholds (headroom: 1.0 = severe-throttle threshold)")]
        [Tooltip("At/above this headroom the device is hot and a down-step is armed.")]
        [SerializeField] private float _downHeadroom = 0.85f;

        [Tooltip("At/below this headroom the device is cool and an up-step is armed.")]
        [SerializeField] private float _upHeadroom = 0.65f;

        [Header("Windows (seconds)")]
        [Tooltip("Sustained hot time before stepping down — short, so throttling is met quickly.")]
        [SerializeField] private float _downSustainSeconds = 10f;

        [Tooltip("Sustained cool time before stepping up — long, to avoid climbing back into the judder.")]
        [SerializeField] private float _upSustainSeconds = 60f;

        [Tooltip("Minimum time at a rung before any up-step, so rungs don't oscillate.")]
        [SerializeField] private float _minDwellSeconds = 90f;

        [Tooltip("How often the governor re-reads the thermal source and advances its timers.")]
        [SerializeField] private float _pollIntervalSeconds = 1f;

        public bool Enabled => _enabled;
        public IReadOnlyList<int> RateLadder => _rateLadder;
        public float DownHeadroom => _downHeadroom;
        public float UpHeadroom => _upHeadroom;
        public float DownSustainSeconds => _downSustainSeconds;
        public float UpSustainSeconds => _upSustainSeconds;
        public float MinDwellSeconds => _minDwellSeconds;
        public float PollIntervalSeconds => _pollIntervalSeconds;
    }
}
