using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
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
        private const int MaxRampSemitones = 12;

        private readonly ISoundPlayer _player;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileCruiseStartedMessage> _cruiseStartedSubscriber;
        private readonly ISubscriber<ProjectileCruiseEndedMessage> _cruiseEndedSubscriber;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<PierceDischargedMessage> _pierceSubscriber;
        private readonly ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        private readonly ISubscriber<ShieldLostMessage> _shieldLostSubscriber;
        private readonly ISubscriber<WallHitMessage> _wallHitSubscriber;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly CompositeDisposable _subscriptions = new();

        private SoundHandle _cruiseHandle = SoundHandle.None;
        private int _bounceCount;
        private int _rainbowPopsThisFlight;
        private int _toughPopsThisFlight;
        private int _unbreakablePopsThisFlight;

        [Inject]
        public CombatSoundRouter(ISoundPlayer player,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileCruiseStartedMessage> cruiseStartedSubscriber,
            ISubscriber<ProjectileCruiseEndedMessage> cruiseEndedSubscriber,
            ISubscriber<ProjectileDoomedStartedMessage> doomedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            ISubscriber<PierceDischargedMessage> pierceSubscriber,
            ISubscriber<ShieldGainedMessage> shieldGainedSubscriber,
            ISubscriber<ShieldLostMessage> shieldLostSubscriber,
            ISubscriber<WallHitMessage> wallHitSubscriber,
            IProjectileFlightConfig flightConfig)
        {
            _player = player;
            _hitSubscriber = hitSubscriber;
            _firedSubscriber = firedSubscriber;
            _loadedSubscriber = loadedSubscriber;
            _cruiseStartedSubscriber = cruiseStartedSubscriber;
            _cruiseEndedSubscriber = cruiseEndedSubscriber;
            _doomedSubscriber = doomedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _pierceSubscriber = pierceSubscriber;
            _shieldGainedSubscriber = shieldGainedSubscriber;
            _shieldLostSubscriber = shieldLostSubscriber;
            _wallHitSubscriber = wallHitSubscriber;
            _flightConfig = flightConfig;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnFired).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(OnLoaded).AddTo(_subscriptions);
            _cruiseStartedSubscriber.Subscribe(OnCruiseStarted).AddTo(_subscriptions);
            _cruiseEndedSubscriber.Subscribe(OnCruiseEnded).AddTo(_subscriptions);
            _doomedSubscriber.Subscribe(OnDoomedStarted).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(OnProjectileDestroyed).AddTo(_subscriptions);
            _pierceSubscriber.Subscribe(OnPierceDischarged).AddTo(_subscriptions);
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(_subscriptions);
            _shieldLostSubscriber.Subscribe(OnShieldLost).AddTo(_subscriptions);
            _wallHitSubscriber.Subscribe(OnWallHit).AddTo(_subscriptions);
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
                _bounceCount = 0;
                var popId = message.Actor is IBalloonModel balloon
                    ? PopSoundFor(balloon.TypeName)
                    : GameSoundId.BalloonPop;

                var offset = PopSemitoneOffset(popId);
                _player.Play(popId, message.WorldPosition, semitoneOffset: offset);
                IncrementPopCounter(popId);
            }
            else if ((outcome & HitOutcome.Deflect) != 0)
            {
                var deflectId = message.Actor is IBalloonModel balloon
                    ? DeflectSoundFor(balloon.TypeName)
                    : GameSoundId.BalloonDeflect;

                if (message.Actor is IHasDurability durability)
                {
                    var hitsLost = durability.MaxHitPoints - durability.HitsRemaining.Value;
                    var offset = -(hitsLost - 1) * MusicalPitchExtensions.WholeToneSemitones;
                    _player.Play(deflectId, message.WorldPosition, semitoneOffset: offset);
                }
                else
                {
                    _player.Play(deflectId, message.WorldPosition, semitoneOffset: -_bounceCount);
                    _bounceCount++;
                }
            }
            else if ((outcome & (HitOutcome.Absorb | HitOutcome.PassThrough)) != 0)
            {
                _bounceCount = 0;
                _player.Play(GameSoundId.BalloonResist, message.WorldPosition);
            }
        }

        // Simple balloons use the melodic BalloonPop; specific kinds get their own pop id (with their
        // own SfxEntry/MelodicMode). Default keeps any unmapped/future type on BalloonPop, never silent.
        private static GameSoundId PopSoundFor(BalloonType type)
        {
            return type switch
            {
                BalloonType.Tough => GameSoundId.BalloonPopTough,
                BalloonType.Rainbow => GameSoundId.BalloonPopRainbow,
                BalloonType.Unbreakable => GameSoundId.BalloonPopUnbreakable,
                BalloonType.SimpleSilver => GameSoundId.BalloonPopSilver,
                BalloonType.SimpleGold => GameSoundId.BalloonPopGold,
                _ => GameSoundId.BalloonPop,
            };
        }

        // Unbreakables get their own deflect cue; everything else uses the generic deflect.
        private static GameSoundId DeflectSoundFor(BalloonType type)
        {
            return type switch
            {
                BalloonType.Unbreakable => GameSoundId.BalloonDeflectUnbreakable,
                _ => GameSoundId.BalloonDeflect,
            };
        }

        private void OnFired(ProjectileFiredMessage message)
        {
            _bounceCount = 0;
            _player.Play(GameSoundId.ShotFired, message.WorldPosition);
        }

        private void OnLoaded(ProjectileLoadedMessage message)
        {
            _rainbowPopsThisFlight = 0;
            _toughPopsThisFlight = 0;
            _unbreakablePopsThisFlight = 0;
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

        private void OnProjectileDestroyed(ProjectileDestroyedMessage message)
        {
            _player.Play(GameSoundId.ProjectileDeath, null);
            var depth = _flightConfig.ShieldToneThreshold;
            _player.Play(GameSoundId.WallHit, null, melodicStreak: depth, semitoneOffset: -MusicalPitchExtensions.TritoneSemitones, volumeScale: 5f);
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

        private void OnWallHit(WallHitMessage message)
        {
            var depth = Math.Max(0, _flightConfig.ShieldToneThreshold - message.ShieldsRemaining);
            var offset = 0;

            if (_bounceCount > 0)
            {
                offset = -_bounceCount;
                _bounceCount++;
            }

            _player.Play(GameSoundId.WallHit, message.Position, depth, semitoneOffset: offset);
        }

        private int PopSemitoneOffset(GameSoundId popId)
        {
            var raw = popId switch
            {
                GameSoundId.BalloonPopRainbow => _rainbowPopsThisFlight * MusicalPitchExtensions.WholeToneSemitones,
                GameSoundId.BalloonPopTough => _toughPopsThisFlight * MusicalPitchExtensions.WholeToneSemitones,
                GameSoundId.BalloonPopUnbreakable => _unbreakablePopsThisFlight * MusicalPitchExtensions.WholeToneSemitones,
                _ => 0
            };

            return Math.Min(raw, MaxRampSemitones);
        }

        private void IncrementPopCounter(GameSoundId popId)
        {
            switch (popId)
            {
                case GameSoundId.BalloonPopRainbow:
                    _rainbowPopsThisFlight++;
                    break;
                case GameSoundId.BalloonPopTough:
                    _toughPopsThisFlight++;
                    break;
                case GameSoundId.BalloonPopUnbreakable:
                    _unbreakablePopsThisFlight++;
                    break;
            }
        }
    }
}
