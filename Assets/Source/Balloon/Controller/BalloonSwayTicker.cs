using System;
using System.Collections.Generic;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Plays the plain balloons' idle sway in one loop, in place of an Animator on each: a balloon's Animator
    ///     cost far more a frame than the one rotation it wrote. The sway itself is unchanged. Its curves and
    ///     crossfade times are baked from the Animator controller (<see cref="IBalloonIdleSwaySettings" />), and
    ///     <see cref="BalloonSwayPlayback" /> steps each balloon through them as that controller's state machine
    ///     did.
    ///     <para>
    ///         A balloon's view begins its sway as it spawns and ends it as it despawns or is handed to a
    ///         cinematic that owns its rotation, where it used to enable and disable its Animator.
    ///     </para>
    ///     <para>
    ///         An <see cref="ILateTickable" /> on scaled time: after everything <c>Update</c> spawned and before
    ///         any <c>LateUpdate</c>, which is where and on what clock the Animators wrote their pose.
    ///     </para>
    /// </summary>
    internal sealed class BalloonSwayTicker : ILateTickable
    {
        private const int InitialCapacity = 64;

        private readonly IBalloonIdleSwaySettings _settings;
        private readonly Dictionary<IBalloonSwayView, int> _slots = new(InitialCapacity);

        private Entry[] _entries = new Entry[InitialCapacity];
        private int _count;

        public BalloonSwayTicker(IBalloonsConfiguration balloonsConfig)
        {
            _settings = balloonsConfig.IdleSway;
        }

        public void LateTick()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>
        ///     Starts <paramref name="view" />'s sway from the beginning: the unstable loop from its start, until
        ///     <see cref="SetStable" /> says otherwise. A view already swaying starts over.
        /// </summary>
        internal void Begin(IBalloonSwayView view)
        {
            if (_slots.TryGetValue(view, out var slot))
            {
                _entries[slot].Playback = default;
                return;
            }

            if (_count == _entries.Length)
            {
                Array.Resize(ref _entries, _count * 2);
            }

            _entries[_count] = new Entry { View = view };
            _slots.Add(view, _count);
            _count++;
        }

        /// <summary>Which loop <paramref name="view" /> should play. Ignored for a view that isn't swaying.</summary>
        internal void SetStable(IBalloonSwayView view, bool stable)
        {
            if (_slots.TryGetValue(view, out var slot))
            {
                _entries[slot].Playback.Stable = stable;
            }
        }

        /// <summary>Stops <paramref name="view" />'s sway where it is: its pivot keeps the last pose written.</summary>
        internal void End(IBalloonSwayView view)
        {
            if (!_slots.Remove(view, out var slot))
            {
                return;
            }

            _count--;
            if (slot != _count)
            {
                _entries[slot] = _entries[_count];
                _slots[_entries[slot].View] = slot;
            }

            _entries[_count] = default;
        }

        // Internal so edit-mode tests can drive the ticker deterministically (Time.deltaTime isn't injectable).
        internal void Advance(float deltaTime)
        {
            for (var i = 0; i < _count; i++)
            {
                ref var entry = ref _entries[i];
                entry.View.ApplySway(entry.Playback.Advance(deltaTime, _settings));
            }
        }

        private struct Entry
        {
            public IBalloonSwayView View;
            public BalloonSwayPlayback Playback;
        }
    }
}
