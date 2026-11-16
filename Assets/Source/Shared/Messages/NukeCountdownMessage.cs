using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by <c>NukeFuse</c> each time an armed nuke's counter changes (PLAN-NukeBalloon): at arming with the
    ///     fuse's whole seconds, then each second down to 1. <see cref="Position" /> is the nuke's.
    /// </summary>
    public readonly struct NukeCountdownMessage
    {
        public Vector3 Position { get; }
        public int SecondsLeft { get; }

        public NukeCountdownMessage(Vector3 position, int secondsLeft)
        {
            Position = position;
            SecondsLeft = secondsLeft;
        }
    }
}
