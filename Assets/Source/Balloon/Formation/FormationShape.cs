namespace BalloonParty.Balloon.Formation
{
    internal enum FormationShape
    {
        Horizontal,
        DiagonalDownRight,
        DiagonalDownLeft,

        // A ring of six around a balloon that carries an item: no shot reaches the item, and it can't move.
        ItemCover
    }
}
