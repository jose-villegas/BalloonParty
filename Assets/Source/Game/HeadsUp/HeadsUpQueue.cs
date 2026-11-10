using System;
using BalloonParty.Configuration.HeadsUp;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     Which heads-ups get a place on screen. A request is checked against its style's argument range, then
    ///     rewrites a showing line of its style and merge key, then waits out its style's cooldown, then takes a
    ///     free slot, or evicts the oldest of the lowest priority showing if that is lower than its own, or equal and
    ///     shown at an earlier moment, or is dropped. A rewrite makes its line the newest. Held styles (the cannon's ready) have a lane of their own outside the cap. Plain C#: the time
    ///     is passed in.
    /// </summary>
    internal sealed class HeadsUpQueue
    {
        // One per held style that can show at once; ready is the only one today.
        private const int HeldLaneSize = 2;

        private readonly IHeadsUpSettings _settings;
        private readonly Slot[] _slots;
        private readonly float[] _lastShown;
        private readonly int _capacity;
        private long _sequence;

        /// <summary>Lines showing, held ones included.</summary>
        internal int ShowingCount
        {
            get
            {
                var count = 0;
                foreach (var slot in _slots)
                {
                    count += slot.Occupied ? 1 : 0;
                }

                return count;
            }
        }

        internal HeadsUpQueue(IHeadsUpSettings settings)
        {
            _settings = settings;
            _capacity = Math.Max(1, settings.MaxConcurrent);
            _slots = new Slot[_capacity + HeldLaneSize];
            _lastShown = new float[HeadsUpStyleIds.Count];
            Array.Fill(_lastShown, float.NegativeInfinity);
        }

        /// <summary>Decides <paramref name="request" /> at time <paramref name="now" /> (seconds).</summary>
        internal HeadsUpOffer Offer(in HeadsUpRequest request, float now)
        {
            var style = _settings.Style(request.Style);
            if (style == null || !style.Accepts(request.Arg))
            {
                return HeadsUpOffer.Dropped;
            }

            // A rewritten line is as fresh as its latest number: the busiest streak mustn't be the first evicted.
            if (TryFindShowing(request.Style, request.MergeKey, style.Held, out var showing))
            {
                _slots[showing].Order = ++_sequence;
                _slots[showing].ShownAt = now;
                return HeadsUpOffer.Rewrite(HandleOf(showing));
            }

            if (style.CooldownSeconds > 0f && now - _lastShown[(int)request.Style] < style.CooldownSeconds)
            {
                return HeadsUpOffer.Dropped;
            }

            var evicted = HeadsUpHandle.None;
            var slot = style.Held ? FreeSlot(_capacity, _slots.Length) : FreeSlot(0, _capacity);
            if (slot < 0 && !style.Held)
            {
                slot = Victim();
                if (!MayEvict(_slots[slot], style.Priority, now))
                {
                    return HeadsUpOffer.Dropped;
                }

                evicted = HandleOf(slot);
            }

            if (slot < 0)
            {
                return HeadsUpOffer.Dropped;
            }

            ref var taken = ref _slots[slot];
            taken.Occupied = true;
            taken.Generation++;
            taken.Style = request.Style;
            taken.Priority = style.Priority;
            taken.MergeKey = request.MergeKey;
            taken.Order = ++_sequence;
            taken.ShownAt = now;
            _lastShown[(int)request.Style] = now;
            return HeadsUpOffer.Show(HandleOf(slot), evicted);
        }

        /// <summary>Frees <paramref name="handle" />'s slot; false for a handle no longer showing.</summary>
        internal bool Free(HeadsUpHandle handle)
        {
            if (!IsShowing(handle))
            {
                return false;
            }

            _slots[handle.Slot].Occupied = false;
            return true;
        }

        internal bool IsShowing(HeadsUpHandle handle)
        {
            return handle.Slot >= 0 && handle.Slot < _slots.Length && _slots[handle.Slot].Occupied
                   && _slots[handle.Slot].Generation == handle.Generation;
        }

        // Generations aren't reset, so every handle given out before stays stale.
        internal void ClearAll()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i].Occupied = false;
            }
        }

        // A held style rewrites its own line whatever the key: there is one ready.
        private bool TryFindShowing(HeadsUpStyleId style, HeadsUpMergeKey key, bool held, out int index)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Occupied && slot.Style == style && (held || (key.IsSet && slot.MergeKey.Equals(key))))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private int FreeSlot(int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                if (!_slots[i].Occupied)
                {
                    return i;
                }
            }

            return -1;
        }

        // The lowest priority showing, the oldest of those.
        private int Victim()
        {
            var victim = 0;
            for (var i = 1; i < _capacity; i++)
            {
                var slot = _slots[i];
                var best = _slots[victim];
                if (slot.Priority < best.Priority || (slot.Priority == best.Priority && slot.Order < best.Order))
                {
                    victim = i;
                }
            }

            return victim;
        }

        // An equal may only take the place of a line from an earlier moment: a burst of equals in one frame would
        // otherwise show and evict each other before a stroke is drawn.
        private static bool MayEvict(in Slot victim, int priority, float now)
        {
            return victim.Priority < priority || (victim.Priority == priority && victim.ShownAt < now);
        }

        private HeadsUpHandle HandleOf(int slot)
        {
            return new HeadsUpHandle(slot, _slots[slot].Generation);
        }

        private struct Slot
        {
            internal bool Occupied;
            internal int Generation;
            internal HeadsUpStyleId Style;
            internal int Priority;
            internal HeadsUpMergeKey MergeKey;
            internal long Order;
            internal float ShownAt;
        }
    }

    internal enum HeadsUpOfferKind
    {
        Dropped,
        Show,
        Rewrite
    }

    /// <summary>The queue's answer: drop it, show it in a slot (evicting a line first, if any), or rewrite a line.</summary>
    internal readonly struct HeadsUpOffer
    {
        public static readonly HeadsUpOffer Dropped = new(HeadsUpOfferKind.Dropped, HeadsUpHandle.None, HeadsUpHandle.None);

        public readonly HeadsUpOfferKind Kind;
        public readonly HeadsUpHandle Handle;

        /// <summary>The line evicted to make room, or <see cref="HeadsUpHandle.None" />.</summary>
        public readonly HeadsUpHandle Evicted;

        private HeadsUpOffer(HeadsUpOfferKind kind, HeadsUpHandle handle, HeadsUpHandle evicted)
        {
            Kind = kind;
            Handle = handle;
            Evicted = evicted;
        }

        public static HeadsUpOffer Show(HeadsUpHandle handle, HeadsUpHandle evicted)
        {
            return new HeadsUpOffer(HeadsUpOfferKind.Show, handle, evicted);
        }

        public static HeadsUpOffer Rewrite(HeadsUpHandle handle)
        {
            return new HeadsUpOffer(HeadsUpOfferKind.Rewrite, handle, HeadsUpHandle.None);
        }
    }
}
