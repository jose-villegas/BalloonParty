using System;
using System.Collections.Generic;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ScoreLevelUpMessage
    {
        public readonly int NewLevel;

        /// <summary>The just-completed level's allowed colors, snapshotted pre-publish — use this,
        /// not the live (possibly already re-resolved) IActiveLevelParameters.AllowedColors.</summary>
        public readonly IReadOnlyList<string> CompletedColors;

        public ScoreLevelUpMessage(int newLevel, IReadOnlyList<string> completedColors = null)
        {
            NewLevel = newLevel;
            CompletedColors = completedColors ?? Array.Empty<string>();
        }
    }
}
