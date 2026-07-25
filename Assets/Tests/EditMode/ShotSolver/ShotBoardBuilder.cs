using System.Collections.Generic;
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
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining = 1)
        {
            return ShotBalloonSnapshot.ForColorTarget(position, radius, colorId, scoreValue, hitsRemaining);
        }

        public static ShotBalloonSnapshot Green(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, IReadOnlyList<NudgeOverride> nudgeOverrides)
        {
            var balance = new BalanceProfile(
                slotIndex, balancePriority, maxBalanceSteps, moveSpeed, directBalanceMotion, nudgeOverrides);
            return ShotBalloonSnapshot.ForColorTarget(position, radius, colorId, scoreValue, hitsRemaining, balance);
        }

        /// <summary>The full-fidelity overload — carries the Phase B bias fields a bias-flip test needs
        /// (the plain balance overload leaves them at their None/0 inert defaults).</summary>
        public static ShotBalloonSnapshot Green(
            Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining,
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, IReadOnlyList<NudgeOverride> nudgeOverrides, bool omnidirectional,
            BalanceBiasKind biasKind, float biasValue, int biasTypeId)
        {
            var balance = new BalanceProfile(
                slotIndex, balancePriority, maxBalanceSteps, moveSpeed, directBalanceMotion, nudgeOverrides,
                omnidirectional, biasKind, biasValue, biasTypeId);
            return ShotBalloonSnapshot.ForColorTarget(position, radius, colorId, scoreValue, hitsRemaining, balance);
        }

        public static ShotBalloonSnapshot Tough(Vector2 position, float radius, int scoreValue, int hitsRemaining)
        {
            return ShotBalloonSnapshot.ForToughTarget(position, radius, scoreValue, hitsRemaining);
        }

        public static ShotBalloonSnapshot Static(
            Vector2 position, float radius, ShotContactKind kind, int hitsRemaining = 1)
        {
            return ShotBalloonSnapshot.ForStaticContact(position, radius, kind, hitsRemaining);
        }
    }
}
