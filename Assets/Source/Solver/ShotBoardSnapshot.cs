using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Nudge;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>Colour identity of a snapshot target — split out of the geometry/scoring core so
    /// rainbow/wildcard fields (Phase D) slot in without perturbing every other factory.
    /// <see cref="IsRainbow" />/<see cref="AllowedColors" /> are gather-populated from Phase D onward;
    /// today they are always false/null.</summary>
    internal readonly struct ColorProfile
    {
        public readonly string ColorId;
        public readonly bool IsRainbow;
        public readonly IReadOnlyList<string> AllowedColors;

        public ColorProfile(string colorId, bool isRainbow, IReadOnlyList<string> allowedColors)
        {
            ColorId = colorId;
            IsRainbow = isRainbow;
            AllowedColors = allowedColors;
        }
    }

    /// <summary>Balance-grid identity/behaviour for a snapshot target — absent (null on the owning
    /// <see cref="ShotBalloonSnapshot" />) for a target with no live dynamic stub (e.g. a Phase A
    /// interactive static). <see cref="Omnidirectional" />/<see cref="BiasKind" />/<see cref="BiasValue" />/
    /// <see cref="BiasTypeId" /> are gather-populated from the actor's <c>IBalanceInfluence</c>/
    /// <c>IBalanceBiasSource</c> (Phase B) so the sim's stub actors reproduce the live weight system.</summary>
    internal readonly struct BalanceProfile
    {
        public readonly Vector2Int SlotIndex;
        public readonly int BalancePriority;
        public readonly int MaxBalanceSteps;
        public readonly float MoveSpeed;
        public readonly bool DirectBalanceMotion;
        public readonly bool Omnidirectional;
        public readonly BalanceBiasKind BiasKind;
        public readonly float BiasValue;
        public readonly int BiasTypeId;
        public readonly IReadOnlyList<NudgeOverride> NudgeOverrides;

        public BalanceProfile(
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, IReadOnlyList<NudgeOverride> nudgeOverrides,
            bool omnidirectional = false, BalanceBiasKind biasKind = BalanceBiasKind.None, float biasValue = 0f,
            int biasTypeId = 0)
        {
            SlotIndex = slotIndex;
            BalancePriority = balancePriority;
            MaxBalanceSteps = maxBalanceSteps;
            MoveSpeed = moveSpeed;
            DirectBalanceMotion = directBalanceMotion;
            NudgeOverrides = nudgeOverrides;
            Omnidirectional = omnidirectional;
            BiasKind = biasKind;
            BiasValue = biasValue;
            BiasTypeId = biasTypeId;
        }
    }

    /// <summary>How a contact resolves once the ray reaches it — data-only until Phase A wires the
    /// Deflector/Gatekeeper/Absorber behaviours; every snapshot today is (and stays) <see cref="Poppable" />.</summary>
    internal enum ShotContactKind
    {
        Poppable,
        DeflectForever,
        Absorb
    }

    /// <summary>One board actor as the solver sees it — enough to reproduce pop/deflect/score rules
    /// without a live <c>IBalloonModel</c>. Grown by composition (@ref plan_shot_solver_accuracy §3):
    /// a geometry/scoring core, a <see cref="ColorProfile" />, an optional <see cref="BalanceProfile" />
    /// (null ⇒ no dynamic stub backs this entry — see <see cref="ShotBoardDynamics" />'s null-<c>Actor</c>
    /// gating), a <see cref="ShotContactKind" />, and an <see cref="ItemType" /> (data-only in this phase).
    /// Construct via the named factories, never the (private) field-order constructor directly — that is
    /// what keeps every caller's field mapping honest as the struct grows.</summary>
    internal readonly struct ShotBalloonSnapshot
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly int ScoreValue;
        public readonly int HitsRemaining;
        public readonly ColorProfile Color;
        public readonly BalanceProfile? Balance;
        public readonly ShotContactKind ContactKind;
        public readonly ItemType Item;

        /// <summary>Passthrough so downstream reads stay tidy — null/empty means colourless, exactly as
        /// <see cref="ColorProfile.ColorId" /> does.</summary>
        public string ColorId => Color.ColorId;

        private ShotBalloonSnapshot(
            Vector2 position, float radius, int scoreValue, int hitsRemaining, ColorProfile color,
            BalanceProfile? balance, ShotContactKind contactKind, ItemType item)
        {
            Position = position;
            Radius = radius;
            ScoreValue = scoreValue;
            HitsRemaining = hitsRemaining;
            Color = color;
            Balance = balance;
            ContactKind = contactKind;
            Item = item;
        }

        /// <summary>A poppable/deflectable colour-scoring target — mirrors a live balloon with
        /// <c>IHasColor</c> (streak + colour adoption rules apply).</summary>
        public static ShotBalloonSnapshot ForColorTarget(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            BalanceProfile? balance = null)
        {
            return new ShotBalloonSnapshot(
                position, radius, scoreValue, hitsRemaining, new ColorProfile(colorId, false, null), balance,
                ShotContactKind.Poppable, ItemType.None);
        }

        /// <summary>A colourless target scored via the flat/streak-breaking tough rule — mirrors
        /// <c>ToughBalloonModel</c> (durable) or an Unbreakable (<c>hitsRemaining == int.MaxValue</c>,
        /// forever-deflecting).</summary>
        public static ShotBalloonSnapshot ForToughTarget(
            Vector2 position, float radius, int scoreValue, int hitsRemaining, BalanceProfile? balance = null)
        {
            return new ShotBalloonSnapshot(
                position, radius, scoreValue, hitsRemaining, default, balance, ShotContactKind.Poppable,
                ItemType.None);
        }

        /// <summary>A collision-only archetype with no score and no balance-grid presence — Phase A's
        /// interactive statics (Deflector/Gatekeeper/Absorber). Balance is always null: statics never
        /// enter the balance grid as movers, so no dynamic stub backs them (see the null-<c>Actor</c>
        /// gating in <see cref="ShotBoardDynamics" />/<see cref="ShotSimulator" />). Unused by the live
        /// gather until Phase A wires static archetypes into the collision board.</summary>
        public static ShotBalloonSnapshot ForStaticContact(
            Vector2 position, float radius, ShotContactKind kind, int hitsRemaining = 1)
        {
            return new ShotBalloonSnapshot(position, radius, 0, hitsRemaining, default, null, kind, ItemType.None);
        }
    }

    /// <summary>Mutable per-simulation copy of a <see cref="ShotBalloonSnapshot" /> — <see cref="HitsRemaining" />
    /// decrements on a deflect; a popped entry is swap-removed from the caller's active count.
    /// <see cref="SlotIndex" /> is copied up front so identity survives both the swap-remove (which
    /// scrambles working-set order) and a null <see cref="Actor" /> (statics have no actor backref to
    /// read it from). <see cref="Actor" /> is null unless the snapshot carries a <see cref="ShotBalloonSnapshot.Balance" />
    /// AND the sim was given a <see cref="ShotBoardDynamics" /> — in which case it is the persistent stub
    /// backing this entry's position/nudge state.</summary>
    internal struct ShotBalloonState
    {
        public Vector2 Position;
        public float Radius;
        public string ColorId;
        public int ScoreValue;
        public int HitsRemaining;
        public Vector2Int SlotIndex;
        public ShotContactKind ContactKind;
        public ItemType Item;
        public bool IsRainbow;
        public ShotSimDynamicActor Actor;

        public ShotBalloonState(in ShotBalloonSnapshot snapshot)
        {
            Position = snapshot.Position;
            Radius = snapshot.Radius;
            ColorId = snapshot.ColorId;
            ScoreValue = snapshot.ScoreValue;
            HitsRemaining = snapshot.HitsRemaining;
            SlotIndex = snapshot.Balance?.SlotIndex ?? default;
            ContactKind = snapshot.ContactKind;
            Item = snapshot.Item;
            IsRainbow = snapshot.Color.IsRainbow;
            Actor = null;
        }
    }
}
