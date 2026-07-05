using System;
using System.Collections.Generic;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ScoreLevelUpMessage
    {
        public readonly int NewLevel;

        /// <summary>
        ///     The allowed-color set for the level that was <em>just completed</em> — snapshotted by
        ///     the publisher before any subscriber (including the level-range resolver) can react and
        ///     re-resolve to the new level's set. Consumers that celebrate the completed level (e.g.
        ///     the level-up glow ceremony) must use this, not the live <c>IActiveLevelParameters
        ///     .AllowedColors</c>, which may already reflect the new level by the time they run —
        ///     subscriber order for this message is unenforced.
        /// </summary>
        public readonly IReadOnlyList<string> CompletedColors;

        public ScoreLevelUpMessage(int newLevel, IReadOnlyList<string> completedColors = null)
        {
            NewLevel = newLevel;
            CompletedColors = completedColors ?? Array.Empty<string>();
        }
    }
}
