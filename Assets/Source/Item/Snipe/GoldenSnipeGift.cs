using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Teaches the golden balloon, which the shot's ordinary hit bounces off and only piercing pops: once the
    ///     run's first golden balloon has turned away <see cref="IAssistSettings.FirstGoldenSnipeAfterDeflects" />
    ///     hits, the next ball loaded is a Snipe lance whatever the Sharpshooter card's chance
    ///     (<see cref="ISnipeGuarantee" />). Once a run: a retry keeps it given.
    /// </summary>
    internal sealed class GoldenSnipeGift : IStartable, IRunResettable, IDisposable
    {
        private readonly ISubscriber<ActorHitMessage> _hits;
        private readonly ISnipeGuarantee _guarantee;
        private readonly IAssistSettings _settings;
        private readonly IRetryState _retryState;

        private IDisposable _subscription;
        // The first golden balloon hit this attempt, the only one counted; a later one is met already knowing.
        private ISlotActor _first;
        private int _deflects;
        private bool _given;

        // Before RetryTracker (Score + 10) consumes the staged retry level this reads.
        public int ResetOrder => RunResetOrder.Counters;

        internal GoldenSnipeGift(
            ISubscriber<ActorHitMessage> hits, ISnipeGuarantee guarantee, IAssistSettings settings,
            IRetryState retryState)
        {
            _hits = hits;
            _guarantee = guarantee;
            _settings = settings;
            _retryState = retryState;
        }

        public void Start()
        {
            _subscription = _hits.Subscribe(OnHit);
        }

        public void ResetRun(int generation)
        {
            if (_retryState.RetryLevel <= 0)
            {
                _given = false;
            }

            _first = null;
            _deflects = 0;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnHit(ActorHitMessage message)
        {
            var needed = _settings.FirstGoldenSnipeAfterDeflects;
            if (_given || needed <= 0 || (message.Outcome & HitOutcome.Deflect) == 0
                || message.Actor is not IBalloonModel { TypeName: BalloonType.Golden })
            {
                return;
            }

            _first ??= message.Actor;
            if (!ReferenceEquals(message.Actor, _first) || ++_deflects < needed)
            {
                return;
            }

            _given = true;
            _guarantee.GuaranteeNext();
        }
    }
}
