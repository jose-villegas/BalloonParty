using System;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    internal sealed class CombatSoundRouter : IStartable, IDisposable
    {
        private readonly ISoundPlayer _player;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileCruiseStartedMessage> _cruiseStartedSubscriber;
        private readonly ISubscriber<ProjectileCruiseEndedMessage> _cruiseEndedSubscriber;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedSubscriber;
        private readonly ISubscriber<PierceDischargedMessage> _pierceSubscriber;
        private readonly ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        private readonly ISubscriber<ShieldLostMessage> _shieldLostSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private SoundHandle _cruiseHandle = SoundHandle.None;

        [Inject]
        public CombatSoundRouter(ISoundPlayer player,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileCruiseStartedMessage> cruiseStartedSubscriber,
            ISubscriber<ProjectileCruiseEndedMessage> cruiseEndedSubscriber,
            ISubscriber<ProjectileDoomedStartedMessage> doomedSubscriber,
            ISubscriber<PierceDischargedMessage> pierceSubscriber,
            ISubscriber<ShieldGainedMessage> shieldGainedSubscriber,
            ISubscriber<ShieldLostMessage> shieldLostSubscriber)
        {
            _player = player;
            _hitSubscriber = hitSubscriber;
            _firedSubscriber = firedSubscriber;
            _loadedSubscriber = loadedSubscriber;
            _cruiseStartedSubscriber = cruiseStartedSubscriber;
            _cruiseEndedSubscriber = cruiseEndedSubscriber;
            _doomedSubscriber = doomedSubscriber;
            _pierceSubscriber = pierceSubscriber;
            _shieldGainedSubscriber = shieldGainedSubscriber;
            _shieldLostSubscriber = shieldLostSubscriber;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnFired).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(OnLoaded).AddTo(_subscriptions);
            _cruiseStartedSubscriber.Subscribe(OnCruiseStarted).AddTo(_subscriptions);
            _cruiseEndedSubscriber.Subscribe(OnCruiseEnded).AddTo(_subscriptions);
            _doomedSubscriber.Subscribe(OnDoomedStarted).AddTo(_subscriptions);
            _pierceSubscriber.Subscribe(OnPierceDischarged).AddTo(_subscriptions);
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(_subscriptions);
            _shieldLostSubscriber.Subscribe(OnShieldLost).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnActorHit(ActorHitMessage message)
        {
            var outcome = message.Outcome;
            if ((outcome & HitOutcome.Pop) != 0)
            {
                _player.Play(GameSoundId.BalloonPop, message.WorldPosition);
            }
            else if ((outcome & HitOutcome.Deflect) != 0)
            {
                _player.Play(GameSoundId.BalloonDeflect, message.WorldPosition);
            }
            else if ((outcome & (HitOutcome.Absorb | HitOutcome.PassThrough)) != 0)
            {
                _player.Play(GameSoundId.BalloonResist, message.WorldPosition);
            }
        }

        private void OnFired(ProjectileFiredMessage message)
        {
            _player.Play(GameSoundId.ShotFired, message.WorldPosition);
        }

        private void OnLoaded(ProjectileLoadedMessage message)
        {
            _player.Play(GameSoundId.ShotReload, null);
        }

        private void OnCruiseStarted(ProjectileCruiseStartedMessage message)
        {
            _cruiseHandle = _player.Play(GameSoundId.CruiseLoopStart, message.WorldPosition);
        }

        private void OnCruiseEnded(ProjectileCruiseEndedMessage message)
        {
            _player.Stop(_cruiseHandle);
            _cruiseHandle = SoundHandle.None;
        }

        private void OnDoomedStarted(ProjectileDoomedStartedMessage message)
        {
            _player.Play(GameSoundId.DoomedWarn, message.WorldPosition);
        }

        private void OnPierceDischarged(PierceDischargedMessage message)
        {
            _player.Play(GameSoundId.PierceDischarge, message.Center);
        }

        private void OnShieldGained(ShieldGainedMessage message)
        {
            _player.Play(GameSoundId.ShieldGained, null);
        }

        private void OnShieldLost(ShieldLostMessage message)
        {
            _player.Play(GameSoundId.ShieldLost, message.Position);
        }
    }
}
