#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Game;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Thermal;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using BalloonParty.UI.GameOver;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     Plays the game by itself when it was launched with an autoplay spec (<see cref="AutoplaySettings" />):
    ///     the Editor playtest bot's player, inside the game, so a phone or an emulator can play while the Editor
    ///     does something else. It presses Play on the launch screen, fires as the chosen player once the cannon
    ///     is ready, answers a game over as the spec says (<see cref="AutoplayOnLoss" />), samples the game twice a second
    ///     (<see cref="AutoplayTickWriter" />) and quits the app when the time is up.
    /// </summary>
    internal sealed class AutoplayController : IStartable, ITickable, IDisposable
    {
        // How often the shooter looks at the cannon, so a shot's timing isn't rounded to the sample rate.
        private const float CheckInterval = 0.05f;
        private const float SampleInterval = 0.5f;
        private const float LaunchPressDelay = 1f;
        private const float PlayRetapDelay = 10f;

        // How long a retry press gets to leave the game-over screen before the next answer down is pressed.
        // No ad SDK is plugged in, so an ad is watched at once; a real one would need longer than this.
        private const float LossRetapDelay = 6f;

        private readonly AutoplaySettings _settings;
        private readonly ILevelProgress _progress;
        private readonly IPlayerHealth _health;
        private readonly IRunScore _score;
        private readonly IBoardFill _fill;
        private readonly IProjectileRegistry _projectiles;
        private readonly IProjectileFlightConfig _flight;
        private readonly FireBestShotCheat _solver;
        private readonly ThermalFrameRateGovernor _governor;
        private readonly IFailureOffers _offers;
        private readonly System.Random _random;

        private AutoplayTickWriter _writer;
        private ThrowerLifetimeScope _throwerScope;
        private float _startedAt = -1f;
        private float _nextCheck;
        private float _nextSample;
        private float _fireAt = -1f;
        private float _nextPlayTap;
        private float _nextLossPress;
        private int _lossPresses;
        private int _shots;
        private bool _restartPressed;
        private bool _warnedNoPlay;
        private bool _switchedOff;
        private bool _finished;

        internal AutoplayController(
            AutoplaySettings settings,
            ILevelProgress progress,
            IPlayerHealth health,
            IRunScore score,
            IBoardFill fill,
            IProjectileRegistry projectiles,
            IProjectileFlightConfig flight,
            FireBestShotCheat solver,
            ThermalFrameRateGovernor governor,
            IFailureOffers offers)
        {
            _settings = settings;
            _progress = progress;
            _health = health;
            _score = score;
            _fill = fill;
            _projectiles = projectiles;
            _flight = flight;
            _solver = solver;
            _governor = governor;
            _offers = offers;
            _random = settings.Seed.HasValue ? new System.Random(settings.Seed.Value) : new System.Random();
        }

        public void Start()
        {
            _writer = new AutoplayTickWriter();
            if (_settings.Fps > 0)
            {
                _governor.Pin(_settings.Fps);
            }

            Log.Info("Autoplay", $"playing as {(_settings.Best ? "best" : "human")} for {_settings.Seconds:F0} s, "
                                 + $"on a game over: {_settings.OnLoss}.");
        }

        // The switches a view can't read for itself, applied once the game scene is up. The HUD's canvases stop
        // drawing only after the launch screen, whose Play button the autoplay has to press.
        private void SwitchOffOnce()
        {
            if (_switchedOff)
            {
                return;
            }

            _switchedOff = true;
            if ((_settings.Off & PerfOff.Post) != 0 && Camera.main != null)
            {
                Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            }

            if ((_settings.Off & PerfOff.Hud) != 0)
            {
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                {
                    canvas.enabled = false;
                }
            }
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
                    if (!OnGameOver(clock))
                    {
                        return;
                    }

                    break;
                default:
                    _restartPressed = false;
                    _lossPresses = 0;
                    _nextLossPress = 0f;
                    SwitchOffOnce();
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

        // False once the session has ended here: no answer was asked for, or a player who never watches an ad
        // has run out of free retries.
        private bool OnGameOver(float clock)
        {
            _fireAt = -1f;
            if (_settings.OnLoss == AutoplayOnLoss.Stop)
            {
                return EndAtGameOver();
            }

            if (_restartPressed || clock < _nextLossPress)
            {
                return true;
            }

            // The screen is still up after a retry press when that press staged nothing (an ad skipped or not
            // shown), so each press for this loss steps down: the bonus retry, then a plain retry, then Restart.
            var offer = _offers.Current;
            var onLoss = _settings.OnLoss;
            if (onLoss == AutoplayOnLoss.Free)
            {
                return OnGameOverWithoutAds(clock, in offer);
            }

            if (onLoss == AutoplayOnLoss.Bonus && _lossPresses == 0 && offer.CanBonusRetry)
            {
                PressRetry(clock, bonus: true, ad: true);
                return true;
            }

            var retryPresses = onLoss == AutoplayOnLoss.Bonus ? 1 : 0;
            if (onLoss != AutoplayOnLoss.Restart && _lossPresses <= retryPresses && offer.CanRetry)
            {
                PressRetry(clock, bonus: false, ad: offer.RetryNeedsAd);
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

        // A player who never watches an ad: the free retries, then the run is over and so is the session.
        private bool OnGameOverWithoutAds(float clock, in FailureOffer offer)
        {
            if (_lossPresses == 0 && offer.CanRetry && !offer.RetryNeedsAd)
            {
                PressRetry(clock, bonus: false, ad: false);
                return true;
            }

            // Only once the screen has brought its buttons in, as a player would see there is no free retry.
            return !AutoplayTap.CanTapButtonThatCalls<GameOverScreen>(nameof(GameOverScreen.OnRestartPressed))
                   || EndAtGameOver();
        }

        private bool EndAtGameOver()
        {
            _writer.WriteMarker("game_over");
            Finish();
            return false;
        }

        private void PressRetry(float clock, bool bonus, bool ad)
        {
            var method = bonus ? nameof(GameOverScreen.OnBonusRetryPressed) : nameof(GameOverScreen.OnRetryPressed);
            if (!AutoplayTap.ButtonThatCalls<GameOverScreen>(method))
            {
                return;
            }

            _lossPresses++;
            _nextLossPress = clock + LossRetapDelay;
            _writer.WriteRetry(bonus, ad);
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
                _projectiles.InFlightCount));
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
