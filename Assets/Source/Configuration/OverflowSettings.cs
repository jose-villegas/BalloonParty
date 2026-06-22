using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Overflow Settings", fileName = "OverflowSettings")]
    internal class OverflowSettings : ScriptableObject, IOverflowSettings
    {
        [Header("Timing")]
        [Tooltip("Appearance delay between columns rejected in the same line, so a line sweeps in.")]
        [SerializeField] private float _appearStaggerSeconds = 0.08f;

        [Tooltip("How long an overflow balloon sits in the pile before it's eligible to pop.")]
        [SerializeField] private float _lingerSeconds = 0.6f;

        [Tooltip("Minimum gap between pops so the pile bursts one balloon at a time.")]
        [SerializeField] private float _popIntervalSeconds = 0.15f;

        [Header("Motion")]
        [Tooltip("Ease sharpness for the rise-in and the slide-up compaction (higher = snappier).")]
        [SerializeField] private float _moveSharpness = 10f;

        [Tooltip("How close to its target row counts as arrived.")]
        [SerializeField] private float _arrivalRadius = 0.02f;

        [Header("Heart trail")]
        [Tooltip("Flight time of the heart trail from the health UI to an overflow pop.")]
        [SerializeField] private float _heartTrailDuration = 0.5f;

        public float AppearStaggerSeconds => _appearStaggerSeconds;
        public float LingerSeconds => _lingerSeconds;
        public float PopIntervalSeconds => _popIntervalSeconds;
        public float MoveSharpness => _moveSharpness;
        public float ArrivalRadius => _arrivalRadius;
        public float HeartTrailDuration => _heartTrailDuration;
    }
}
