using BalloonParty.Configuration.HeadsUp;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     What a router asks to be written: a style, its number, where, and optionally the line it rewrites
    ///     (<see cref="MergeKey" />) and a word for a style written with one (a colour's name). A struct with an
    ///     <c>int</c> argument, so a heads-up per pop allocates nothing.
    /// </summary>
    internal readonly struct HeadsUpRequest
    {
        public readonly HeadsUpStyleId Style;
        public readonly int Arg;
        public readonly HeadsUpAnchor Anchor;
        public readonly HeadsUpMergeKey MergeKey;

        /// <summary>For a style whose format takes a word rather than the number; an existing string, never built.</summary>
        public readonly string Word;

        public HeadsUpRequest(
            HeadsUpStyleId style, int arg, HeadsUpAnchor anchor, HeadsUpMergeKey mergeKey = default, string word = null)
        {
            Style = style;
            Arg = arg;
            Anchor = anchor;
            MergeKey = mergeKey;
            Word = word;
        }
    }
}
