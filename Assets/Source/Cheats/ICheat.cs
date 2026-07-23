#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

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
