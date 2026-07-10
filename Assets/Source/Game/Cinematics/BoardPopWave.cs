using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Detaches every balloon into the transition's outgoing group so they ride the fake-camera travel
    ///     out, then pops them in a slow-mo wave along anti-diagonals from the far corners inward — the
    ///     "gone" beat of the game-over restart (vs. the level-up's float-away "survived"). Detaching frees
    ///     the grid so the incoming scenery fills fully; the balloons stay registered so the wave can still
    ///     pop them. Scoreless (no <c>ActorHitMessage</c>); tuned by the <c>LevelAscend</c> pop settings.
    /// </summary>
    internal sealed class BoardPopWave : IBoardEffect
    {
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly TimeScaleService _timeScale;
        private readonly ICinematicsSettings _settings;
        private readonly Dictionary<int, List<IBalloonModel>> _bands = new();

        private int _minBand;
        private int _maxBand;

        internal BoardPopWave(
            SlotGrid grid,
            BalloonControllerRegistry balloonRegistry,
            ScenarioContentRoot scenarioRoot,
            TimeScaleService timeScale,
            ICinematicsSettings settings)
        {
            _grid = grid;
            _balloonRegistry = balloonRegistry;
            _scenarioRoot = scenarioRoot;
            _timeScale = timeScale;
            _settings = settings;
        }

        // Records the balloons by anti-diagonal band and detaches each into the outgoing holder (off the
        // grid, offset by exitDrop so it rides the travel out) while leaving it registered, so PlayAsync can
        // still pop it in the wave. Call while the grid is populated and the root sits at origin.
        public void Collect(float exitDrop)
        {
            _bands.Clear();
            _minBand = int.MaxValue;
            _maxBand = int.MinValue;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var balloon = _grid.ActorAt<IWriteableBalloonModel>(new Vector2Int(col, row));
                    if (balloon == null)
                    {
                        continue;
                    }

                    var band = col + row;
                    if (!_bands.TryGetValue(band, out var models))
                    {
                        models = new List<IBalloonModel>();
                        _bands[band] = models;
                    }

                    models.Add(balloon);
                    _minBand = Mathf.Min(_minBand, band);
                    _maxBand = Mathf.Max(_maxBand, band);

                    if (_balloonRegistry.TryResolve(balloon, out var controller))
                    {
                        controller.DetachForOutgoing(_scenarioRoot.OutgoingBalloons, exitDrop);
                    }
                }
            }
        }

        // Wall-clock length of the wave; mirror this if the wave loop's cadence changes.
        public float EstimateSeconds()
        {
            if (_bands.Count == 0)
            {
                return 0f;
            }

            var steps = ((_maxBand - _minBand) / 2) + 1;
            var ascend = _settings.LevelAscend;
            return steps * ascend.PopWaveBandSeconds / ascend.PopSlowMoTimeScale;
        }

        public async UniTask PlayAsync(CancellationToken ct)
        {
            if (_bands.Count == 0)
            {
                return;
            }

            _timeScale.Claim(TimeScaleSource.LevelTransition, _settings.LevelAscend.PopSlowMoTimeScale);
            try
            {
                // Sweep from the outermost POPULATED bands, not the grid corners, which may be empty.
                for (int near = _minBand, far = _maxBand; near <= far; near++, far--)
                {
                    PopBand(near);
                    if (far != near)
                    {
                        PopBand(far);
                    }

                    // Scaled delay so slow-mo also stretches the wave's cadence.
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_settings.LevelAscend.PopWaveBandSeconds),
                        ignoreTimeScale: false, cancellationToken: ct);
                }
            }
            finally
            {
                _timeScale.Release(TimeScaleSource.LevelTransition);
            }
        }

        private void PopBand(int band)
        {
            if (!_bands.TryGetValue(band, out var models))
            {
                return;
            }

            for (var i = 0; i < models.Count; i++)
            {
                _balloonRegistry.TryPopSingle(models[i]);
            }
        }
    }
}
