#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE

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
