using UnityEngine;

namespace BalloonParty.Thrower
{
    // Fixed-capacity ring buffer of (timestamp, direction) aim samples backing the aim latch: firing
    // resolves the direction from a moment before release instead of the live sample, so a
    // touchscreen's lift-off displacement (a finger rolling off the glass reads as a last-instant
    // position change) falls inside the discarded window rather than skewing the shot. Zero
    // allocation once constructed — ThrowerController owns one instance for its lifetime.
    internal sealed class AimDirectionHistory
    {
        private readonly float[] _timestamps;
        private readonly Vector3[] _directions;
        private readonly int _capacity;

        private int _count;
        private int _head;

        internal AimDirectionHistory(int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
            _timestamps = new float[_capacity];
            _directions = new Vector3[_capacity];
        }

        internal void Record(float timestamp, Vector3 direction)
        {
            _timestamps[_head] = timestamp;
            _directions[_head] = direction;
            _head = (_head + 1) % _capacity;
            _count = Mathf.Min(_count + 1, _capacity);
        }

        internal void Clear()
        {
            _count = 0;
            _head = 0;
        }

        // Newest sample at or before targetTime, walking backward from the most recently recorded
        // one. Falls back to the oldest sample held if the history doesn't reach back that far —
        // better an under-latched shot than a failed one. False only when nothing has been recorded.
        internal bool TryResolve(float targetTime, out Vector3 direction)
        {
            if (_count == 0)
            {
                direction = default;
                return false;
            }

            var oldestIndex = _head;
            for (var i = 0; i < _count; i++)
            {
                var index = (_head - 1 - i + _capacity) % _capacity;
                if (_timestamps[index] <= targetTime)
                {
                    direction = _directions[index];
                    return true;
                }

                oldestIndex = index;
            }

            direction = _directions[oldestIndex];
            return true;
        }
    }
}
