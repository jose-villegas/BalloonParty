#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;

namespace BalloonParty.Cheats
{
    public interface ICheat
    {
        string Name { get; }
        string Section { get; }
        IReadOnlyList<string> Tags { get; }
        void Execute();
    }
}
#endif
