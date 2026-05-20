using System;

namespace BalloonParty.Game.Cinematics
{
    internal sealed class CinematicScene
    {
        internal readonly Action OnBegin;
        internal readonly Action OnTick;
        internal readonly Action OnLateTick;
        internal readonly Action OnEnd;

        internal CinematicScene(
            Action onBegin = null,
            Action onTick = null,
            Action onLateTick = null,
            Action onEnd = null)
        {
            OnBegin = onBegin;
            OnTick = onTick;
            OnLateTick = onLateTick;
            OnEnd = onEnd;
        }
    }
}
