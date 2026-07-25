using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Item.Effects
{
    /// <summary>What an <see cref="EffectHit" /> resolves into once the loop applies it — see the
    /// per-kind dispatch in <c>ApplyEffectHits</c> (Phase C1 onward): <see cref="PiercingDamage" />
    /// always pops (unbreakables included); <see cref="Damage" /> decrements durability, popping at
    /// zero, never redirecting; <see cref="Recolor" /> overwrites colour identity without popping.</summary>
    internal enum EffectHitKind
    {
        Damage,
        PiercingDamage,
        Recolor
    }

    /// <summary>One board actor as an item effect core sees it — deliberately NOT
    /// <c>HitsRemaining</c>/damage-capable (ISP: a core only ever SELECTS occupants and emits
    /// <see cref="EffectHit" />s against them; the loop, not the core, owns mutation). <see cref="Position" />
    /// is the live/time-evaluated centre (Bomb + Laser read this); <see cref="SlotPosition" /> is the
    /// lattice home (Paint + Lightning read this — their selection is grid-topology, not physical
    /// overlap).</summary>
    internal readonly struct EffectOccupant
    {
        public readonly object Handle;
        public readonly Vector2Int Slot;
        public readonly Vector2 Position;
        public readonly Vector2 SlotPosition;
        public readonly float Radius;
        public readonly string ColorId;
        public readonly bool IsPaintable;
        public readonly bool ResistsPaint;

        public EffectOccupant(
            object handle, Vector2Int slot, Vector2 position, Vector2 slotPosition, float radius, string colorId,
            bool isPaintable, bool resistsPaint)
        {
            Handle = handle;
            Slot = slot;
            Position = position;
            SlotPosition = slotPosition;
            Radius = radius;
            ColorId = colorId;
            IsPaintable = isPaintable;
            ResistsPaint = resistsPaint;
        }
    }

    /// <summary>One effect core's verdict on one occupant — the loop, not the core, applies it (see
    /// <c>ShotItemLayer</c>/<c>ApplyEffectHits</c>). <see cref="Group" /> is a per-hit ordering/grouping
    /// index (a paint blob index, a lightning jump index) cores use to stage sequential application;
    /// 0 for a core with no grouping concept.</summary>
    internal readonly struct EffectHit
    {
        public readonly object Handle;
        public readonly EffectHitKind Kind;
        public readonly string ColorId;
        public readonly int Group;

        private EffectHit(object handle, EffectHitKind kind, string colorId, int group)
        {
            Handle = handle;
            Kind = kind;
            ColorId = colorId;
            Group = group;
        }

        public static EffectHit Damage(object handle, int group = 0)
        {
            return new EffectHit(handle, EffectHitKind.Damage, null, group);
        }

        public static EffectHit PiercingDamage(object handle, int group = 0)
        {
            return new EffectHit(handle, EffectHitKind.PiercingDamage, null, group);
        }

        public static EffectHit Recolor(object handle, string colorId, int group = 0)
        {
            return new EffectHit(handle, EffectHitKind.Recolor, colorId, group);
        }
    }

    /// <summary>The occupant read-set an item-effect core selects against — implemented by
    /// <see cref="GridEffectBoard" /> (live) and <c>ShotSimEffectBoard</c> (solver), so the same core
    /// (<c>BombBlast</c>/<c>LaserCross</c>/<c>LightningChain</c>/<c>PaintSpread</c>) runs identically
    /// over either. Deliberately has no hex-neighbour helper — <c>HexCoordinates</c> is pure and
    /// already reachable from every core in this assembly.</summary>
    internal interface IEffectBoard
    {
        IReadOnlyList<EffectOccupant> Occupants { get; }

        /// <summary>A board-wide bound (in grid cells) a core may use to size a hex-neighbour search —
        /// the board's own occupant-count concern, not a core's.</summary>
        int SearchRadius { get; }

        bool TryGetOccupantAt(Vector2Int slot, out EffectOccupant occupant);
    }
}
