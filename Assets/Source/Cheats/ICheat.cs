#if UNITY_EDITOR || DEVELOPMENT_BUILD

#region

using System.Collections.Generic;

#endregion

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
