namespace BalloonParty.Balloon.Formation
{
    internal enum FormationShape
    {
        Horizontal,
        DiagonalDownRight,
        DiagonalDownLeft,

        // A row right under a balloon that carries an item, so shots from below can't reach the item.
        ItemCover
    }
}
