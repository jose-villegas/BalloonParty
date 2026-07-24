using System.Collections.Generic;

namespace BalloonParty.Audio
{
    internal sealed class VoiceLimiter
    {
        private readonly int _globalCap;
        private readonly int[] _perIdCount;
        private readonly Slot[] _slots;
        private readonly Stack<int> _free;

        private long _sequence;
        private int _activeCount;

        public int ActiveCount => _activeCount;

        public VoiceLimiter(int globalCap)
        {
            _globalCap = globalCap;
            _perIdCount = new int[SoundIds.Count];
            _slots = new Slot[globalCap];
            _free = new Stack<int>(globalCap);
            for (var i = globalCap - 1; i >= 0; i--)
            {
                _free.Push(i);
            }
        }

        public bool TryAcquire(GameSoundId id, int perIdCap, int priority, out int voiceId, out int stolenVoiceId)
        {
            var ordinal = (int)id;

            if (_perIdCount[ordinal] >= perIdCap)
            {
                var oldest = OldestSlotFor(id);
                Stamp(oldest, id, priority);
                voiceId = oldest;
                stolenVoiceId = oldest;
                return true;
            }

            if (_free.Count > 0)
            {
                var slot = _free.Pop();
                Stamp(slot, id, priority);
                _perIdCount[ordinal]++;
                _activeCount++;
                voiceId = slot;
                stolenVoiceId = -1;
                return true;
            }

            var lowest = LowestPrioritySlot();
            if (priority >= _slots[lowest].Priority)
            {
                _perIdCount[(int)_slots[lowest].Id]--;
                _perIdCount[ordinal]++;
                Stamp(lowest, id, priority);
                voiceId = lowest;
                stolenVoiceId = lowest;
                return true;
            }

            voiceId = -1;
            stolenVoiceId = -1;
            return false;
        }

        public void Release(int voiceId)
        {
            if (voiceId < 0 || voiceId >= _slots.Length || !_slots[voiceId].Active)
            {
                return;
            }

            _perIdCount[(int)_slots[voiceId].Id]--;
            _slots[voiceId].Active = false;
            _activeCount--;
            _free.Push(voiceId);
        }

        public void Clear()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = default;
            }

            for (var i = 0; i < _perIdCount.Length; i++)
            {
                _perIdCount[i] = 0;
            }

            _free.Clear();
            for (var i = _slots.Length - 1; i >= 0; i--)
            {
                _free.Push(i);
            }

            _sequence = 0;
            _activeCount = 0;
        }

        public int ActiveCountFor(GameSoundId id)
        {
            return _perIdCount[(int)id];
        }

        private void Stamp(int slot, GameSoundId id, int priority)
        {
            _slots[slot].Active = true;
            _slots[slot].Id = id;
            _slots[slot].Priority = priority;
            _slots[slot].Sequence = _sequence++;
        }

        private int OldestSlotFor(GameSoundId id)
        {
            var best = -1;
            var bestSequence = long.MaxValue;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Active && _slots[i].Id == id && _slots[i].Sequence < bestSequence)
                {
                    bestSequence = _slots[i].Sequence;
                    best = i;
                }
            }

            return best;
        }

        private int LowestPrioritySlot()
        {
            var best = -1;
            var bestPriority = int.MaxValue;
            var bestSequence = long.MaxValue;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                {
                    continue;
                }

                if (_slots[i].Priority < bestPriority
                    || (_slots[i].Priority == bestPriority && _slots[i].Sequence < bestSequence))
                {
                    bestPriority = _slots[i].Priority;
                    bestSequence = _slots[i].Sequence;
                    best = i;
                }
            }

            return best;
        }

        private struct Slot
        {
            public bool Active;
            public GameSoundId Id;
            public int Priority;
            public long Sequence;
        }
    }
}
