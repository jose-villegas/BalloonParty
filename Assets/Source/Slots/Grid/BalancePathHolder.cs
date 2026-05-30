using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     Tracks grid slots that are currently in-transit due to balance
    ///     animations. Spawn path computation consults this to avoid
    ///     traversal conflicts with relocating balloons. Transit slots are
    ///     released per-actor when their animation completes.
    /// </summary>
    internal class BalancePathHolder
    {
        private readonly Dictionary<IWriteableDynamicSlotActor, List<Vector2Int>> _actorSlots = new();
        private readonly HashSet<Vector2Int> _transitSlots = new();

        internal bool IsInTransit(Vector2Int slot)
        {
            return _transitSlots.Contains(slot);
        }

        internal bool IsInTransit(int col, int row)
        {
            return _transitSlots.Contains(new Vector2Int(col, row));
        }

        internal void Reserve(IWriteableDynamicSlotActor actor, Vector2Int slot)
        {
            _transitSlots.Add(slot);

            if (!_actorSlots.TryGetValue(actor, out var slots))
            {
                slots = new List<Vector2Int>();
                _actorSlots[actor] = slots;
            }

            slots.Add(slot);
        }

        internal void Release(IWriteableDynamicSlotActor actor)
        {
            if (!_actorSlots.TryGetValue(actor, out var slots))
            {
                return;
            }

            foreach (var slot in slots)
            {
                _transitSlots.Remove(slot);
            }

            _actorSlots.Remove(actor);
        }
    }
}
