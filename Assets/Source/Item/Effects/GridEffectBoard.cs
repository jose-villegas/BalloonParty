using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item.Effects
{
    /// <summary>Live <see cref="IEffectBoard" /> over a real <see cref="SlotGrid" /> — occupants are
    /// every <see cref="IBalloonModel" /> on the grid (statics are never <see cref="IBalloonModel" />,
    /// so they're excluded for free) minus the popped host and minus any view whose collider is
    /// currently disabled (<see cref="BalloonView.HasActiveCollider" /> — mid-despawn, e.g.). Position
    /// comes from the view transform (balance/nudge displace it); <paramref name="viewlessRadius" />
    /// only ever backs a slot with no live view, keeping this EditMode-testable against a headless grid
    /// the same way <c>ShotBoardGather</c> already reads it.</summary>
    internal sealed class GridEffectBoard : IEffectBoard
    {
        private readonly SlotGrid _grid;
        private readonly float _viewlessRadius;
        private readonly List<EffectOccupant> _occupants = new();

        // Parallel to _occupants (same index) — EffectOccupant deliberately carries no model
        // reference (see its Handle doc), so the live model a handle resolves to lives here instead.
        private readonly List<IBalloonModel> _models = new();

        public IReadOnlyList<EffectOccupant> Occupants => _occupants;
        public int SearchRadius => Mathf.Max(_grid.Columns, _grid.Rows);

        public GridEffectBoard(SlotGrid grid, float viewlessRadius = 0f)
        {
            _grid = grid;
            _viewlessRadius = viewlessRadius;
        }

        public bool TryGetOccupantAt(Vector2Int slot, out EffectOccupant occupant)
        {
            for (var i = 0; i < _occupants.Count; i++)
            {
                if (_occupants[i].Slot == slot)
                {
                    occupant = _occupants[i];
                    return true;
                }
            }

            occupant = default;
            return false;
        }

        /// <summary>Rebuilds the live occupant snapshot for one activation. <paramref name="exclude" />
        /// is the popped host's own slot — its own effect must never select itself back (the live
        /// host is already gone by the time an activation runs; this guards a test/board that hasn't
        /// removed it yet).</summary>
        public void Rebuild(Vector2Int? exclude = null)
        {
            _occupants.Clear();
            _models.Clear();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var index = new Vector2Int(col, row);
                    if (index == exclude || _grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    if (_grid.At(index) is not IBalloonModel model)
                    {
                        continue;
                    }

                    var view = _grid.ActorViewAt<BalloonView>(index);
                    if (view != null && !view.HasActiveCollider)
                    {
                        continue;
                    }

                    var slotPosition = (Vector2)_grid.IndexToWorldPosition(index);
                    var position = view != null ? (Vector2)view.transform.position : slotPosition;
                    var radius = view != null ? view.ContactRadius : _viewlessRadius;
                    var colorId = model is IHasColor colorable ? colorable.Color.Value : null;

                    _models.Add(model);
                    _occupants.Add(new EffectOccupant(
                        _occupants.Count, index, position, slotPosition, radius, colorId, model is IPaintable,
                        model is IResistsPaint));
                }
            }
        }

        /// <summary>Resolves an <see cref="EffectHit.Handle" /> this board produced back into the live
        /// model it identifies.</summary>
        public IBalloonModel ModelAt(int handle)
        {
            return _models[handle];
        }
    }
}
