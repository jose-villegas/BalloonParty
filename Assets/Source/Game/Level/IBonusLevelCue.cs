using System;

namespace BalloonParty.Game.Level
{
    /// <summary>When a purple (extra-colour) level starts: once per level, as play begins on it.</summary>
    internal interface IBonusLevelCue
    {
        /// <summary>The level number, as play begins on a purple level.</summary>
        IObservable<int> Started { get; }
    }
}
