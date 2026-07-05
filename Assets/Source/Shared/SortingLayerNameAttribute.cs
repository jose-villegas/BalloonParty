using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Draws a <c>string</c> field as a dropdown of the project's sorting layers instead of a
    ///     free-text box — so a name can't drift out of sync with the Tags &amp; Layers settings.
    /// </summary>
    public class SortingLayerNameAttribute : PropertyAttribute
    {
    }
}
