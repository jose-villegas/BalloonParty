using BalloonParty.Game.Level;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     How full the board is, in balloons: against the level's starting fill (its board lines across
    ///     every column), and as a share of the slots grid actors leave open. Grid actors aren't counted:
    ///     they aren't targets. Recounted only when the grid mutates, so per-frame reads are free.
    /// </summary>
    internal sealed class BoardFill : IBoardFill
    {
        private readonly SlotGrid _grid;
        private readonly IActiveLevelParameters _levelParams;

        private int _countedVersion = -1;
        private int _balloons;
        private int _gridActors;

        public float StartingFill
        {
            get
            {
                Recount();
                var startingFill = _grid.Columns * _levelParams.Current.BoardLines;
                return startingFill > 0 ? Mathf.Clamp01((float)_balloons / startingFill) : 1f;
            }
        }

        public float Occupancy
        {
            get
            {
                Recount();
                var open = _grid.Columns * _grid.Rows - _gridActors;
                return open > 0 ? Mathf.Clamp01((float)_balloons / open) : 1f;
            }
        }

        public BoardFill(SlotGrid grid, IActiveLevelParameters levelParams)
        {
            _grid = grid;
            _levelParams = levelParams;
        }

        private void Recount()
        {
            if (_countedVersion == _grid.MutationVersion)
            {
                return;
            }

            _countedVersion = _grid.MutationVersion;
            _balloons = 0;
            _gridActors = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsKind(col, row, SlotActorKind.Dynamic))
                    {
                        _balloons++;
                    }
                    else if (_grid.IsKind(col, row, SlotActorKind.Static))
                    {
                        _gridActors++;
                    }
                }
            }
        }
    }
}
