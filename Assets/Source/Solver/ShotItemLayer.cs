using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Capabilities;
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
    /// <see cref="ShotFlightState.HasSpeedBuff" /> exists. <see cref="Damage" />/<see cref="Flags" />
    /// (@ref plan_shot_solver_accuracy Phase C2) are this activation's OWN item-type config — every
    /// <see cref="EffectHitKind.Damage" /> hit <c>ApplyEffectHits</c> applies for THIS activation reads
    /// them (a <see cref="EffectHitKind.PiercingDamage" /> hit ignores <see cref="Flags" /> entirely and
    /// always pops with <c>DamageFlags.Piercing</c> alone — see the class's own Bomb-case doc); 0/
    /// <c>Normal</c> for Shield/Snipe, which never emit hits at all.</summary>
    internal readonly struct ShotItemOutcome
    {
        public readonly int ShieldDelta;
        public readonly bool GrantsRainbowBuffUntilWall;
        public readonly bool GrantsRainbowBuffUntilPierceEnd;
        public readonly bool ArmsPierce;
        public readonly float SpeedBuffMultiplier;
        public readonly int Damage;
        public readonly DamageFlags Flags;

        public ShotItemOutcome(
            int shieldDelta, bool grantsRainbowBuffUntilWall, bool grantsRainbowBuffUntilPierceEnd, bool armsPierce,
            float speedBuffMultiplier, int damage = 0, DamageFlags flags = DamageFlags.Normal)
        {
            ShieldDelta = shieldDelta;
            GrantsRainbowBuffUntilWall = grantsRainbowBuffUntilWall;
            GrantsRainbowBuffUntilPierceEnd = grantsRainbowBuffUntilPierceEnd;
            ArmsPierce = armsPierce;
            SpeedBuffMultiplier = speedBuffMultiplier;
            Damage = damage;
            Flags = flags;
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
    /// Wired per sub-phase (C1 Shield .. C6 Snipe) — an item type with no core (Shield, Snipe) never
    /// touches <see cref="_effectParams" />/<see cref="_effectBoard" /> at all; every other branch
    /// still returns a no-op outcome and no hits until its own sub-phase lands.</summary>
    internal sealed class ShotItemLayer
    {
        internal const int MaxActivationsPerFlight = 32;

        private readonly IReadOnlyDictionary<ItemType, ItemEffectParams> _effectParams;
        private readonly ShotSimEffectBoard _effectBoard;
        private readonly string _rainbowColorId;
        private readonly Queue<ShotItemActivation> _queue = new();

        private int _activationCount;

        internal ShotItemLayer(
            IReadOnlyDictionary<ItemType, ItemEffectParams> effectParams, in ShotSlotLattice lattice,
            string rainbowColorId = null)
        {
            _effectParams = effectParams;
            _effectBoard = new ShotSimEffectBoard(in lattice);
            _rainbowColorId = rainbowColorId;
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

        /// <summary>Resolves an <see cref="EffectHit.Handle" /> the most recent <see cref="Resolve" />
        /// call produced back into the working-set <see cref="ShotBalloonState.SlotIndex" /> it
        /// identifies — a thin passthrough onto the bound effect board (@ref plan_shot_solver_accuracy
        /// Phase C2's handle-boxing reconciliation; <c>ApplyEffectHits</c> is the caller).</summary>
        internal Vector2Int SlotOf(int handle)
        {
            return _effectBoard.SlotOf(handle);
        }

        /// <summary><paramref name="hitsOut" />, when non-null, is cleared and filled with this
        /// activation's <see cref="EffectHit" />s (caller-owned scratch list, same convention as
        /// <c>ShotSimulator.Simulate</c>'s <c>pathOut</c>).</summary>
        internal ShotItemOutcome Resolve(
            in ShotItemActivation activation, string projectileColorId, ShotBalloonState[] workingSet,
            int activeCount, List<EffectHit> hitsOut)
        {
            hitsOut?.Clear();

            switch (activation.Item)
            {
                case ItemType.Shield:
                    return ResolveShield(in activation);

                case ItemType.Bomb:
                    return ResolveBomb(in activation, workingSet, activeCount, hitsOut);

                // Every other item type is plumbing-only until its own sub-phase (C3 Laser .. C6
                // Snipe) wires the real effect against _effectParams/_effectBoard.
                case ItemType.Laser:
                case ItemType.Lightning:
                case ItemType.Paint:
                case ItemType.Snipe:
                default:
                    return default;
            }
        }

        // Mirrors ShieldItemHandler.Activate (verified 2026-07-25): +1 shield on the projectile
        // always; a rainbow host ADDITIONALLY grants the RainbowShield buff (there, a
        // WallBounceEndCondition-gated ProjectileBuff; here, ShotFlightState.RainbowBuffUntilWall).
        // No core, no EffectHits — Shield is a pure state grant.
        private static ShotItemOutcome ResolveShield(in ShotItemActivation activation)
        {
            return new ShotItemOutcome(
                shieldDelta: 1, grantsRainbowBuffUntilWall: activation.IsRainbowHost,
                grantsRainbowBuffUntilPierceEnd: false, armsPierce: false, speedBuffMultiplier: 0f);
        }

        // Mirrors BombItemHandler.Activate's selection (verified 2026-07-25): the shockwave/light/
        // disturbance-field presentation is unmodeled (Risk R5 — document-first), but the kill-radius
        // geometry itself is BombBlast, run over the bound effect board — every active, non-static,
        // non-host occupant (see ShotSimEffectBoard.Bind's own exclusion). No config known for this
        // flight's item set (an empty test dictionary) is a no-op, same as items:null upstream.
        private ShotItemOutcome ResolveBomb(
            in ShotItemActivation activation, ShotBalloonState[] workingSet, int activeCount, List<EffectHit> hitsOut)
        {
            if (!_effectParams.TryGetValue(ItemType.Bomb, out var settings))
            {
                return default;
            }

            _effectBoard.Bind(workingSet, activeCount, activation.Slot);
            var blastParams = new BombBlastParams(settings.Bomb.Radius, settings.Bomb.RainbowConversionRange);
            BombBlast.Resolve(
                _effectBoard, activation.Origin, activation.Slot, activation.IsRainbowHost, _rainbowColorId,
                in blastParams, hitsOut);

            return new ShotItemOutcome(
                shieldDelta: 0, grantsRainbowBuffUntilWall: false, grantsRainbowBuffUntilPierceEnd: false,
                armsPierce: false, speedBuffMultiplier: 0f, damage: settings.Damage, flags: settings.Flags);
        }
    }
}
