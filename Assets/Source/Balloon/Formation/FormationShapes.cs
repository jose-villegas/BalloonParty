using System;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     A set of <see cref="FormationShape" />s, one bit each: the shapes a level lets its unbreakables form
    ///     (<c>ILevelParameters.AllowedFormationShapes</c>). The meadow allows the snake alone.
    /// </summary>
    [Flags]
    internal enum FormationShapes
    {
        None = 0,
        Horizontal = 1 << (int)FormationShape.Horizontal,
        DiagonalDownRight = 1 << (int)FormationShape.DiagonalDownRight,
        DiagonalDownLeft = 1 << (int)FormationShape.DiagonalDownLeft,
        ItemCover = 1 << (int)FormationShape.ItemCover,
        Snake = 1 << (int)FormationShape.Snake,
        Ring = 1 << (int)FormationShape.Ring,
        Spinner = 1 << (int)FormationShape.Spinner,
        All = Horizontal | DiagonalDownRight | DiagonalDownLeft | ItemCover | Snake | Ring | Spinner
    }

    internal static class FormationShapesExtensions
    {
        internal static bool Allows(this FormationShapes shapes, FormationShape shape)
        {
            return ((int)shapes & 1 << (int)shape) != 0;
        }
    }
}
