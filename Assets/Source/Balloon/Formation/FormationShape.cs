namespace BalloonParty.Balloon.Formation
{
    internal enum FormationShape
    {
        Horizontal,
        DiagonalDownRight,
        DiagonalDownLeft,

        // Three to six around a balloon carrying a bomb, a snipe or a lightning, or around a gift balloon (any
        // reward balloon), shielding it from the cannon: below it first, then its sides, then above. Six surround
        // it. Named for the items it began with; the name stays, as tests, plans and levels' sets use it.
        ItemCover,

        // A line that walks the board on the balancer's pulse, each member stepping into the slot the one ahead
        // just left.
        Snake,

        // Four or five round one slot, so at least one of its six neighbours stays open. On every beat the whole
        // ring turns one slot round its centre, and now and then every ring and spinner turns the other way together.
        Ring,

        // A straight line of three or five that turns about the member in its middle, 60° on every beat: flat,
        // then one diagonal, then the other. It turns the other way on the same beat as every ring.
        Spinner
    }
}
