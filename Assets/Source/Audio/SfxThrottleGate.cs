using System;

namespace BalloonParty.Audio
{
    internal sealed class SfxThrottleGate
    {
        private readonly Func<float> _clock;
        private readonly float _windowSeconds;
        private readonly int _maxBurst;
        private readonly float[] _lastPass;
        private readonly float[] _windowStart;
        private readonly int[] _burstCount;

        public SfxThrottleGate(Func<float> clock, float coalesceWindowSeconds, int maxBurstPerWindow)
        {
            _clock = clock;
            _windowSeconds = coalesceWindowSeconds;
            _maxBurst = maxBurstPerWindow;
            _lastPass = new float[SoundIds.Count];
            _windowStart = new float[SoundIds.Count];
            _burstCount = new int[SoundIds.Count];
            Reset();
        }

        public bool TryPass(GameSoundId id, float cooldownSeconds, out int burstIndex)
        {
            var ordinal = (int)id;
            var now = _clock();

            if (now - _lastPass[ordinal] < cooldownSeconds)
            {
                burstIndex = 0;
                return false;
            }

            if (now - _windowStart[ordinal] > _windowSeconds)
            {
                _windowStart[ordinal] = now;
                _burstCount[ordinal] = 0;
            }

            if (_burstCount[ordinal] >= _maxBurst)
            {
                burstIndex = 0;
                return false;
            }

            burstIndex = _burstCount[ordinal]++;
            _lastPass[ordinal] = now;
            return true;
        }

        public void Reset()
        {
            for (var i = 0; i < _lastPass.Length; i++)
            {
                _lastPass[i] = float.NegativeInfinity;
                _windowStart[i] = float.NegativeInfinity;
                _burstCount[i] = 0;
            }
        }
    }
}
