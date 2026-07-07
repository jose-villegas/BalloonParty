#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     An <see cref="ICheat" /> that renders its own inline IMGUI controls (dropdowns, counters, …)
    ///     in place of the console's plain Execute button. Drawn inside the cheat's row by
    ///     <c>CheatConsoleView</c>; called only from within <c>OnGUI</c>.
    /// </summary>
    internal interface ICheatControls
    {
        void DrawControls();
    }
}
#endif
