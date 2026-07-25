using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Nudge;
using BalloonParty.Slots.Actor;
using BalloonParty.Solver;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Thin test-only board construction — forwards straight to the production
    /// <see cref="ShotBalloonSnapshot" /> factories rather than re-implementing field mapping, so the
    /// suite staying green after any factory change IS the field-mapping test.</summary>
    internal static class ShotBoardBuilder
    {
        public static ShotBalloonSnapshot Green(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining = 1,
            ItemType item = ItemType.None, float spin = 0f)
        {
            return ShotBalloonSnapshot.ForColorTarget(
                position, radius, colorId, scoreValue, hitsRemaining, item: BuildItemProfile(item, spin));
        }

        public static ShotBalloonSnapshot Green(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, IReadOnlyList<NudgeOverride> nudgeOverrides, ItemType item = ItemType.None,
            float spin = 0f)
        {
            var balance = new BalanceProfile(
                slotIndex, balancePriority, maxBalanceSteps, moveSpeed, directBalanceMotion, nudgeOverrides);
            return ShotBalloonSnapshot.ForColorTarget(
                position, radius, colorId, scoreValue, hitsRemaining, balance, BuildItemProfile(item, spin));
        }

        /// <summary>The full-fidelity overload — carries the Phase B bias fields a bias-flip test needs
        /// (the plain balance overload leaves them at their None/0 inert defaults).</summary>
        public static ShotBalloonSnapshot Green(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, IReadOnlyList<NudgeOverride> nudgeOverrides, bool omnidirectional,
            BalanceBiasKind biasKind, float biasValue, int biasTypeId, ItemType item = ItemType.None, float spin = 0f)
        {
            var balance = new BalanceProfile(
                slotIndex, balancePriority, maxBalanceSteps, moveSpeed, directBalanceMotion, nudgeOverrides,
                omnidirectional, biasKind, biasValue, biasTypeId);
            return ShotBalloonSnapshot.ForColorTarget(
                position, radius, colorId, scoreValue, hitsRemaining, balance, BuildItemProfile(item, spin));
        }

        public static ShotBalloonSnapshot Tough(
            Vector2 position, float radius, int scoreValue, int hitsRemaining, bool washes = false,
            bool paysSourceColor = false)
        {
            return ShotBalloonSnapshot.ForToughTarget(
                position, radius, scoreValue, hitsRemaining, washesProjectileColor: washes,
                paysSourceColor: paysSourceColor);
        }

        /// <summary>The SlotIndex-carrying overload — needed by any Bomb-effect test placing more than
        /// one Tough/Unbreakable-shaped occupant in the same activation (the plain overload above always
        /// defaults to slot (0,0), colliding two such entries into the same working-set slot).</summary>
        public static ShotBalloonSnapshot Tough(
            Vector2 position, float radius, int scoreValue, int hitsRemaining, Vector2Int slotIndex,
            int balancePriority, int maxBalanceSteps, float moveSpeed, bool directBalanceMotion,
            IReadOnlyList<NudgeOverride> nudgeOverrides, bool washes = false, bool paysSourceColor = false)
        {
            var balance = new BalanceProfile(
                slotIndex, balancePriority, maxBalanceSteps, moveSpeed, directBalanceMotion, nudgeOverrides);
            return ShotBalloonSnapshot.ForToughTarget(
                position, radius, scoreValue, hitsRemaining, balance, washesProjectileColor: washes,
                paysSourceColor: paysSourceColor);
        }

        public static ShotBalloonSnapshot Rainbow(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining = 1,
            ItemType item = ItemType.None, float spin = 0f)
        {
            return ShotBalloonSnapshot.ForRainbowTarget(
                position, radius, colorId, scoreValue, hitsRemaining, item: BuildItemProfile(item, spin));
        }

        /// <summary>The SlotIndex-carrying overload — needed by any test exercising the rainbow-buff
        /// hex-neighbour conversion, which addresses the working set by <see cref="Vector2Int" /> slot,
        /// not array index.</summary>
        public static ShotBalloonSnapshot Rainbow(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, IReadOnlyList<NudgeOverride> nudgeOverrides, ItemType item = ItemType.None,
            float spin = 0f)
        {
            var balance = new BalanceProfile(
                slotIndex, balancePriority, maxBalanceSteps, moveSpeed, directBalanceMotion, nudgeOverrides);
            return ShotBalloonSnapshot.ForRainbowTarget(
                position, radius, colorId, scoreValue, hitsRemaining, balance, BuildItemProfile(item, spin));
        }

        public static ShotBalloonSnapshot Static(
            Vector2Int slotIndex, Vector2 position, float radius, ShotContactKind kind, int hitsRemaining = 1)
        {
            return ShotBalloonSnapshot.ForStaticContact(slotIndex, position, radius, kind, hitsRemaining);
        }

        // item:/spin: forwards to ItemProfile — spin is the item's SpinDegreesPerSecond (a test board
        // is authored fresh, never mid-spin, so the starting SpinDegrees is always 0).
        private static ItemProfile? BuildItemProfile(ItemType item, float spin)
        {
            return item == ItemType.None ? null : new ItemProfile(item, 0f, spin);
        }
    }
}
