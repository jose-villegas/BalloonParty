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
        /// <summary>
        ///     True for a single-control cheat (e.g. one toggle) whose control already reads as a full
        ///     label — drawn inline on one row instead of under a separate name header/panel.
        /// </summary>
        bool Compact { get; }

        void DrawControls();
    }
}
#endif
