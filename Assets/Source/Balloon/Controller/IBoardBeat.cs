using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     The board's rhythm, for whatever else on the board keeps time with it: the unbreakables' beat
    ///     (<see cref="IUnbreakableFormationSettings.BeatSeconds" />), which <see cref="BalloonBalancer" /> counts
    ///     in seconds of play. It runs whether or not the board holds an unbreakable or a snake, a pause holds it,
    ///     and it starts over with a run. A reader keeps no timer of its own: it remembers the count it last saw.
    /// </summary>
    internal interface IBoardBeat
    {
        /// <summary>
        ///     How many off-beats have passed in this run. An off-beat falls <paramref name="share" /> of a beat
        ///     (0 to 1) after each beat, the first one that far into the run. The count never rises by more than
        ///     one between two reads a frame apart, and falls only when a run starts over, back to 0.
        /// </summary>
        int OffBeats(float share);
    }
}
