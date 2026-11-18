using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     What a bubble cluster may be filled to right now (<see cref="RipeBubbleRule.FillLimit" />), which keeps ripe
    ///     clusters few and apart: on a fast clock, with nothing to stop them, full clusters followed one another
    ///     into a run of blasts with no gap. A merge or a foam's hand-out fills a cluster to
    ///     <see cref="IBalloonsConfiguration.BubbleMergeMax" />, where it ripens, only while fewer than
    ///     <see cref="IRipeBubbleSettings.MaxRipeClusters" /> clusters on the board are full and
    ///     <see cref="IRipeBubbleSettings.RipenCooldownBeats" /> of the bubbles' off-beats
    ///     (<see cref="IBoardBeat" />, <see cref="IRipeBubbleSettings.MergeOffBeat" />) have passed since a ripe
    ///     cluster last ended (<see cref="RipeEnded" />); otherwise it stops a bubble short and waits there, unripe.
    ///     The gap is counted on the rhythm clusters merge on, so it is over on an off-beat, when the merge service
    ///     looks. Whoever fills clusters counts the full ones once (<see cref="CountFull" />) and adds each it
    ///     fills, so one pass that could fill several stops at the cap. With ripeness off
    ///     (<see cref="IRipeBubbleSettings.FuseSeconds" /> 0) nothing is limited.
    /// </summary>
    internal sealed class RipeRoom : IRunResettable
    {
        private readonly SlotGrid _grid;
        private readonly IBoardBeat _beat;
        private readonly ILevelProgress _levelProgress;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IRipeBubbleSettings _settings;

        // The off-beat count as a ripe cluster last ended, and the level it ended on. A level starts with no gap to
        // wait out, whatever ended on the one before; a run starts the count itself over, so a reset forgets it.
        private bool _ended;
        private int _endedOffBeats;
        private int _endedLevel;

        public int ResetOrder => RunResetOrder.Counters;

        private bool Limits => _settings.FuseSeconds > 0f;

        [Inject]
        internal RipeRoom(SlotGrid grid, IBoardBeat beat, ILevelProgress levelProgress,
            IBalloonsConfiguration balloonsConfig)
        {
            _grid = grid;
            _beat = beat;
            _levelProgress = levelProgress;
            _balloonsConfig = balloonsConfig;
            _settings = balloonsConfig.RipeBubble;
        }

        public void ResetRun(int generation)
        {
            _ended = false;
        }

        /// <summary>
        ///     How many clusters on the board are full: at the merge cap or over it, ripe or about to ripen.
        ///     <paramref name="leaving" /> is left out, the cluster whose foam is being handed out: it is still in its
        ///     slot, and gone once it has.
        /// </summary>
        internal int CountFull(ISlotActor leaving = null)
        {
            var max = _balloonsConfig.BubbleMergeMax;
            var full = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.At(new Vector2Int(col, row)) is BubbleClusterModel cluster
                        && !ReferenceEquals(cluster, leaving)
                        && RipeBubbleRule.IsRipe(cluster.HitsRemaining.Value, max))
                    {
                        full++;
                    }
                }
            }

            return full;
        }

        /// <summary>How many more clusters may be filled to the cap, with <paramref name="fullClusters" /> full
        /// already: the board's count (<see cref="CountFull" />) plus those the caller has filled since.</summary>
        internal int FillsLeft(int fullClusters)
        {
            return Limits
                ? RipeBubbleRule.FillsLeft(
                    fullClusters, _settings.MaxRipeClusters, OffBeatsSinceEnded(), _settings.RipenCooldownBeats)
                : int.MaxValue;
        }

        /// <summary>The most bubbles a cluster may be filled to, with <paramref name="fullClusters" /> full already:
        /// the merge cap, or one short of it. A cluster already over the limit keeps what it holds.</summary>
        internal int FillLimit(int fullClusters)
        {
            var max = _balloonsConfig.BubbleMergeMax;
            return Limits
                ? RipeBubbleRule.FillLimit(
                    max, fullClusters, _settings.MaxRipeClusters, OffBeatsSinceEnded(), _settings.RipenCooldownBeats)
                : max;
        }

        /// <summary>A ripe cluster ended, burst by a hit or its fuse run out: the gap is counted from now.</summary>
        internal void RipeEnded()
        {
            _ended = true;
            _endedOffBeats = _beat.OffBeats(_settings.MergeOffBeat);
            _endedLevel = _levelProgress.Level.Value;
        }

        private int OffBeatsSinceEnded()
        {
            if (!_ended || _levelProgress.Level.Value != _endedLevel)
            {
                return int.MaxValue;
            }

            // A count below the one kept means the beat started over without a reset reaching here first: no gap.
            var since = _beat.OffBeats(_settings.MergeOffBeat) - _endedOffBeats;
            return since >= 0 ? since : int.MaxValue;
        }
    }
}
