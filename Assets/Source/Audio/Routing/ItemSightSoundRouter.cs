using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Item;
using BalloonParty.Shared.Extensions;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    /// <summary>
    ///     Plays a rising chime run once the aim's prediction line settles on a "chain" of
    ///     item-hosting balloons — every item host <c>ItemSightTracker</c> currently reports, in the
    ///     order it reports them (first-to-last along the trace). Gated on
    ///     <c>IItemPreviewConfig.SightSoundSettleSeconds</c>, the same settle-before-reacting shape
    ///     <c>SightDelaySeconds</c> already uses for the visual telegraph: a fast sweep across several
    ///     items stays silent until the aim actually stops, rather than retriggering on every
    ///     transient frame the raw sighted chain happens to take on its way there. Whenever the
    ///     SETTLED chain then changes — a new item joins the path, one drops off the end, or the aim
    ///     settles on an entirely different chain — the whole thing replays from its first item
    ///     through to its last as an ascending run, so a chain that grew reads as one continuously
    ///     re-confirmed phrase rather than a single new note tacked onto an old one. Unlike every
    ///     other router in this folder, this one doesn't translate an existing MessagePipe message —
    ///     nothing publishes "a balloon was sighted" today — so it reads <c>ItemSightTracker</c>'s
    ///     already-detected chain every `Tick` instead of subscribing.
    /// </summary>
    internal sealed class ItemSightSoundRouter : ILateTickable
    {
        private const int MaxRampSemitones = 24;

        private readonly ItemSightTracker _tracker;
        private readonly ISoundPlayer _player;
        private readonly IItemPreviewConfig _config;

        private readonly List<Vector2Int> _candidateChainSlots = new();
        private readonly List<Vector2Int> _currentChainSlots = new();
        private readonly Queue<Vector3> _pending = new();
        private readonly List<SoundHandle> _activeHandles = new();

        private float _settleElapsed;
        private float _staggerTimer;
        private int _streakIndex;

        [Inject]
        public ItemSightSoundRouter(ItemSightTracker tracker, ISoundPlayer player, IItemPreviewConfig config)
        {
            _tracker = tracker;
            _player = player;
            _config = config;
        }

        public void LateTick()
        {
            UpdateSettleAndTrigger(Time.deltaTime);
            DrainPending(Time.deltaTime);
        }

        // The pitch step for the Nth note (0-indexed) in one replay of a chain: a whole tone higher
        // than the last, capped so an unusually long chain can't run the pitch off the top of the
        // audible range.
        internal static int SemitoneOffsetForStreak(int streakIndex)
        {
            return Math.Min(streakIndex * MusicalPitchExtensions.WholeToneSemitones, MaxRampSemitones);
        }

        // Runs every Tick, independent of ItemSightTracker's own version gate, since a settle timer
        // measures wall time against a HELD aim — exactly the stretch where the tracker's underlying
        // trace stops changing and its own grid walk stops running at all. Three outcomes: nothing
        // sighted cuts the cue dead immediately (no settle delay on the way OUT, only on the way in);
        // the raw chain just changed, so the settle timer restarts against the new candidate; or the
        // candidate has held long enough and differs from what's currently playing, so it replays
        // whole.
        private void UpdateSettleAndTrigger(float deltaTime)
        {
            var sighted = _tracker.Sighted;

            if (sighted.Count == 0)
            {
                if (_currentChainSlots.Count > 0 || _candidateChainSlots.Count > 0)
                {
                    _pending.Clear();
                    StopAllActive();
                    _streakIndex = 0;
                    _currentChainSlots.Clear();
                    _candidateChainSlots.Clear();
                    _settleElapsed = 0f;
                }

                return;
            }

            if (!ItemSightTracker.ChainMatches(sighted, _candidateChainSlots))
            {
                _candidateChainSlots.Clear();
                for (var i = 0; i < sighted.Count; i++)
                {
                    _candidateChainSlots.Add(sighted[i].Slot);
                }

                _settleElapsed = 0f;
                return;
            }

            _settleElapsed += deltaTime;
            if (_settleElapsed < _config.SightSoundSettleSeconds
                || ItemSightTracker.ChainMatches(sighted, _currentChainSlots))
            {
                return;
            }

            // Settled on a chain that isn't what's currently playing (or played last) — cut whatever
            // that previous chain's run was still doing and replay the WHOLE new one from its first
            // host, so a chain that just grew doesn't add a single new note onto the old run, it
            // replays start to end again, one host longer.
            _pending.Clear();
            StopAllActive();
            _streakIndex = 0;

            _currentChainSlots.Clear();
            for (var i = 0; i < sighted.Count; i++)
            {
                _currentChainSlots.Add(sighted[i].Slot);
                _pending.Enqueue(sighted[i].Position);
            }
        }

        // Plays at most one note per elapsed stagger interval rather than all at once — the timer is
        // left wherever it stopped while the queue is empty (never allowed to drift further negative),
        // so a fresh chain's replay always starts immediately, with only the LATER notes in that same
        // replay waiting their turn. Every handle played is tracked so StopAllActive can cut a replay
        // dead the moment the chain changes again or the aim leaves every item (see
        // UpdateSettleAndTrigger), not just stop queuing new notes and let the already-playing ones
        // ring out on their own.
        private void DrainPending(float deltaTime)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            _staggerTimer -= deltaTime;
            while (_staggerTimer <= 0f && _pending.Count > 0)
            {
                var position = _pending.Dequeue();
                var handle = _player.Play(
                    GameSoundId.ItemSighted, position, semitoneOffset: SemitoneOffsetForStreak(_streakIndex));
                PruneDeadHandles();
                _activeHandles.Add(handle);
                _streakIndex++;
                _staggerTimer += _config.SightSoundStaggerSeconds;
            }
        }

        // Run right before a new handle joins the list, not on some separate timer — the list only
        // ever holds a handful of concurrent notes, so a pass here costs nothing and keeps it from
        // growing across an arbitrarily long session of near-continuous item sighting.
        private void PruneDeadHandles()
        {
            for (var i = _activeHandles.Count - 1; i >= 0; i--)
            {
                if (!_player.IsAlive(_activeHandles[i]))
                {
                    _activeHandles.RemoveAt(i);
                }
            }
        }

        private void StopAllActive()
        {
            for (var i = 0; i < _activeHandles.Count; i++)
            {
                _player.Stop(_activeHandles[i]);
            }

            _activeHandles.Clear();
        }
    }
}
