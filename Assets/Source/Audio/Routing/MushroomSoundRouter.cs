using System;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    /// <summary>
    ///     The meadow's mushrooms in sound (@ref plan_meadow_mushrooms): a soft bump on every bounce, a step higher
    ///     with each wall of caps the chain counts, the Snipe's own cue when a chain pays the lance, a stroke
    ///     for each cap a lance cuts, a step higher with each cap the flight has cut, and the Snipe's cue once
    ///     more, a step up, for the first charge a flight's cuts bank it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The bump is the mushrooms' own</b> (<see cref="BounceSound" />, <c>MushroomBounce</c>): a short
    ///         rubbery clip made for it, with its own entry in the sound bank. So the entry's own limits are the
    ///         bump's and nobody else's: four voices at once, the fifth ending the oldest bump, and the sound
    ///         service's burst gate. A ball can bounce every 80 ms or so and the clip is 130 ms long, so two or
    ///         three bumps overlap at the most, and this router keeps no voices of its own.
    ///     </para>
    ///     <para>
    ///         The other two are still borrowed. The reward plays what a Snipe pickup plays, so a lance earned off
    ///         the mushrooms is announced as any lance is; the pierce loop that follows is
    ///         <c>CombatSoundRouter</c>'s, off the ball's own piercing state. The cut borrows the strikethrough's
    ///         stroke (<see cref="CutSound" />), the one sound in the bank that is a blade's pass: a lance cuts a
    ///         handful of caps a flight at the most, each once.
    ///     </para>
    ///     <para>
    ///         <b>The bank</b> (decision K: every cap a lance cuts banks its flight a charge). A lance through a
    ///         wall of caps banks three charges within a few frames, so the Snipe's cue is not played for each:
    ///         it rings once a flight, on the first charge (<see cref="MushroomCutMessage.Charges" /> is 1), a
    ///         whole tone above the pitch an arming plays it at, which is what a banked charge rang before the
    ///         bank was dropped. Every charge is heard in its cut instead: the stroke climbs a whole tone with
    ///         each cap the flight has cut, up to <see cref="MaxCutSteps" />.
    ///     </para>
    ///     <para>
    ///         <b>A blast plays nothing here.</b> A cap a bomb or a nuke blows off
    ///         (<see cref="MushroomCutCause.Blast" />) is heard in the blast's own sound: a bomb taking four caps
    ///         would otherwise play four strokes at once. A sway is silent too.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomSoundRouter : IStartable, IDisposable
    {
        /// <summary>The cue a bounce plays: the mushrooms' own bump.</summary>
        internal const GameSoundId BounceSound = GameSoundId.MushroomBounce;

        /// <summary>How loud the bump is against its clip at full: a bounce costs nothing and happens often, so
        /// it sits under the pops.</summary>
        internal const float BounceVolume = 0.6f;

        /// <summary>The cue a cap a lance cut plays. A stand-in: the heart's strikethrough stroke.</summary>
        internal const GameSoundId CutSound = GameSoundId.Strikethrough;

        /// <summary>How loud the cut is against the stroke it borrows.</summary>
        internal const float CutVolume = 0.8f;

        /// <summary>The most steps the cut's stroke climbs: one for each cap after the first that a flight has
        /// cut, so a board's seven caps each have their note, an octave from the first to the last.</summary>
        internal const int MaxCutSteps = 6;

        /// <summary>The cue of the first charge a flight banks by cutting a cap: the Snipe's own.</summary>
        internal const GameSoundId BankSound = GameSoundId.ItemSnipe;

        /// <summary>The most steps the bump climbs: a ball that keeps bouncing after its chain's count has
        /// stopped rising holds the top note.</summary>
        internal const int MaxBounceSteps = 4;

        private readonly ISoundPlayer _player;
        private readonly ISubscriber<MushroomBouncedMessage> _bouncedSubscriber;
        private readonly ISubscriber<MushroomLanceMessage> _lanceSubscriber;
        private readonly ISubscriber<MushroomCutMessage> _cutSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        [Inject]
        public MushroomSoundRouter(ISoundPlayer player,
            ISubscriber<MushroomBouncedMessage> bouncedSubscriber,
            ISubscriber<MushroomLanceMessage> lanceSubscriber,
            ISubscriber<MushroomCutMessage> cutSubscriber)
        {
            _player = player;
            _bouncedSubscriber = bouncedSubscriber;
            _lanceSubscriber = lanceSubscriber;
            _cutSubscriber = cutSubscriber;
        }

        public void Start()
        {
            _bouncedSubscriber.Subscribe(OnBounced).AddTo(_subscriptions);
            _lanceSubscriber.Subscribe(OnLance).AddTo(_subscriptions);
            _cutSubscriber.Subscribe(OnCut).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // The chain's count is the note: the first wall of caps bumps at the root and each one counted after it a
        // whole tone higher, so a chain is heard climbing toward the lance. A bounce that did not count (the
        // same wall twice running) repeats its note, and a wall of the board starts the climb over with the count.
        private void OnBounced(MushroomBouncedMessage message)
        {
            var steps = Mathf.Clamp(message.Count - 1, 0, MaxBounceSteps);
            _player.Play(BounceSound, message.Point,
                semitoneOffset: steps * MusicalPitchExtensions.WholeToneSemitones, volumeScale: BounceVolume);
        }

        private void OnLance(MushroomLanceMessage message)
        {
            _player.Play(GameSoundId.ItemSnipe, null);
        }

        // The count of caps the flight has cut is the stroke's note, as the chain's count is the bump's. A cut
        // no flight made (Charges 0) plays at the root and banks nothing.
        private void OnCut(MushroomCutMessage message)
        {
            if (message.Cause != MushroomCutCause.Lance)
            {
                return;
            }

            var steps = Mathf.Clamp(message.Charges - 1, 0, MaxCutSteps);
            _player.Play(CutSound, (Vector3)message.Point,
                semitoneOffset: steps * MusicalPitchExtensions.WholeToneSemitones, volumeScale: CutVolume);
            if (message.Charges == 1)
            {
                _player.Play(BankSound, null, semitoneOffset: MusicalPitchExtensions.WholeToneSemitones);
            }
        }
    }
}
