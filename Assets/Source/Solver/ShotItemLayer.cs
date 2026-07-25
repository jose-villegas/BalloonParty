using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>One item activation's selection inputs — the host balloon's own state at the moment it
    /// popped, not yet an effect (see <see cref="ShotItemLayer.Resolve" />). <see cref="Origin" /> is
    /// the host's centre at contact time; <see cref="ProjectileDirection" /> is
    /// <see cref="Vector2.zero" /> for a CHAINED activation (a popped item triggering another item) —
    /// live passes <c>Vector3.zero</c> too, and Paint fans its blobs upward in that case.
    /// <see cref="SpinDegrees" /> is the Laser's extrapolated-to-contact-time rotation (gather captures
    /// the rate; the sim advances it to the predicted hit).</summary>
    internal readonly struct ShotItemActivation
    {
        public readonly ItemType Item;
        public readonly Vector2 Origin;
        public readonly Vector2 ProjectileDirection;
        public readonly Vector2Int Slot;
        public readonly string SourceColorId;
        public readonly bool IsRainbowHost;
        public readonly float SpinDegrees;
        public readonly bool IsDirectHit;

        public ShotItemActivation(
            ItemType item, Vector2 origin, Vector2 projectileDirection, Vector2Int slot, string sourceColorId,
            bool isRainbowHost, float spinDegrees, bool isDirectHit)
        {
            Item = item;
            Origin = origin;
            ProjectileDirection = projectileDirection;
            Slot = slot;
            SourceColorId = sourceColorId;
            IsRainbowHost = isRainbowHost;
            SpinDegrees = spinDegrees;
            IsDirectHit = isDirectHit;
        }
    }

    /// <summary>The buff/state side-effects one activation grants, applied by the caller (the item's
    /// <see cref="EffectHit" />s against the board are returned separately, via <c>Resolve</c>'s
    /// <c>hitsOut</c>). <see cref="SpeedBuffMultiplier" /> of 0 means "grants none" — <c>1f</c> can't
    /// express that (it's also the buffless multiplier), mirroring why
    /// <see cref="ShotFlightState.HasSpeedBuff" /> exists.</summary>
    internal readonly struct ShotItemOutcome
    {
        public readonly int ShieldDelta;
        public readonly bool GrantsRainbowBuffUntilWall;
        public readonly bool GrantsRainbowBuffUntilPierceEnd;
        public readonly bool ArmsPierce;
        public readonly float SpeedBuffMultiplier;

        public ShotItemOutcome(
            int shieldDelta, bool grantsRainbowBuffUntilWall, bool grantsRainbowBuffUntilPierceEnd, bool armsPierce,
            float speedBuffMultiplier)
        {
            ShieldDelta = shieldDelta;
            GrantsRainbowBuffUntilWall = grantsRainbowBuffUntilWall;
            GrantsRainbowBuffUntilPierceEnd = grantsRainbowBuffUntilPierceEnd;
            ArmsPierce = armsPierce;
            SpeedBuffMultiplier = speedBuffMultiplier;
        }
    }

    /// <summary>Item-carrier selection + params for the shot solver (@ref plan_shot_solver_accuracy
    /// Phase C) — selection and outcome computation ONLY; <c>ShotSimulator</c> stays the sole mutator
    /// of flight/board state (mirrors <c>dynamics</c>'s split: this layer never touches
    /// <see cref="ShotBalloonState" /> directly). A FIFO queue mirrors the live frame cadence (a popped
    /// item's own effect can enqueue another activation — a chained item pop — which resolves on a
    /// later iteration, breadth-first, exactly like <c>ItemActivator</c>'s per-frame draining).
    /// <see cref="MaxActivationsPerFlight" /> bounds a pathological chain (e.g. bomb-chains-into-bomb)
    /// so a sweep of thousands of angles can never spin forever.
    /// C0 (this phase) is plumbing only: <see cref="Resolve" /> dispatches on <see cref="ItemType" />
    /// but every branch returns a no-op outcome and no hits — each item's own sub-phase (C1 Shield ..
    /// C6 Snipe) wires its real effect in.</summary>
    internal sealed class ShotItemLayer
    {
        internal const int MaxActivationsPerFlight = 32;

        private readonly IReadOnlyDictionary<ItemType, ItemEffectParams> _effectParams;
        private readonly ShotSimEffectBoard _effectBoard;
        private readonly Queue<ShotItemActivation> _queue = new();

        private int _activationCount;

        internal ShotItemLayer(IReadOnlyDictionary<ItemType, ItemEffectParams> effectParams, in ShotSlotLattice lattice)
        {
            _effectParams = effectParams;
            _effectBoard = new ShotSimEffectBoard(in lattice);
        }

        internal void ResetForNewFlight()
        {
            _queue.Clear();
            _activationCount = 0;
        }

        /// <summary>Enqueues an activation if the per-flight budget allows it — false means the budget
        /// is spent and the caller must drop the activation silently (mirrors a chain simply running
        /// out of steam live, never an error).</summary>
        internal bool TryBeginActivation(in ShotItemActivation activation)
        {
            if (_activationCount >= MaxActivationsPerFlight)
            {
                return false;
            }

            _activationCount++;
            _queue.Enqueue(activation);
            return true;
        }

        internal bool TryDequeue(out ShotItemActivation activation)
        {
            if (_queue.Count == 0)
            {
                activation = default;
                return false;
            }

            activation = _queue.Dequeue();
            return true;
        }

        /// <summary><paramref name="hitsOut" />, when non-null, is cleared and filled with this
        /// activation's <see cref="EffectHit" />s (caller-owned scratch list, same convention as
        /// <c>ShotSimulator.Simulate</c>'s <c>pathOut</c>).</summary>
        internal ShotItemOutcome Resolve(
            in ShotItemActivation activation, string projectileColorId, ShotBalloonState[] workingSet,
            int activeCount, List<EffectHit> hitsOut)
        {
            hitsOut?.Clear();

            // C0 plumbing only — every item type resolves to a no-op outcome/no hits until its own
            // sub-phase wires the real effect against _effectParams/_effectBoard.
            switch (activation.Item)
            {
                case ItemType.Shield:
                case ItemType.Bomb:
                case ItemType.Laser:
                case ItemType.Lightning:
                case ItemType.Paint:
                case ItemType.Snipe:
                default:
                    return default;
            }
        }
    }
}
