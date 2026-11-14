namespace BalloonParty.Balloon.Formation
{
    internal enum FormationShape
    {
        Horizontal,
        DiagonalDownRight,
        DiagonalDownLeft,

        // Three to six around a balloon carrying a bomb, a snipe or a lightning, shielding it from the cannon:
        // below it first, then its sides, then above. Six close the ring.
        ItemCover,

        // A line that walks the board on the balancer's pulse, each member stepping into the slot the one ahead
        // just left.
        Snake
    }
}
