using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Flight;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
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
        private readonly ISubscriber<ThrowerReadyMessage> _readySubscriber;
        private readonly ISubscriber<ProjectileCruiseStartedMessage> _cruiseStartedSubscriber;
        private readonly ISubscriber<ProjectileCruiseEndedMessage> _cruiseEndedSubscriber;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<PierceDischargedMessage> _pierceSubscriber;
        private readonly ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        private readonly ISubscriber<ShieldLostMessage> _shieldLostSubscriber;
        private readonly ISubscriber<WallHitMessage> _wallHitSubscriber;
        private readonly ISubscriber<SpeedTapMintedMessage> _speedTapSubscriber;
        private readonly ISubscriber<StreakChangedMessage> _streakChangedSubscriber;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IActiveProjectilePierce _activePierce;
        private readonly IFlightStatsSource _flightStats;
        private readonly IProjectileRegistry _projectiles;
        private readonly IGamePalette _palette;
        private readonly CompositeDisposable _subscriptions = new();
        // Same-colour pops per ball, each ball climbing its own ramp. Inner maps are pooled so a ball
        // per shot allocates nothing once warm; pops no ball caused share the unattributed map.
        private readonly Dictionary<IProjectileModel, Dictionary<string, int>> _colorPopsByBall = new();
        private readonly Stack<Dictionary<string, int>> _spareColorPops = new();
        private readonly Dictionary<string, int> _unattributedColorPops = new();
        private readonly Dictionary<IProjectileModel, SoundHandle> _cruiseHandles = new();

        private SoundHandle _pierceLoopHandle = SoundHandle.None;

        [Inject]
        public CombatSoundRouter(ISoundPlayer player,
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ThrowerReadyMessage> readySubscriber,
            ISubscriber<ProjectileCruiseStartedMessage> cruiseStartedSubscriber,
            ISubscriber<ProjectileCruiseEndedMessage> cruiseEndedSubscriber,
            ISubscriber<ProjectileDoomedStartedMessage> doomedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            ISubscriber<PierceDischargedMessage> pierceSubscriber,
            ISubscriber<ShieldGainedMessage> shieldGainedSubscriber,
            ISubscriber<ShieldLostMessage> shieldLostSubscriber,
            ISubscriber<WallHitMessage> wallHitSubscriber,
            ISubscriber<SpeedTapMintedMessage> speedTapSubscriber,
            ISubscriber<StreakChangedMessage> streakChangedSubscriber,
            IProjectileFlightConfig flightConfig,
            IActiveProjectilePierce activePierce,
            IFlightStatsSource flightStats,
            IProjectileRegistry projectiles,
            IGamePalette palette)
        {
            _player = player;
            _hitSubscriber = hitSubscriber;
            _firedSubscriber = firedSubscriber;
            _loadedSubscriber = loadedSubscriber;
            _readySubscriber = readySubscriber;
            _cruiseStartedSubscriber = cruiseStartedSubscriber;
            _cruiseEndedSubscriber = cruiseEndedSubscriber;
            _doomedSubscriber = doomedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _pierceSubscriber = pierceSubscriber;
            _shieldGainedSubscriber = shieldGainedSubscriber;
            _shieldLostSubscriber = shieldLostSubscriber;
            _wallHitSubscriber = wallHitSubscriber;
            _speedTapSubscriber = speedTapSubscriber;
            _streakChangedSubscriber = streakChangedSubscriber;
            _flightConfig = flightConfig;
            _activePierce = activePierce;
            _flightStats = flightStats;
            _projectiles = projectiles;
            _palette = palette;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnFired).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(OnLoaded).AddTo(_subscriptions);

            // The "cock" plays when a release would fire, not at the load: the next ball loads the
            // instant a shot leaves, so a cue there would only echo the shot.
            _readySubscriber.Subscribe(_ => _player.Play(GameSoundId.ShotReload, null)).AddTo(_subscriptions);
            _cruiseStartedSubscriber.Subscribe(OnCruiseStarted).AddTo(_subscriptions);
            _cruiseEndedSubscriber.Subscribe(OnCruiseEnded).AddTo(_subscriptions);
            _doomedSubscriber.Subscribe(OnDoomedStarted).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(OnProjectileDestroyed).AddTo(_subscriptions);
            _pierceSubscriber.Subscribe(OnPierceDischarged).AddTo(_subscriptions);
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(_subscriptions);
            _shieldLostSubscriber.Subscribe(OnShieldLost).AddTo(_subscriptions);
            _wallHitSubscriber.Subscribe(OnWallHit).AddTo(_subscriptions);
            _speedTapSubscriber.Subscribe(OnSpeedTapMinted).AddTo(_subscriptions);
            _streakChangedSubscriber.Subscribe(OnStreakChanged).AddTo(_subscriptions);
            _activePierce.IsPiercing.Subscribe(OnPierceStateChanged).AddTo(_subscriptions);

            // A ball scaled away by game over or a reset publishes neither CruiseEnded nor Destroyed.
            _projectiles.Retired.Subscribe(OnRetired).AddTo(_subscriptions);
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
                var popId = message.Actor is IBalloonModel balloon
                    ? PopSoundFor(balloon.TypeName)
                    : GameSoundId.BalloonPop;

                if (popId == GameSoundId.BalloonPop && message.Actor is IHasColor colored)
                {
                    var color = colored.Color.Value;
                    var colorPops = ColorPopsOf(message.Context.Projectile);
                    colorPops.TryGetValue(color, out var count);
                    var rootOffset = MusicalPitchExtensions.ColorRootOffset(_palette.ProgressColorNames, color);
                    var ramp = Math.Min(count * MusicalPitchExtensions.WholeToneSemitones, MaxRampSemitones);
                    _player.Play(popId, message.WorldPosition, semitoneOffset: rootOffset + ramp);
                    colorPops[color] = count + 1;
                }
                else
                {
                    var offset = PopSemitoneOffset(popId, message.Actor, message.Context.Projectile);
                    _player.Play(popId, message.WorldPosition, semitoneOffset: offset);
                }
            }
            else if ((outcome & HitOutcome.Deflect) != 0)
            {
                var deflectId = message.Actor is IBalloonModel balloon
                    ? DeflectSoundFor(balloon.TypeName)
                    : GameSoundId.BalloonDeflect;

                if (message.Actor is IHasDurability durability)
                {
                    var hitsLost = durability.MaxHitPoints - durability.HitsRemaining.Value;
                    var offset = -(hitsLost - 1);
                    _player.Play(deflectId, message.WorldPosition, semitoneOffset: offset);
                }
                else if (deflectId is GameSoundId.BalloonDeflectUnbreakable or GameSoundId.BalloonDeflectGolden)
                {
                    // Clamped: a deflect no ball caused (a cheat) isn't counted, and would play below the root.
                    // Each type climbs on its own deflects.
                    var type = message.Actor is IBalloonModel deflected
                        ? deflected.TypeName
                        : BalloonType.Unbreakable;
                    var step = Math.Max(0, _flightStats.Of(message.Context.Projectile).DeflectsOf(type) - 1);
                    _player.Play(deflectId, message.WorldPosition, semitoneOffset: step);
                }
                else
                {
                    _player.Play(deflectId, message.WorldPosition);
                }
            }
            else if ((outcome & (HitOutcome.Absorb | HitOutcome.PassThrough)) != 0)
            {
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
                BalloonType.Tougher => GameSoundId.BalloonPopTougher,
                BalloonType.Rainbow => GameSoundId.BalloonPopRainbow,
                BalloonType.Unbreakable => GameSoundId.BalloonPopUnbreakable,
                BalloonType.Golden => GameSoundId.BalloonPopGolden,
                BalloonType.Heart => GameSoundId.BalloonPopHeart,
                BalloonType.SimpleSilver => GameSoundId.BalloonPopSilver,
                BalloonType.SimpleGold => GameSoundId.BalloonPopGold,
                _ => GameSoundId.BalloonPop,
            };
        }

        // Unbreakables get the metallic deflect, the plated golden balloon its own clink; the generic one otherwise.
        private static GameSoundId DeflectSoundFor(BalloonType type)
        {
            return type switch
            {
                BalloonType.Tough => GameSoundId.BalloonDeflectTough,
                BalloonType.Tougher => GameSoundId.BalloonDeflectTougher,
                BalloonType.Unbreakable => GameSoundId.BalloonDeflectUnbreakable,
                BalloonType.Golden => GameSoundId.BalloonDeflectGolden,
                _ => GameSoundId.BalloonDeflect,
            };
        }

        private void OnFired(ProjectileFiredMessage message)
        {
            _player.Play(GameSoundId.ShotFired, message.WorldPosition);
        }

        // The type/deflect/pierce/bounce counters live in FlightStatsService and reset with the flight
        // there. The colour maps stay local: they also clear on a streak break, so each ball's scope is
        // "since the later of its flight start and its last broken streak" — a pitch cursor, not a
        // statistic. A load only resets the unattributed map; each ball's map starts empty.
        private void OnLoaded(ProjectileLoadedMessage message)
        {
            _unattributedColorPops.Clear();
        }

        // Keyed by shot: one ball's cruise ending must not cut another's loop.
        private void OnCruiseStarted(ProjectileCruiseStartedMessage message)
        {
            StopCruise(message.Projectile);
            _cruiseHandles[message.Projectile] = _player.Play(GameSoundId.CruiseLoopStart, message.WorldPosition);
        }

        private void OnCruiseEnded(ProjectileCruiseEndedMessage message)
        {
            StopCruise(message.Projectile);
        }

        private void OnDoomedStarted(ProjectileDoomedStartedMessage message)
        {
            _player.Play(GameSoundId.DoomedWarn, message.WorldPosition);
        }

        private void OnProjectileDestroyed(ProjectileDestroyedMessage message)
        {
            StopCruise(message.Projectile);
            _player.Play(GameSoundId.ProjectileDeath, null);
            var depth = _flightConfig.ShieldToneThreshold;
            _player.Play(GameSoundId.WallHit, null, melodicStreak: depth, semitoneOffset: -MusicalPitchExtensions.TritoneSemitones, volumeScale: 5f);
        }

        private void OnPierceDischarged(PierceDischargedMessage message)
        {
            var step = _flightStats.Of(message.Projectile).PierceDischarges - 1;
            var offset = step * MusicalPitchExtensions.WholeToneSemitones;
            _player.Play(GameSoundId.PierceDischarge, message.Center, semitoneOffset: offset);
        }

        private void OnShieldGained(ShieldGainedMessage message)
        {
            _player.Play(GameSoundId.ShieldGained, null);
        }

        private void OnShieldLost(ShieldLostMessage message)
        {
            _player.Play(GameSoundId.ShieldLost, message.Position);
        }

        // Each earned tap is one rung of the speed ladder, so the cue climbs with the tap count: authored
        // as MelodicMode.ScaleWalkUp, the count IS the melodic step (VariationPicker walks the scale from
        // it). Every rung sounds a degree higher than the last, and a fresh shot starts from the root
        // because the count resets with the projectile.
        private void OnSpeedTapMinted(SpeedTapMintedMessage message)
        {
            _player.Play(GameSoundId.SpeedTap, message.Position, message.TotalTaps);
        }

        // A break clears only the ramp of the ball whose streak broke. A ball-less reset is the shared
        // streak's, which resets on every load (every shot) — clearing every ball's ramp there would undo
        // the per-ball ramps. That reset also covers the level-up reset of every streak, whose balls are
        // popped moments later anyway.
        private void OnStreakChanged(StreakChangedMessage message)
        {
            if (message.Streak != 0)
            {
                return;
            }

            if (message.Projectile == null)
            {
                _unattributedColorPops.Clear();
            }
            else if (_colorPopsByBall.TryGetValue(message.Projectile, out var colorPops))
            {
                colorPops.Clear();
            }
        }

        // Each bounce of a shot sounds a semitone below the last, so a long ricochet reads as losing
        // energy. Clamped like the pop ramps — an unbounded descent runs a cruising shot into the floor.
        private void OnWallHit(WallHitMessage message)
        {
            var depth = Math.Max(0, _flightConfig.ShieldToneThreshold - message.ShieldsRemaining);
            var offset = -Math.Min(_flightStats.Of(message.Projectile).WallBounces - 1, MaxRampSemitones);

            _player.Play(GameSoundId.WallHit, message.Position, depth, semitoneOffset: offset);
        }

        private void OnPierceStateChanged(bool piercing)
        {
            if (piercing)
            {
                _pierceLoopHandle = _player.Play(GameSoundId.PierceLoop, null);
            }
            else if (_pierceLoopHandle != SoundHandle.None)
            {
                _player.Stop(_pierceLoopHandle);
                _pierceLoopHandle = SoundHandle.None;
            }
        }

        // FlightStatsService has already counted this pop by the time the message reaches us — HitPipeline
        // records before publishing — so the step is the count minus one, and the flight's first pop of a
        // type lands on the root.
        //
        // Only types with a dedicated pop id ramp. Flight stats count every balloon type, but a type that
        // falls through to the generic BalloonPop without a colour (BubbleCluster today) has no voice of
        // its own to climb, and never ramped before this counter was shared.
        private int PopSemitoneOffset(GameSoundId popId, ISlotActor actor, IProjectileModel projectile)
        {
            if (popId == GameSoundId.BalloonPop || actor is not IBalloonModel balloon)
            {
                return 0;
            }

            var step = Math.Max(0, _flightStats.Of(projectile).PopsOf(balloon.TypeName) - 1);
            return Math.Min(step * MusicalPitchExtensions.WholeToneSemitones, MaxRampSemitones);
        }

        private void OnRetired(IProjectileModel projectile)
        {
            StopCruise(projectile);
            if (_colorPopsByBall.Remove(projectile, out var colorPops))
            {
                colorPops.Clear();
                _spareColorPops.Push(colorPops);
            }
        }

        // A ball the registry no longer tracks shares the unattributed map, so a late pop can't create
        // an entry nothing would ever drop.
        private Dictionary<string, int> ColorPopsOf(IProjectileModel projectile)
        {
            if (projectile == null || !_projectiles.IsTracked(projectile))
            {
                return _unattributedColorPops;
            }

            if (!_colorPopsByBall.TryGetValue(projectile, out var colorPops))
            {
                colorPops = _spareColorPops.Count > 0 ? _spareColorPops.Pop() : new Dictionary<string, int>();
                _colorPopsByBall[projectile] = colorPops;
            }

            return colorPops;
        }

        private void StopCruise(IProjectileModel projectile)
        {
            if (projectile != null && _cruiseHandles.Remove(projectile, out var handle))
            {
                _player.Stop(handle);
            }
        }
    }
}
