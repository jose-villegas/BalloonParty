#if UNITY_ENABLE_CHECKS
using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Game;
using BalloonParty.Game.Coins;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using BalloonParty.UI.GameOver;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     Plays the game by itself when it was launched with an autoplay spec (<see cref="AutoplaySettings" />):
    ///     the Editor playtest bot's player, inside the game, so a phone or an emulator can play while the Editor
    ///     does something else. It presses Play on the launch screen, fires as the chosen player once the cannon
    ///     is ready, presses Restart on the game-over screen (or ends there), samples the game twice a second
    ///     (<see cref="AutoplayTickWriter" />) and quits the app when the time is up.
    /// </summary>
    internal sealed class AutoplayController : IStartable, ITickable, IDisposable
    {
        // How often the shooter looks at the cannon, so a shot's timing isn't rounded to the sample rate.
        private const float CheckInterval = 0.05f;
        private const float SampleInterval = 0.5f;
        private const float LaunchPressDelay = 1f;
        private const float PlayRetapDelay = 10f;

        private readonly AutoplaySettings _settings;
        private readonly ILevelProgress _progress;
        private readonly IPlayerHealth _health;
        private readonly IRunScore _score;
        private readonly IBoardFill _fill;
        private readonly IProjectileRegistry _projectiles;
        private readonly ICoinBalance _coins;
        private readonly IProjectileFlightConfig _flight;
        private readonly FireBestShotCheat _solver;
        private readonly System.Random _random;

        private AutoplayTickWriter _writer;
        private ThrowerLifetimeScope _throwerScope;
        private float _startedAt = -1f;
        private float _nextCheck;
        private float _nextSample;
        private float _fireAt = -1f;
        private float _nextPlayTap;
        private int _shots;
        private bool _restartPressed;
        private bool _warnedNoPlay;
        private bool _finished;

        internal AutoplayController(
            AutoplaySettings settings,
            ILevelProgress progress,
            IPlayerHealth health,
            IRunScore score,
            IBoardFill fill,
            IProjectileRegistry projectiles,
            ICoinBalance coins,
            IProjectileFlightConfig flight,
            FireBestShotCheat solver)
        {
            _settings = settings;
            _progress = progress;
            _health = health;
            _score = score;
            _fill = fill;
            _projectiles = projectiles;
            _coins = coins;
            _flight = flight;
            _solver = solver;
            _random = settings.Seed.HasValue ? new System.Random(settings.Seed.Value) : new System.Random();
        }

        public void Start()
        {
            _writer = new AutoplayTickWriter();
            Log.Info("Autoplay", $"playing as {(_settings.Best ? "best" : "human")} for {_settings.Seconds:F0} s.");
        }

        public void Tick()
        {
            if (_finished)
            {
                return;
            }

            var clock = Time.realtimeSinceStartup;
            if (_startedAt < 0f)
            {
                _startedAt = clock;
            }

            var navigation = Navigation.Current.Value;
            switch (navigation)
            {
                case NavigationState.Launch:
                    PressPlay(clock);
                    break;
                case NavigationState.GameOver:
                    if (!OnGameOver())
                    {
                        return;
                    }

                    break;
                default:
                    _restartPressed = false;
                    Shoot(clock, navigation);
                    break;
            }

            if (clock >= _nextSample)
            {
                _nextSample = clock + SampleInterval;
                Sample(navigation);
            }

            if (clock - _startedAt >= _settings.Seconds)
            {
                Finish();
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }

        private void PressPlay(float clock)
        {
            if (clock - _startedAt < LaunchPressDelay || clock < _nextPlayTap)
            {
                return;
            }

            if (AutoplayTap.ButtonThatCalls<LaunchPlayTrigger>(nameof(LaunchPlayTrigger.Play)))
            {
                if (_nextPlayTap > 0f)
                {
                    Log.Warn("Autoplay", "still on the launch screen after tapping Play; tapped it again.");
                }

                // The game shows a couple of seconds after the tap (press animation, then the ascend), so tap
                // again only if that one plainly didn't take.
                _nextPlayTap = clock + PlayRetapDelay;
            }
            else if (!_warnedNoPlay && clock - _startedAt > PlayRetapDelay)
            {
                Log.Warn("Autoplay", "no Play button a finger could tap on the launch screen; still looking.");
                _warnedNoPlay = true;
            }
        }

        // False once the session has ended here (no restart asked for).
        private bool OnGameOver()
        {
            _fireAt = -1f;
            if (!_settings.Restart)
            {
                _writer.WriteMarker("game_over");
                Finish();
                return false;
            }

            if (_restartPressed)
            {
                return true;
            }

            // No tap until the screen has brought its Restart button in, as for a player.
            if (AutoplayTap.ButtonThatCalls<GameOverScreen>(nameof(GameOverScreen.OnRestartPressed)))
            {
                _restartPressed = true;
                _writer.WriteMarker("restart");
            }

            return true;
        }

        private void Shoot(float clock, NavigationState navigation)
        {
            if (clock < _nextCheck)
            {
                return;
            }

            _nextCheck = clock + CheckInterval;
            var thrower = FindThrower();
            if (navigation != NavigationState.Game || _progress.Phase.Value != LevelUpPhase.Playing
                || thrower == null || !thrower.IsReadyToFire())
            {
                // A reaction is to a ready cannon: one that stops being ready (a level-up) starts it over.
                _fireAt = -1f;
                return;
            }

            if (_fireAt < 0f)
            {
                _fireAt = _settings.Best
                    ? clock
                    : clock + AutoplayDice.ReactionDelay(_settings.ReactFloor, _settings.ReactMean, NextUniform());
            }

            if (clock < _fireAt)
            {
                return;
            }

            _fireAt = -1f;
            thrower.FireAt(ShotBoardGather.DirectionFromDegrees(ChooseAngle()));
            _shots++;
        }

        // The solver's angle; a human adds a normal aim error, or now and then (WildChance) fires anywhere in
        // the aim range: a player who shoots without reading where the balloons are.
        private float ChooseAngle()
        {
            if (!_settings.Best && NextUniform() < _settings.WildChance)
            {
                return Mathf.Lerp(_flight.AimAngleMinDegrees, _flight.AimAngleMaxDegrees, NextUniform());
            }

            var angle = _solver.TryFindBestAngle(out var best) ? best : 90f;
            if (_settings.Best)
            {
                return angle;
            }

            angle += AutoplayDice.Normal(NextUniform(), NextUniform()) * _settings.AimSd;
            return Mathf.Clamp(angle, _flight.AimAngleMinDegrees, _flight.AimAngleMaxDegrees);
        }

        private ThrowerController FindThrower()
        {
            if (_throwerScope == null)
            {
                _throwerScope = UnityEngine.Object.FindAnyObjectByType<ThrowerLifetimeScope>();
            }

            return _throwerScope != null && _throwerScope.Container != null
                ? (ThrowerController)_throwerScope.Container.Resolve(typeof(ThrowerController))
                : null;
        }

        private void Sample(NavigationState navigation)
        {
            _writer.WriteSample(new AutoplaySample(
                Time.time,
                _progress.Level.Value,
                _score.TotalScore.Value,
                _health.Current.Value,
                navigation.ToString(),
                _progress.Phase.Value.ToString(),
                _shots,
                _fill.Occupancy,
                _projectiles.InFlightCount,
                _coins.Balance.Value));
        }

        private float NextUniform()
        {
            return (float)_random.NextDouble();
        }

        private void Finish()
        {
            _finished = true;
            _writer.Finish();
            Log.Info("Autoplay", "session over; quitting.");
            Application.Quit();
        }
    }
}
#endif
